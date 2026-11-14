using System.Text.Json;
using CasaDomotica.Security.Application;
using CasaDomotica.Security.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class CaptureLifecycleTests
{
    private sealed class Content : ICaptureContent
    {
        public bool Deleted; public bool Match = true;
        public Task<CaptureContent?> ReadAsync(CaptureRow capture, CancellationToken ct) => Task.FromResult<CaptureContent?>(new([1, 2, 3], "image/png"));
        public Task<bool> DeleteAsync(CaptureRow capture, CancellationToken ct) { Deleted = true; return Task.FromResult(Match); }
    }
    private sealed class Analyzer(bool fail = false) : IImageAnalyzer
    {
        public Task<string> AnalyzeAsync(byte[] image, CancellationToken ct) => fail ? throw new HttpRequestException("isolated provider failure") : Task.FromResult("A test image.");
    }
    private static async Task<(SqliteConnection Connection, SecurityDb Db, Guid House, Guid Capture)> Database()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var db = new SecurityDb(new DbContextOptionsBuilder<SecurityDb>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        return (connection, db, Guid.NewGuid(), Guid.NewGuid());
    }
    [Fact]
    public async Task CaptureEventRequiresMatchingHouseDeviceAndImmutableVersion()
    {
        var (connection, db, house, capture) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        var now = DateTimeOffset.UtcNow; var handler = new SecurityEvents(new SqlSecurityEventStore(db, TimeProvider.System));
        string Event(Guid eventId, string blob, string etag) => JsonSerializer.Serialize(new { schemaVersion = 1, eventId, correlationId = Guid.NewGuid(), houseId = house, sourceDeviceId = "camera-device", type = "CaptureAvailable", occurredAtUtc = now, receivedAtUtc = now, payload = new { captureId = capture, blobName = blob, blobETag = etag } });
        var name = $"{house:D}/camera-device/{capture:D}.png";
        await handler.IngestAsync(Event(Guid.NewGuid(), name, "etag-1"), default); db.ChangeTracker.Clear();
        await handler.IngestAsync(Event(Guid.NewGuid(), name, "etag-1"), default); db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Captures.CountAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => handler.IngestAsync(Event(Guid.NewGuid(), name, "etag-2"), default));
        await Assert.ThrowsAsync<InvalidDataException>(() => handler.IngestAsync(Event(Guid.NewGuid(), $"{Guid.NewGuid():D}/camera-device/{capture:D}.png", "etag-1"), default));
    }
    [Fact]
    public async Task DisabledRetentionPreservesEvidenceAndExplicitDeletionUsesVersionCheck()
    {
        var (connection, db, house, capture) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        db.Captures.Add(new CaptureRow { CaptureId = capture, HouseId = house, DeviceId = "device", BlobName = "name", BlobETag = "etag", OccurredAtUtc = DateTimeOffset.UtcNow.AddYears(-1), ReceivedAtUtc = DateTimeOffset.UtcNow.AddYears(-1) }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var content = new Content(); var cleanup = new CaptureCleanup(db, content, TimeProvider.System);
        await cleanup.RunOnceAsync(default); Assert.False(content.Deleted);
        await db.Captures.Where(x => x.CaptureId == capture).ExecuteUpdateAsync(u => u.SetProperty(x => x.DeleteRequestedAtUtc, DateTimeOffset.UtcNow));
        content.Match = false; await cleanup.RunOnceAsync(default);
        var row = await db.Captures.AsNoTracking().SingleAsync(); Assert.True(content.Deleted); Assert.Null(row.DeletedAtUtc); Assert.Equal("blobChanged", row.DeleteError);
    }
    [Fact]
    public async Task RequestedDeletionIsConfirmedOnlyAfterMatchingBlobIsDeleted()
    {
        var (connection, db, house, capture) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        db.Captures.Add(new CaptureRow { CaptureId = capture, HouseId = house, DeviceId = "device", BlobName = "name", BlobETag = "etag", OccurredAtUtc = DateTimeOffset.UtcNow, ReceivedAtUtc = DateTimeOffset.UtcNow, DeleteRequestedAtUtc = DateTimeOffset.UtcNow, AnalysisText = "analysis" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var content = new Content(); await new CaptureCleanup(db, content, TimeProvider.System).RunOnceAsync(default);
        var row = await db.Captures.AsNoTracking().SingleAsync(); Assert.True(content.Deleted); Assert.NotNull(row.DeletedAtUtc); Assert.Null(row.AnalysisText);
    }
    [Fact]
    public async Task VisionFailureDoesNotDeleteCaptureAndCanBeRetriedExplicitly()
    {
        var (connection, db, house, capture) = await Database(); await using var connectionScope = connection; await using var dbScope = db;
        db.Captures.Add(new CaptureRow { CaptureId = capture, HouseId = house, DeviceId = "device", BlobName = "name", BlobETag = "etag", OccurredAtUtc = DateTimeOffset.UtcNow, ReceivedAtUtc = DateTimeOffset.UtcNow, AnalysisStatus = "pending" }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Incidents.Add(new IncidentRow { EventId = Guid.NewGuid(), HouseId = house, RoomId = "sala", OccurredAtUtc = DateTimeOffset.UtcNow, ReceivedAtUtc = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var content = new Content();
        await new CaptureAnalysis(db, content, new Analyzer(true), TimeProvider.System).RunOnceAsync(default);
        var failed = await db.Captures.AsNoTracking().SingleAsync(); Assert.Equal("failed", failed.AnalysisStatus); Assert.Equal("providerUnavailable", failed.AnalysisError); Assert.False(content.Deleted); Assert.Equal(1, await db.Incidents.CountAsync());
        await db.Captures.Where(x => x.CaptureId == capture).ExecuteUpdateAsync(u => u.SetProperty(x => x.AnalysisStatus, "pending"));
        await new CaptureAnalysis(db, content, new Analyzer(), TimeProvider.System).RunOnceAsync(default);
        var completed = await db.Captures.AsNoTracking().SingleAsync(); Assert.Equal("completed", completed.AnalysisStatus); Assert.Equal("A test image.", completed.AnalysisText);
    }
}
