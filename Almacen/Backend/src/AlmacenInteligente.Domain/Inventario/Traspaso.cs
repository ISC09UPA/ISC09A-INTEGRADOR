using AlmacenInteligente.Domain.Almacenes;
using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Productos;

namespace AlmacenInteligente.Domain.Inventario;

/// <summary>Mueve existencias entre almacenes al costo promedio de origen. No genera movimiento financiero.</summary>
public sealed class Traspaso : Documento
{
    private readonly List<LineaTraspaso> _lineas = [];

    public Guid AlmacenOrigenId { get; private set; }
    public Guid AlmacenDestinoId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public IReadOnlyList<LineaTraspaso> Lineas => _lineas;

    protected override int NumeroDeLineas => _lineas.Count;
    protected override string Nombre => "traspaso";
    protected override bool NombreFemenino => false;

    private Traspaso() { }

    public Traspaso(Almacen origen, Almacen destino, DateOnly fecha, Auditoria creado) : base(creado)
    {
        if (origen.Id == destino.Id)
            throw new DomainException("El almacén de origen y el de destino deben ser distintos.");
        origen.AsegurarActivo();
        destino.AsegurarActivo();
        AlmacenOrigenId = origen.Id;
        AlmacenDestinoId = destino.Id;
        Fecha = fecha;
    }

    public LineaTraspaso AgregarLinea(Producto producto, decimal cantidad)
    {
        AsegurarBorrador();
        producto.AsegurarUsable(Guard.Positivo(cantidad, "La cantidad"));
        var linea = new LineaTraspaso(producto.Id, cantidad);
        _lineas.Add(linea);
        return linea;
    }
}

public sealed class LineaTraspaso : Entity
{
    public Guid ProductoId { get; private init; }
    public decimal Cantidad { get; private init; }

    /// <summary>Costo promedio de origen al confirmar.</summary>
    public decimal CostoUnitario { get; private set; }

    private LineaTraspaso() { }

    internal LineaTraspaso(Guid productoId, decimal cantidad)
    {
        ProductoId = productoId;
        Cantidad = cantidad;
    }

    internal void FijarCosto(decimal costoUnitario) => CostoUnitario = costoUnitario;
}
