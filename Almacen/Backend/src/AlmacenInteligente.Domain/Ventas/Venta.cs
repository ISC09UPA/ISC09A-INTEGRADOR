using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Ventas;

/// <summary>Salida de mercancía a un cliente. Cada línea elige su almacén de salida y el agente captura el precio.</summary>
public sealed class Venta : Documento
{
    private readonly List<LineaVenta> _lineas = [];

    public Guid ClienteId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public IReadOnlyList<LineaVenta> Lineas => _lineas;
    public Importe Importe => _lineas.Aggregate(Importe.Cero, (total, l) => total + l.Importe);

    protected override int NumeroDeLineas => _lineas.Count;
    protected override string Nombre => "venta";
    protected override bool NombreFemenino => true;

    private Venta() { }

    public Venta(Cliente cliente, DateOnly fecha, Auditoria creado) : base(creado)
    {
        cliente.AsegurarActivo("cliente");
        ClienteId = cliente.Id;
        Fecha = fecha;
    }

    public LineaVenta AgregarLinea(Producto producto, Guid almacenId, decimal cantidad, decimal precioUnitario, TasaIva tasaIva)
    {
        AsegurarBorrador();
        producto.AsegurarUsable(Guard.Positivo(cantidad, "La cantidad"));
        var linea = new LineaVenta(producto.Id, Guard.Id(almacenId, "El almacén de salida"), cantidad,
            Guard.NoNegativo(precioUnitario, "El precio unitario"), tasaIva);
        _lineas.Add(linea);
        return linea;
    }

    public void QuitarLinea(Guid lineaId)
    {
        AsegurarBorrador();
        _lineas.RemoveAll(l => l.Id == lineaId);
    }

    internal void AlConfirmar() => Emitir(new VentaConfirmada(Id, ClienteId, Confirmado!.Fecha));

    internal void AlCancelar() => Emitir(new VentaCancelada(Id, ClienteId, MotivoCancelacion!, Cancelado!.Fecha));
}

public sealed class LineaVenta : Entity
{
    public Guid ProductoId { get; private init; }
    public Guid AlmacenId { get; private init; }
    public decimal Cantidad { get; private init; }
    public decimal PrecioUnitario { get; private init; }
    public TasaIva TasaIva { get; private init; }
    public Importe Importe { get; private init; }

    /// <summary>Costo promedio del almacén al confirmar; se usa para margen y para reponer al cancelar.</summary>
    public decimal CostoUnitario { get; private set; }

    private LineaVenta() { }

    internal LineaVenta(Guid productoId, Guid almacenId, decimal cantidad, decimal precioUnitario, TasaIva tasaIva)
    {
        ProductoId = productoId;
        AlmacenId = almacenId;
        Cantidad = cantidad;
        PrecioUnitario = precioUnitario;
        TasaIva = tasaIva;
        Importe = Importe.DeLinea(cantidad, precioUnitario, tasaIva);
    }

    internal void FijarCosto(decimal costoUnitario) => CostoUnitario = costoUnitario;
}

public sealed record VentaConfirmada(Guid VentaId, Guid ClienteId, DateTimeOffset OcurrioEn) : IDomainEvent;

public sealed record VentaCancelada(Guid VentaId, Guid ClienteId, string Motivo, DateTimeOffset OcurrioEn) : IDomainEvent;
