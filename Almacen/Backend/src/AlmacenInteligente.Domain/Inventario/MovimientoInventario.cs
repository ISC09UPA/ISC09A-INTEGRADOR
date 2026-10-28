using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Inventario;

public enum TipoMovimientoInventario
{
    Inicial = 1,
    Recepcion = 2,
    Venta = 3,
    TraspasoSalida = 4,
    TraspasoEntrada = 5,
    Ajuste = 6,
    CancelacionRecepcion = 7,
    CancelacionVenta = 8,
}

public enum TipoDocumento
{
    Recepcion = 1,
    Venta = 2,
    Traspaso = 3,
}

public sealed record DocumentoRef(TipoDocumento Tipo, Guid Id);

/// <summary>
/// Renglón inmutable del kardex. Cantidad es positiva en entradas y negativa en salidas;
/// SaldoCantidad y CostoPromedio son los de la existencia después del movimiento.
/// </summary>
public sealed class MovimientoInventario : Entity
{
    public Guid AlmacenId { get; private init; }
    public Guid ProductoId { get; private init; }
    public TipoMovimientoInventario Tipo { get; private init; }
    public decimal Cantidad { get; private init; }
    public decimal CostoUnitario { get; private init; }
    public decimal SaldoCantidad { get; private init; }
    public decimal CostoPromedio { get; private init; }
    public DocumentoRef? Documento { get; private init; }
    public Auditoria Registro { get; private init; } = null!;

    private MovimientoInventario() { }

    internal MovimientoInventario(
        Existencia existencia, TipoMovimientoInventario tipo, decimal cantidad, decimal costoUnitario,
        DocumentoRef? documento, Auditoria registro)
    {
        AlmacenId = existencia.AlmacenId;
        ProductoId = existencia.ProductoId;
        Tipo = tipo;
        Cantidad = cantidad;
        CostoUnitario = costoUnitario;
        SaldoCantidad = existencia.Cantidad;
        CostoPromedio = existencia.CostoPromedio;
        Documento = documento;
        Registro = registro;
    }
}
