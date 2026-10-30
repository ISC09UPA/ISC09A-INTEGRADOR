using System.Security.Cryptography;
using System.Text;
using AirportParking.Application.Common.Interfaces;
using Konscious.Security.Cryptography;

namespace AirportParking.Infrastructure.Cryptography;

/// <summary>
/// Implementación criptográfica de Argon2id para hashing irreversible de contraseñas.
/// Conforme a ADR-14 y las especificaciones de AGENT_CONTEXT.md.
/// </summary>
public class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;       // 128 bits CSPRNG
    private const int HashSizeBytes = 32;       // 256 bits
    private const int MemoryCostKb = 65536;     // 64 MB
    private const int Iterations = 3;           // 3 iteraciones
    private const int DegreeOfParallelism = 4;  // 4 hilos

    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = ComputeArgon2idHash(password, salt);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool VerifyPassword(string providedPassword, string storedHash, string storedSalt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(storedSalt);
            var expectedHashBytes = Convert.FromBase64String(storedHash);

            var computedHashBytes = ComputeArgon2idHash(providedPassword, saltBytes);

            // Comparación en tiempo constante para mitigar ataques de temporización (Timing Attacks)
            return CryptographicOperations.FixedTimeEquals(computedHashBytes, expectedHashBytes);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ComputeArgon2idHash(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemoryCostKb
        };

        return argon2.GetBytes(HashSizeBytes);
    }
}
