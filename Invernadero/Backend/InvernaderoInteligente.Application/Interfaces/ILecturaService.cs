using Telemetria.DTOs;

namespace Telemetria.Services;

public interface ILecturaService
{
    Task<LecturaDto> CrearAsync(CrearLecturaDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<LecturaDto>> ObtenerAsync(FiltroLecturasDto filtro, CancellationToken ct = default);
    Task<LecturaDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}