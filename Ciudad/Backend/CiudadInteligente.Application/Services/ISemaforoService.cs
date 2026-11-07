using CiudadInteligente.Application.DTOs;

namespace CiudadInteligente.Application.Services;

/// <summary>Casos de uso del control del cruce (lo que usa SemaforoController de la API).</summary>
public interface ISemaforoService
{
    EstadoCruceDto ObtenerEstado();

    /// <summary>"adaptativo", "fijo" o "manual".</summary>
    Task<EstadoCruceDto> CambiarModoAsync(CambiarModoDto dto);

    /// <summary>Aplica colores a uno o mas accesos (pasa a manual si hace falta).</summary>
    Task<EstadoCruceDto> AplicarSecuenciaAsync(SecuenciaManualDto dto);

    /// <summary>Manual con los 4 accesos en rojo.</summary>
    Task<EstadoCruceDto> DetenerTodoAsync();
}
