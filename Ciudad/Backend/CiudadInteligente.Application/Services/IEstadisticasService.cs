using CiudadInteligente.Application.DTOs;

namespace CiudadInteligente.Application.Services;

/// <summary>Analisis del historial de trafico (lo que usa EstadisticasController).</summary>
public interface IEstadisticasService
{
    /// <param name="dias">Cuantos dias hacia atras analizar (1-30).</param>
    Task<ResumenEstadisticasDto> ObtenerResumenAsync(int dias);
}
