using Microsoft.AspNetCore.Identity;

namespace AlmacenInteligente.Infrastructure.Seguridad;

public sealed class Usuario : IdentityUser<Guid>
{
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

public sealed class Rol : IdentityRole<Guid>
{
    public Rol() { }

    public Rol(string nombre) : base(nombre) { }
}
