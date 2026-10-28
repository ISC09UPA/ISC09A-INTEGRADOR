namespace AlmacenInteligente.Domain.ValueObjects;

/// <summary>Subtotal, IVA y total de una línea o de un documento.</summary>
public readonly record struct Importe(Dinero Subtotal, Dinero Iva)
{
    public static Importe Cero => new(Dinero.Cero, Dinero.Cero);

    public Dinero Total => Subtotal + Iva;

    /// <summary>Subtotal = cantidad × precio; IVA = subtotal × tasa; ambos redondeados a 2 decimales por línea.</summary>
    public static Importe DeLinea(decimal cantidad, decimal precioUnitario, TasaIva tasa)
    {
        var subtotal = Dinero.Redondeado(cantidad * precioUnitario);
        var iva = Dinero.Redondeado(subtotal.Monto * tasa.Valor);
        return new Importe(subtotal, iva);
    }

    public static Importe operator +(Importe a, Importe b) => new(a.Subtotal + b.Subtotal, a.Iva + b.Iva);
    public static Importe operator -(Importe a) => new(-a.Subtotal, -a.Iva);
}
