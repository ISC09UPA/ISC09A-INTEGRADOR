namespace AlmacenInteligente.Domain.Inventario;

/// <summary>Color con que se muestra el nivel de stock: en la web, en la app y en los LEDs del embebido.</summary>
public enum NivelStock
{
    /// <summary>Arriba del stock mínimo (o hay existencia y no se definió mínimo).</summary>
    Verde = 1,

    /// <summary>Hay existencia, pero está en o por debajo del stock mínimo.</summary>
    Amarillo = 2,

    /// <summary>Sin existencia.</summary>
    Rojo = 3,
}

public static class SemaforoStock
{
    /// <summary>Rojo sin existencia; amarillo en o por debajo del mínimo; verde arriba del mínimo.</summary>
    public static NivelStock Nivel(decimal cantidad, decimal minimo) =>
        cantidad <= 0 ? NivelStock.Rojo
        : minimo > 0 && cantidad <= minimo ? NivelStock.Amarillo
        : NivelStock.Verde;
}
