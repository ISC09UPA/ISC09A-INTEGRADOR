using CiudadInteligente.Application.DTOs;

namespace CiudadInteligente.Application.Services;

/// <summary>Casos de uso del corredor de emergencia (lo que usa EmergenciasController).</summary>
public interface IEmergenciaService
{
    Task<EmergenciaDto> SolicitarAsync(SolicitudEmergenciaDto dto);

    Task FinalizarAsync();

    Task<IReadOnlyList<EmergenciaDto>> ObtenerRecientesAsync(int cantidad);
}
