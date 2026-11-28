using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Consultas;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Application.Iot;

// Modo inteligente: mientras un agente lo tiene, lo que pasa por el lector NFC se agrega a su documento.
// En venta son piezas (UID + familia); en recepción, paquetes (código proveedor) y se pregunta de cuántas piezas son.
// Hay un solo lector, así que un solo modo y un solo usuario a la vez. Los mismos casos de uso sirven a Blazor y a la API.
// Latido, liberar y recoger escaneos son [Publica] porque los usan quien vende y quien recibe: el manejador exige
// sesión y alguno de los dos permisos, y solo actúan sobre el candado del propio usuario.

/// <summary>Quién tiene el modo inteligente ahora (null si está libre o venció).</summary>
[Publica]
public sealed record ObtenerModoInteligente : ISolicitud<SesionVentaDto?>;

/// <summary>Toma el lector NFC para vender. Falla si otro usuario lo tiene vigente.</summary>
[RequierePermiso(Permisos.Ventas.Crear)]
public sealed record TomarModoInteligente(Guid AlmacenId) : ISolicitud<SesionVentaDto>;

/// <summary>Toma el lector NFC para recibir paquetes. Falla si otro usuario lo tiene vigente.</summary>
[RequierePermiso(Permisos.Recepciones.Crear)]
public sealed record TomarModoRecepcion(Guid AlmacenId) : ISolicitud<SesionVentaDto>;

/// <summary>Mantiene vivo el candado (cada ~15 s). Falla si ya no es del usuario.</summary>
[Publica]
public sealed record LatidoModoInteligente : ISolicitud<SesionVentaDto>;

[Publica]
public sealed record LiberarModoInteligente : ISolicitud<bool>;

/// <summary>Escaneos nuevos de la sesión del usuario. Se entregan una sola vez: quedan marcados como procesados.</summary>
[Publica]
public sealed record TomarEscaneosPendientes : ISolicitud<IReadOnlyList<EscaneoDto>>;

