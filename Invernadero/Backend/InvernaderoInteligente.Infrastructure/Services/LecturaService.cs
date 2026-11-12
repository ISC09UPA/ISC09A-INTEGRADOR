using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Application.Exceptions;
using InvernaderoInteligente.Application.Interfaces;
using InvernaderoInteligente.Application.Mappings;
using InvernaderoInteligente.Domain.Entities;

namespace InvernaderoInteligente.Infrastructure.Services;

public class LecturaService : ILecturaService
{
    private readonly ILecturaDao _dao;

    public LecturaService(ILecturaDao dao) => _dao = dao;

    public async Task<LecturaDto> CrearAsync(CrearLecturaDto dto, CancellationToken ct = default)
    {
        var lectura = dto.ToEntity();

        var existe = await _dao.ExisteAsync(lectura.SensorId, lectura.Tipo, lectura.FechaHora, ct);

        if (existe)
            throw new RegistroDuplicadoException(MensajeDuplicado(lectura));

        await _dao.AgregarAsync(lectura, ct);

        return lectura.ToDto();
    }

    public async Task<IReadOnlyList<LecturaDto>> ObtenerAsync(FiltroLecturasDto filtro, CancellationToken ct = default)
    {
        var lecturas = await _dao.ObtenerAsync(filtro, ct);

        return lecturas.Select(lectura => lectura.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<LecturaDto>> ObtenerParaExportacionAsync(
        FiltroExportacionLecturasDto filtro,
        CancellationToken ct = default)
    {
        var lecturas = await _dao.ObtenerParaExportacionAsync(filtro, ct);

        return lecturas.Select(lectura => lectura.ToDto()).ToList();
    }

    public async Task<LecturaDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var lectura = await _dao.ObtenerPorIdAsync(id, ct);
        return lectura?.ToDto();
    }

    private static string MensajeDuplicado(LecturaSensor l) =>
        $"Ya existe una lectura del sensor '{l.SensorId}' ({l.Tipo}) en {l.FechaHora:O}.";

}
