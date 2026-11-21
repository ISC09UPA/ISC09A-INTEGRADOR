using System.Text.Json;
using CasaDomotica.Security.Application;
using CasaDomotica.Security.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class SecurityPersistenceTests
{
    private sealed class UnavailableStore : ISecurityEventStore
    {
        public Task StoreAsync(CasaDomotica.Security.Domain.HouseEvent message, CasaDomotica.Security.Domain.ProjectionChange change, string bodyHash, CancellationToken ct) => throw new InvalidOperationException("Database temporarily unavailable");
    }
    [Fact]
    public async Task DatabaseFailureIsNotClassifiedAsInvalidEvent()
    {
        var now = DateTimeOffset.UtcNow;
        var body = JsonSerializer.Serialize(new { schemaVersion = 1, eventId = Guid.NewGuid(), correlationId = Guid.NewGuid(), houseId = Guid.NewGuid(), sourceDeviceId = "device", type = "MotionDetected", occurredAtUtc = now, receivedAtUtc = now, payload = new { roomId = "sala", securityModeApplied = "armed" } });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SecurityEvents(new UnavailableStore()).IngestAsync(body, default));
    }
    [Fact]
    public void CursorQueryTranslatesForSqlServer()
    {
        var options = new DbContextOptionsBuilder<SecurityDb>().UseSqlServer("Server=localhost;Database=SecurityDb;User ID=unused;Password=unused;TrustServerCertificate=True").Options;
        using var db = new SecurityDb(options);
        var time = DateTimeOffset.UtcNow; var id = Guid.NewGuid();
        var query = db.Activity.Where(x => x.OccurredAtUtc < time || x.OccurredAtUtc == time && x.EventId.CompareTo(id) < 0).OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.EventId);
        Assert.Contains("ORDER BY", query.ToQueryString());
        var house = Guid.NewGuid(); const int days = 30;
        var retention = db.Captures.Where(x => x.HouseId == house && x.DeleteRequestedAtUtc == null && x.ReceivedAtUtc < time && db.CapturePolicies.Any(p => p.HouseId == x.HouseId && p.RetentionDays == days));
        Assert.Contains("SecurityCapturePolicies", retention.ToQueryString());
    }

    [Fact]
    public async Task DuplicateAndConflictingEventsNeverCreateAnotherIncident()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SecurityDb>().UseSqlite(connection).Options;
        await using var db = new SecurityDb(options); await db.Database.EnsureCreatedAsync();
        var handler = new SecurityEvents(new SqlSecurityEventStore(db, TimeProvider.System));
        var id = Guid.NewGuid(); var house = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var body = JsonSerializer.Serialize(new { schemaVersion = 1, eventId = id, correlationId = Guid.NewGuid(), houseId = house, sourceDeviceId = "device", type = "MotionDetected", occurredAtUtc = now, receivedAtUtc = now, payload = new { roomId = "sala", securityModeApplied = "armed" } });
        await handler.IngestAsync(body, default);
        await handler.IngestAsync(body, default);
        await Assert.ThrowsAsync<InvalidDataException>(() => handler.IngestAsync(body.Replace("sala", "cocina"), default));
        Assert.Equal(1, await db.Inbox.CountAsync());
        Assert.Equal(1, await db.Activity.CountAsync());
        Assert.Equal(1, await db.Incidents.CountAsync());
        Assert.Equal(1, await db.Outbox.CountAsync());
        Assert.NotEqual(id, (await db.Outbox.SingleAsync()).EventId);
    }

    [Fact]
    public async Task OutOfOrderModeKeepsNewestAuthenticatedControllerState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SecurityDb>().UseSqlite(connection).Options;
        await using var db = new SecurityDb(options); await db.Database.EnsureCreatedAsync();
        var handler = new SecurityEvents(new SqlSecurityEventStore(db, TimeProvider.System));
        var house = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        string Make(string mode, DateTimeOffset time, bool controller) => JsonSerializer.Serialize(new { schemaVersion = 1, eventId = Guid.NewGuid(), correlationId = Guid.NewGuid(), houseId = house, sourceDeviceId = "device", type = "DeviceStateChanged", occurredAtUtc = time, receivedAtUtc = now, isSecurityController = controller, payload = new { securityModeApplied = mode } });
        await handler.IngestAsync(Make("disarmed", now.AddSeconds(-1), true), default);
        await handler.IngestAsync(Make("armed", now.AddSeconds(-30), true), default);
        await handler.IngestAsync(Make("armed", now, false), default);
        Assert.Equal("disarmed", (await db.Modes.SingleAsync()).Mode);
        Assert.Equal(3, await db.Inbox.CountAsync());
    }
}
