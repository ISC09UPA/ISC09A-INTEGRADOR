using System.Security.Cryptography;
using CiudadInteligente.Application.Interfaces;

namespace CiudadInteligente.Infrastructure.Seguridad;

/// <summary>
/// PBKDF2 con SHA-256, 100,000 iteraciones y sal aleatoria de 16 bytes
/// (lo que recomienda OWASP para PBKDF2). Se guarda como
/// "PBKDF2-SHA256$iteraciones$sal$hash" (sal y hash en Base64), asi se
/// pueden subir las iteraciones despues sin romper los hashes viejos.
/// </summary>
public sealed class HasherContrasenas : IHasherContrasenas
{
    private const string Prefijo = "PBKDF2-SHA256";
    private const int Iteraciones = 100_000;
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    public string Hash(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"{Prefijo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string contrasena, string hashGuardado)
    {
        var partes = hashGuardado.Split('$');
        if (partes.Length != 4 || partes[0] != Prefijo || !int.TryParse(partes[1], out var iteraciones))
        {
            return false;
        }

        try
        {
            var sal = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);

            // comparacion en tiempo constante: no deja adivinar por cuanto tarda
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
