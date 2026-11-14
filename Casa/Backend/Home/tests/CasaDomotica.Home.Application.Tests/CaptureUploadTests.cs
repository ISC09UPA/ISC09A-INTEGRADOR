using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using Xunit;

namespace CasaDomotica.Home.Application.Tests;

public sealed class CaptureUploadTests
{
    private sealed class Authorizer : IUserAuthorizer
    {
        public bool Allowed = true;
        public Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct) => Task.FromResult(Allowed && action == "home:control:capture");
    }
    private sealed class Store : IPrivateCaptureStore
    {
        public int Calls;
        public Task<string> CreateAsync(string name, byte[] bytes, string contentType, CancellationToken ct) { Calls++; return Task.FromResult("isolated-etag"); }
    }
    private static byte[] Image()
    {
        using var bitmap = new SKBitmap(8, 8); bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }
    private static async Task<(SqliteConnection Connection, HomeDb Db, CommandRow Command, Guid Capture)> CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        var house = Guid.NewGuid(); var capture = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        db.Devices.Add(new DeviceRow { DeviceId = "device", HouseId = house });
        var command = new CommandRow { CommandId = Guid.NewGuid().ToString(), HouseId = house, LightId = "camera", DeviceId = "device", Type = "CaptureImage", PayloadJson = JsonSerializer.Serialize(new { captureId = capture }), RequestHash = new string('A', 64), IdempotencyKey = Guid.NewGuid().ToString(), UserId = Guid.NewGuid(), SessionId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), RequestedAtUtc = now, ExpiresAtUtc = now.AddSeconds(30), DispatchedAtUtc = now };
        db.Commands.Add(command);
        db.CaptureJobs.Add(new CaptureJobRow { CaptureId = capture, HouseId = house, DeviceId = "device", CommandId = command.CommandId, CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(5) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        return (connection, db, command, capture);
    }
    [Fact]
    public async Task ScopedTokenIsHashedAndUploadIsIdempotentWithoutOverwriting()
    {
        var (connection, db, command, capture) = await CreateDatabase(); await using var connectionScope = connection; await using var dbScope = db;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Capture:UploadBaseUrl"] = "https://example.invalid" }).Build();
        var ticket = await new CaptureUploadTickets(db, config, TimeProvider.System).PrepareAsync(command, default);
        db.ChangeTracker.Clear(); var job = await db.CaptureJobs.AsNoTracking().SingleAsync();
        Assert.NotEqual(ticket.Token, job.UploadTokenHash);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket.Token))), job.UploadTokenHash);
        var store = new Store(); var upload = new CaptureUploadService(db, new Authorizer(), store, TimeProvider.System); var bytes = Image();
        var denied = await Assert.ThrowsAsync<HomeException>(() => upload.UploadAsync(capture, new string('X', 43), "image/png", bytes.Length, new MemoryStream(bytes), default));
        Assert.Equal("unauthorized", denied.Code); Assert.Equal(0, store.Calls);
        var first = await upload.UploadAsync(capture, ticket.Token, "image/png", bytes.Length, new MemoryStream(bytes), default);
        var replay = await upload.UploadAsync(capture, ticket.Token, "image/png", bytes.Length, new MemoryStream(bytes), default);
        Assert.Equal(first, replay); Assert.Equal(1, store.Calls);
        var changed = (byte[])bytes.Clone(); changed[^1] ^= 1;
        var conflict = await Assert.ThrowsAsync<HomeException>(() => upload.UploadAsync(capture, ticket.Token, "image/png", changed.Length, new MemoryStream(changed), default));
        Assert.Equal("duplicateChanged", conflict.Code); Assert.Equal(1, store.Calls);
        Assert.EndsWith($"/{capture:D}.png", first.BlobName);
    }
    [Fact]
    public async Task RevokedPermissionAndInvalidContentNeverReachStorage()
    {
        var (connection, db, command, capture) = await CreateDatabase(); await using var connectionScope = connection; await using var dbScope = db;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Capture:UploadBaseUrl"] = "https://example.invalid" }).Build();
        var ticket = await new CaptureUploadTickets(db, config, TimeProvider.System).PrepareAsync(command, default);
        db.ChangeTracker.Clear(); var store = new Store(); var authorizer = new Authorizer { Allowed = false }; var upload = new CaptureUploadService(db, authorizer, store, TimeProvider.System); var bytes = Image();
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<HomeException>(() => upload.UploadAsync(capture, ticket.Token, "image/png", bytes.Length, new MemoryStream(bytes), default))).Code);
        authorizer.Allowed = true;
        Assert.Equal("invalidPayload", (await Assert.ThrowsAsync<HomeException>(() => upload.UploadAsync(capture, ticket.Token, "image/png", 3, new MemoryStream([1, 2, 3]), default))).Code);
        Assert.Equal(0, store.Calls);
        Assert.Equal("failed", (await db.CaptureJobs.AsNoTracking().SingleAsync()).UploadStatus);
    }
}
