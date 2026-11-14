using System.Security.Claims;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Web.Seguridad;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace AlmacenInteligente.Web.Components.Comunes;

/// <summary>Base de páginas y diálogos: ejecuta casos de uso y convierte sus errores en mensajes para el usuario.</summary>
public abstract class PaginaBase : ComponentBase
{
    [Inject] protected Operaciones Operaciones { get; set; } = default!;
    [Inject] protected ISnackbar Avisos { get; set; } = default!;
    [Inject] private ILogger<PaginaBase> Log { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    protected ClaimsPrincipal Usuario { get; private set; } = new(new ClaimsIdentity());

    protected override async Task OnInitializedAsync()
    {
        if (EstadoAutenticacion is not null)
            Usuario = (await EstadoAutenticacion).User;
        await AlIniciarAsync();
    }

    protected virtual Task AlIniciarAsync() => Task.CompletedTask;

    protected bool Puede(string permiso) => Usuario.HasClaim(UsuarioActualPrincipal.ClaimPermiso, permiso);

    /// <summary>Ejecuta el caso de uso; si falla muestra el motivo y devuelve <c>Exito = false</c>.</summary>
    protected async Task<(bool Exito, T? Valor)> IntentarAsync<T>(ISolicitud<T> solicitud, string? mensajeExito = null)
    {
        var error = await EjecutarAsync(solicitud);
        if (error.Mensaje is not null)
        {
            Avisos.Add(error.Mensaje, Severity.Error);
            return (false, default);
        }
        if (mensajeExito is not null)
            Avisos.Add(mensajeExito, Severity.Success);
        return (true, error.Valor);
    }

    /// <summary>Como <see cref="IntentarAsync{T}"/> pero devuelve el mensaje de error en vez de mostrarlo (para diálogos).</summary>
    protected async Task<(T? Valor, string? Mensaje)> EjecutarAsync<T>(ISolicitud<T> solicitud)
    {
        try
        {
            return (await Operaciones.EnviarAsync(solicitud), null);
        }
        catch (ValidacionException ex)
        {
            return (default, string.Join(" ", ex.Errores.SelectMany(e => e.Value).Distinct()));
        }
        catch (Exception ex) when (ex is DomainException or AccesoDenegadoException or NoAutenticadoException or NoEncontradoException)
        {
            return (default, ex.Message);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Error al ejecutar {Solicitud}", solicitud.GetType().Name);
            return (default, "Ocurrió un error inesperado. Intenta de nuevo.");
        }
    }
}
