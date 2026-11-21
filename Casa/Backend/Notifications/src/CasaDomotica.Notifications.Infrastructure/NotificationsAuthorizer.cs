using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CasaDomotica.Notifications.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CasaDomotica.Notifications.Infrastructure;

public sealed class NotificationsAuthorizationException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class NotificationsServiceTokenCache : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _until;

    public void Invalidate() => _until = DateTimeOffset.MinValue;

    public async Task<string> GetAsync(Func<Task<string>> fetch, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && _until > DateTimeOffset.UtcNow) return _token;
            _token = await fetch();
            _until = DateTimeOffset.UtcNow.AddSeconds(270);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}

public sealed class NotificationsAuthorizer(HttpClient client, IConfiguration configuration, NotificationsServiceTokenCache cache) : INotificationsAuthorizer
{
    public async Task<bool> CheckAsync(Guid userId, Guid sessionId, Guid houseId, string action, Guid correlationId, CancellationToken ct)
    {
        var root = configuration["Identity:BaseUrl"]?.TrimEnd('/') ?? throw new NotificationsAuthorizationException("unavailable");
        if (!Uri.TryCreate(root, UriKind.Absolute, out var url) || url.Scheme != "https" && !url.IsLoopback)
            throw new NotificationsAuthorizationException("unavailable");

        try
        {
            var access = await cache.GetAsync(async () =>
            {
                var pem = configuration["Identity:ServicePrivateKeyPem"] ?? throw new NotificationsAuthorizationException("unavailable");
                using var ec = ECDsa.Create();
                ec.ImportFromPem(pem);
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var assertion = new JwtSecurityToken(
                    new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(ec) { KeyId = "notifications-v1" }, SecurityAlgorithms.EcdsaSha256)),
                    new JwtPayload
                    {
                        ["iss"] = "service:notifications",
                        ["sub"] = "service:notifications",
                        ["aud"] = "Identity.TokenEndpoint",
                        ["client_id"] = "notifications",
                        ["jti"] = Guid.NewGuid().ToString(),
                        ["iat"] = now,
                        ["exp"] = now + 60
                    });
                using var tokenResponse = await client.PostAsJsonAsync(
                    root + "/api/v1/identity/internal/token",
                    new { grantType = "urn:ietf:params:oauth:grant-type:jwt-bearer", assertion = new JwtSecurityTokenHandler().WriteToken(assertion) },
                    ct);
                if (!tokenResponse.IsSuccessStatusCode) throw new NotificationsAuthorizationException("unavailable");
                var body = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (body.GetProperty("expiresIn").GetInt32() != 300) throw new NotificationsAuthorizationException("unavailable");
                return body.GetProperty("accessToken").GetString() ?? throw new NotificationsAuthorizationException("unavailable");
            }, ct);

            using var request = new HttpRequestMessage(HttpMethod.Post, root + "/api/v1/identity/internal/authz/check");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            request.Content = JsonContent.Create(new { userId, sessionId, houseId, action, correlationId });
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (problem.TryGetProperty("code", out var code) && code.GetString() == "sessionRevoked")
                    throw new NotificationsAuthorizationException("sessionRevoked");
                return false;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized) cache.Invalidate();
            if (!response.IsSuccessStatusCode) throw new NotificationsAuthorizationException("unavailable");
            var decision = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return decision.GetProperty("decision").GetString() == "allowed" &&
                   decision.GetProperty("userId").GetGuid() == userId &&
                   decision.GetProperty("sessionId").GetGuid() == sessionId &&
                   decision.GetProperty("houseId").GetGuid() == houseId &&
                   decision.GetProperty("action").GetString() == action &&
                   decision.GetProperty("correlationId").GetGuid() == correlationId;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or CryptographicException or FormatException)
        {
            throw new NotificationsAuthorizationException("unavailable");
        }
    }
}
