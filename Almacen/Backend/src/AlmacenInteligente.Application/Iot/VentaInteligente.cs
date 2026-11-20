using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Application.Iot;

// Modo inteligente de venta: mientras un agente lo tiene, cada producto que pasa por el lector NFC
// se agrega como línea de su venta. Los mismos casos de uso sirven a Blazor y a la API.

/// <summary>Quién tiene el modo inteligente ahora (null si está libre o venció).</summary>
[RequierePermiso(Permisos.Ventas.Crear)]
public sealed record ObtenerModoInteligente : ISolicitud<SesionVentaDto?>;

/// <summary>Toma el lector NFC para el usuario actual. Falla si otro usuario lo tiene vigente.</summary>
[RequierePermiso(Permisos.Ventas.Crear)]
public sealed record TomarModoInteligente(Guid AlmacenId) : ISolicitud<SesionVentaDto>;

/// <summary>Mantiene vivo el candado (cada ~15 s). Falla si ya no es del usuario.</summary>
[RequierePermiso(Permisos.Ventas.Crear)]
public sealed record LatidoModoInteligente : ISolicitud<SesionVentaDto>;

[RequierePermiso(Permisos.Ventas.Crear)]
public sealed record LiberarModoInteligente : ISolicitud<bool>;

/// <summary>Escaneos nuevos de la sesión del usuario. Se entregan una sola vez: quedan marcados como procesados.</summary>
[RequierePermiso(Permisos.Ventas.Crear)]
public sealed record TomarEscaneosPendientes : ISolicitud<IReadOnlyList<EscaneoDto>>;

internal sealed class VentaInteligenteManejador(
    ICandadoVentaInteligente candado, IGestorUsuarios usuarios, IAlmacenRepositorio almacenes, IConsultas consultas,
    IUsuarioActual usuario, IReloj reloj, OpcionesIot opciones) :
    IManejador<ObtenerModoInteligente, SesionVentaDto?>,
    IManejador<TomarModoInteligente, SesionVentaDto>,
    IManejador<LatidoModoInteligente, SesionVentaDto>,
    IManejador<LiberarModoInteligente, bool>,
    IManejador<TomarEscaneosPendientes, IReadOnlyList<EscaneoDto>>
{
    private Guid UsuarioId => usuario.Id!.Value;

    public async Task<SesionVentaDto?> ManejarAsync(ObtenerModoInteligente s, CancellationToken ct)
    {
        var sesion = await candado.ObtenerAsync(ct);
        return sesion is not null && sesion.EstaVigente(reloj.Ahora, opciones.TimeoutSesion) ? await DtoAsync(sesion, ct) : null;
    }

    public async Task<SesionVentaDto> ManejarAsync(TomarModoInteligente s, CancellationToken ct)
    {
        var almacen = await almacenes.ObtenerAsync(s.AlmacenId, ct);
        if (almacen is not { Activo: true })
            throw new DomainException("Elige un almacén activo para el modo inteligente.");

        var ahora = reloj.Ahora;
        if (!await candado.IntentarTomarAsync(UsuarioId, s.AlmacenId, ahora, ahora - opciones.TimeoutSesion, ct))
        {
            var actual = await candado.ObtenerAsync(ct);
            var nombre = actual?.UsuarioId is { } otro ? (await usuarios.ObtenerAsync(otro, ct))?.Nombre : null;
            throw new DomainException($"El modo inteligente lo está usando {nombre ?? "otro usuario"}.");
        }
        return await ActualAsync(ct);
    }

    public async Task<SesionVentaDto> ManejarAsync(LatidoModoInteligente s, CancellationToken ct)
    {
        var ahora = reloj.Ahora;
        var sesion = await candado.ObtenerAsync(ct);
        // Si venció y alguien más lo tomó, el latido ya no aplica; si venció pero nadie lo tomó, se recupera.
        if (sesion is null || sesion.UsuarioId != UsuarioId || !await candado.LatidoAsync(UsuarioId, ahora, ct))
            throw new DomainException("Ya no tienes el modo inteligente: venció o lo tomó otro usuario. Actívalo de nuevo.");
        return await ActualAsync(ct);
    }

    public async Task<bool> ManejarAsync(LiberarModoInteligente s, CancellationToken ct)
    {
        await candado.LiberarAsync(UsuarioId, ct);
        return true;
    }

    public async Task<IReadOnlyList<EscaneoDto>> ManejarAsync(TomarEscaneosPendientes s, CancellationToken ct)
    {
        var escaneos = await candado.TomarEscaneosPendientesAsync(UsuarioId, ct);
        if (escaneos.Count == 0)
            return [];
        var productos = (await consultas.ObtenerProductosDisponiblesAsync(escaneos.Select(e => e.ProductoId).Distinct().ToList(), ct))
            .ToDictionary(p => p.Id);
        return escaneos
            .Where(e => productos.ContainsKey(e.ProductoId))
            .OrderBy(e => e.Fecha)
            .Select(e => new EscaneoDto(e.Id, e.Fecha, productos[e.ProductoId]))
            .ToList();
    }

    private async Task<SesionVentaDto> ActualAsync(CancellationToken ct) =>
        await DtoAsync(await candado.ObtenerAsync(ct) ?? throw new DomainException("No se pudo leer el modo inteligente."), ct);

    private async Task<SesionVentaDto> DtoAsync(SesionVentaInteligente sesion, CancellationToken ct)
    {
        var usuarioId = sesion.UsuarioId!.Value;
        var almacenId = sesion.AlmacenId!.Value;
        var nombre = (await usuarios.ObtenerAsync(usuarioId, ct))?.Nombre ?? "—";
        var almacen = (await almacenes.ObtenerAsync(almacenId, ct))?.Nombre ?? "—";
        return new SesionVentaDto(sesion.SesionId!.Value, usuarioId, nombre, almacenId, almacen,
            sesion.Inicio!.Value, sesion.UltimoLatido!.Value, usuarioId == usuario.Id);
    }
}

public enum ResultadoEscaneo
{
    Registrado = 1,
    CodigoInvalido = 2,
    ProductoNoEncontrado = 3,
    ProductoNoDisponible = 4,
    SinSesion = 5,
}

/// <summary>Recibe un escaneo NFC del embebido (cola iot-venta-escaneos). Sin usuario en sesión: lo llama el worker.</summary>
public sealed class ServicioEscaneosIot(IProductoRepositorio productos, ICandadoVentaInteligente candado, IReloj reloj, OpcionesIot opciones)
{
    public async Task<(ResultadoEscaneo Resultado, Guid? ProductoId)> RegistrarAsync(string? nfcRecibido, CancellationToken ct = default)
    {
        string? nfc;
        try
        {
            nfc = CodigoNfc.Normalizar(nfcRecibido);
        }
        catch (DomainException)
        {
            return (ResultadoEscaneo.CodigoInvalido, null);
        }
        if (nfc is null)
            return (ResultadoEscaneo.CodigoInvalido, null);

        var producto = await productos.ObtenerPorCodigoNfcAsync(nfc, ct);
        if (producto is null)
            return (ResultadoEscaneo.ProductoNoEncontrado, null);
        if (!producto.EstaDisponible)
            return (ResultadoEscaneo.ProductoNoDisponible, producto.Id);

        var ahora = reloj.Ahora;
        return await candado.RegistrarEscaneoAsync(producto.Id, ahora, ahora - opciones.TimeoutSesion, ct)
            ? (ResultadoEscaneo.Registrado, producto.Id)
            : (ResultadoEscaneo.SinSesion, producto.Id);
    }
}
