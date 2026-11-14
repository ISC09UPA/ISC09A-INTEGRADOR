namespace AlmacenInteligente.Domain.ValueObjects;

/// <summary>Importe en MXN. Los importes finales se redondean a 2 decimales (half away from zero).</summary>
public readonly record struct Dinero(decimal Monto) : IComparable<Dinero>
{
    public const string Moneda = "MXN";

    public static Dinero Cero => new(0m);

    public static Dinero Redondeado(decimal monto) => new(Math.Round(monto, 2, MidpointRounding.AwayFromZero));

    public static Dinero operator +(Dinero a, Dinero b) => new(a.Monto + b.Monto);
    public static Dinero operator -(Dinero a, Dinero b) => new(a.Monto - b.Monto);
    public static Dinero operator -(Dinero a) => new(-a.Monto);
    public static bool operator <(Dinero a, Dinero b) => a.Monto < b.Monto;
    public static bool operator >(Dinero a, Dinero b) => a.Monto > b.Monto;
    public static bool operator <=(Dinero a, Dinero b) => a.Monto <= b.Monto;
    public static bool operator >=(Dinero a, Dinero b) => a.Monto >= b.Monto;

    public int CompareTo(Dinero other) => Monto.CompareTo(other.Monto);

    public override string ToString() => $"{Monto:N2} {Moneda}";
}
