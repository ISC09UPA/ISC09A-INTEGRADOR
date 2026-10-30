namespace AirportParking.Application.Common.Interfaces;

/// <summary>
/// Servicio de cifrado autenticado simétrico de alta velocidad (AES-256-GCM)
/// para campos sensibles (PII) antes de su persistencia en Azure Database for MySQL.
/// </summary>
public interface ICryptographyService
{
    /// <summary>
    /// Cifra una cadena de texto en claro con AES-256-GCM.
    /// </summary>
    /// <param name="plainText">Texto en claro a cifrar.</param>
    /// <returns>Payload cifrado codificado en Base64 con Nonce y Tag incluidos.</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Descifra un payload Base64 cifrado con AES-256-GCM.
    /// </summary>
    /// <param name="cipherText">Texto cifrado en Base64.</param>
    /// <returns>Texto original en claro.</returns>
    string Decrypt(string cipherText);
}
