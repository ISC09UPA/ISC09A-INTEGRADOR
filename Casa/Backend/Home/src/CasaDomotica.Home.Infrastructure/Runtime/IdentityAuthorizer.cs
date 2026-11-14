using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
namespace CasaDomotica.Home.Infrastructure.Runtime;
public sealed class HomeServiceTokenCache : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _value; private DateTimeOffset _expires;
    public void Invalidate() => _expires = DateTimeOffset.MinValue;
    public async Task<string> GetAsync(Func<Task<string>> fetch, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_value is not null && _expires > DateTimeOffset.UtcNow) return _value;
            _value = await fetch(); _expires = DateTimeOffset.UtcNow.AddSeconds(270); return _value;
        }
        finally { _gate.Release(); }
    }
    public void Dispose() => _gate.Dispose();
}
public sealed class IdentityAuthorizer(HttpClient client, IConfiguration configuration, HomeServiceTokenCache cache) : IUserAuthorizer
{
    public async Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct)
    {
        var root = configuration["Identity:BaseUrl"]?.TrimEnd('/') ?? throw new HomeException("unavailable", "Identity is not configured.");
        if (!Uri.TryCreate(root, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" && !endpoint.IsLoopback) throw new HomeException("unavailable", "Identity endpoint is not trusted.");
        try
        {
            var access = await cache.GetAsync(async () =>
            {
                var privateKey = configuration["Identity:ServicePrivateKeyPem"] ?? throw new HomeException("unavailable", "Service identity is not configured.");
                using var ec = ECDsa.Create(); ec.ImportFromPem(privateKey);
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var assertion = new JwtSecurityToken(new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(ec) { KeyId = "home-v1" }, SecurityAlgorithms.EcdsaSha256)), new JwtPayload { ["iss"] = "service:home", ["sub"] = "service:home", ["aud"] = "Identity.TokenEndpoint", ["client_id"] = "home", ["jti"] = Guid.NewGuid().ToString(), ["iat"] = now, ["exp"] = now + 60 });
                using var tokenResponse = await client.PostAsJsonAsync(root + "/api/v1/identity/internal/token", new { grantType = "urn:ietf:params:oauth:grant-type:jwt-bearer", assertion = new JwtSecurityTokenHandler().WriteToken(assertion) }, ct);
                if (!tokenResponse.IsSuccessStatusCode) throw new HomeException("unavailable", "Identity service authentication failed.");
                var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (token.GetProperty("expiresIn").GetInt32() != 300) throw new HomeException("unavailable", "Identity service token lifetime is incompatible.");
                return token.GetProperty("accessToken").GetString() ?? throw new HomeException("unavailable", "Identity service token is missing.");
            }, ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, root + "/api/v1/identity/internal/authz/check");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            request.Content = JsonContent.Create(new { userId = user.UserId, sessionId = user.SessionId, houseId, action, correlationId });
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (problem.TryGetProperty("code", out var problemCode) && problemCode.GetString() == "sessionRevoked") throw new HomeException("sessionRevoked", "The user session was revoked.");
                return false;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized) cache.Invalidate();
            if (!response.IsSuccessStatusCode) throw new HomeException("unavailable", "Identity authorization is unavailable.");
            var decision = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return decision.GetProperty("decision").GetString() == "allowed" && decision.GetProperty("userId").GetGuid() == user.UserId && decision.GetProperty("sessionId").GetGuid() == user.SessionId && decision.GetProperty("houseId").GetGuid() == houseId && decision.GetProperty("correlationId").GetGuid() == correlationId && decision.GetProperty("action").GetString() == action;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or CryptographicException or FormatException) { throw new HomeException("unavailable", "Identity authorization is unavailable."); }
    }
}
