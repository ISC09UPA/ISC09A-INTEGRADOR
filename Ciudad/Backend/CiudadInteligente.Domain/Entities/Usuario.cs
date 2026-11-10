using CiudadInteligente.Domain.Enums;

namespace CiudadInteligente.Domain.Entities;

/// <summary>Una persona que entra a la app (tabla Usuarios). La contrasena nunca se guarda, solo su hash.</summary>
public class Usuario
{
    public long Id { get; set; }

    /// <summary>Con el que inicia sesion (unico, sin importar mayusculas).</summary>
    public string NombreUsuario { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; }

    /// <summary>PBKDF2-SHA256 con sal (ver Infrastructure/Seguridad/HasherContrasenas).</summary>
    public string HashContrasena { get; set; } = string.Empty;

    public DateTimeOffset CreadoEn { get; set; }
}
