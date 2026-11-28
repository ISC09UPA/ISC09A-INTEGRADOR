using AlmacenInteligente.Domain.Common;
using AlmacenInteligente.Domain.Documentos;
using AlmacenInteligente.Domain.Iot;
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

    /// <param name="piezas">
    /// UID de fábrica de la etiqueta NFC de cada pieza física que sale en esta línea (modo inteligente).
    /// Si el producto se vende por pieza (sin fracciones) debe haber exactamente un UID por unidad: los que no se
    /// escanearon se capturan a mano. Los productos por peso o volumen no llevan UID. Un UID no se repite en la venta.
    /// </param>
    public LineaVenta AgregarLinea(Producto producto, Guid almacenId, decimal cantidad, decimal precioUnitario, TasaIva tasaIva,
        IEnumerable<string>? piezas = null)
    {
        AsegurarBorrador();
        producto.AsegurarUsable(Guard.Positivo(cantidad, "La cantidad"));

        var uids = new List<string>();
        foreach (var pieza in piezas ?? [])
        {
            var uid = CodigoNfc.Normalizar(pieza) ?? throw new DomainException("Hay una pieza sin UID de etiqueta NFC.");
            if (uids.Contains(uid) || _lineas.Any(l => l.Piezas.Any(p => p.Uid == uid)))
                throw new DomainException($"La pieza {uid} aparece dos veces en la venta.");
            uids.Add(uid);
        }
        if (producto.PermiteFracciones && uids.Count > 0)
            throw new DomainException($"{producto.Referencia} se vende por peso o volumen: no lleva UID por pieza.");
        if (!producto.PermiteFracciones && uids.Count != cantidad)
            throw new DomainException(uids.Count < cantidad
                ? $"A la línea de {producto.Referencia} le faltan {cantidad - uids.Count:0} UID: cada pieza vendida necesita el de su etiqueta NFC."
                : $"La línea de {producto.Referencia} tiene {uids.Count} UID y cantidad {cantidad:0.###}: deben coincidir.");

        var linea = new LineaVenta(producto.Id, Guard.Id(almacenId, "El almacén de salida"), cantidad,
            Guard.NoNegativo(precioUnitario, "El precio unitario"), tasaIva, uids);
        _lineas.Add(linea);
        return linea;
    }

    /// <summary>UID de todas las piezas que salen en esta venta.</summary>
    public IEnumerable<string> UidsPiezas => _lineas.SelectMany(l => l.Piezas).Select(p => p.Uid);

    public void QuitarLinea(Guid lineaId)
    {
        AsegurarBorrador();
        _lineas.RemoveAll(l => l.Id == lineaId);
    }

    internal void AlConfirmar() => Emitir(new VentaConfirmada(Id, ClienteId, Confirmado!.Fecha));

    /// <summary>Al cancelar, las piezas vuelven al stock: sus UID quedan libres para venderse otra vez.</summary>
    internal void AlCancelar()
    {
        foreach (var pieza in _lineas.SelectMany(l => l.Piezas))
            pieza.Devolver();
        Emitir(new VentaCancelada(Id, ClienteId, MotivoCancelacion!, Cancelado!.Fecha));
    }
}

public sealed class LineaVenta : Entity
{
    private readonly List<PiezaVendida> _piezas = [];

    public Guid ProductoId { get; private init; }
    public Guid AlmacenId { get; private init; }
    public decimal Cantidad { get; private init; }
    public decimal PrecioUnitario { get; private init; }
    public TasaIva TasaIva { get; private init; }
    public Importe Importe { get; private init; }

    /// <summary>Costo promedio del almacén al confirmar; se usa para margen y para reponer al cancelar.</summary>
    public decimal CostoUnitario { get; private set; }

    /// <summary>Piezas físicas identificadas por su etiqueta NFC; vacía si la línea se capturó a mano.</summary>
    public IReadOnlyList<PiezaVendida> Piezas => _piezas;

    private LineaVenta() { }

    internal LineaVenta(Guid productoId, Guid almacenId, decimal cantidad, decimal precioUnitario, TasaIva tasaIva, IEnumerable<string> piezas)
    {
        _piezas.AddRange(piezas.Select(uid => new PiezaVendida(uid)));
        ProductoId = productoId;
        AlmacenId = almacenId;
        Cantidad = cantidad;
        PrecioUnitario = precioUnitario;
        TasaIva = tasaIva;
        Importe = Importe.DeLinea(cantidad, precioUnitario, tasaIva);
    }

    internal void FijarCosto(decimal costoUnitario) => CostoUnitario = costoUnitario;
}

/// <summary>
/// Una pieza física que salió en una venta, identificada por el UID de fábrica de su etiqueta NFC.
/// Mientras no esté devuelta, ese UID ya no está en stock y no puede venderse otra vez (índice único en la base).
/// </summary>
public sealed class PiezaVendida : Entity
{
    public string Uid { get; private init; } = null!;

    /// <summary>True si la venta se canceló y la pieza regresó al almacén.</summary>
    public bool Devuelta { get; private set; }

    private PiezaVendida() { }

    internal PiezaVendida(string uid) => Uid = uid;

    internal void Devolver() => Devuelta = true;
}

public sealed record VentaConfirmada(Guid VentaId, Guid ClienteId, DateTimeOffset OcurrioEn) : IDomainEvent;

public sealed record VentaCancelada(Guid VentaId, Guid ClienteId, string Motivo, DateTimeOffset OcurrioEn) : IDomainEvent;
