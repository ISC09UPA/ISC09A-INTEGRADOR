namespace CiudadInteligente.Application.DTOs;

/// <summary>Cuerpo de POST api/trafico (lo manda la camara desde la Raspberry Pi).</summary>
/// <param name="Vehiculos">4 enteros: Norte, Sur, Este, Oeste.</param>
public record RegistrarConteoDto(IReadOnlyList<int> Vehiculos);

/// <summary>Un conteo guardado, para el historial.</summary>
public record ConteoTraficoDto(
    long Id,
    DateTimeOffset RegistradoEn,
    int Norte,
    int Sur,
    int Este,
    int Oeste,
    int Total);
