using AirportParking.Infrastructure.Cryptography;
using Xunit;

namespace AirportParking.UnitTests;

public class CryptographyTests
{
    [Fact]
    public void Argon2id_ShouldHashAndVerifyPasswordSuccessfully()
    {
        // Arrange
        var hasher = new Argon2idPasswordHasher();
        var password = "AeroParkSecurePassword2026!";

        // Act
        var (hash, salt) = hasher.HashPassword(password);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.False(string.IsNullOrWhiteSpace(salt));
        Assert.True(hasher.VerifyPassword(password, hash, salt));
        Assert.False(hasher.VerifyPassword("WrongPassword123!", hash, salt));
    }

    [Fact]
    public void Argon2id_ShouldProduceDifferentSaltsForSamePassword()
    {
        // Arrange
        var hasher = new Argon2idPasswordHasher();
        var password = "IdenticalPassword!";

        // Act
        var result1 = hasher.HashPassword(password);
        var result2 = hasher.HashPassword(password);

        // Assert - Salt must be unique and irreproducible
        Assert.NotEqual(result1.Salt, result2.Salt);
        Assert.NotEqual(result1.Hash, result2.Hash);
    }

    [Fact]
    public void Aes256Gcm_ShouldEncryptAndDecryptAccurately()
    {
        // Arrange
        var crypto = new Aes256GcmCryptographyService();
        var sensitiveData = "laura.sanchez@aeropark.mx (Tel: +52 449 987 6543)";

        // Act
        var encrypted = crypto.Encrypt(sensitiveData);
        var decrypted = crypto.Decrypt(encrypted);

        // Assert
        Assert.NotEqual(sensitiveData, encrypted);
        Assert.Equal(sensitiveData, decrypted);
    }
}
