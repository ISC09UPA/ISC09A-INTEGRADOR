using System.Security.Claims;
using AlmacenInteligente.Application.Abstracciones;
using Microsoft.AspNetCore.Http;

namespace AlmacenInteligente.Infrastructure.Seguridad;

/// <summary>
/// Usuario tomado de los claims de la petición (JWT en la API, cookie en Blazor).
/// Los permisos viajan como claims "permiso" que se emiten al iniciar sesión a partir de los roles.
/// </summary>
internal sealed class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    public const string ClaimPermiso = "permiso";

    private ClaimsPrincipal? Principal => accesor.HttpContext?.User;

    public Guid? Id => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated == true && Id is not null;

    public bool TieneRol(string rol) => Principal?.IsInRole(rol) == true;

    public bool TienePermiso(string permiso) => Principal?.HasClaim(ClaimPermiso, permiso) == true;
}
