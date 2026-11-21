using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CasaDomotica.Notifications.Functions;

public sealed class IdentityKey : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create();
    public ECDsaSecurityKey SecurityKey { get; }

    public IdentityKey(IConfiguration config)
    {
        try
        {
            _key.ImportFromPem(config["Jwt:PublicKeyPem"] ?? throw new InvalidOperationException("Identity public key is required."));
            if (_key.KeySize != 256) throw new InvalidOperationException("Identity public key must be P-256.");
            SecurityKey = new ECDsaSecurityKey(_key) { KeyId = config["Jwt:KeyId"] ?? "identity-v1" };
        }
        catch
        {
            _key.Dispose();
            throw;
        }
    }

    public void Dispose() => _key.Dispose();
}
