using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CasaDomotica.Security.Infrastructure;
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

public sealed class SecurityApiTests
{
    private sealed class Authorizer(Guid allowedHouse, Guid allowedUser, Guid admin) : ISecurityAuthorizer
    {
        public Task<bool> CheckAsync(Guid user, Guid session, Guid house, string action, Guid correlation, CancellationToken ct) => Task.FromResult(house == allowedHouse && (user == admin || user == allowedUser && action == "security:read"));
    }
    private sealed class TestContent : ICaptureContent
    {
        public bool Available = true;
        public Task<CaptureContent?> ReadAsync(CaptureRow capture, CancellationToken ct) => Task.FromResult<CaptureContent?>(Available ? new([1, 2, 3], "image/png") : null);
        public Task<bool> DeleteAsync(CaptureRow capture, CancellationToken ct) => Task.FromResult(true);
    }
    [Fact]
    public async Task ActivityDetailRequiresValidTokenAndCurrentHouseAccess()
    {
        var house = Guid.NewGuid(); var user = Guid.NewGuid(); var admin = Guid.NewGuid(); var eventId = Guid.NewGuid(); var captureId = Guid.NewGuid();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SecurityDb>().UseSqlite(connection).Options;
        await using (var db = new SecurityDb(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Activity.Add(new ActivityRow { EventId = eventId, HouseId = house, Type = "motion", Result = "armed", RoomId = "sala", OccurredAtUtc = DateTimeOffset.UtcNow, ReceivedAtUtc = DateTimeOffset.UtcNow });
            db.Captures.Add(new CaptureRow { CaptureId = captureId, HouseId = house, DeviceId = "isolated", BlobName = "isolated.png", BlobETag = "test-etag", OccurredAtUtc = DateTimeOffset.UtcNow, ReceivedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var content = new TestContent();
        var config = new Dictionary<string, string?> { ["ConnectionStrings:SecurityDb"] = "Server=unused;Database=unused;", ["Jwt:PublicKeyPem"] = key.ExportSubjectPublicKeyInfoPem(), ["Jwt:KeyId"] = "test-key" };
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development"); builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(config));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SecurityDb>>(); services.RemoveAll<IDbContextOptionsConfiguration<SecurityDb>>(); services.RemoveAll<SecurityDb>();
                services.AddDbContext<SecurityDb>(o => o.UseSqlite(connection));
                services.RemoveAll<ISecurityAuthorizer>(); services.AddScoped<ISecurityAuthorizer>(_ => new Authorizer(house, user, admin));
                services.RemoveAll<ICaptureContent>(); services.AddSingleton<ICaptureContent>(content);
                foreach (var descriptor in services.Where(x => x.ServiceType == typeof(IHostedService) && (x.ImplementationType == typeof(SecurityBusWorker) || x.ImplementationType == typeof(CaptureCleanupWorker) || x.ImplementationType == typeof(CaptureAnalysisWorker))).ToArray()) services.Remove(descriptor);
            });
        });
        using var client = factory.CreateClient(); var path = $"/api/v1/security/houses/{house}/activity/{eventId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        string Token(ECDsa signingKey, string audience = "Security.Public", Guid? subject = null, int expiresIn = 300)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = new JwtPayload { ["iss"] = "Identity", ["aud"] = audience, ["sub"] = (subject ?? user).ToString(), ["sid"] = Guid.NewGuid().ToString(), ["iat"] = now, ["exp"] = now + expiresIn };
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(signingKey) { KeyId = "test-key" }, SecurityAlgorithms.EcdsaSha256)), payload));
        }
        using var wrong = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(wrong));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(key, "Home.Public"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(key, expiresIn: -30));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/security/houses/{house}/captures/{captureId}/content")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(key));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/security/houses/{Guid.NewGuid()}/activity/{eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/security/houses/{Guid.NewGuid()}/diagnostics/bus")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/security/houses/{house}/activity/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        var image = await client.GetAsync($"/api/v1/security/houses/{house}/captures/{captureId}/content");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode); Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType); Assert.True(image.Headers.CacheControl!.NoStore);
        content.Available = false;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/security/houses/{house}/captures/{captureId}/content")).StatusCode);
        content.Available = true;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/security/houses/{Guid.NewGuid()}/captures/{captureId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/v1/security/houses/{house}/captures/{captureId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/security/houses/{house}/captures/{captureId}/analysis", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(key, subject: admin));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/security/houses/{house}/capture-retention")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/security/houses/{house}/capture-retention", new { retentionDays = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/api/v1/security/houses/{house}/captures/{captureId}/analysis", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync($"/api/v1/security/houses/{house}/captures/{captureId}")).StatusCode);
        var deletion = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/security/houses/{house}/captures/{captureId}/deletion");
        Assert.Equal("pending", deletion.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/security/houses/{house}/captures/{captureId}/content")).StatusCode);
    }
}
