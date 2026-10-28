using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlmacenInteligente.Infrastructure.Seguridad;

/// <summary>Un usuario desactivado no puede iniciar sesión.</summary>
internal sealed class GestorInicioSesion(
    UserManager<Usuario> usuarios,
    IHttpContextAccessor contexto,
    IUserClaimsPrincipalFactory<Usuario> fabrica,
    IOptions<IdentityOptions> opciones,
    ILogger<SignInManager<Usuario>> log,
    IAuthenticationSchemeProvider esquemas,
    IUserConfirmation<Usuario> confirmacion)
    : SignInManager<Usuario>(usuarios, contexto, fabrica, opciones, log, esquemas, confirmacion)
{
    public override async Task<bool> CanSignInAsync(Usuario user) =>
        user.Activo && await base.CanSignInAsync(user);
}
