using System.Security.Claims;
using AlmacenInteligente.Application.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AlmacenInteligente.Infrastructure.Seguridad;

/// <summary>
/// Al iniciar sesión agrega el nombre y un claim "permiso" por cada permiso de los roles del usuario.
/// Los permisos de cada rol se guardan como claims del rol (sembrados desde <see cref="MatrizPermisos"/>).
/// </summary>
internal sealed class FabricaPrincipal(
    UserManager<Usuario> usuarios, RoleManager<Rol> roles, IOptions<IdentityOptions> opciones)
    : UserClaimsPrincipalFactory<Usuario, Rol>(usuarios, roles, opciones)
{
    public const string ClaimNombre = "nombre";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Usuario usuario)
    {
        var identidad = await base.GenerateClaimsAsync(usuario);
        identidad.AddClaim(new Claim(ClaimNombre, usuario.Nombre));
        return identidad;
    }
}
