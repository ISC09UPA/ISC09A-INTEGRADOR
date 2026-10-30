using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class Customer : BaseEntity
{
    public string? ExternalId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    
    /// <summary>
    /// Correo electrónico cifrado a nivel de columna mediante AES-256-GCM.
    /// </summary>
    public string CorreoCifrado { get; set; } = string.Empty;
    
    /// <summary>
    /// Hash ciego (blind index) SHA-256 para búsquedas exactas sin descifrar el correo.
    /// </summary>
    public string CorreoHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Teléfono cifrado a nivel de columna mediante AES-256-GCM.
    /// </summary>
    public string TelefonoCifrado { get; set; } = string.Empty;
    
    /// <summary>
    /// Hash unidireccional generado con Argon2id + salt aleatorio de 128 bits. Imposible de descifrar.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Salt criptográfico único generado por CSPRNG.
    /// </summary>
    public string PasswordSalt { get; set; } = string.Empty;
    
    public string Idioma { get; set; } = "es-MX";
    public string Estado { get; set; } = "ACTIVE";
    public string Role { get; set; } = "CUSTOMER"; // CUSTOMER, ADMIN, SUPERVISOR
    
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
