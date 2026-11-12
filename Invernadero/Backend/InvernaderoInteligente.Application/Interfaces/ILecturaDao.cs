using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Domain.Entities;

namespace InvernaderoInteligente.Application.Interfaces;

public interface ILecturaDao
{
    Task<bool> ExisteAsync(string sensorId, string tipo, DateTime fechaHora, CancellationToken ct = default);
    Task AgregarAsync(LecturaSensor lectura, CancellationToken ct = default);
    Task<IReadOnlyList<LecturaSensor>> ObtenerAsync(FiltroLecturasDto filtro, CancellationToken ct = default);
    Task<IReadOnlyList<LecturaSensor>> ObtenerParaExportacionAsync(
        FiltroExportacionLecturasDto filtro,
        CancellationToken ct = default);
    Task<LecturaSensor?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}
