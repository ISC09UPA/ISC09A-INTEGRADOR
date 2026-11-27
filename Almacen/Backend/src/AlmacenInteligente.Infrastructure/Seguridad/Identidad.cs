using Microsoft.AspNetCore.Identity;

namespace AlmacenInteligente.Infrastructure.Seguridad;

public sealed class Usuario : IdentityUser<Guid>
{
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    /// <summary>UID normalizado de la tarjeta NFC con la que entra por el acceso IoT. Único.</summary>
    public string? CodigoNfc { get; set; }
}

public sealed class Rol : IdentityRole<Guid>
{
    public Rol() { }

    public Rol(string nombre) : base(nombre) { }
}
