using System.Security.Claims;
using AlmacenInteligente.Application.Abstracciones;

namespace AlmacenInteligente.Web.Seguridad;

/// <summary>
/// En Blazor Server no hay HttpContext durante el circuito: <see cref="Operaciones"/> copia aquí el usuario
/// del circuito antes de ejecutar cada caso de uso.
/// </summary>
public sealed class UsuarioActualPrincipal : IUsuarioActual
{
    public const string ClaimPermiso = "permiso";

    public ClaimsPrincipal Principal { get; set; } = new(new ClaimsIdentity());

    public Guid? Id => Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public bool EstaAutenticado => Principal.Identity?.IsAuthenticated == true && Id is not null;

    public bool TieneRol(string rol) => Principal.IsInRole(rol);

    public bool TienePermiso(string permiso) => Principal.HasClaim(ClaimPermiso, permiso);
}
