using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Application.Iot;

[RequierePermiso(Permisos.Iot.VerAccesos)]
public sealed record ListarAccesosIot(FiltroAccesosIot Filtro) : ISolicitud<Pagina<AccesoIotDto>>;

internal sealed class AccesosManejador(IAccesosIotRepositorio accesos) : IManejador<ListarAccesosIot, Pagina<AccesoIotDto>>
{
    public Task<Pagina<AccesoIotDto>> ManejarAsync(ListarAccesosIot s, CancellationToken ct) =>
        accesos.ListarAsync(s.Filtro with { NumeroPagina = Math.Max(1, s.Filtro.NumeroPagina), Tamano = Math.Clamp(s.Filtro.Tamano, 1, 200) }, ct);
}

/// <summary>
/// Decide un intento de acceso del embebido: tarjeta NFC (quién dice ser) + foto de la webcam (¿es él?).
/// No pasa por el mediador porque no hay usuario en sesión: lo invoca el worker de Service Bus.
/// Las fallas que no se arreglan reintentando (tarjeta, imagen, modelo) se responden como negadas;
/// las demás (red, base) se propagan para que el worker reintente el mensaje.
/// </summary>
public sealed class ServicioAccesoIot(
    IGestorUsuarios usuarios, IReconocimientoFacial reconocimiento, IAccesosIotRepositorio accesos,
    ISolicitudesInicioSesionIot iniciosSesion, IUnidadDeTrabajo unidad, IReloj reloj, OpcionesIot opciones)
{
    public async Task<RespuestaAccesoIot> EvaluarAsync(string? nfcRecibido, byte[] imagen, CancellationToken ct = default)
    {
        string? nfc;
        try
        {
            nfc = CodigoNfc.Normalizar(nfcRecibido);
        }
        catch (Domain.Common.DomainException)
        {
            return await RegistrarAsync(nfcRecibido, null, Negado("Código NFC inválido"), ct);
        }
        if (nfc is null)
            return await RegistrarAsync(null, null, Negado("Falta el código NFC"), ct);

        var usuario = await usuarios.ObtenerPorNfcAsync(nfc, ct);
        if (usuario is null)
            return await RegistrarAsync(nfc, null, Negado("Tarjeta no registrada"), ct);
        if (!usuario.Activo)
            return await RegistrarAsync(nfc, usuario, Negado("Usuario desactivado"), ct);
        if (!EsImagenValida(imagen))
            return await RegistrarAsync(nfc, usuario, Negado("Imagen inválida: se espera un JPEG"), ct);

        DecisionAcceso decision;
        try
        {
            var predicciones = await reconocimiento.ClasificarAsync(imagen, ct);
            decision = ReglaAcceso.Decidir(usuario.Id, predicciones, opciones.UmbralConfianza);
        }
        catch (ImagenInvalidaException ex)
        {
            decision = Negado($"Imagen inválida: {ex.Message}");
        }
        catch (ReconocimientoNoDisponibleException ex)
        {
            decision = Negado(ex.Message);
        }
        return await RegistrarAsync(nfc, usuario, decision, ct);
    }

    /// <summary>JPEG: empieza con FF D8 FF. Máximo 4 MB (límite de Custom Vision).</summary>
    public static bool EsImagenValida(byte[]? imagen) =>
        imagen is { Length: > 3 and <= 4 * 1024 * 1024 } && imagen[0] == 0xFF && imagen[1] == 0xD8 && imagen[2] == 0xFF;

    private static DecisionAcceso Negado(string motivo) => new(false, 0, motivo);

    private async Task<RespuestaAccesoIot> RegistrarAsync(string? nfc, UsuarioDto? usuario, DecisionAcceso decision, CancellationToken ct)
    {
        var ahora = reloj.Ahora;
        accesos.Agregar(new AccesoIot(ahora, nfc, usuario?.Id, decision.Permitido, decision.Confianza, decision.Motivo));
        await unidad.GuardarAsync(ct);
        // Si la app móvil está esperando un inicio de sesión con el embebido, este acceso lo resuelve.
        await iniciosSesion.ResolverConAccesoAsync(decision.Permitido, usuario?.Id, decision.Motivo, ahora, ct);
        return new RespuestaAccesoIot(decision.Permitido, usuario?.Id, usuario?.Nombre, Math.Round(decision.Confianza, 4), decision.Motivo);
    }
}
