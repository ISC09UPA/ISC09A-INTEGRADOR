using InvernaderoInteligente.Application.DTOs;

namespace InvernaderoInteligente.Application.Interfaces;

public interface ILecturaService
{
    Task<LecturaDto> CrearAsync(CrearLecturaDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<LecturaDto>> ObtenerAsync(FiltroLecturasDto filtro, CancellationToken ct = default);
    Task<IReadOnlyList<LecturaDto>> ObtenerParaExportacionAsync(FiltroExportacionLecturasDto filtro, CancellationToken ct = default);
    Task<LecturaDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}
