namespace AirportParking.Application.Common.Interfaces;

/// <summary>
/// Servicio de hashing irreversible de contraseñas mediante Argon2id con salt único por usuario.
/// Imposibilidad criptográfica y matemática de descifrado o reversión.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Genera un hash irreversible a partir de la contraseña en claro y una sal criptográfica única.
    /// </summary>
    /// <param name="password">Contraseña en claro introducida por el usuario.</param>
    /// <returns>Tupla con el hash Argon2id en Base64 y el salt CSPRNG generado.</returns>
    (string Hash, string Salt) HashPassword(string password);

    /// <summary>
    /// Verifica si la contraseña proporcionada coincide con el hash almacenado mediante comparación en tiempo constante.
    /// </summary>
    /// <param name="providedPassword">Contraseña enviada para verificación.</param>
    /// <param name="storedHash">Hash Argon2id almacenado.</param>
    /// <param name="storedSalt">Salt almacenado correspondiente a la cuenta.</param>
    /// <returns>True si el hash recalculado coincide en tiempo constante; false en caso contrario.</returns>
    bool VerifyPassword(string providedPassword, string storedHash, string storedSalt);
}
