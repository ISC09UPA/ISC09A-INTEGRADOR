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
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @pwd nvarchar(128)={password}; DECLARE @sql nvarchar(max); IF USER_ID(N'home_api') IS NULL SET @sql=N'CREATE USER [home_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39)); ELSE SET @sql=N'ALTER USER [home_api] WITH PASSWORD='+QUOTENAME(@pwd, CHAR(39)); EXEC sp_executesql @sql; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeDevices TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeLights TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeResources TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeCommands TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeInbox TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeOutbox TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeTelemetry TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeCaptureJobs TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeVideoSessions TO home_api; GRANT SELECT,INSERT,UPDATE,DELETE ON dbo.HomeVideoSegments TO home_api; GRANT SELECT ON dbo.HomeSchemaVersions TO home_api;", ct);
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
}
