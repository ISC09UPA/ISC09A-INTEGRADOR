using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CasaDomotica.Home.Application.Tests;
public sealed class HomeApiIntegrationTests
{
    private sealed class TestAuthorizer(Guid house, Guid admin, Guid resident) : IUserAuthorizer
    {
        public Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct) => Task.FromResult(houseId == house && (user.UserId == admin || user.UserId == resident && action == "home:read"));
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid House = Guid.NewGuid(), Admin = Guid.NewGuid(), Resident = Guid.NewGuid();
        public ECDsa SigningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public SqliteConnection Connection = new("Data Source=:memory:");
        public WebApplicationFactory<Program>? Factory;
        public HttpClient? Client;
        public async Task Start()
        {
            await Connection.OpenAsync();
            using (var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(Connection).Options)) await db.Database.EnsureCreatedAsync();
            var config = new Dictionary<string, string?> { ["ConnectionStrings:HomeDb"] = "Server=unused;Database=unused;", ["ConnectionStrings:SignalR"] = "Endpoint=https://example.invalid;AccessKey=AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE=;Version=1.0;", ["Jwt:PublicKeyPem"] = SigningKey.ExportSubjectPublicKeyInfoPem(), ["Jwt:KeyId"] = "test-key", ["IoT:HostName"] = "example.invalid" };
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(config));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<HomeDb>>(); services.RemoveAll<IDbContextOptionsConfiguration<HomeDb>>(); services.RemoveAll<HomeDb>();
                    services.AddDbContext<HomeDb>(options => options.UseSqlite(Connection));
                    services.RemoveAll<IUserAuthorizer>(); services.AddScoped<IUserAuthorizer>(_ => new TestAuthorizer(House, Admin, Resident));
                    foreach (var descriptor in services.Where(x => x.ServiceType == typeof(IHostedService) && (x.ImplementationType == typeof(CommandWorker) || x.ImplementationType == typeof(OutboxWorker) || x.ImplementationType == typeof(VideoCleanupWorker))).ToArray()) services.Remove(descriptor);
                });
            });
            Client = Factory.CreateClient();
        }
        public string Token(Guid user, ECDsa? key = null, Guid? sessionId = null)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = new JwtPayload { ["iss"] = "Identity", ["aud"] = "Home.Public", ["sub"] = user.ToString(), ["sid"] = (sessionId ?? Guid.NewGuid()).ToString(), ["iat"] = now, ["exp"] = now + 300 };
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(key ?? SigningKey) { KeyId = "test-key" }, SecurityAlgorithms.EcdsaSha256)), payload));
        }
        public async Task AddCamera(string resourceId = "camera")
        {
            await using var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(Connection).Options);
            db.Devices.Add(new DeviceRow { DeviceId = "device", HouseId = House, LastReportedAtUtc = DateTimeOffset.UtcNow });
            db.Resources.Add(new ResourceRow { HouseId = House, ResourceId = resourceId, DeviceId = "device", DeviceResourceId = "camera-driver", Kind = "camera" });
            await db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync() { Client?.Dispose(); if (Factory is not null) await Factory.DisposeAsync(); await Connection.DisposeAsync(); SigningKey.Dispose(); }
    }
    [Fact]
    public async Task ProtectedResourcesRejectMissingOrForgedTokens()
    {
        await using var fixture = new Fixture(); await fixture.Start();
        var path = $"/api/v1/houses/{fixture.House}/resources";
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client!.GetAsync(path)).StatusCode);
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin, wrongKey));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task CaptureUploadRejectsMissingAndUnknownOneTimeTokens()
    {
        await using var fixture = new Fixture(); await fixture.Start();
        var path = $"/internal/captures/{Guid.NewGuid()}/upload";
        using var missing = new ByteArrayContent([1, 2, 3]); missing.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client!.PostAsync(path, missing)).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent([1, 2, 3]) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png"); request.Headers.Add("X-Capture-Token", new string('X', 43));
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.SendAsync(request)).StatusCode);
    }
    [Fact]
    public async Task HouseIsolationAndUnconfiguredDoorNeverCreateOrders()
    {
        await using var fixture = new Fixture(); await fixture.Start();
        var client = fixture.Client!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/houses/{Guid.NewGuid()}/resources")).StatusCode);
        var response = await client.GetAsync($"/api/v1/houses/{fixture.House}/resources"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        var key = Guid.NewGuid();
        var body = new { type = "OpenDoor", payload = new { }, idempotencyKey = key, expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(10) };
        response = await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/resources/missing/commands", body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{key}")).StatusCode);
    }
    [Fact]
    public async Task ResidentCannotRequestDoorEvenIfResourceExists()
    {
        await using var fixture = new Fixture(); await fixture.Start();
        await using (var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            db.Devices.Add(new DeviceRow { DeviceId = "isolated-device", HouseId = fixture.House, LastReportedAtUtc = DateTimeOffset.UtcNow });
            db.Resources.Add(new ResourceRow { HouseId = fixture.House, ResourceId = "door", DeviceId = "isolated-device", DeviceResourceId = "physical-door", Kind = "door" });
            await db.SaveChangesAsync();
        }
        var client = fixture.Client!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Resident));
        var body = new { type = "OpenDoor", payload = new { }, idempotencyKey = Guid.NewGuid(), expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(10) };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/resources/door/commands", body)).StatusCode);
        await using var verify = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options);
        Assert.Equal(0, await verify.Commands.CountAsync());
    }
    [Fact]
    public async Task PhysicalCommandHttpReplayKeepsOnePendingOrderAndRejectsChangedContent()
    {
        await using var fixture = new Fixture(); await fixture.Start();
        await using (var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            db.Devices.Add(new DeviceRow { DeviceId = "isolated-device", HouseId = fixture.House, LastReportedAtUtc = DateTimeOffset.UtcNow });
            db.Resources.Add(new ResourceRow { HouseId = fixture.House, ResourceId = "security", DeviceId = "isolated-device", DeviceResourceId = "security", Kind = "security" });
            await db.SaveChangesAsync();
        }
        var client = fixture.Client!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin));
        var key = Guid.NewGuid(); var route = $"/api/v1/houses/{fixture.House}/resources/security/commands";
        var body = new { type = "SetSecurityMode", payload = new { mode = "armed" }, idempotencyKey = key, expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(30) };
        var first = await client.PostAsJsonAsync(route, body); Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("pending", firstResult.GetProperty("status").GetString());
        var replay = await client.PostAsJsonAsync(route, body); Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        var replayResult = await replay.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(firstResult.GetProperty("commandId").GetString(), replayResult.GetProperty("commandId").GetString());
        var changed = new { type = "SetSecurityMode", payload = new { mode = "disarmed" }, idempotencyKey = key, expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(30) };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, changed)).StatusCode);
        var recovered = await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{key}"); Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        var recoveredResult = await recovered.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("pending", recoveredResult.GetProperty("status").GetString());
        await using var verify = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options); Assert.Equal(1, await verify.Commands.CountAsync());
    }
    [Fact]
    public async Task VideoRoutesRejectMissingTokensAndUnconfiguredCamera()
    {
        await using var fixture = new Fixture(); await fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client!.GetAsync($"/api/v1/houses/{fixture.House}/video/sessions/{Guid.NewGuid()}")).StatusCode);
        using var segment = new ByteArrayContent([0x47]);
        segment.Headers.ContentType = new MediaTypeHeaderValue("video/mp2t");
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsync($"/internal/video/{Guid.NewGuid()}/segments/0", segment)).StatusCode);
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin));
        var body = new { resourceId = "camera", idempotencyKey = Guid.NewGuid(), correlationId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/video/sessions", body)).StatusCode);
    }
    [Fact]
    public async Task VideoSessionStartsConnectingAndNeverFabricatesLive()
    {
        await using var fixture = new Fixture(); await fixture.Start(); await fixture.AddCamera();
        var sessionId = Guid.NewGuid();
        fixture.Client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin, sessionId: sessionId));
        var key = Guid.NewGuid();
        var body = new { resourceId = "camera", idempotencyKey = key, correlationId = Guid.NewGuid() };
        var start = await fixture.Client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/video/sessions", body);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var started = await start.Content.ReadFromJsonAsync<JsonElement>();
        var id = started.GetProperty("id").GetGuid();
        Assert.Equal("connecting", started.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, started.GetProperty("streamUrl").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/v1/houses/{fixture.House}/video/sessions/{id}/playlist.m3u8")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/v1/houses/{fixture.House}/video/sessions/{id}/segments/0.ts")).StatusCode);
        var byKey = await fixture.Client.GetAsync($"/api/v1/houses/{fixture.House}/video/sessions/by-key/{key}");
        Assert.Equal(HttpStatusCode.OK, byKey.StatusCode);
        var closed = await fixture.Client.DeleteAsync($"/api/v1/houses/{fixture.House}/video/sessions/{id}");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var ended = await closed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ended", ended.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, ended.GetProperty("streamUrl").ValueKind);
    }
    [Fact]
    public async Task ResidentCannotStartOrReadAnotherUsersVideoSession()
    {
        await using var fixture = new Fixture(); await fixture.Start(); await fixture.AddCamera();
        fixture.Client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin, sessionId: Guid.NewGuid()));
        var adminStart = await fixture.Client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/video/sessions", new { resourceId = "camera", idempotencyKey = Guid.NewGuid(), correlationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Accepted, adminStart.StatusCode);
        var id = (await adminStart.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        fixture.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Resident, sessionId: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/video/sessions", new { resourceId = "camera", idempotencyKey = Guid.NewGuid(), correlationId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/v1/houses/{fixture.House}/video/sessions/{id}")).StatusCode);
    }
}
