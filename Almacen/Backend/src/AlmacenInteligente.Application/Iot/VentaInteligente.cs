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
        var ids = escaneos.Where(e => e.Aceptado && e.ProductoId is not null).Select(e => e.ProductoId!.Value).Distinct().ToList();
        var productos = ids.Count == 0 ? [] : (await consultas.ObtenerProductosDisponiblesAsync(ids, ct)).ToDictionary(p => p.Id);
        return escaneos
            .OrderBy(e => e.Fecha)
            .Select(e =>
            {
                var producto = e.ProductoId is { } id ? productos.GetValueOrDefault(id) : null;
                // Pudo desactivarse entre el escaneo y ahora.
                var rechazo = e.Rechazo ?? (producto is null ? $"El producto de la familia {e.Familia} ya no está disponible." : null);
                return new EscaneoDto(e.Id, e.Fecha, e.Uid, e.Familia, producto, rechazo);
            })
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
    /// <summary>El UID no es hexadecimal o viene vacío.</summary>
    CodigoInvalido = 2,
    /// <summary>Falta la familia o no hay producto con esa referencia.</summary>
    ProductoNoEncontrado = 3,
    ProductoNoDisponible = 4,
    SinSesion = 5,
    /// <summary>La pieza (UID) ya salió en una venta no cancelada: no está en stock.</summary>
    PiezaYaVendida = 6,
    /// <summary>La pieza ya estaba pendiente en la misma sesión (lectura doble del lector).</summary>
    PiezaRepetida = 7,
}

/// <summary>
/// Recibe un escaneo NFC del embebido (cola iot-venta-escaneos). Sin usuario en sesión: lo llama el worker.
/// <paramref name="uidRecibido"/> es el UID de fábrica de la etiqueta (una por pieza física) y <c>familia</c> la
/// referencia del producto en el catálogo. Los rechazos también se anotan en la sesión para que el agente los vea.
/// </summary>
public sealed class ServicioEscaneosIot(
    IProductoRepositorio productos, IVentaRepositorio ventas, ICandadoVentaInteligente candado, IReloj reloj, OpcionesIot opciones)
{
    public async Task<(ResultadoEscaneo Resultado, Guid? ProductoId)> RegistrarAsync(string? uidRecibido, string? familiaRecibida, CancellationToken ct = default)
    {
        string? uid;
        try
        {
            uid = CodigoNfc.Normalizar(uidRecibido);
        }
        catch (DomainException)
        {
            return (ResultadoEscaneo.CodigoInvalido, null);
        }
        if (uid is null)
            return (ResultadoEscaneo.CodigoInvalido, null);

        // La familia es el código NFC del producto: mismo formato hexadecimal que su campo en el catálogo.
        string? familia;
        bool familiaValida = true;
        try
        {
            familia = CodigoNfc.Normalizar(familiaRecibida);
        }
        catch (DomainException)
        {
            (familia, familiaValida) = (familiaRecibida!.Trim(), false);
        }
        var producto = familia is null || !familiaValida ? null : await productos.ObtenerPorCodigoNfcAsync(familia, ct);

        var (resultado, rechazo) =
            familia is null ? (ResultadoEscaneo.ProductoNoEncontrado, $"La pieza {uid} llegó sin familia.")
            : !familiaValida ? (ResultadoEscaneo.ProductoNoEncontrado, $"La familia {familia} no es un código NFC válido (hexadecimal).")
            : producto is null ? (ResultadoEscaneo.ProductoNoEncontrado, $"La familia {familia} no existe en el catálogo.")
            : !producto.EstaDisponible ? (ResultadoEscaneo.ProductoNoDisponible, $"El producto {producto.Referencia} no está aprobado o activo.")
            : (await ventas.PiezasVendidasAsync([uid], ct)).Count > 0 ? (ResultadoEscaneo.PiezaYaVendida, $"La pieza {uid} ({producto.Referencia}) ya se vendió.")
            : (ResultadoEscaneo.Registrado, (string?)null);

        var ahora = reloj.Ahora;
        var registro = await candado.RegistrarEscaneoAsync(uid, familia ?? string.Empty, producto?.Id, rechazo, ahora, ahora - opciones.TimeoutSesion, ct);
        return registro switch
        {
            RegistroEscaneo.SinSesion => (ResultadoEscaneo.SinSesion, producto?.Id),
            RegistroEscaneo.Repetido => (ResultadoEscaneo.PiezaRepetida, producto?.Id),
            _ => (resultado, producto?.Id),
        };
    }
}
