using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Finanzas;
using AlmacenInteligente.Domain.Inventario;
using AlmacenInteligente.Domain.Productos;

namespace AlmacenInteligente.Application.Consultas;

// Modelos de lectura: lo que muestran las pantallas y devuelve la API. No llevan reglas.

public sealed record UnidadDto(Guid Id, string Clave, string Nombre, bool PermiteFracciones);

public sealed record ProductoDto(
    Guid Id, string Referencia, string Descripcion, Guid UnidadId, string Unidad, bool PermiteFracciones,
    string? ImagenRuta, bool Activo, EstadoAprobacion Estado, Guid? GerenteAprobadorId, string? GerenteAprobador,
    string CreadoPor, DateTimeOffset CreadoEn, string? MotivoRechazo, decimal Existencia);

public sealed record FiltroProductos(
    string? Busqueda = null, EstadoAprobacion? Estado = null, bool IncluirInactivos = false,
    bool SoloMisAprobaciones = false, int NumeroPagina = 1, int Tamano = 20);

public sealed record AlmacenDto(Guid Id, string Nombre, string Ubicacion, bool Activo, int Productos, decimal ValorInventario);

public sealed record ExistenciaDto(
    Guid AlmacenId, string Almacen, Guid ProductoId, string Referencia, string Descripcion, string Unidad,
    bool PermiteFracciones, decimal Cantidad, decimal StockMinimo, decimal CostoPromedio, decimal Valor, bool BajoMinimo);

public sealed record FiltroExistencias(
    Guid? AlmacenId = null, string? Busqueda = null, bool SoloBajoMinimo = false, int NumeroPagina = 1, int Tamano = 50);

public sealed record KardexDto(
    DateTimeOffset Fecha, TipoMovimientoInventario Tipo, decimal Cantidad, decimal CostoUnitario,
    decimal Saldo, decimal CostoPromedio, TipoDocumento? DocumentoTipo, Guid? DocumentoId, long? Folio, string Usuario);

/// <summary>Producto aprobado y activo para elegir en un documento, con lo que hay en cada almacén.</summary>
public sealed record ProductoOpcionDto(
    Guid Id, string Referencia, string Descripcion, string Unidad, bool PermiteFracciones,
    IReadOnlyList<ExistenciaOpcionDto> Existencias)
{
    public override string ToString() => $"{Referencia} · {Descripcion}";
}

public sealed record ExistenciaOpcionDto(Guid AlmacenId, string Almacen, decimal Cantidad, decimal CostoPromedio);

public sealed record OpcionDto(Guid Id, string Texto)
{
    public override string ToString() => Texto;
}

public sealed record FiltroDocumentos(
    string? Busqueda = null, EstadoDocumento? Estado = null, DateOnly? Desde = null, DateOnly? Hasta = null,
    int NumeroPagina = 1, int Tamano = 20);

public sealed record DocumentoResumenDto(
    Guid Id, long Folio, DateOnly Fecha, string Tercero, string Almacen, EstadoDocumento Estado,
    int Lineas, decimal Subtotal, decimal Iva, decimal Total);

public sealed record LineaDocumentoDto(
    Guid ProductoId, string Referencia, string Descripcion, string Unidad, string? Almacen,
    decimal Cantidad, decimal PrecioUnitario, decimal TasaIva, decimal Subtotal, decimal Iva, decimal CostoUnitario)
{
    public decimal Total => Subtotal + Iva;
}

public sealed record DocumentoDetalleDto(
    Guid Id, long Folio, DateOnly Fecha, EstadoDocumento Estado,
    string? Tercero, string? TerceroRfc, string? TerceroCorreo,
    string Almacen, string? AlmacenDestino,
    IReadOnlyList<LineaDocumentoDto> Lineas,
    string CreadoPor, DateTimeOffset CreadoEn,
    string? CanceladoPor, DateTimeOffset? CanceladoEn, string? MotivoCancelacion)
{
    public decimal Subtotal => Lineas.Sum(l => l.Subtotal);
    public decimal Iva => Lineas.Sum(l => l.Iva);
    public decimal Total => Subtotal + Iva;
}

public sealed record FiltroFinanzas(
    DateOnly? Desde = null, DateOnly? Hasta = null, NaturalezaFinanciera? Naturaleza = null, int NumeroPagina = 1, int Tamano = 50);

public sealed record MovimientoFinancieroDto(
    Guid Id, DateTimeOffset Fecha, NaturalezaFinanciera Naturaleza, TipoDocumento DocumentoTipo, Guid DocumentoId,
    long Folio, string Tercero, decimal Subtotal, decimal Iva, decimal Total, bool EsReverso);

public sealed record ResumenFinancieroDto(
    IReadOnlyList<MovimientoFinancieroDto> Movimientos, int Total,
    decimal Ingresos, decimal Egresos, decimal IvaTrasladado, decimal IvaAcreditable)
{
    public decimal Saldo => Ingresos - Egresos;
}

public sealed record PuntoDto(string Etiqueta, decimal Valor);

public sealed record ReporteVentasDto(
    int NumeroVentas, decimal Subtotal, decimal Iva, decimal Total,
    IReadOnlyList<PuntoDto> PorPeriodo, IReadOnlyList<PuntoDto> TopClientes,
    IReadOnlyList<PuntoDto> TopProductos, IReadOnlyList<PuntoDto> PorAlmacen)
{
    public decimal TicketPromedio => NumeroVentas == 0 ? 0 : Math.Round(Total / NumeroVentas, 2);
}

public sealed record ReporteComprasDto(
    int NumeroRecepciones, decimal Subtotal, decimal Iva, decimal Total,
    IReadOnlyList<PuntoDto> PorPeriodo, IReadOnlyList<PuntoDto> PorProveedor);

public sealed record ReporteFinancieroDto(
    IReadOnlyList<string> Periodos, IReadOnlyList<decimal> Ingresos, IReadOnlyList<decimal> Egresos,
    decimal TotalIngresos, decimal TotalEgresos, decimal IvaTrasladado, decimal IvaAcreditable)
{
    public decimal Saldo => TotalIngresos - TotalEgresos;
}

public sealed record ReporteInventarioDto(
    decimal ValorTotal, IReadOnlyList<PuntoDto> ValorPorAlmacen, IReadOnlyList<ExistenciaDto> BajoMinimo);

public sealed record MargenProductoDto(
    string Referencia, string Descripcion, decimal Cantidad, decimal Ventas, decimal Costo)
{
    public decimal Margen => Ventas - Costo;
    public decimal MargenPorcentaje => Ventas == 0 ? 0 : Math.Round(Margen / Ventas * 100, 1);
}
