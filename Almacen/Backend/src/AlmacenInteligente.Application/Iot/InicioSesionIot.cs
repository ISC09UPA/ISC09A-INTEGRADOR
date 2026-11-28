using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Application.Iot;

/// <summary>
/// Solicitudes de inicio de sesión con el embebido. Las operaciones que deciden (crear, resolver, consumir, cancelar)
/// son atómicas en la base, así dos apps o dos accesos simultáneos no se pisan.
/// </summary>
public interface ISolicitudesInicioSesionIot
{
    /// <summary>Guarda la solicitud solo si no hay otra pendiente vigente. False si la hay.</summary>
    Task<bool> IntentarCrearAsync(SolicitudInicioSesionIot solicitud, CancellationToken ct = default);

    Task<SolicitudInicioSesionIot?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Un acceso del embebido resuelve la solicitud pendiente vigente creada antes de él (si hay):
    /// la completa con el usuario si fue permitido o la niega con el motivo.
    /// </summary>
    Task ResolverConAccesoAsync(bool permitido, Guid? usuarioId, string motivo, DateTimeOffset fechaAcceso, CancellationToken ct = default);

    /// <summary>Pasa la solicitud de completada a consumida y devuelve el usuario. Null si no estaba completada.</summary>
    Task<Guid?> ConsumirAsync(Guid id, CancellationToken ct = default);

    /// <summary>Cancela la solicitud si sigue pendiente.</summary>
    Task CancelarAsync(Guid id, CancellationToken ct = default);
}

public sealed record SolicitudInicioSesionDto(Guid Id, DateTimeOffset Expira, int VigenciaSegundos);

/// <summary>
/// Lo que la app ve al sondear. Estado: pendiente, completada, negada, expirada, consumida o cancelada.
/// Token solo viene (una vez) cuando el estado es completada.
/// </summary>
public sealed record EstadoInicioSesionDto(string Estado, string? Motivo, DateTimeOffset Expira, TokenApiDto? Token);

/// <summary>
/// Inicio de sesión de la app móvil con el embebido (tarjeta NFC + rostro), sin contraseña.
/// No pasa por el mediador: lo llama la API sin usuario en sesión.
/// </summary>
public sealed class ServicioInicioSesionIot(ISolicitudesInicioSesionIot solicitudes, IEmisorTokens emisor, IReloj reloj)
{
    public const string MensajeOcupado = "Hay otro inicio de sesión en curso, espera unos segundos.";

    public async Task<SolicitudInicioSesionDto> CrearAsync(CancellationToken ct = default)
    {
        var solicitud = new SolicitudInicioSesionIot(reloj.Ahora);
        if (!await solicitudes.IntentarCrearAsync(solicitud, ct))
            throw new DomainException(MensajeOcupado);
        return new SolicitudInicioSesionDto(solicitud.Id, solicitud.Expira, SolicitudInicioSesionIot.VigenciaSegundos);
    }

    public async Task<EstadoInicioSesionDto> ConsultarAsync(Guid id, CancellationToken ct = default)
    {
        var solicitud = await solicitudes.ObtenerAsync(id, ct) ?? throw new NoEncontradoException("la solicitud de inicio de sesión", id);
        var estado = solicitud.EstadoEn(reloj.Ahora);
        switch (estado)
        {
            case EstadoSolicitudInicioSesion.Completada:
                // Dos sondeos simultáneos: solo uno consume; el otro ve "consumida".
                if (await solicitudes.ConsumirAsync(id, ct) is not { } usuarioId)
                    return Dto(EstadoSolicitudInicioSesion.Consumida, "El inicio de sesión ya se usó.", solicitud, null);
                try
                {
                    return Dto(estado, null, solicitud, await emisor.EmitirAsync(usuarioId, ct));
                }
                catch (DomainException ex)
                {
                    // Por ejemplo, desactivaron al usuario entre el acceso y el sondeo.
                    return Dto(EstadoSolicitudInicioSesion.Negada, ex.Message, solicitud, null);
                }
            case EstadoSolicitudInicioSesion.Expirada:
                return Dto(estado, "Se acabó el tiempo. Intenta de nuevo.", solicitud, null);
            case EstadoSolicitudInicioSesion.Consumida:
                return Dto(estado, "El inicio de sesión ya se usó.", solicitud, null);
            default:
                return Dto(estado, solicitud.Motivo, solicitud, null);
        }
    }

    public Task CancelarAsync(Guid id, CancellationToken ct = default) => solicitudes.CancelarAsync(id, ct);

    public static string Texto(EstadoSolicitudInicioSesion estado) => estado.ToString().ToLowerInvariant();

    private static EstadoInicioSesionDto Dto(EstadoSolicitudInicioSesion estado, string? motivo, SolicitudInicioSesionIot s, TokenApiDto? token) =>
        new(Texto(estado), motivo, s.Expira, token);
}
