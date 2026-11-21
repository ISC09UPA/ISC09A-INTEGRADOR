using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CasaDomotica.Security.Infrastructure;

public sealed class SecurityAuthorizationException(string code) : Exception(code) { public string Code { get; } = code; }
public interface ISecurityAuthorizer
{
    Task<bool> CheckAsync(Guid user, Guid session, Guid house, string action, Guid correlation, CancellationToken ct);
}
public sealed class SecurityServiceTokenCache : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1); private string? token; private DateTimeOffset until;
    public void Invalidate() => until = DateTimeOffset.MinValue;
    public async Task<string> GetAsync(Func<Task<string>> fetch, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { if (token is not null && until > DateTimeOffset.UtcNow) return token; token = await fetch(); until = DateTimeOffset.UtcNow.AddSeconds(270); return token; }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
public sealed class SecurityAuthorizer(HttpClient client, IConfiguration configuration, SecurityServiceTokenCache cache) : ISecurityAuthorizer
{
    public async Task<bool> CheckAsync(Guid user, Guid session, Guid house, string action, Guid correlation, CancellationToken ct)
    {
        var root = configuration["Identity:BaseUrl"]?.TrimEnd('/') ?? throw new SecurityAuthorizationException("unavailable");
        if (!Uri.TryCreate(root, UriKind.Absolute, out var url) || url.Scheme != "https" && !url.IsLoopback) throw new SecurityAuthorizationException("unavailable");
        try
        {
            var access = await cache.GetAsync(async () =>
            {
                var pem = configuration["Identity:ServicePrivateKeyPem"] ?? throw new SecurityAuthorizationException("unavailable");
                using var ec = ECDsa.Create(); ec.ImportFromPem(pem);
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var assertion = new JwtSecurityToken(new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(ec) { KeyId = "security-v1" }, SecurityAlgorithms.EcdsaSha256)), new JwtPayload { ["iss"] = "service:security", ["sub"] = "service:security", ["aud"] = "Identity.TokenEndpoint", ["client_id"] = "security", ["jti"] = Guid.NewGuid().ToString(), ["iat"] = now, ["exp"] = now + 60 });
                using var response = await client.PostAsJsonAsync(root + "/api/v1/identity/internal/token", new { grantType = "urn:ietf:params:oauth:grant-type:jwt-bearer", assertion = new JwtSecurityTokenHandler().WriteToken(assertion) }, ct);
                if (!response.IsSuccessStatusCode) throw new SecurityAuthorizationException("unavailable");
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (body.GetProperty("expiresIn").GetInt32() != 300) throw new SecurityAuthorizationException("unavailable");
                return body.GetProperty("accessToken").GetString() ?? throw new SecurityAuthorizationException("unavailable");
            }, ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, root + "/api/v1/identity/internal/authz/check");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            request.Content = JsonContent.Create(new { userId = user, sessionId = session, houseId = house, action, correlationId = correlation });
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (problem.TryGetProperty("code", out var code) && code.GetString() == "sessionRevoked") throw new SecurityAuthorizationException("sessionRevoked");
                return false;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized) cache.Invalidate();
            if (!response.IsSuccessStatusCode) throw new SecurityAuthorizationException("unavailable");
            var decision = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return decision.GetProperty("decision").GetString() == "allowed" && decision.GetProperty("userId").GetGuid() == user && decision.GetProperty("sessionId").GetGuid() == session && decision.GetProperty("houseId").GetGuid() == house && decision.GetProperty("action").GetString() == action && decision.GetProperty("correlationId").GetGuid() == correlation;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or CryptographicException or FormatException)
        { throw new SecurityAuthorizationException("unavailable"); }
    }
}
