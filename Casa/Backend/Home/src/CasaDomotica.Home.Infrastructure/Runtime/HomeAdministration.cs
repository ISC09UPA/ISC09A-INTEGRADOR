using Microsoft.EntityFrameworkCore;
namespace CasaDomotica.Home.Infrastructure.Runtime;
public sealed class HomeAdministration(HomeDb db)
{
    public async Task MigrateAsync(CancellationToken ct)
    {
        var assembly = typeof(HomeAdministration).Assembly;
        var scripts = assembly.GetManifestResourceNames().Where(x => x.EndsWith(".sql", StringComparison.Ordinal)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (scripts.Length == 0) throw new InvalidOperationException("Home migrations were not embedded.");
        foreach (var name in scripts)
        {
            await using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(ct), ct);
        }
    }
    public async Task ProvisionRuntimeAsync(string password, CancellationToken ct)
    {
        if (password.Length is < 32 or > 128) throw new ArgumentException("Runtime credential length is invalid.");
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @pwd nvarchar(128)={password}; DECLARE @sql nvarchar(max); IF USER_ID(N'home_api') IS NULL SET @sql=N'CREATE USER [home_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39)); ELSE SET @sql=N'ALTER USER [home_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39)); EXEC sp_executesql @sql; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeDevices TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeLights TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeResources TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeCommands TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeExpiredCommandKeys TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeInbox TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeOutbox TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeTelemetry TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeCaptureJobs TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeVideoSessions TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeVideoSegments TO home_api; GRANT SELECT ON dbo.HomeSchemaVersions TO home_api;", ct);
    }
    public async Task BindDeviceAsync(Guid houseId, string deviceId, string lightId, CancellationToken ct)
    {
        if (houseId == Guid.Empty || string.IsNullOrWhiteSpace(deviceId) || deviceId.Length > 128 || string.IsNullOrWhiteSpace(lightId) || lightId.Length > 128) throw new ArgumentException("Provide a real house and registered device/light identity.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var existing = await db.Devices.SingleOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        if (existing is not null && existing.HouseId != houseId) throw new InvalidOperationException("Device already belongs to another house.");
        if (existing is null) db.Devices.Add(new() { DeviceId = deviceId, HouseId = houseId });
        if (await db.Resources.AnyAsync(x => x.HouseId == houseId && x.ResourceId == lightId, ct)) throw new InvalidOperationException("Light identifier is already assigned to another resource.");
        var light = await db.Lights.SingleOrDefaultAsync(x => x.HouseId == houseId && x.LightId == lightId, ct);
        if (light is not null && light.DeviceId != deviceId) throw new InvalidOperationException("Light already belongs to another device.");
        if (light is null) db.Lights.Add(new() { HouseId = houseId, LightId = lightId, DeviceId = deviceId });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task BindResourceAsync(Guid houseId, string deviceId, string resourceId, string kind, string deviceResourceId, CancellationToken ct)
    {
        if (houseId == Guid.Empty || string.IsNullOrWhiteSpace(deviceId) || deviceId.Length > 128 || string.IsNullOrWhiteSpace(resourceId) || resourceId.Length > 128 || string.IsNullOrWhiteSpace(deviceResourceId) || deviceResourceId.Length > 128 || kind is not ("security" or "door" or "camera")) throw new ArgumentException("Provide a real device, house and supported resource mapping.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await db.Devices.AnyAsync(x => x.DeviceId == deviceId && x.HouseId == houseId && x.Enabled, ct)) throw new InvalidOperationException("Device is not enabled for this house.");
        if (await db.Lights.AnyAsync(x => x.HouseId == houseId && x.LightId == resourceId, ct)) throw new InvalidOperationException("Resource identifier is already assigned to a light.");
        var existing = await db.Resources.SingleOrDefaultAsync(x => x.HouseId == houseId && x.ResourceId == resourceId, ct);
        if (existing is not null && (existing.DeviceId != deviceId || existing.Kind != kind || existing.DeviceResourceId != deviceResourceId)) throw new InvalidOperationException("Resource mapping already exists with different properties.");
        if (existing is null) db.Resources.Add(new() { HouseId = houseId, DeviceId = deviceId, ResourceId = resourceId, Kind = kind, DeviceResourceId = deviceResourceId });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    public async Task VerifySqlIsolationAsync(string connectionString, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("HomeDb connection string is required.");
        var options = new DbContextOptionsBuilder<HomeDb>().AddInterceptors(new SqlWarmupInterceptor()).UseSqlServer(connectionString).Options;
        var houseId = Guid.NewGuid();
        var deviceId = "verify-isolation-" + Guid.NewGuid().ToString("N");
        var t0 = DateTimeOffset.UtcNow;
        var deadline = t0.AddSeconds(10);
        try
        {
            await using (var seed = new HomeDb(options))
            {
                seed.Devices.Add(new DeviceRow { DeviceId = deviceId, HouseId = houseId, Enabled = true, LastReportedAtUtc = t0 });
                seed.Resources.Add(new ResourceRow { HouseId = houseId, ResourceId = "door", DeviceId = deviceId, DeviceResourceId = "principal", Kind = "door", Enabled = true });
                await seed.SaveChangesAsync(ct);
            }

            var keyRestart = Guid.NewGuid().ToString("D");
            await using (var db1 = new HomeDb(options))
            {
                var rt1 = new SqlHomeRuntime(db1, new FixedClock(t0.AddSeconds(15)));
                var ex1 = await AssertHomeExceptionAsync(() => rt1.FindByKeyAsync(houseId, keyRestart, ct, deadline));
                if (ex1.Code != "requestExpired") throw new InvalidOperationException($"Expected requestExpired on instance 1, got {ex1.Code}.");
            }

            await using (var db2 = new HomeDb(options))
            {
                var rt2 = new SqlHomeRuntime(db2, new FixedClock(t0.AddSeconds(5)));
                var exRead = await AssertHomeExceptionAsync(() => rt2.FindByKeyAsync(houseId, keyRestart, ct));
                if (exRead.Code != "requestExpired") throw new InvalidOperationException($"Expected persistent requestExpired across restart, got {exRead.Code}.");
                var exAdmit = await AssertHomeExceptionAsync(() => rt2.AdmitCapabilityAsync(new(Guid.NewGuid(), Guid.NewGuid()), houseId, "door", "OpenDoor", "{}", new string('A', 64), keyRestart, Guid.NewGuid(), t0, deadline, ct));
                if (exAdmit.Code != "requestExpired") throw new InvalidOperationException($"Expected late admission block across restart, got {exAdmit.Code}.");
                var cmdCount = await db2.Commands.CountAsync(x => x.HouseId == houseId, ct);
                var expCount = await db2.ExpiredCommandKeys.CountAsync(x => x.HouseId == houseId && x.IdempotencyKey == keyRestart, ct);
                if (cmdCount != 0 || expCount != 1) throw new InvalidOperationException($"Restart invariant failed: cmd={cmdCount}, exp={expCount}.");
            }

            for (var i = 0; i < 3; i++)
            {
                var keyRace = Guid.NewGuid().ToString("D");
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var admitTask = Task.Run(async () =>
                {
                    await using var dbA = new HomeDb(options);
                    await dbA.Database.OpenConnectionAsync(ct);
                    await gate.Task.WaitAsync(ct);
                    var rtA = new SqlHomeRuntime(dbA, new FixedClock(t0.AddSeconds(5)));
                    try { await rtA.AdmitCapabilityAsync(new(Guid.NewGuid(), Guid.NewGuid()), houseId, "door", "OpenDoor", "{}", new string('B', 64), keyRace, Guid.NewGuid(), t0, deadline, ct); }
                    catch (CasaDomotica.Home.Domain.Interfaces.HomeException e) when (e.Code == "requestExpired") { }
                }, ct);
                var expireTask = Task.Run(async () =>
                {
                    await using var dbB = new HomeDb(options);
                    await dbB.Database.OpenConnectionAsync(ct);
                    await gate.Task.WaitAsync(ct);
                    var rtB = new SqlHomeRuntime(dbB, new FixedClock(t0.AddSeconds(15)));
                    try { await rtB.FindByKeyAsync(houseId, keyRace, ct, deadline); }
                    catch (CasaDomotica.Home.Domain.Interfaces.HomeException e) when (e.Code == "requestExpired") { }
                }, ct);
                await Task.Delay(50, ct);
                gate.SetResult();
                await Task.WhenAll(admitTask, expireTask);

                await using var checkDb = new HomeDb(options);
                var cmdCount = await checkDb.Commands.CountAsync(x => x.HouseId == houseId && x.IdempotencyKey == keyRace, ct);
                var expCount = await checkDb.ExpiredCommandKeys.CountAsync(x => x.HouseId == houseId && x.IdempotencyKey == keyRace, ct);
                if (!((cmdCount == 1 && expCount == 0) || (cmdCount == 0 && expCount == 1)))
                    throw new InvalidOperationException($"Concurrency invariant failed on attempt {i + 1}: cmd={cmdCount}, exp={expCount}.");
                await checkDb.Commands.Where(x => x.HouseId == houseId).ExecuteDeleteAsync(ct);
                await checkDb.ExpiredCommandKeys.Where(x => x.HouseId == houseId).ExecuteDeleteAsync(ct);
            }
        }
        finally
        {
            await using var cleanup = new HomeDb(options);
            await cleanup.ExpiredCommandKeys.Where(x => x.HouseId == houseId).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.Commands.Where(x => x.HouseId == houseId).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.Resources.Where(x => x.HouseId == houseId).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.Devices.Where(x => x.DeviceId == deviceId).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private static async Task<CasaDomotica.Home.Domain.Interfaces.HomeException> AssertHomeExceptionAsync(Func<Task> action)
    {
        try { await action(); }
        catch (CasaDomotica.Home.Domain.Interfaces.HomeException ex) { return ex; }
        throw new InvalidOperationException("Expected HomeException was not thrown.");
    }
}
