using System.Text;
using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CasaDomotica.Home.Application.Tests;

public sealed class VideoSessionTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Authorization : IUserAuthorizer
    {
        public bool Allowed = true, Control = true, View = true;
        public Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct) => Task.FromResult(Allowed && (action == "home:control:capture" && Control || action == "home:read" && View));
    }
    private sealed class Storage : IPrivateVideoStore
    {
        private readonly Dictionary<(Guid, long), byte[]> items = new();
        public int Creates, Deletes; public bool Matches = true;
        public bool FailCreateOnce, FailAfterCreateOnce, FailDeletesOnce;
        public Task<string?> FindAsync(VideoSegmentRow row, byte[] bytes, CancellationToken ct) => Task.FromResult(items.TryGetValue((row.SessionId, row.Sequence), out var existing) && existing.SequenceEqual(bytes) ? "test-etag" : null);
        public Task<string> CreateAsync(VideoSegmentRow row, byte[] bytes, CancellationToken ct)
        {
            Creates++;
            if (FailCreateOnce) { FailCreateOnce = false; throw new InvalidOperationException("ambiguous upload"); }
            items[(row.SessionId, row.Sequence)] = bytes;
            if (FailAfterCreateOnce) { FailAfterCreateOnce = false; throw new InvalidOperationException("ambiguous upload after blob"); }
            return Task.FromResult("test-etag");
        }
        public Task<byte[]?> ReadAsync(VideoSegmentRow row, CancellationToken ct) => Task.FromResult(items.GetValueOrDefault((row.SessionId, row.Sequence)));
        public Task<bool> DeleteAsync(VideoSegmentRow row, CancellationToken ct)
        {
            Deletes++;
            if (FailDeletesOnce) { FailDeletesOnce = false; throw new InvalidOperationException("transient storage failure"); }
            if (Matches) items.Remove((row.SessionId, row.Sequence));
            return Task.FromResult(Matches);
        }
    }
    private static byte[] Segment(byte value = 1)
    {
        var bytes = new byte[188 * 4]; Array.Fill(bytes, value); for (var i = 0; i < bytes.Length; i += 188) { bytes[i] = 0x47; bytes[i + 1] = 0x1f; bytes[i + 2] = 0xff; bytes[i + 3] = 0x10; } return bytes;
    }
    private static async Task<(SqliteConnection Connection, HomeDb Db, Clock Clock, Guid House, UserContext User)> Database(bool camera = true)
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        var clock = new Clock(); var house = Guid.NewGuid(); var user = new UserContext(Guid.NewGuid(), Guid.NewGuid());
        if (camera)
        {
            db.Devices.Add(new DeviceRow { DeviceId = "device", HouseId = house, LastReportedAtUtc = clock.Now });
            db.Resources.Add(new ResourceRow { HouseId = house, ResourceId = "camera", DeviceId = "device", DeviceResourceId = "camera-driver", Kind = "camera" });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        return (connection, db, clock, house, user);
    }
    [Fact]
    public async Task SessionsRequireAnActualCameraAndEnforceOwnershipIdempotenceAndExclusivity()
    {
        var (connection, db, clock, house, user) = await Database(); await using var connScope = connection; await using var dbScope = db;
        var auth = new Authorization(); var sessions = new VideoSessions(db, auth, clock); var key = Guid.NewGuid().ToString();
        var first = await sessions.StartAsync(user, house, "camera", key, Guid.NewGuid(), default); db.ChangeTracker.Clear();
        var replay = await sessions.StartAsync(user, house, "camera", key, Guid.NewGuid(), default); db.ChangeTracker.Clear();
        Assert.Equal(first.Id, replay.Id); Assert.Equal("connecting", first.Status); Assert.Equal(1, await db.VideoSessions.CountAsync()); Assert.Equal(1, await db.Commands.CountAsync());
        var changed = await Assert.ThrowsAsync<HomeException>(() => sessions.StartAsync(user, house, "other", key, Guid.NewGuid(), default)); Assert.Equal("duplicateChanged", changed.Code); db.ChangeTracker.Clear();
        var concurrent = await Assert.ThrowsAsync<HomeException>(() => sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default)); Assert.Equal("pendingCommand", concurrent.Code); db.ChangeTracker.Clear();
        Assert.Null(await sessions.GetAsync(new(user.UserId, Guid.NewGuid()), house, first.Id, default));
        auth.Allowed = false; Assert.Equal("forbidden", (await Assert.ThrowsAsync<HomeException>(() => sessions.GetAsync(user, house, first.Id, default))).Code);
        auth.Allowed = true; Assert.Equal("ended", (await sessions.CloseAsync(user, house, first.Id, default))!.Status);
        Assert.Equal("ended", (await sessions.CloseAsync(user, house, first.Id, default))!.Status);
        db.ChangeTracker.Clear(); Assert.Equal("ended", (await sessions.StartAsync(user, house, "camera", key, Guid.NewGuid(), default)).Status);
        var (otherConnection, otherDb, otherClock, otherHouse, otherUser) = await Database(false); await using var otherConnectionScope = otherConnection; await using var otherDbScope = otherDb;
        var noCamera = await Assert.ThrowsAsync<HomeException>(() => new VideoSessions(otherDb, new Authorization(), otherClock).StartAsync(otherUser, otherHouse, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default));
        Assert.Equal("notFound", noCamera.Code); Assert.Equal(0, await otherDb.VideoSessions.CountAsync());
    }
    [Fact]
    public async Task PrivatePlaylistRequiresRealProducerProgressAndStopsImmediatelyOnClose()
    {
        var (connection, db, clock, house, user) = await Database(); await using var connScope = connection; await using var dbScope = db;
        var auth = new Authorization(); var store = new Storage(); var sessions = new VideoSessions(db, auth, clock);
        var session = await sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default); db.ChangeTracker.Clear();
        await db.Commands.Where(x => x.CommandId == session.CommandId).ExecuteUpdateAsync(x => x.SetProperty(c => c.DispatchedAtUtc, clock.Now));
        var command = await db.Commands.AsNoTracking().SingleAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Video:UploadBaseUrl"] = "https://example.invalid" }).Build();
        var ticket = await new VideoProducerTickets(db, config, clock).PrepareAsync(command, default); db.ChangeTracker.Clear();
        Assert.DoesNotContain(ticket.UploadToken, (await db.VideoSessions.AsNoTracking().SingleAsync()).ProducerTokenHash!);
        var producer = new VideoProducer(db, auth, store, clock); var playback = new VideoPlayback(db, sessions, store, clock); var bytes = Segment();
        Assert.Equal("unauthorized", (await Assert.ThrowsAsync<HomeException>(() => producer.UploadSegmentAsync(session.Id, new string('X', 43), 0, 2000, bytes.Length, new MemoryStream(bytes), default))).Code);
        Assert.Null(await playback.PlaylistAsync(user, house, session.Id, default));
        await producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, bytes.Length, new MemoryStream(bytes), default); db.ChangeTracker.Clear();
        await producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, bytes.Length, new MemoryStream(bytes), default); db.ChangeTracker.Clear();
        Assert.Equal(1, store.Creates);
        var changed = Segment(2); Assert.Equal("duplicateChanged", (await Assert.ThrowsAsync<HomeException>(() => producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, changed.Length, new MemoryStream(changed), default))).Code); db.ChangeTracker.Clear();
        await producer.PublishPlaylistAsync(session.Id, ticket.UploadToken, [0], default); db.ChangeTracker.Clear();
        var playlist = await playback.PlaylistAsync(user, house, session.Id, default); Assert.Contains("#EXTM3U", playlist); Assert.Contains("segments/0.ts", playlist); Assert.DoesNotContain("https://", playlist);
        Assert.Equal(bytes, await playback.SegmentAsync(user, house, session.Id, 0, default));
        Assert.Equal("live", (await sessions.GetAsync(user, house, session.Id, default))!.Status);
        clock.Now = clock.Now.AddSeconds(21);
        await producer.PublishPlaylistAsync(session.Id, ticket.UploadToken, [0], default); db.ChangeTracker.Clear();
        Assert.Null(await playback.PlaylistAsync(user, house, session.Id, default));
        Assert.Equal("connecting", (await sessions.GetAsync(user, house, session.Id, default))!.Status);
        var next = Segment(3); await producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 1, 2000, next.Length, new MemoryStream(next), default); db.ChangeTracker.Clear();
        await producer.PublishPlaylistAsync(session.Id, ticket.UploadToken, [0, 1], default); db.ChangeTracker.Clear();
        Assert.Equal("live", (await sessions.GetAsync(user, house, session.Id, default))!.Status);
        await sessions.CloseAsync(user, house, session.Id, default); db.ChangeTracker.Clear();
        Assert.Null(await playback.PlaylistAsync(user, house, session.Id, default)); Assert.Null(await playback.SegmentAsync(user, house, session.Id, 1, default));
        Assert.Equal("requestExpired", (await Assert.ThrowsAsync<HomeException>(() => producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 2, 2000, next.Length, new MemoryStream(next), default))).Code);
        clock.Now = clock.Now.AddSeconds(31); await new VideoCleanup(db, store, clock).RunOnceAsync(default);
        Assert.Equal(2, store.Deletes); Assert.Equal(2, await db.VideoSegments.CountAsync(x => x.DeletedAtUtc != null));
    }
    [Fact]
    public async Task SegmentBoundsAndRevokedPermissionsPreventProducerWrites()
    {
        var (connection, db, clock, house, user) = await Database(); await using var connScope = connection; await using var dbScope = db;
        var auth = new Authorization(); var sessions = new VideoSessions(db, auth, clock); var session = await sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default); db.ChangeTracker.Clear();
        await db.Commands.Where(x => x.CommandId == session.CommandId).ExecuteUpdateAsync(x => x.SetProperty(c => c.DispatchedAtUtc, clock.Now));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Video:UploadBaseUrl"] = "https://example.invalid" }).Build();
        var ticket = await new VideoProducerTickets(db, config, clock).PrepareAsync(await db.Commands.AsNoTracking().SingleAsync(), default); db.ChangeTracker.Clear();
        var store = new Storage(); var producer = new VideoProducer(db, auth, store, clock); var bytes = Segment();
        Assert.Equal("invalidPayload", (await Assert.ThrowsAsync<HomeException>(() => producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, VideoProducer.MaximumSegmentBytes + 1L, new MemoryStream(bytes), default))).Code);
        bytes[188] = 0; Assert.Equal("invalidPayload", (await Assert.ThrowsAsync<HomeException>(() => producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, bytes.Length, new MemoryStream(bytes), default))).Code);
        auth.Allowed = false; Assert.Equal("forbidden", (await Assert.ThrowsAsync<HomeException>(() => producer.PublishPlaylistAsync(session.Id, ticket.UploadToken, [0], default))).Code);
        Assert.Equal(0, store.Creates);
    }
    [Fact]
    public async Task FailedSegmentWithoutBlobCanRetryTheSameSequence()
    {
        var (connection, db, clock, house, user) = await Database(); await using var connScope = connection; await using var dbScope = db;
        var auth = new Authorization(); var sessions = new VideoSessions(db, auth, clock); var session = await sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default); db.ChangeTracker.Clear();
        await db.Commands.Where(x => x.CommandId == session.CommandId).ExecuteUpdateAsync(x => x.SetProperty(c => c.DispatchedAtUtc, clock.Now));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Video:UploadBaseUrl"] = "https://example.invalid" }).Build();
        var ticket = await new VideoProducerTickets(db, config, clock).PrepareAsync(await db.Commands.AsNoTracking().SingleAsync(), default); db.ChangeTracker.Clear();
        var store = new Storage { FailCreateOnce = true }; var producer = new VideoProducer(db, auth, store, clock); var bytes = Segment();
        await Assert.ThrowsAsync<InvalidOperationException>(() => producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, bytes.Length, new MemoryStream(bytes), default));
        db.ChangeTracker.Clear();
        Assert.Equal("failed", (await db.VideoSegments.AsNoTracking().SingleAsync()).State);
        Assert.Equal(1, store.Creates);
        var retried = await producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, bytes.Length, new MemoryStream(bytes), default);
        Assert.Equal("ready", retried.Status);
        Assert.Equal(2, store.Creates);
        db.ChangeTracker.Clear();
        Assert.Equal("ready", (await db.VideoSegments.AsNoTracking().SingleAsync()).State);
    }
    [Fact]
    public async Task AmbiguousBlobIsAdoptedWithoutUploadingItAgain()
    {
        var (connection, db, clock, house, user) = await Database(); await using var connScope = connection; await using var dbScope = db;
        var auth = new Authorization(); var sessions = new VideoSessions(db, auth, clock); var session = await sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default); db.ChangeTracker.Clear();
        await db.Commands.Where(x => x.CommandId == session.CommandId).ExecuteUpdateAsync(x => x.SetProperty(c => c.DispatchedAtUtc, clock.Now));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Video:UploadBaseUrl"] = "https://example.invalid" }).Build();
        var ticket = await new VideoProducerTickets(db, config, clock).PrepareAsync(await db.Commands.AsNoTracking().SingleAsync(), default); db.ChangeTracker.Clear();
        var store = new Storage { FailAfterCreateOnce = true }; var producer = new VideoProducer(db, auth, store, clock); var bytes = Segment();
        await Assert.ThrowsAsync<InvalidOperationException>(() => producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, bytes.Length, new MemoryStream(bytes), default));
        db.ChangeTracker.Clear();
        Assert.Equal("failed", (await db.VideoSegments.AsNoTracking().SingleAsync()).State);
        var retried = await producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, bytes.Length, new MemoryStream(bytes), default);
        Assert.Equal("ready", retried.Status);
        Assert.Equal(1, store.Creates);
        db.ChangeTracker.Clear();
        var row = await db.VideoSegments.AsNoTracking().SingleAsync();
        Assert.Equal("ready", row.State);
        Assert.Equal("test-etag", row.BlobETag);
    }
    [Fact]
    public async Task CleanupRetriesTransientStorageFailures()
    {
        var (connection, db, clock, house, user) = await Database(); await using var connScope = connection; await using var dbScope = db;
        var auth = new Authorization(); var sessions = new VideoSessions(db, auth, clock); var session = await sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default); db.ChangeTracker.Clear();
        await db.Commands.Where(x => x.CommandId == session.CommandId).ExecuteUpdateAsync(x => x.SetProperty(c => c.DispatchedAtUtc, clock.Now));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Video:UploadBaseUrl"] = "https://example.invalid" }).Build();
        var ticket = await new VideoProducerTickets(db, config, clock).PrepareAsync(await db.Commands.AsNoTracking().SingleAsync(), default); db.ChangeTracker.Clear();
        var store = new Storage { FailDeletesOnce = true }; var producer = new VideoProducer(db, auth, store, clock); var bytes = Segment();
        await producer.UploadSegmentAsync(session.Id, ticket.UploadToken, 0, 2000, bytes.Length, new MemoryStream(bytes), default); db.ChangeTracker.Clear();
        await sessions.CloseAsync(user, house, session.Id, default); db.ChangeTracker.Clear();
        clock.Now = clock.Now.AddSeconds(31);
        await new VideoCleanup(db, store, clock).RunOnceAsync(default); db.ChangeTracker.Clear();
        var afterFailure = await db.VideoSegments.AsNoTracking().SingleAsync();
        Assert.Null(afterFailure.DeletedAtUtc); Assert.Null(afterFailure.CleanupError);
        await new VideoCleanup(db, store, clock).RunOnceAsync(default); db.ChangeTracker.Clear();
        Assert.NotNull((await db.VideoSegments.AsNoTracking().SingleAsync()).DeletedAtUtc);
    }
    [Fact]
    public async Task MembersCanViewButNotStartOrCloseVideo()
    {
        var (connection, db, clock, house, user) = await Database(); await using var connScope = connection; await using var dbScope = db;
        var auth = new Authorization(); var sessions = new VideoSessions(db, auth, clock);
        var session = await sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default); db.ChangeTracker.Clear();
        auth.Control = false; auth.View = true;
        Assert.NotNull(await sessions.GetAsync(user, house, session.Id, default));
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<HomeException>(() => sessions.CloseAsync(user, house, session.Id, default))).Code);
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<HomeException>(() => new VideoSessions(db, auth, clock).StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default))).Code);
        auth.View = false;
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<HomeException>(() => new VideoPlayback(db, sessions, new Storage(), clock).PlaylistAsync(user, house, session.Id, default))).Code);
    }
    [Fact]
    public async Task ClosingSessionExpiresUnconfirmedStartVideoAndUnlocksCamera()
    {
        var (connection, db, clock, house, user) = await Database(); await using var connScope = connection; await using var dbScope = db;
        var auth = new Authorization(); var sessions = new VideoSessions(db, auth, clock);
        var first = await sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default); db.ChangeTracker.Clear();
        await sessions.CloseAsync(user, house, first.Id, default); db.ChangeTracker.Clear();
        var command = await db.Commands.AsNoTracking().SingleAsync();
        Assert.Equal("expired", command.Status);
        Assert.False(command.Active);
        Assert.Contains("\"reasonCode\":\"expired\"", command.ResultJson);
        var second = await sessions.StartAsync(user, house, "camera", Guid.NewGuid().ToString(), Guid.NewGuid(), default);
        Assert.Equal("connecting", second.Status);
        Assert.NotEqual(first.Id, second.Id);
    }
}
