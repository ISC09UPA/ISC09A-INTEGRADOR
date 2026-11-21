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
    private sealed class TestAuthorizer(Guid house, Guid admin, Guid resident, Func<string, CancellationToken, Task>? onCheck = null) : IUserAuthorizer
    {
        public async Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct)
        {
            if (onCheck is not null) await onCheck(action, ct);
            return houseId == house && (user.UserId == admin || user.UserId == resident && action == "home:read");
        }
    }
    private sealed class FakeClock(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _now = utcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid House = Guid.NewGuid(), Admin = Guid.NewGuid(), Resident = Guid.NewGuid();
        public ECDsa SigningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public SqliteConnection Connection = new("Data Source=:memory:");
        public WebApplicationFactory<Program>? Factory;
        public HttpClient? Client;
        public async Task Start(Func<string, CancellationToken, Task>? onCheck = null, TimeProvider? clock = null)
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
                    services.RemoveAll<IUserAuthorizer>(); services.AddScoped<IUserAuthorizer>(_ => new TestAuthorizer(House, Admin, Resident, onCheck));
                    if (clock is not null) { services.RemoveAll<TimeProvider>(); services.AddSingleton(clock); }
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
    [Fact]
    public async Task PreAdmissionRejectionsPreserveExplicitProblemCodesWithoutCreatingOrders()
    {
        await using var fixture = new Fixture(); await fixture.Start();
        await using (var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            db.Devices.Add(new DeviceRow { DeviceId = "dev-offline", HouseId = fixture.House, LastReportedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5) });
            db.Devices.Add(new DeviceRow { DeviceId = "dev-online", HouseId = fixture.House, LastReportedAtUtc = DateTimeOffset.UtcNow });
            db.Lights.Add(new LightRow { HouseId = fixture.House, LightId = "offline-light", DeviceId = "dev-offline", LightOn = false, LightingMode = "manual", StateRevision = 1, AppliedAtUtc = DateTimeOffset.UtcNow, StateReportedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5) });
            db.Lights.Add(new LightRow { HouseId = fixture.House, LightId = "auto-light", DeviceId = "dev-online", LightOn = false, LightingMode = "auto", StateRevision = 1, AppliedAtUtc = DateTimeOffset.UtcNow, StateReportedAtUtc = DateTimeOffset.UtcNow });
            db.Lights.Add(new LightRow { HouseId = fixture.House, LightId = "light-1", DeviceId = "dev-online", LightOn = false, LightingMode = "manual", StateRevision = 1, AppliedAtUtc = DateTimeOffset.UtcNow, StateReportedAtUtc = DateTimeOffset.UtcNow });
            db.Resources.Add(new ResourceRow { HouseId = fixture.House, ResourceId = "door", DeviceId = "dev-online", DeviceResourceId = "physical-door", Kind = "door" });
            await db.SaveChangesAsync();
        }
        var client = fixture.Client!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin));

        var offlineResp = await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/lights/offline-light/commands", new { type = "SetLight", payload = new { on = true }, idempotencyKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, offlineResp.StatusCode);
        Assert.Equal("deviceOffline", (await offlineResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var manualResp = await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/lights/auto-light/commands", new { type = "SetLight", payload = new { on = true }, idempotencyKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, manualResp.StatusCode);
        Assert.Equal("manualModeRequired", (await manualResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var expiredResp = await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/resources/door/commands", new { type = "OpenDoor", payload = new { }, idempotencyKey = Guid.NewGuid(), expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expiredResp.StatusCode);
        Assert.Equal("requestExpired", (await expiredResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var expiredLightResp = await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/lights/light-1/commands", new { type = "SetLight", payload = new { on = true }, idempotencyKey = Guid.NewGuid(), expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expiredLightResp.StatusCode);
        Assert.Equal("requestExpired", (await expiredLightResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await using (var verify = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            Assert.Equal(0, await verify.Commands.CountAsync());
        }

        var firstKey = Guid.NewGuid();
        var accepted = await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/lights/auto-light/commands", new { type = "SetLightingMode", payload = new { mode = "manual" }, idempotencyKey = firstKey });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

        var dupChanged = await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/lights/auto-light/commands", new { type = "SetLightingMode", payload = new { mode = "auto" }, idempotencyKey = firstKey });
        Assert.Equal(HttpStatusCode.Conflict, dupChanged.StatusCode);
        Assert.Equal("duplicateChanged", (await dupChanged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var pending = await client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/lights/auto-light/commands", new { type = "SetLightingMode", payload = new { mode = "manual" }, idempotencyKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode);
        Assert.Equal("pendingCommand", (await pending.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
    [Fact]
    public async Task DelayedOpenDoorAdmissionReturns404OnEarlyByKeyQueriesAndReusedKeyNeverDuplicatesOrder()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedOnce = 0;
        await using var fixture = new Fixture();
        await fixture.Start(async (action, ct) =>
        {
            if (action == "home:control:door" && Interlocked.Exchange(ref delayedOnce, 1) == 0)
            {
                entered.TrySetResult();
                await gate.Task.WaitAsync(ct);
            }
        });
        await using (var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            db.Devices.Add(new DeviceRow { DeviceId = "door-device", HouseId = fixture.House, LastReportedAtUtc = DateTimeOffset.UtcNow });
            db.Resources.Add(new ResourceRow { HouseId = fixture.House, ResourceId = "door", DeviceId = "door-device", DeviceResourceId = "principal", Kind = "door" });
            await db.SaveChangesAsync();
        }
        var client = fixture.Client!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin));
        var key = Guid.NewGuid();
        var route = $"/api/v1/houses/{fixture.House}/resources/door/commands";
        var requestBody = new { type = "OpenDoor", payload = new { }, idempotencyKey = key, expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(10) };

        // Start original request; it pauses inside authorization before admission commits.
        var delayedPost = client.PostAsJsonAsync(route, requestBody);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Two 404 by-key lookups plus a successful resources refresh happen while original admission is still in flight.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{key}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{key}")).StatusCode);
        var resourcesResp = await client.GetAsync($"/api/v1/houses/{fixture.House}/resources");
        Assert.Equal(HttpStatusCode.OK, resourcesResp.StatusCode);
        var resources = await resourcesResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, resources[0].GetProperty("pendingCommand").ValueKind);

        // Now the delayed original admission finishes on the server.
        gate.SetResult();
        var originalResponse = await delayedPost;
        Assert.Equal(HttpStatusCode.Accepted, originalResponse.StatusCode);
        var originalId = (await originalResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("commandId").GetString();

        // When the user requests OpenDoor again reusing the conserved key, idempotency returns the same commandId without creating a second order.
        var retriedWithSameKey = await client.PostAsJsonAsync(route, new { type = "OpenDoor", payload = new { }, idempotencyKey = key, expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(10) });
        Assert.Equal(HttpStatusCode.Accepted, retriedWithSameKey.StatusCode);
        Assert.Equal(originalId, (await retriedWithSameKey.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("commandId").GetString());

        var recovered = await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{key}");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(originalId, (await recovered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("commandId").GetString());

        await using (var verify = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            Assert.Equal(1, await verify.Commands.CountAsync());
        }
    }
    [Fact]
    public async Task ByKeyRecoveryAuthoritativelyResolvesServerExpiryAndPreservesClaimedUnknownOrders()
    {
        var t0 = DateTimeOffset.UtcNow;
        var clock = new FakeClock(t0);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedOnce = 0;
        await using var fixture = new Fixture();
        await fixture.Start(async (action, ct) =>
        {
            if (action == "home:control:door" && Interlocked.Exchange(ref delayedOnce, 1) == 0)
            {
                entered.TrySetResult();
                await gate.Task.WaitAsync(ct);
            }
        }, clock);
        await using (var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            db.Devices.Add(new DeviceRow { DeviceId = "door-device", HouseId = fixture.House, LastReportedAtUtc = t0 });
            db.Resources.Add(new ResourceRow { HouseId = fixture.House, ResourceId = "door", DeviceId = "door-device", DeviceResourceId = "principal", Kind = "door" });
            await db.SaveChangesAsync();
        }
        var client = fixture.Client!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(fixture.Admin));

        // Non-UTC offset in expiresAtUtc query parameter is rejected with 400 invalidPayload.
        var badOffsetResp = await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{Guid.NewGuid()}?expiresAtUtc={Uri.EscapeDataString("2026-11-18T12:00:00+02:00")}");
        Assert.Equal(HttpStatusCode.BadRequest, badOffsetResp.StatusCode);

        // 1. Delayed request with future deadline on server clock: by-key returns 404 while still within server deadline.
        var key1 = Guid.NewGuid();
        var deadline1 = t0.AddSeconds(10);
        var encodedDeadline1 = Uri.EscapeDataString(deadline1.ToString("O"));
        var delayedPost = client.PostAsJsonAsync($"/api/v1/houses/{fixture.House}/resources/door/commands", new { type = "OpenDoor", payload = new { }, idempotencyKey = key1, expiresAtUtc = deadline1 });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{key1}?expiresAtUtc={encodedDeadline1}")).StatusCode);

        // 2. Once server clock reaches/passes deadline without admission, by-key authoritatively returns 422 requestExpired
        // and prevents the delayed request from committing afterward.
        clock.Advance(TimeSpan.FromSeconds(11));
        var expiredLookup = await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{key1}?expiresAtUtc={encodedDeadline1}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expiredLookup.StatusCode);
        Assert.Equal("requestExpired", (await expiredLookup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        gate.SetResult();
        var delayedResponse = await delayedPost;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, delayedResponse.StatusCode);
        Assert.Equal("requestExpired", (await delayedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // Simulating a restart / second Home instance (even with clock behind deadline1): tombstone persists in DB and blocks future queries or late admission.
        await using (var freshDb = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            Assert.True(await freshDb.ExpiredCommandKeys.AnyAsync(x => x.HouseId == fixture.House && x.IdempotencyKey == key1.ToString("D")));
            var freshRuntime = new SqlHomeRuntime(freshDb, new FakeClock(t0.AddSeconds(5)));
            var readEx = await Assert.ThrowsAsync<HomeException>(() => freshRuntime.FindByKeyAsync(fixture.House, key1.ToString("D"), CancellationToken.None));
            Assert.Equal("requestExpired", readEx.Code);
            var admitEx = await Assert.ThrowsAsync<HomeException>(() => freshRuntime.AdmitCapabilityAsync(new(fixture.Admin, Guid.NewGuid()), fixture.House, "door", "OpenDoor", "{}", new string('Z', 64), key1.ToString("D"), Guid.NewGuid(), t0, deadline1, CancellationToken.None));
            Assert.Equal("requestExpired", admitEx.Code);
        }

        // 3. Order claimed before expiry whose outcome is still unknown transitions to status "unknown" (Active = true) and returns 200 OK.
        var claimedKey = Guid.NewGuid();
        var unclaimedKey = Guid.NewGuid();
        await using (var db = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            Assert.Equal(0, await db.Commands.CountAsync());
            db.Commands.Add(new CommandRow { CommandId = "cmd-claimed-overdue", HouseId = fixture.House, LightId = "door", DeviceId = "door-device", Type = "OpenDoor", PayloadJson = "{}", RequestHash = new string('A', 64), IdempotencyKey = claimedKey.ToString("D"), Sequence = 1, UserId = fixture.Admin, SessionId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), RequestedAtUtc = t0, ExpiresAtUtc = deadline1, DispatchedAtUtc = t0.AddSeconds(2), Status = "pending", Active = true });
            db.Commands.Add(new CommandRow { CommandId = "cmd-unclaimed-overdue", HouseId = fixture.House, LightId = "door-unclaimed", DeviceId = "door-device", Type = "OpenDoor", PayloadJson = "{}", RequestHash = new string('B', 64), IdempotencyKey = unclaimedKey.ToString("D"), Sequence = 2, UserId = fixture.Admin, SessionId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), RequestedAtUtc = t0, ExpiresAtUtc = deadline1, DispatchedAtUtc = null, Status = "pending", Active = true });
            await db.SaveChangesAsync();
        }

        var claimedResp = await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{claimedKey}?expiresAtUtc={encodedDeadline1}");
        Assert.Equal(HttpStatusCode.OK, claimedResp.StatusCode);
        var claimedJson = await claimedResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cmd-claimed-overdue", claimedJson.GetProperty("commandId").GetString());
        Assert.Equal("unknown", claimedJson.GetProperty("status").GetString());

        var unclaimedResp = await client.GetAsync($"/api/v1/houses/{fixture.House}/commands/by-key/{unclaimedKey}?expiresAtUtc={encodedDeadline1}");
        Assert.Equal(HttpStatusCode.OK, unclaimedResp.StatusCode);
        Assert.Equal("expired", (await unclaimedResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        await using (var verify = new HomeDb(new DbContextOptionsBuilder<HomeDb>().UseSqlite(fixture.Connection).Options))
        {
            var claimedRow = await verify.Commands.SingleAsync(x => x.CommandId == "cmd-claimed-overdue");
            Assert.Equal("unknown", claimedRow.Status);
            Assert.True(claimedRow.Active);
            var unclaimedRow = await verify.Commands.SingleAsync(x => x.CommandId == "cmd-unclaimed-overdue");
            Assert.Equal("expired", unclaimedRow.Status);
            Assert.False(unclaimedRow.Active);
        }
    }
}
