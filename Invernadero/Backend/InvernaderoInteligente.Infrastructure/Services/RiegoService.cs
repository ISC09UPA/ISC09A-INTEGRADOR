using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Application.Exceptions;
using InvernaderoInteligente.Application.Interfaces;
using InvernaderoInteligente.Application.Mappings;
using InvernaderoInteligente.Domain.Entities;

namespace InvernaderoInteligente.Infrastructure.Services;

public class RiegoService : IRiegoService
{
    private readonly IRiegoDao _dao;

    public RiegoService(IRiegoDao dao) => _dao = dao;

    public async Task<RiegoDto> CrearAsync(CrearRiegoDto dto, CancellationToken ct = default)
    {
        var riego = dto.ToEntity();

        var existe = await _dao.ExisteAsync(
            riego.InvernaderoId,
            riego.Zona,
            riego.FechaHoraInicio,
            ct);

        if (existe)
            throw new RegistroDuplicadoException(MensajeDuplicado(riego));

        await _dao.AgregarAsync(riego, ct);

        return riego.ToDto();
    }

    public async Task<IReadOnlyList<RiegoDto>> ObtenerAsync(FiltroRiegosDto filtro, CancellationToken ct = default)
    {
        var riegos = await _dao.ObtenerAsync(filtro, ct);

        return riegos.Select(riego => riego.ToDto()).ToList();
    }

    public async Task<RiegoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var riego = await _dao.ObtenerPorIdAsync(id, ct);
        return riego?.ToDto();
    }

    private static string MensajeDuplicado(Riego r) =>
        $"Ya existe un riego en la zona '{r.Zona ?? "sin zona"}' que inició en {r.FechaHoraInicio:O}.";

}
