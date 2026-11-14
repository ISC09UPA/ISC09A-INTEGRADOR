using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

public sealed class IdentityKey : IDisposable
{
    private readonly ECDsa key = ECDsa.Create();
    public ECDsaSecurityKey SecurityKey { get; }
    public IdentityKey(IConfiguration config)
    {
        try
        {
            key.ImportFromPem(config["Jwt:PublicKeyPem"] ?? throw new InvalidOperationException("Identity public key is required."));
            if (key.KeySize != 256) throw new InvalidOperationException("Identity public key must be P-256.");
            SecurityKey = new ECDsaSecurityKey(key) { KeyId = config["Jwt:KeyId"] ?? "identity-v1" };
        }
        catch { key.Dispose(); throw; }
    }
    public void Dispose() => key.Dispose();
}
