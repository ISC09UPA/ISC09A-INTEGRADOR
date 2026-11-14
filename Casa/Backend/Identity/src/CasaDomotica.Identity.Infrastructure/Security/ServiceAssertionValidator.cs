using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CasaDomotica.Identity.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CasaDomotica.Identity.Infrastructure.Security;

public sealed record ValidServiceAssertion(string ClientId, string Jti, DateTimeOffset ExpiresAt);
public sealed class ServiceAssertionValidator(IConfiguration configuration)
{
    public ValidServiceAssertion? Validate(string assertion)
    {
        if (string.IsNullOrEmpty(assertion) || assertion.Length > 8192) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var unverified = handler.ReadJwtToken(assertion);
            var client = unverified.Claims.FirstOrDefault(x => x.Type == "client_id")?.Value;
            if (!ServiceAuthorization.IsKnownClient(client)) return null;
            var name = client switch { "home" => "Home", "security" => "Security", "notifications" => "Notifications", "assistant" => "Assistant", _ => throw new InvalidOperationException() };
            var pem = configuration[$"Services:{name}PublicKeyPem"];
            if (string.IsNullOrWhiteSpace(pem)) return null;
            using var key = ECDsa.Create(); key.ImportFromPem(pem);
            if (key.KeySize != 256) return null;
            var selectedPublicKey = key.ExportSubjectPublicKeyInfo();
            foreach (var other in new[] { "Home", "Security", "Notifications", "Assistant" }.Where(x => x != name))
            {
                var otherPem = configuration[$"Services:{other}PublicKeyPem"];
                if (string.IsNullOrWhiteSpace(otherPem)) continue;
                using var otherKey = ECDsa.Create(); otherKey.ImportFromPem(otherPem);
                if (CryptographicOperations.FixedTimeEquals(selectedPublicKey, otherKey.ExportSubjectPublicKeyInfo())) return null;
            }
            // The key is scoped to this validation and disposed below; do not cache a provider that refers to it.
            var parameters = new TokenValidationParameters { ValidIssuer = "service:" + client, ValidAudience = "Identity.TokenEndpoint", IssuerSigningKey = new ECDsaSecurityKey(key), CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }, ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256], RequireSignedTokens = true, RequireExpirationTime = true, ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(10) };
            var principal = handler.ValidateToken(assertion, parameters, out var token);
            if (token is not JwtSecurityToken jwt || principal.FindFirst("sub")?.Value != "service:" + client || principal.FindFirst("client_id")?.Value != client || !long.TryParse(principal.FindFirst("iat")?.Value, out var issued) || jwt.ValidTo <= DateTimeOffset.FromUnixTimeSeconds(issued).UtcDateTime || jwt.ValidTo > DateTimeOffset.FromUnixTimeSeconds(issued).UtcDateTime.AddSeconds(60) || issued > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10 || !Guid.TryParse(principal.FindFirst("jti")?.Value, out var jti)) return null;
            return new(client!, jti.ToString(), new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
        }
        catch (Exception error) when (error is SecurityTokenException or ArgumentException or CryptographicException or FormatException) { return null; }
    }
}
