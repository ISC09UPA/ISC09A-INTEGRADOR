using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using Microsoft.AspNetCore.Components.Authorization;

namespace AlmacenInteligente.Web.Seguridad;

/// <summary>
/// Puerta de las páginas hacia los casos de uso. Cada operación corre en su propio scope de DI,
/// así un circuito de Blazor no comparte un DbContext de larga vida entre clics.
/// </summary>
public sealed class Operaciones(IServiceScopeFactory scopes, AuthenticationStateProvider autenticacion)
{
    public async Task<TResultado> EnviarAsync<TResultado>(ISolicitud<TResultado> solicitud, CancellationToken ct = default)
    {
        var estado = await autenticacion.GetAuthenticationStateAsync();
        await using var scope = scopes.CreateAsyncScope();
        ((UsuarioActualPrincipal)scope.ServiceProvider.GetRequiredService<IUsuarioActual>()).Principal = estado.User;
        return await scope.ServiceProvider.GetRequiredService<IMediador>().EnviarAsync(solicitud, ct);
    }
}
