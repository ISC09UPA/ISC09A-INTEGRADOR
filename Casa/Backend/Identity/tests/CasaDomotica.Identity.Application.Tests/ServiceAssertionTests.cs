using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using CasaDomotica.Identity.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CasaDomotica.Identity.Application.Tests;
public sealed class ServiceAssertionTests
{
    [Fact]
    public void OnlyThePinnedServiceKeyCanAssertItsIdentity()
    {
        using var home = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var security = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Services:HomePublicKeyPem"] = home.ExportSubjectPublicKeyInfoPem(), ["Services:SecurityPublicKeyPem"] = security.ExportSubjectPublicKeyInfoPem() }).Build();
        var validator = new ServiceAssertionValidator(config);
        Assert.Equal("home", validator.Validate(Create(home, "home"))?.ClientId);
        Assert.Equal("security", validator.Validate(Create(security, "security"))?.ClientId);
        Assert.Null(validator.Validate(Create(home, "security")));
        Assert.Null(validator.Validate(Create(security, "home")));
        Assert.Null(validator.Validate(Create(home, "assistant")));
        Assert.Null(validator.Validate(Create(home, "unknown")));
        Assert.Null(validator.Validate("not-a-token"));
    }
    [Fact]
    public void RejectsAssertionsWithWrongAudienceOrExcessLifetime()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Services:HomePublicKeyPem"] = key.ExportSubjectPublicKeyInfoPem() }).Build();
        var validator = new ServiceAssertionValidator(config);
        Assert.Null(validator.Validate(Create(key, "home", 61)));
        Assert.Null(validator.Validate(Create(key, "home", 0)));
        Assert.Null(validator.Validate(Create(key, "home", 60, "wrong-audience")));
    }
    [Fact]
    public void RejectsKeysReusedAcrossDifferentServices()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportSubjectPublicKeyInfoPem();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Services:HomePublicKeyPem"] = publicKey, ["Services:SecurityPublicKeyPem"] = publicKey }).Build();
        Assert.Null(new ServiceAssertionValidator(config).Validate(Create(key, "home")));
    }
    private static string Create(ECDsa key, string client, int lifetime = 60, string audience = "Identity.TokenEndpoint")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new JwtPayload { ["iss"] = "service:" + client, ["sub"] = "service:" + client, ["aud"] = audience, ["client_id"] = client, ["jti"] = Guid.NewGuid().ToString(), ["iat"] = now, ["exp"] = now + lifetime };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(key), SecurityAlgorithms.EcdsaSha256)), payload));
    }
}
