using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

public sealed class IdentityVerificationKey : IDisposable
{
    private readonly ECDsa _key;
    public ECDsaSecurityKey SecurityKey { get; }
    public IdentityVerificationKey(IConfiguration configuration)
    {
        var pem = configuration["Jwt:PublicKeyPem"] ?? throw new InvalidOperationException("Configure the trusted Identity public key.");
        _key = ECDsa.Create();
        try
        {
            _key.ImportFromPem(pem);
            if (_key.KeySize != 256) throw new InvalidOperationException("Identity verification requires a P-256 key.");
            SecurityKey = new ECDsaSecurityKey(_key) { KeyId = configuration["Jwt:KeyId"] ?? "identity-v1" };
        }
        catch { _key.Dispose(); throw; }
    }
    public void Dispose() => _key.Dispose();
}
