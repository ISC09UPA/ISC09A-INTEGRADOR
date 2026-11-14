using System.Globalization;
using System.Security.Cryptography;
using CasaDomotica.Identity.Domain.Interfaces;
namespace CasaDomotica.Identity.Infrastructure.Security;
public sealed class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 600000;
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }
    public bool Verify(string hash, string password)
    {
        try
        {
            var parts = hash.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 100000 || count > 2000000) return false;
            var salt = Convert.FromBase64String(parts[2]); var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            return CryptographicOperations.FixedTimeEquals(expected, Rfc2898DeriveBytes.Pbkdf2(password, salt, count, HashAlgorithmName.SHA256, 32));
        }
        catch (FormatException) { return false; }
    }
}
