using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Application.Usuarios;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Application.Iot;

// Comprobar stock: semáforo verde / amarillo / rojo por producto, con la existencia del almacén del usuario
// (el superadmin ve el total de todos los almacenes).
// Se ve en la web y en la app, y el embebido lo muestra en su pantalla y sus LEDs cuando pasa una pieza
// por el lector y nadie tiene activo el modo inteligente de venta.

/// <param name="AlmacenId">Lo fija el caso de uso con el almacén del usuario; null = todos.</param>
public sealed record FiltroSemaforoStock(string? Busqueda = null, NivelStock? Nivel = null, int NumeroPagina = 1, int Tamano = 50, Guid? AlmacenId = null);

public sealed record SemaforoAlmacenDto(Guid AlmacenId, string Almacen, decimal Cantidad, decimal Minimo, NivelStock Nivel);

/// <summary>Un producto aprobado y activo con su existencia y mínimo sumados en todos los almacenes.</summary>
public sealed record SemaforoProductoDto(
    Guid ProductoId, string Referencia, string Descripcion, string Unidad, string? FamiliaNfc,
    decimal Existencia, decimal Minimo, NivelStock Nivel, IReadOnlyList<SemaforoAlmacenDto> Almacenes);

/// <summary>La página pedida y cuántos productos hay en cada color (con la búsqueda, sin el filtro de color).</summary>
public sealed record SemaforoStockDto(Pagina<SemaforoProductoDto> Productos, int Verdes, int Amarillos, int Rojos);

public sealed record FiltroConsultasStockIot(int NumeroPagina = 1, int Tamano = 50, Guid? AlmacenId = null);

public sealed record ConsultaStockIotDto(
    Guid Id, DateTimeOffset Fecha, string Uid, string Familia, string? Producto,
    decimal? Existencia, decimal? Minimo, NivelStock? Nivel, string Motivo);

[RequierePermiso(Permisos.Almacenes.VerExistencias)]
public sealed record ConsultarSemaforoStock(FiltroSemaforoStock Filtro) : ISolicitud<SemaforoStockDto>;

[RequierePermiso(Permisos.Almacenes.VerExistencias)]
public sealed record ListarConsultasStockIot(FiltroConsultasStockIot Filtro) : ISolicitud<Pagina<ConsultaStockIotDto>>;

/// <summary>Lecturas del semáforo y bitácora de comprobaciones del embebido.</summary>
public interface IConsultasStock
{
    Task<SemaforoStockDto> SemaforoAsync(FiltroSemaforoStock filtro, CancellationToken ct = default);

    /// <summary>Existencia y mínimo del producto en ese almacén, o sumados en todos si es null (0 y 0 si no tiene).</summary>
    Task<(decimal Existencia, decimal Minimo)> TotalesAsync(Guid productoId, Guid? almacenId, CancellationToken ct = default);

    Task<Pagina<ConsultaStockIotDto>> ListarConsultasAsync(FiltroConsultasStockIot filtro, CancellationToken ct = default);

    void AgregarConsulta(ConsultaStockIot consulta);
}

internal sealed class ComprobarStockManejador(IConsultasStock stock, AlcanceAlmacen alcance) :
    IManejador<ConsultarSemaforoStock, SemaforoStockDto>,
    IManejador<ListarConsultasStockIot, Pagina<ConsultaStockIotDto>>
{
    public async Task<SemaforoStockDto> ManejarAsync(ConsultarSemaforoStock s, CancellationToken ct) =>
        await stock.SemaforoAsync(s.Filtro with
        {
            NumeroPagina = Math.Max(s.Filtro.NumeroPagina, 1),
            Tamano = Math.Clamp(s.Filtro.Tamano, 1, 200),
            AlmacenId = await alcance.AlmacenAsync(ct),
        }, ct);

    public async Task<Pagina<ConsultaStockIotDto>> ManejarAsync(ListarConsultasStockIot s, CancellationToken ct) =>
        await stock.ListarConsultasAsync(s.Filtro with
        {
            NumeroPagina = Math.Max(s.Filtro.NumeroPagina, 1),
            Tamano = Math.Clamp(s.Filtro.Tamano, 1, 200),
            AlmacenId = await alcance.AlmacenAsync(ct),
        }, ct);
}

/// <summary>
/// Lo que el embebido recibe en la cola iot-escaneos-respuestas por cada pieza que pasó por el lector.
/// <c>Modo</c> es "venta" o "recepcion" (alguien tiene ese modo inteligente) o "consulta" (comprobar stock).
/// <c>Nivel</c> ("verde", "amarillo", "rojo") indica qué LED prender. En consulta es el nivel de stock (null si no se
/// reconoció el producto); en venta es el resultado: verde agregada, amarillo pieza duplicada, rojo rechazada.
/// </summary>
public sealed record RespuestaEscaneoIot(
    string Modo, bool Aceptado, string Motivo, string? Producto, decimal? Existencia, decimal? Minimo, string? Nivel);

