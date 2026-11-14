using System.Text.Json;
using CasaDomotica.Home.Application.Commands;
using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CasaDomotica.Home.Application.Tests;
/// <summary>Isolated in-memory database only; never connects to the project's production data.</summary>
public sealed class RuntimePersistenceTests
{
    private sealed class Allow : IUserAuthorizer
    {
        public Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct) => Task.FromResult(true);
    }
    private static async Task<(SqliteConnection Connection, HomeDb Db, Guid House)> Database()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var house = Guid.NewGuid();
        db.Devices.Add(new DeviceRow { DeviceId = "test-device", HouseId = house, LastReportedAtUtc = DateTimeOffset.UtcNow });
        db.Resources.AddRange(new ResourceRow { HouseId = house, ResourceId = "door", DeviceId = "test-device", DeviceResourceId = "configured-door", Kind = "door" }, new ResourceRow { HouseId = house, ResourceId = "camera", DeviceId = "test-device", DeviceResourceId = "configured-camera", Kind = "camera" }, new ResourceRow { HouseId = house, ResourceId = "security", DeviceId = "test-device", DeviceResourceId = "configured-security", Kind = "security" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        return (connection, db, house);
    }
    [Fact]
    public async Task AdmissionIsDurableIdempotentAndUsesServerOwnedDoorParameters()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        var store = new SqlHomeRuntime(db); var operation = new CapabilityOperations(store, new Allow(), TimeProvider.System);
        var user = new UserContext(Guid.NewGuid(), Guid.NewGuid()); var key = Guid.NewGuid().ToString();
        var first = await operation.SendAsync(user, house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default);
        db.ChangeTracker.Clear();
        var repeat = await operation.SendAsync(user, house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default);
        Assert.Equal(first.CommandId, repeat.CommandId); Assert.Equal(1, await db.Commands.CountAsync());
        db.ChangeTracker.Clear();
        var lateRepeat = await operation.SendAsync(user, house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(-1), default);
        Assert.Equal(first.CommandId, lateRepeat.CommandId);
        using var payload = JsonDocument.Parse(first.PayloadJson);
        Assert.Equal("configured-door", payload.RootElement.GetProperty("doorId").GetString()); Assert.Equal(1000, payload.RootElement.GetProperty("durationMs").GetInt32());
        db.ChangeTracker.Clear();
        var error = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(user, house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default));
        Assert.Equal("pendingCommand", error.Code);
    }
    [Fact]
    public async Task HouseAndResourceKindCannotBeSelectedAcrossBoundaries()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        var operation = new CapabilityOperations(new SqlHomeRuntime(db), new Allow(), TimeProvider.System);
        var user = new UserContext(Guid.NewGuid(), Guid.NewGuid());
        var wrongHouse = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(user, Guid.NewGuid(), "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default));
        Assert.Equal("notFound", wrongHouse.Code); db.ChangeTracker.Clear();
        var wrongKind = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(user, house, "camera", "OpenDoor", JsonSerializer.SerializeToElement(new { }), Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default));
        Assert.Equal("notFound", wrongKind.Code); Assert.Equal(0, await db.Commands.CountAsync());
    }
    [Fact]
    public async Task ReusingAKeyForChangedPayloadIsRejectedAndDoesNotRepeatAction()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        var operation = new CapabilityOperations(new SqlHomeRuntime(db), new Allow(), TimeProvider.System);
        var user = new UserContext(Guid.NewGuid(), Guid.NewGuid()); var key = Guid.NewGuid().ToString();
        await operation.SendAsync(user, house, "security", "SetSecurityMode", JsonSerializer.SerializeToElement(new { mode = "armed" }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default);
        db.ChangeTracker.Clear();
        var error = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(user, house, "security", "SetSecurityMode", JsonSerializer.SerializeToElement(new { mode = "disarmed" }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default));
        Assert.Equal("duplicateChanged", error.Code); Assert.Equal(1, await db.Commands.CountAsync());
    }
    [Fact]
    public async Task CaptureJobIsCreatedOnlyOnceAndRejectsOutOfWindowEvents()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        var operation = new CapabilityOperations(new SqlHomeRuntime(db), new Allow(), TimeProvider.System);
        var user = new UserContext(Guid.NewGuid(), Guid.NewGuid()); var key = Guid.NewGuid().ToString();
        var first = await operation.SendAsync(user, house, "camera", "CaptureImage", JsonSerializer.SerializeToElement(new { }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default);
        db.ChangeTracker.Clear();
        var again = await operation.SendAsync(user, house, "camera", "CaptureImage", JsonSerializer.SerializeToElement(new { }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default);
        Assert.Equal(first.CommandId, again.CommandId); Assert.Equal(1, await db.CaptureJobs.CountAsync());
        var job = await db.CaptureJobs.SingleAsync();
        var verifier = new CaptureVerifier(db, new ConfigurationBuilder().Build());
        var payload = JsonSerializer.SerializeToElement(new { captureId = job.CaptureId, blobName = "not-read" });
        var error = await Assert.ThrowsAsync<HomeException>(() => verifier.VerifyAsync("test-device", payload, job.ExpiresAtUtc.AddSeconds(1), default));
        Assert.Equal("invalidEvent", error.Code);
        error = await Assert.ThrowsAsync<HomeException>(() => verifier.VerifyAsync("test-device", payload, DateTimeOffset.UtcNow, default));
        Assert.Equal("invalidEvent", error.Code); Assert.Contains("dispatched", error.Message);
    }
    [Fact]
    public async Task DuplicateAndReorderedEventsDoNotRepeatOrRefreshOldLightState()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        db.Lights.Add(new LightRow { HouseId = house, LightId = "light", DeviceId = "test-device" }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ingest = new DeviceIngestion(db, new CaptureVerifier(db, new ConfigurationBuilder().Build()));
        var now = DateTimeOffset.UtcNow.AddSeconds(-5);
        JsonElement Event(Guid id, long revision, bool on, DateTimeOffset at) => JsonSerializer.SerializeToElement(new { schemaVersion = 2, type = "DeviceStateReported", eventId = id, correlationId = Guid.NewGuid(), occurredAtUtc = at, payload = new { lightId = "light", snapshot = new { lightOn = on, lightingMode = "manual", source = "device", appliedAtUtc = now.AddSeconds(-1) }, stateRevision = revision, securityModeApplied = "armed" } });
        var first = Event(Guid.NewGuid(), 3, true, now);
        await ingest.IngestAsync("test-device", first, default); db.ChangeTracker.Clear();
        await ingest.IngestAsync("test-device", first, default); db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Inbox.CountAsync()); Assert.Equal(1, await db.Outbox.CountAsync()); Assert.Equal(1, await db.Telemetry.CountAsync());
        using (var emitted = JsonDocument.Parse((await db.Outbox.SingleAsync()).BodyJson)) Assert.True(emitted.RootElement.GetProperty("isSecurityController").GetBoolean());
        db.Resources.Add(new ResourceRow { HouseId = house, ResourceId = "security-other", DeviceId = "test-device", DeviceResourceId = "other", Kind = "security" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await ingest.IngestAsync("test-device", Event(Guid.NewGuid(), 2, false, now.AddSeconds(1)), default); db.ChangeTracker.Clear();
        var outbox = await db.Outbox.ToListAsync();
        Assert.Contains(outbox, row => !JsonDocument.Parse(row.BodyJson).RootElement.GetProperty("isSecurityController").GetBoolean());
        var light = await db.Lights.SingleAsync();
        Assert.Equal(3, light.StateRevision); Assert.True(light.LightOn); Assert.Equal(now, light.StateReportedAtUtc);
    }
    [Fact]
    public async Task OfflineOrExpiredResourcesNeverCreateACommand()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        var device = await db.Devices.SingleAsync(); device.LastReportedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var operation = new CapabilityOperations(new SqlHomeRuntime(db), new Allow(), TimeProvider.System);
        var error = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(new(Guid.NewGuid(), Guid.NewGuid()), house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default));
        Assert.Equal("deviceOffline", error.Code); Assert.Equal(0, await db.Commands.CountAsync());
    }
    [Fact]
    public async Task ADeviceClockAheadCannotExtendStateFreshnessPastServerReceipt()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        db.Lights.Add(new LightRow { HouseId = house, LightId = "light", DeviceId = "test-device" }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var future = DateTimeOffset.UtcNow.AddSeconds(20);
        var envelope = JsonSerializer.SerializeToElement(new { schemaVersion = 2, type = "DeviceStateReported", eventId = Guid.NewGuid(), correlationId = Guid.NewGuid(), occurredAtUtc = future, payload = new { lightId = "light", snapshot = new { lightOn = true, lightingMode = "manual", source = "device", appliedAtUtc = future }, stateRevision = 1, securityModeApplied = "armed" } });
        await new DeviceIngestion(db, new CaptureVerifier(db, new ConfigurationBuilder().Build())).IngestAsync("test-device", envelope, default);
        var after = DateTimeOffset.UtcNow; db.ChangeTracker.Clear();
        var light = await db.Lights.SingleAsync(); var device = await db.Devices.SingleAsync();
        Assert.True(light.StateReportedAtUtc <= after); Assert.True(device.LastReportedAtUtc <= after);
    }
    [Fact]
    public async Task AnExpiredNewRequestCannotCreateAnOrder()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        var operation = new CapabilityOperations(new SqlHomeRuntime(db), new Allow(), TimeProvider.System);
        var error = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(new(Guid.NewGuid(), Guid.NewGuid()), house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(-1), default));
        Assert.Equal("requestExpired", error.Code); Assert.Equal(0, await db.Commands.CountAsync());
    }
    [Fact]
    public async Task RecentUnrelatedDeviceActivityDoesNotAuthorizeAnOldLightState()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        db.Lights.Add(new LightRow { HouseId = house, LightId = "light", DeviceId = "test-device", LightOn = false, LightingMode = "manual", StateRevision = 1, AppliedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var operation = new HomeOperations(new SqlHomeRuntime(db), new Allow()); var user = new UserContext(Guid.NewGuid(), Guid.NewGuid());
        var error = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(user, house, "light", "SetLight", JsonSerializer.SerializeToElement(new { on = true }), Guid.NewGuid().ToString(), Guid.NewGuid(), default));
        Assert.Equal("deviceOffline", error.Code); Assert.Equal(0, await db.Commands.CountAsync());
        db.ChangeTracker.Clear(); var light = await db.Lights.SingleAsync(); light.StateReportedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var order = await operation.SendAsync(user, house, "light", "SetLight", JsonSerializer.SerializeToElement(new { on = true }), Guid.NewGuid().ToString(), Guid.NewGuid(), default);
        Assert.Equal("pending", order.Status);
    }
    [Fact]
    public async Task AuthorizationFailureCannotOverwriteAConcurrentClaimInStorage()
    {
        var (connection, db, house) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        var operation = new CapabilityOperations(new SqlHomeRuntime(db), new Allow(), TimeProvider.System);
        var order = await operation.SendAsync(new(Guid.NewGuid(), Guid.NewGuid()), house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(10), default);
        db.ChangeTracker.Clear();
        var command = await db.Commands.SingleAsync(); command.DispatchedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await new SqlDispatchQueue(db).CompleteAsync(command, "rejected", "forbidden", "Permission revoked", true, default);
        db.ChangeTracker.Clear(); var saved = await db.Commands.SingleAsync();
        Assert.Equal(order.CommandId, saved.CommandId); Assert.Equal("pending", saved.Status); Assert.True(saved.Active); Assert.NotNull(saved.DispatchedAtUtc);
    }
}
