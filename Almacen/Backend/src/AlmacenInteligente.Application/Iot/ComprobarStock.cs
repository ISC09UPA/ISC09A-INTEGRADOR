using AlmacenInteligente.Application.Abstracciones;
using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Application.Seguridad;
using AlmacenInteligente.Application.Terceros;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Iot;

namespace AlmacenInteligente.Application.Iot;

// Comprobar stock: semáforo verde / amarillo / rojo por producto (total de todos los almacenes).
// Se ve en la web y en la app, y el embebido lo muestra en su pantalla y sus LEDs cuando pasa una pieza
// por el lector y nadie tiene activo el modo inteligente de venta.

public sealed record FiltroSemaforoStock(string? Busqueda = null, NivelStock? Nivel = null, int NumeroPagina = 1, int Tamano = 50);

public sealed record SemaforoAlmacenDto(Guid AlmacenId, string Almacen, decimal Cantidad, decimal Minimo, NivelStock Nivel);

/// <summary>Un producto aprobado y activo con su existencia y mínimo sumados en todos los almacenes.</summary>
public sealed record SemaforoProductoDto(
    Guid ProductoId, string Referencia, string Descripcion, string Unidad, string? FamiliaNfc,
    decimal Existencia, decimal Minimo, NivelStock Nivel, IReadOnlyList<SemaforoAlmacenDto> Almacenes);

/// <summary>La página pedida y cuántos productos hay en cada color (con la búsqueda, sin el filtro de color).</summary>
public sealed record SemaforoStockDto(Pagina<SemaforoProductoDto> Productos, int Verdes, int Amarillos, int Rojos);

public sealed record FiltroConsultasStockIot(int NumeroPagina = 1, int Tamano = 50);

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

    /// <summary>Existencia y mínimo del producto sumados en todos los almacenes (0 y 0 si no tiene existencias).</summary>
    Task<(decimal Existencia, decimal Minimo)> TotalesAsync(Guid productoId, CancellationToken ct = default);

    Task<Pagina<ConsultaStockIotDto>> ListarConsultasAsync(FiltroConsultasStockIot filtro, CancellationToken ct = default);

    void AgregarConsulta(ConsultaStockIot consulta);
}

internal sealed class ComprobarStockManejador(IConsultasStock stock) :
    IManejador<ConsultarSemaforoStock, SemaforoStockDto>,
    IManejador<ListarConsultasStockIot, Pagina<ConsultaStockIotDto>>
{
    public Task<SemaforoStockDto> ManejarAsync(ConsultarSemaforoStock s, CancellationToken ct) =>
        stock.SemaforoAsync(s.Filtro with { NumeroPagina = Math.Max(s.Filtro.NumeroPagina, 1), Tamano = Math.Clamp(s.Filtro.Tamano, 1, 200) }, ct);

    public Task<Pagina<ConsultaStockIotDto>> ManejarAsync(ListarConsultasStockIot s, CancellationToken ct) =>
        stock.ListarConsultasAsync(s.Filtro with { NumeroPagina = Math.Max(s.Filtro.NumeroPagina, 1), Tamano = Math.Clamp(s.Filtro.Tamano, 1, 200) }, ct);
}

/// <summary>
/// Lo que el embebido recibe en la cola iot-escaneos-respuestas por cada pieza que pasó por el lector.
/// <c>Modo</c> es "venta" (alguien tiene el modo inteligente) o "consulta" (comprobar stock).
/// <c>Nivel</c> ("verde", "amarillo", "rojo") indica qué LED prender; null si no se reconoció el producto.
/// </summary>
public sealed record RespuestaEscaneoIot(
    string Modo, bool Aceptado, string Motivo, string? Producto, decimal? Existencia, decimal? Minimo, string? Nivel);

/// <summary>Arma la respuesta del embebido para cada pieza. Sin usuario en sesión: lo llama el worker.</summary>
public sealed class ServicioConsultaStockIot(
    IProductoRepositorio productos, IVentaRepositorio ventas, IConsultasStock stock, IUnidadDeTrabajo unidad, IReloj reloj)
{
    public const string ModoVenta = "venta";
    public const string ModoConsulta = "consulta";

    /// <summary>Comprobar stock: responde la existencia total y el nivel, y lo anota en la bitácora.</summary>
    public async Task<RespuestaEscaneoIot> ConsultarAsync(string? uidRecibido, string? familiaRecibida, CancellationToken ct = default)
    {
        var uid = Normalizar(uidRecibido);
        var familia = Normalizar(familiaRecibida);
        var uidTexto = uid ?? uidRecibido?.Trim() ?? string.Empty;
        var familiaTexto = familia ?? familiaRecibida?.Trim() ?? string.Empty;

        var producto = familia is null ? null : await productos.ObtenerPorCodigoNfcAsync(familia, ct);
        RespuestaEscaneoIot respuesta;
        if (producto is null)
            respuesta = new RespuestaEscaneoIot(ModoConsulta, false,
                familia is null ? "Etiqueta sin familia NFC válida" : "Familia no registrada", null, null, null, null);
        else
        {
            var (existencia, minimo) = await stock.TotalesAsync(producto.Id, ct);
            var vendida = uid is not null && (await ventas.PiezasVendidasAsync([uid], ct)).Count > 0;
            var motivo = !producto.EstaDisponible ? "Producto no disponible para venta"
                : vendida ? "Esta pieza ya se vendió"
                : "Stock " + Texto(SemaforoStock.Nivel(existencia, minimo));
            respuesta = new RespuestaEscaneoIot(ModoConsulta, true, motivo, $"{producto.Referencia} · {producto.Descripcion}",
                existencia, minimo, Color(SemaforoStock.Nivel(existencia, minimo)));
        }

        stock.AgregarConsulta(new ConsultaStockIot(reloj.Ahora, uidTexto, familiaTexto, producto?.Id,
            respuesta.Existencia, respuesta.Minimo, respuesta.Motivo));
        await unidad.GuardarAsync(ct);
        return respuesta;
    }

    /// <summary>La pieza fue al modo inteligente de venta: se le dice al embebido qué pasó y el nivel de stock del producto.</summary>
    public async Task<RespuestaEscaneoIot> RespuestaVentaAsync(ResultadoEscaneo resultado, Guid? productoId, CancellationToken ct = default)
    {
        var producto = productoId is { } id ? await productos.ObtenerAsync(id, ct) : null;
        decimal? existencia = null, minimo = null;
        if (producto is not null)
            (existencia, minimo) = await stock.TotalesAsync(producto.Id, ct);
        var motivo = resultado switch
        {
            ResultadoEscaneo.Registrado => "Agregada a la venta",
            ResultadoEscaneo.PiezaYaVendida => "Pieza ya vendida",
            ResultadoEscaneo.PiezaRepetida => "Pieza ya escaneada",
            ResultadoEscaneo.ProductoNoDisponible => "Producto no disponible",
            ResultadoEscaneo.ProductoNoEncontrado => "Familia no registrada",
            ResultadoEscaneo.CodigoInvalido => "UID inválido",
            _ => "Sin venta activa",
        };
        return new RespuestaEscaneoIot(ModoVenta, resultado == ResultadoEscaneo.Registrado, motivo,
            producto is null ? null : $"{producto.Referencia} · {producto.Descripcion}",
            existencia, minimo, existencia is { } e ? Color(SemaforoStock.Nivel(e, minimo ?? 0)) : null);
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
