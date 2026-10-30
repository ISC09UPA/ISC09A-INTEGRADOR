using System.Security.Cryptography;
using System.Text;
using AirportParking.Application.Common.Interfaces;

namespace AirportParking.Infrastructure.Cryptography;

/// <summary>
/// Servicio de cifrado a nivel de columna con AES-256-GCM para información PII.
/// Estructura de payload: Nonce (12B) + Tag (16B) + CipherText -> Base64.
/// </summary>
public class Aes256GcmCryptographyService : ICryptographyService
{
    private const int NonceSizeBytes = 12; // 96 bits
    private const int TagSizeBytes = 16;   // 128 bits
    private readonly byte[] _key;

    public Aes256GcmCryptographyService()
    {
        // En producción se inyecta desde Azure Key Vault (ADR-13).
        // Clave de desarrollo fija de 256 bits (32 bytes) derivada con SHA-256.
        _key = SHA256.HashData(Encoding.UTF8.GetBytes("AeroPark_SuperSecret_MasterKey_2026_AES256GCM"));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var tag = new byte[TagSizeBytes];
        var cipherBytes = new byte[plainBytes.Length];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Concatenar: Nonce + Tag + CipherBytes
        var result = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        var fullPayload = Convert.FromBase64String(cipherText);
        if (fullPayload.Length < NonceSizeBytes + TagSizeBytes)
            throw new CryptographicException("Payload cifrado inválido.");

        var nonce = new byte[NonceSizeBytes];
        var tag = new byte[TagSizeBytes];
        var cipherLength = fullPayload.Length - NonceSizeBytes - TagSizeBytes;
        var cipherBytes = new byte[cipherLength];
        var plainBytes = new byte[cipherLength];

        Buffer.BlockCopy(fullPayload, 0, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(fullPayload, NonceSizeBytes, tag, 0, TagSizeBytes);
        Buffer.BlockCopy(fullPayload, NonceSizeBytes + TagSizeBytes, cipherBytes, 0, cipherLength);

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
