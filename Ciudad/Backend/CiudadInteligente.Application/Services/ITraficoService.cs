using CiudadInteligente.Application.DTOs;

namespace CiudadInteligente.Application.Services;

/// <summary>Casos de uso de la densidad de trafico (lo que usa TraficoController).</summary>
public interface ITraficoService
{
    /// <summary>
    /// Conteo de ahora: actualiza el cruce (verde adaptativo) y lo guarda.
    /// Regresa false si no se pudo guardar en la base de datos (el cruce si se actualizo).
    /// </summary>
    Task<bool> RegistrarConteoAsync(RegistrarConteoDto dto);

    /// <summary>Conteos con fecha (historial): solo se guardan. Regresa cuantos se guardaron.</summary>
    Task<int> RegistrarLoteAsync(RegistrarLoteConteosDto dto);

    Task<IReadOnlyList<ConteoTraficoDto>> ObtenerHistorialAsync(int cantidad);
}
