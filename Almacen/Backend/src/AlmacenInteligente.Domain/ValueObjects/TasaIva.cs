using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Domain.ValueObjects;

/// <summary>Tasa de IVA como fracción (0.16 = 16 %). Se captura en cada línea del documento.</summary>
public readonly record struct TasaIva
{
    public static readonly TasaIva General = new(0.16m);
    public static readonly TasaIva Frontera = new(0.08m);
    public static readonly TasaIva Cero = new(0m);

    public decimal Valor { get; }

    public TasaIva(decimal valor)
    {
        if (valor is < 0m or > 1m)
            throw new DomainException("La tasa de IVA debe estar entre 0 y 1 (por ejemplo 0.16).");
        Valor = valor;
    }

    public override string ToString() => $"{Valor:P0}";
}
