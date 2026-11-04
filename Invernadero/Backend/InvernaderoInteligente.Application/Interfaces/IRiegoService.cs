using InvernaderoInteligente.Application.DTOs;

namespace InvernaderoInteligente.Application.Interfaces;

public interface IRiegoService
{
    Task<RiegoDto> CrearAsync(CrearRiegoDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<RiegoDto>> ObtenerAsync(FiltroRiegosDto filtro, CancellationToken ct = default);
    Task<RiegoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}
