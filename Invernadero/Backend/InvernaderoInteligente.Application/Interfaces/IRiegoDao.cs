using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Domain.Entities;

namespace InvernaderoInteligente.Application.Interfaces;

public interface IRiegoDao
{
    Task<bool> ExisteAsync(int? invernaderoId, string? zona, DateTime fechaHoraInicio, CancellationToken ct = default);
    Task AgregarAsync(Riego riego, CancellationToken ct = default);
    Task<IReadOnlyList<Riego>> ObtenerAsync(FiltroRiegosDto filtro, CancellationToken ct = default);
    Task<Riego?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}