/// <summary>Arma la respuesta del embebido para cada pieza. Sin usuario en sesión: lo llama el worker.</summary>
public sealed class ServicioConsultaStockIot(
    IProductoRepositorio productos, IVentaRepositorio ventas, IConsultasStock stock, IGestorUsuarios usuarios,
    ICandadoVentaInteligente candado, IUnidadDeTrabajo unidad, IReloj reloj)
{
    public const string ModoVenta = "venta";
    public const string ModoConsulta = "consulta";
    public const string ModoRecepcion = "recepcion";

    /// <summary>
    /// Comprobar stock: responde la existencia en el almacén del usuario que tiene sesión en el embebido
    /// (<paramref name="usuarioId"/>) y el nivel, y lo anota en la bitácora. Sin usuario válido no responde stock.
    /// </summary>
    public async Task<RespuestaEscaneoIot> ConsultarAsync(string? uidRecibido, string? familiaRecibida, Guid? usuarioId, CancellationToken ct = default)
    {
        var uid = Normalizar(uidRecibido);
        var familia = Normalizar(familiaRecibida);
        var uidTexto = uid ?? uidRecibido?.Trim() ?? string.Empty;
        var familiaTexto = familia ?? familiaRecibida?.Trim() ?? string.Empty;

        var usuario = usuarioId is { } u ? await usuarios.ObtenerAsync(u, ct) : null;
        var almacenId = usuario is null ? null : AlcanceAlmacen.De(usuario);
        // Una pieza trae su familia; un paquete, el código proveedor. Para comprobar stock sirven los dos.
        var producto = familia is null ? null
            : await productos.ObtenerPorCodigoNfcAsync(familia, ct) ?? await productos.ObtenerPorCodigoProveedorAsync(familia, ct);
        RespuestaEscaneoIot respuesta;
        if (usuario is not { Activo: true })
            respuesta = new RespuestaEscaneoIot(ModoConsulta, false, "Inicia sesión en el lector", null, null, null, null);
        else if (usuario.Rol != Roles.Superadmin && usuario.AlmacenId is null)
            respuesta = new RespuestaEscaneoIot(ModoConsulta, false, "Usuario sin almacén", null, null, null, null);
        else if (producto is null)
            respuesta = new RespuestaEscaneoIot(ModoConsulta, false,
                familia is null ? "Etiqueta sin familia NFC válida" : "Familia no registrada", null, null, null, null);
        else
        {
            var (existencia, minimo) = await stock.TotalesAsync(producto.Id, almacenId, ct);
            var vendida = uid is not null && (await ventas.PiezasVendidasAsync([uid], ct)).Count > 0;
            var motivo = !producto.EstaDisponible ? "Producto no disponible para venta"
                : vendida ? "Esta pieza ya se vendió"
                : "Stock " + Texto(SemaforoStock.Nivel(existencia, minimo));
            respuesta = new RespuestaEscaneoIot(ModoConsulta, true, motivo, $"{producto.Referencia} · {producto.Descripcion}",
                existencia, minimo, Color(SemaforoStock.Nivel(existencia, minimo)));
        }

        stock.AgregarConsulta(new ConsultaStockIot(reloj.Ahora, uidTexto, familiaTexto, producto?.Id,
            respuesta.Existencia, respuesta.Minimo, respuesta.Motivo, almacenId, usuario?.Id));
        await unidad.GuardarAsync(ct);
        return respuesta;
    }

    /// <summary>
    /// La lectura fue a un modo inteligente (venta o recepción): se le dice al embebido qué pasó y la existencia del
    /// producto en el almacén de quien tiene el lector.
    /// </summary>
    public async Task<RespuestaEscaneoIot> RespuestaVentaAsync(ResultadoEscaneo resultado, Guid? productoId, CancellationToken ct = default)
    {
        var sesion = await candado.ObtenerAsync(ct);
        var recepcion = sesion?.Modo == ModoLector.Recepcion;
        var producto = productoId is { } id ? await productos.ObtenerAsync(id, ct) : null;
        decimal? existencia = null, minimo = null;
        if (producto is not null)
            (existencia, minimo) = await stock.TotalesAsync(producto.Id, sesion?.AlmacenId, ct);
        var motivo = resultado switch
        {
            ResultadoEscaneo.Registrado => recepcion ? "Paquete leído" : "Agregada a la venta",
            ResultadoEscaneo.PiezaYaVendida => "Pieza ya vendida",
            ResultadoEscaneo.PiezaRepetida => recepcion ? "Paquete duplicado" : "Pieza duplicada",
            ResultadoEscaneo.ProductoNoDisponible => "Producto no disponible",
            ResultadoEscaneo.ProductoNoEncontrado => recepcion ? "Código proveedor no registrado" : "Familia no registrada",
            ResultadoEscaneo.CodigoInvalido => "UID inválido",
            _ => "Sin venta activa",
        };
        // En venta el LED dice qué pasó con la pieza: verde agregada, amarillo duplicada, rojo rechazada.
        var nivel = resultado switch
        {
            ResultadoEscaneo.Registrado => NivelStock.Verde,
            ResultadoEscaneo.PiezaRepetida => NivelStock.Amarillo,
            _ => NivelStock.Rojo,
        };
        return new RespuestaEscaneoIot(recepcion ? ModoRecepcion : ModoVenta, resultado == ResultadoEscaneo.Registrado, motivo,
            producto is null ? null : $"{producto.Referencia} · {producto.Descripcion}",
            existencia, minimo, Color(nivel));
    }

    public static string Color(NivelStock nivel) => nivel switch
    {
        NivelStock.Verde => "verde",
        NivelStock.Amarillo => "amarillo",
        _ => "rojo",
    };

    private static string Texto(NivelStock nivel) => nivel switch
    {
        NivelStock.Verde => "suficiente",
        NivelStock.Amarillo => "bajo",
        _ => "agotado",
    };

    private static string? Normalizar(string? valor)
    {
        try
        {
            return CodigoNfc.Normalizar(valor);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}
