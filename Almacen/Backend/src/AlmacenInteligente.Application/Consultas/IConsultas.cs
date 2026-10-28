using AlmacenInteligente.Application.Terceros;

namespace AlmacenInteligente.Application.Consultas;

/// <summary>
/// Puerto de lectura: listados, detalles y reportes que se resuelven directo en la base de datos.
/// Los casos de uso de consulta lo envuelven para aplicar permisos.
/// </summary>
public interface IConsultas
{
    Task<IReadOnlyList<UnidadDto>> ListarUnidadesAsync(CancellationToken ct);
    Task<Pagina<ProductoDto>> ListarProductosAsync(FiltroProductos filtro, Guid? usuarioId, CancellationToken ct);
    Task<ProductoDto?> ObtenerProductoAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProductoOpcionDto>> BuscarProductosDisponiblesAsync(string? texto, int maximo, CancellationToken ct);

    Task<IReadOnlyList<AlmacenDto>> ListarAlmacenesAsync(bool incluirInactivos, CancellationToken ct);
    Task<Pagina<ExistenciaDto>> ListarExistenciasAsync(FiltroExistencias filtro, CancellationToken ct);
    Task<IReadOnlyList<KardexDto>> KardexAsync(Guid almacenId, Guid productoId, DateOnly? desde, DateOnly? hasta, CancellationToken ct);

    Task<IReadOnlyList<OpcionDto>> BuscarTercerosActivosAsync(bool clientes, string? texto, int maximo, CancellationToken ct);

    Task<Pagina<DocumentoResumenDto>> ListarRecepcionesAsync(FiltroDocumentos filtro, CancellationToken ct);
    Task<DocumentoDetalleDto?> ObtenerRecepcionAsync(Guid id, CancellationToken ct);
    Task<Pagina<DocumentoResumenDto>> ListarVentasAsync(FiltroDocumentos filtro, CancellationToken ct);
    Task<DocumentoDetalleDto?> ObtenerVentaAsync(Guid id, CancellationToken ct);
    Task<Pagina<DocumentoResumenDto>> ListarTraspasosAsync(FiltroDocumentos filtro, CancellationToken ct);
    Task<DocumentoDetalleDto?> ObtenerTraspasoAsync(Guid id, CancellationToken ct);

    Task<ResumenFinancieroDto> ListarMovimientosFinancierosAsync(FiltroFinanzas filtro, CancellationToken ct);

    Task<ReporteVentasDto> ReporteVentasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<ReporteComprasDto> ReporteComprasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<ReporteFinancieroDto> ReporteFinancieroAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<ReporteInventarioDto> ReporteInventarioAsync(CancellationToken ct);
    Task<IReadOnlyList<MargenProductoDto>> ReporteMargenAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}
