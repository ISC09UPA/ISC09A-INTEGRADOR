using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CasaDomotica.Identity.Domain.Interfaces;
using CasaDomotica.Identity.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
namespace CasaDomotica.Identity.Infrastructure.Security;
public sealed class JwtTokens : ITokenService, IDisposable
{
    private readonly ECDsa _key;
    private readonly ECDsaSecurityKey _securityKey;
    private readonly object _sync = new();
    public JwtTokens(IConfiguration configuration)
    {
        _key = ECDsa.Create();
        var pem = configuration["Jwt:PrivateKeyPem"] ?? throw new InvalidOperationException("Configure Jwt:PrivateKeyPem through protected configuration.");
        _key.ImportFromPem(pem);
        if (_key.KeySize != 256) throw new InvalidOperationException("The Identity signing key must use P-256.");
        _ = _key.ExportECPrivateKey(); // Fail closed when only a public key was supplied.
        _securityKey = new ECDsaSecurityKey(_key) { KeyId = configuration["Jwt:KeyId"] ?? "identity-v1" };
    }
    public SecurityKey VerificationKey => _securityKey;
    public object PublicJwks()
    {
        var p = _key.ExportParameters(false);
        return new { keys = new[] { new { kty = "EC", crv = "P-256", use = "sig", alg = "ES256", kid = _securityKey.KeyId, x = Base64UrlEncoder.Encode(p.Q.X!), y = Base64UrlEncoder.Encode(p.Q.Y!) } } };
    }
    public string GenerateAccessToken(Guid userId, Guid sessionId, IEnumerable<string> audiences) => Sign(new() { ["aud"] = audiences.ToArray(), ["sub"] = userId.ToString(), ["sid"] = sessionId.ToString(), ["scope"] = "home:read home:control:lights" }, 300);
    public string GenerateServiceToken(string clientId, string scope)
    {
        if (!ServiceAuthorization.IsKnownClient(clientId) || scope != "identity:authorize") throw new InvalidOperationException("Service scope is not configured.");
        return Sign(new() { ["aud"] = "Identity.Internal", ["sub"] = "service:" + clientId, ["client_id"] = clientId, ["scope"] = scope }, 300);
    }
    private string Sign(Dictionary<string, object> values, int lifetime)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new JwtPayload { ["iss"] = "Identity", ["iat"] = now, ["exp"] = now + lifetime, ["jti"] = Guid.NewGuid().ToString() };
        foreach (var pair in values) payload[pair.Key] = pair.Value;
        var token = new JwtSecurityToken(new JwtHeader(new SigningCredentials(_securityKey, SecurityAlgorithms.EcdsaSha256)), payload);
        lock (_sync) return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public void Dispose() => _key.Dispose();
}
