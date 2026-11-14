using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Productos;
using AlmacenInteligente.Domain.Terceros;
using AlmacenInteligente.Domain.ValueObjects;

namespace AlmacenInteligente.Domain.Recepciones;

/// <summary>Entrada de mercancía de un proveedor a un almacén.</summary>
public sealed class Recepcion : Documento
{
    private readonly List<LineaRecepcion> _lineas = [];

    public Guid ProveedorId { get; private set; }
    public Guid AlmacenId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public IReadOnlyList<LineaRecepcion> Lineas => _lineas;
    public Importe Importe => _lineas.Aggregate(Importe.Cero, (total, l) => total + l.Importe);

    protected override int NumeroDeLineas => _lineas.Count;
    protected override string Nombre => "recepción";
    protected override bool NombreFemenino => true;

    private Recepcion() { }

    public Recepcion(Proveedor proveedor, Almacen almacen, DateOnly fecha, Auditoria creado) : base(creado)
    {
        proveedor.AsegurarActivo("proveedor");
        almacen.AsegurarActivo();
        ProveedorId = proveedor.Id;
        AlmacenId = almacen.Id;
        Fecha = fecha;
    }

    public LineaRecepcion AgregarLinea(Producto producto, decimal cantidad, decimal costoUnitario, TasaIva tasaIva)
    {
        AsegurarBorrador();
        producto.AsegurarUsable(Guard.Positivo(cantidad, "La cantidad"));
        var linea = new LineaRecepcion(producto.Id, cantidad, Guard.NoNegativo(costoUnitario, "El costo unitario"), tasaIva);
        _lineas.Add(linea);
        return linea;
    }

    public void QuitarLinea(Guid lineaId)
    {
        AsegurarBorrador();
        _lineas.RemoveAll(l => l.Id == lineaId);
    }

    internal void AlConfirmar() => Emitir(new RecepcionConfirmada(Id, ProveedorId, Confirmado!.Fecha));

    internal void AlCancelar() => Emitir(new RecepcionCancelada(Id, ProveedorId, MotivoCancelacion!, Cancelado!.Fecha));
}

public sealed class LineaRecepcion : Entity
{
    public Guid ProductoId { get; private init; }
    public decimal Cantidad { get; private init; }
    public decimal CostoUnitario { get; private init; }
    public TasaIva TasaIva { get; private init; }
    public Importe Importe { get; private init; }

    private LineaRecepcion() { }

    internal LineaRecepcion(Guid productoId, decimal cantidad, decimal costoUnitario, TasaIva tasaIva)
    {
        ProductoId = productoId;
        Cantidad = cantidad;
        CostoUnitario = costoUnitario;
        TasaIva = tasaIva;
        Importe = Importe.DeLinea(cantidad, costoUnitario, tasaIva);
    }
}

public sealed record RecepcionConfirmada(Guid RecepcionId, Guid ProveedorId, DateTimeOffset OcurrioEn) : IDomainEvent;

public sealed record RecepcionCancelada(Guid RecepcionId, Guid ProveedorId, string Motivo, DateTimeOffset OcurrioEn) : IDomainEvent;