internal sealed class VentaInteligenteManejador(
    ICandadoVentaInteligente candado, IGestorUsuarios usuarios, IAlmacenRepositorio almacenes, IConsultas consultas,
    IUsuarioActual usuario, IReloj reloj, OpcionesIot opciones, AlcanceAlmacen alcance) :
    IManejador<ObtenerModoInteligente, SesionVentaDto?>,
    IManejador<TomarModoInteligente, SesionVentaDto>,
    IManejador<TomarModoRecepcion, SesionVentaDto>,
    IManejador<LatidoModoInteligente, SesionVentaDto>,
    IManejador<LiberarModoInteligente, bool>,
    IManejador<TomarEscaneosPendientes, IReadOnlyList<EscaneoDto>>
{
    private Guid UsuarioId => usuario.Id!.Value;

    /// <summary>Para las solicitudes [Publica]: sesión y permiso de vender o de recibir.</summary>
    private void AsegurarPermiso()
    {
        if (!usuario.EstaAutenticado || usuario.Id is null)
            throw new NoAutenticadoException();
        if (!usuario.TienePermiso(Permisos.Ventas.Crear) && !usuario.TienePermiso(Permisos.Recepciones.Crear))
            throw new AccesoDenegadoException(Permisos.Ventas.Crear);
    }

    public async Task<SesionVentaDto?> ManejarAsync(ObtenerModoInteligente s, CancellationToken ct)
    {
        AsegurarPermiso();
        var sesion = await candado.ObtenerAsync(ct);
        return sesion is not null && sesion.EstaVigente(reloj.Ahora, opciones.TimeoutSesion) ? await DtoAsync(sesion, ct) : null;
    }

    public Task<SesionVentaDto> ManejarAsync(TomarModoInteligente s, CancellationToken ct) => TomarAsync(s.AlmacenId, ModoLector.Venta, ct);

    public Task<SesionVentaDto> ManejarAsync(TomarModoRecepcion s, CancellationToken ct) => TomarAsync(s.AlmacenId, ModoLector.Recepcion, ct);

    private async Task<SesionVentaDto> TomarAsync(Guid almacenId, ModoLector modo, CancellationToken ct)
    {
        await alcance.AsegurarAsync(almacenId, ct);
        var almacen = await almacenes.ObtenerAsync(almacenId, ct);
        if (almacen is not { Activo: true })
            throw new DomainException("Elige un almacén activo para el modo inteligente.");

        var ahora = reloj.Ahora;
        if (!await candado.IntentarTomarAsync(UsuarioId, almacenId, modo, ahora, ahora - opciones.TimeoutSesion, ct))
        {
            var actual = await candado.ObtenerAsync(ct);
            var nombre = actual?.UsuarioId is { } otro ? (await usuarios.ObtenerAsync(otro, ct))?.Nombre : null;
            throw new DomainException($"El modo inteligente lo está usando {nombre ?? "otro usuario"}.");
        }
        return await ActualAsync(ct);
    }

    public async Task<SesionVentaDto> ManejarAsync(LatidoModoInteligente s, CancellationToken ct)
    {
        AsegurarPermiso();
        var ahora = reloj.Ahora;
        var sesion = await candado.ObtenerAsync(ct);
        // Si venció y alguien más lo tomó, el latido ya no aplica; si venció pero nadie lo tomó, se recupera.
        if (sesion is null || sesion.UsuarioId != UsuarioId || !await candado.LatidoAsync(UsuarioId, ahora, ct))
            throw new DomainException("Ya no tienes el modo inteligente: venció o lo tomó otro usuario. Actívalo de nuevo.");
        return await ActualAsync(ct);
    }

    public async Task<bool> ManejarAsync(LiberarModoInteligente s, CancellationToken ct)
    {
        AsegurarPermiso();
        await candado.LiberarAsync(UsuarioId, ct);
        return true;
    }

    public async Task<IReadOnlyList<EscaneoDto>> ManejarAsync(TomarEscaneosPendientes s, CancellationToken ct)
    {
        AsegurarPermiso();
        var escaneos = await candado.TomarEscaneosPendientesAsync(UsuarioId, ct);
        if (escaneos.Count == 0)
            return [];
        var ids = escaneos.Where(e => e.Aceptado && e.ProductoId is not null).Select(e => e.ProductoId!.Value).Distinct().ToList();
        var mio = await alcance.AlmacenAsync(ct);
        var productos = ids.Count == 0 ? [] : AlcanceAlmacen.Filtrar(await consultas.ObtenerProductosDisponiblesAsync(ids, ct), mio).ToDictionary(p => p.Id);
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
            sesion.Inicio!.Value, sesion.UltimoLatido!.Value, usuarioId == usuario.Id, sesion.Modo);
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
        // En recepción el texto grabado es el código proveedor del paquete; en venta, la familia de la pieza.
        var ahora = reloj.Ahora;
        var sesion = await candado.ObtenerAsync(ct);
        if (sesion is { Modo: ModoLector.Recepcion } && sesion.EstaVigente(ahora, opciones.TimeoutSesion))
            return await RegistrarPaqueteAsync(uid, familia, familiaValida, ahora, ct);

        var producto = familia is null || !familiaValida ? null : await productos.ObtenerPorCodigoNfcAsync(familia, ct);

        var (resultado, rechazo) =
            familia is null ? (ResultadoEscaneo.ProductoNoEncontrado, $"La pieza {uid} llegó sin familia.")
            : !familiaValida ? (ResultadoEscaneo.ProductoNoEncontrado, $"La familia {familia} no es un código NFC válido (hexadecimal).")
            : producto is null ? (ResultadoEscaneo.ProductoNoEncontrado, $"La familia {familia} no existe en el catálogo.")
            : !producto.EstaDisponible ? (ResultadoEscaneo.ProductoNoDisponible, $"El producto {producto.Referencia} no está aprobado o activo.")
            : (await ventas.PiezasVendidasAsync([uid], ct)).Count > 0 ? (ResultadoEscaneo.PiezaYaVendida, $"La pieza {uid} ({producto.Referencia}) ya se vendió.")
            : (ResultadoEscaneo.Registrado, (string?)null);

        return await GuardarAsync(uid, familia, producto?.Id, resultado, rechazo, ahora, ct);
    }

    /// <summary>
    /// Recepción: el código grabado en el paquete es el código proveedor del producto. No se revisa si "ya se vendió"
    /// (no son piezas); un mismo paquete (UID) leído dos veces en la sesión es un duplicado.
    /// </summary>
    private async Task<(ResultadoEscaneo, Guid?)> RegistrarPaqueteAsync(string uid, string? codigo, bool valido, DateTimeOffset ahora, CancellationToken ct)
    {
        var producto = codigo is null || !valido ? null : await productos.ObtenerPorCodigoProveedorAsync(codigo, ct);
        var (resultado, rechazo) =
            codigo is null ? (ResultadoEscaneo.ProductoNoEncontrado, $"El paquete {uid} no trae código proveedor grabado.")
            : !valido ? (ResultadoEscaneo.ProductoNoEncontrado, $"El código {codigo} no es un código NFC válido (hexadecimal).")
            : producto is null ? (ResultadoEscaneo.ProductoNoEncontrado, $"El código proveedor {codigo} no está en ningún producto.")
            : !producto.EstaDisponible ? (ResultadoEscaneo.ProductoNoDisponible, $"El producto {producto.Referencia} no está aprobado o activo.")
            : (ResultadoEscaneo.Registrado, (string?)null);
        return await GuardarAsync(uid, codigo, producto?.Id, resultado, rechazo, ahora, ct);
    }

    private async Task<(ResultadoEscaneo, Guid?)> GuardarAsync(
        string uid, string? familia, Guid? productoId, ResultadoEscaneo resultado, string? rechazo, DateTimeOffset ahora, CancellationToken ct)
    {
        var registro = await candado.RegistrarEscaneoAsync(uid, familia ?? string.Empty, productoId, rechazo, ahora, ahora - opciones.TimeoutSesion, ct);
        return registro switch
        {
            RegistroEscaneo.SinSesion => (ResultadoEscaneo.SinSesion, productoId),
            RegistroEscaneo.Repetido => (ResultadoEscaneo.PiezaRepetida, productoId),
            _ => (resultado, productoId),
        };
    }
}
