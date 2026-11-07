using CiudadInteligente.Application.DTOs;

namespace CiudadInteligente.Application.Services;

/// <summary>Casos de uso de la densidad de trafico (lo que usa TraficoController).</summary>
public interface ITraficoService
{
    Task RegistrarConteoAsync(RegistrarConteoDto dto);

    Task<IReadOnlyList<ConteoTraficoDto>> ObtenerHistorialAsync(int cantidad);
}
