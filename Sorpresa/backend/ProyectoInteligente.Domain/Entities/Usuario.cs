using LockerSmart.Domain.Enums;

namespace LockerSmart.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Rol Rol { get; set; } = Rol.Usuario;
    public string? FaceId { get; set; }      // ID de persona en Face API
    public string? FotoUrl { get; set; }     // URL en Blob Storage
}
