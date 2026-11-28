using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.Inventario;

/// <summary>
/// Existencia de un producto en un almacén. Es su propio agregado para que dos ventas concurrentes
/// choquen en la misma fila (control optimista en infraestructura) y nunca quede negativa.
/// </summary>
public sealed class Existencia : AggregateRoot
{
    /// <summary>Decimales con que se guarda el costo promedio ponderado.</summary>
    public const int DecimalesCosto = 4;

    public Guid AlmacenId { get; private init; }
    public Guid ProductoId { get; private init; }
    public decimal Cantidad { get; private set; }
    public decimal StockMinimo { get; private set; }
    public decimal CostoPromedio { get; private set; }

    /// <summary>True mientras la existencia esté en o por debajo del mínimo y ya se avisó. Se rearma al subir.</summary>
    public bool AlertaBajoStockEnviada { get; private set; }

    public bool EstaBajoMinimo => StockMinimo > 0 && Cantidad <= StockMinimo;

    public decimal ValorInventario => Math.Round(Cantidad * CostoPromedio, 2, MidpointRounding.AwayFromZero);

    private Existencia() { }

    public Existencia(Guid almacenId, Guid productoId)
    {
        AlmacenId = Guard.Id(almacenId, "El almacén");
        ProductoId = Guard.Id(productoId, "El producto");
    }

    public void DefinirStockMinimo(decimal minimo, DateTimeOffset ahora)
    {
        StockMinimo = Guard.NoNegativo(minimo, "El stock mínimo");
        EvaluarMinimo(ahora);
    }

    /// <summary>Entrada a un costo dado; recalcula el costo promedio ponderado.</summary>
    internal MovimientoInventario Entrar(
        TipoMovimientoInventario tipo, decimal cantidad, decimal costoUnitario, DocumentoRef? documento, Auditoria registro)
    {
        Guard.Positivo(cantidad, "La cantidad");
        Guard.NoNegativo(costoUnitario, "El costo unitario");

        var nuevaCantidad = Cantidad + cantidad;
        CostoPromedio = Redondear((Cantidad * CostoPromedio + cantidad * costoUnitario) / nuevaCantidad);
        Cantidad = nuevaCantidad;

        EvaluarMinimo(registro.Fecha);
        return new MovimientoInventario(this, tipo, cantidad, costoUnitario, documento, registro);
    }

    /// <summary>Salida al costo promedio vigente. Nunca deja la existencia negativa.</summary>
    internal MovimientoInventario Salir(
        TipoMovimientoInventario tipo, decimal cantidad, DocumentoRef? documento, Auditoria registro)
    {
        AsegurarDisponible(cantidad);
        Cantidad -= cantidad;
        EvaluarMinimo(registro.Fecha);
        return new MovimientoInventario(this, tipo, -cantidad, CostoPromedio, documento, registro);
    }

    /// <summary>Deshace una entrada (cancelación de recepción) quitando su aporte al costo promedio.</summary>
    internal MovimientoInventario RevertirEntrada(
        TipoMovimientoInventario tipo, decimal cantidad, decimal costoUnitario, DocumentoRef? documento, Auditoria registro)
    {
        AsegurarDisponible(cantidad);

        var restante = Cantidad - cantidad;
        if (restante > 0)
            CostoPromedio = Math.Max(0, Redondear((Cantidad * CostoPromedio - cantidad * costoUnitario) / restante));
        Cantidad = restante;

        EvaluarMinimo(registro.Fecha);
        return new MovimientoInventario(this, tipo, -cantidad, costoUnitario, documento, registro);
    }

    private void AsegurarDisponible(decimal cantidad)
    {
        Guard.Positivo(cantidad, "La cantidad");
        if (cantidad > Cantidad)
            throw new ExistenciaInsuficienteException(AlmacenId, ProductoId, Cantidad, cantidad);
    }

    private void EvaluarMinimo(DateTimeOffset ahora)
    {
        if (EstaBajoMinimo)
        {
            if (AlertaBajoStockEnviada)
                return;
            AlertaBajoStockEnviada = true;
            Emitir(new StockBajoMinimo(AlmacenId, ProductoId, Cantidad, StockMinimo, ahora));
        }
        else
        {
            AlertaBajoStockEnviada = false;
        }
    }

    private static decimal Redondear(decimal valor) => Math.Round(valor, DecimalesCosto, MidpointRounding.AwayFromZero);
}

public sealed class ExistenciaInsuficienteException(Guid almacenId, Guid productoId, decimal disponible, decimal solicitada)
    : DomainException($"Existencia insuficiente: hay {disponible} y se requieren {solicitada}.")
{
    public Guid AlmacenId { get; } = almacenId;
    public Guid ProductoId { get; } = productoId;
    public decimal Disponible { get; } = disponible;
    public decimal Solicitada { get; } = solicitada;
}

/// <summary>Se emite solo cuando la existencia cruza el mínimo; no se repite hasta que vuelva a subir.</summary>
public sealed record StockBajoMinimo(Guid AlmacenId, Guid ProductoId, decimal Cantidad, decimal StockMinimo, DateTimeOffset OcurrioEn)
    : IDomainEvent;
