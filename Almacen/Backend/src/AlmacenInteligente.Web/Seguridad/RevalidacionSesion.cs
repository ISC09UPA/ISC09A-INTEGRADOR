using System.Security.Claims;
using AlmacenInteligente.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AlmacenInteligente.Web.Seguridad;

/// <summary>
/// Revisa cada minuto que el sello de seguridad del usuario siga vigente, para que un usuario
/// desactivado o con rol cambiado pierda su circuito abierto.
/// </summary>
internal sealed class RevalidacionSesion(
    ILoggerFactory log, IServiceScopeFactory scopes, IOptions<IdentityOptions> opciones)
    : RevalidatingServerAuthenticationStateProvider(log)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState estado, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await usuarios.GetUserAsync(estado.User);
        if (usuario is null || !usuario.Activo)
            return false;
        if (!usuarios.SupportsUserSecurityStamp)
            return true;
        var sello = estado.User.FindFirstValue(opciones.Value.ClaimsIdentity.SecurityStampClaimType);
        return sello == await usuarios.GetSecurityStampAsync(usuario);
    }
}
