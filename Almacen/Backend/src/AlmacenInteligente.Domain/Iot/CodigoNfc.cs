using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Iot;

/// <summary>
/// UID de una tarjeta o etiqueta NFC. Los lectores lo entregan como "04:A2:1B:7C", "04-a2-1b-7c" o "04A21B7C":
/// se guarda y se compara siempre en hexadecimal, en mayúsculas y sin separadores.
/// </summary>
public static class CodigoNfc
{
    public const int LongitudMaxima = 32;

    /// <summary>Devuelve el UID normalizado, o null si viene vacío. Lanza DomainException si no es hexadecimal.</summary>
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        var limpio = new string(valor.Where(c => c is not (':' or '-' or ' ')).ToArray()).Trim().ToUpperInvariant();
        if (limpio.Length == 0)
            return null;
        if (limpio.Length > LongitudMaxima || !limpio.All(Uri.IsHexDigit))
            throw new DomainException($"El código NFC debe ser hexadecimal de máximo {LongitudMaxima} caracteres (por ejemplo 04:A2:1B:7C).");
        return limpio;
    }
}
