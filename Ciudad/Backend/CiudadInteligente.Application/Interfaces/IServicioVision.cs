namespace CiudadInteligente.Application.Interfaces;

/// <summary>Cuenta vehiculos en una foto del cruce (Azure Custom Vision).</summary>
public interface IServicioVision
{
    /// <summary>false si faltan Vision:PredictionUrl / Vision:PredictionKey.</summary>
    bool EstaConfigurado { get; }

    /// <summary>
    /// Regresa 4 enteros en el orden del protocolo (Norte, Sur, Este, Oeste).
    /// La foto se divide en 4 franjas verticales, de izquierda a derecha, una por acceso.
    /// </summary>
    Task<IReadOnlyList<int>> ContarPorAccesoAsync(Stream imagen, CancellationToken ct);
}
