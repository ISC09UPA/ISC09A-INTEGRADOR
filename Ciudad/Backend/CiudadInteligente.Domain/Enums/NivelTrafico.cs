namespace CiudadInteligente.Domain.Enums;

/// <summary>Que tan cargado esta un acceso segun los vehiculos que ve la camara.</summary>
public enum NivelTrafico
{
    Bajo,
    Medio,
    Alto
}

public static class NivelTraficoReglas
{
    /// <summary>Menos de este numero de vehiculos = Bajo.</summary>
    public const int LimiteMedio = 5;

    /// <summary>Desde este numero de vehiculos = Alto.</summary>
    public const int LimiteAlto = 12;

    public static NivelTrafico Calcular(int vehiculos) => vehiculos switch
    {
        < LimiteMedio => NivelTrafico.Bajo,
        < LimiteAlto => NivelTrafico.Medio,
        _ => NivelTrafico.Alto
    };
}
