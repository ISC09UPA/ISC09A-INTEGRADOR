using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Application.Exceptions;
using InvernaderoInteligente.Application.Interfaces;
using InvernaderoInteligente.Domain.Entities;
using InvernaderoInteligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvernaderoInteligente.Infrastructure.Services;

public class RiegoService : IRiegoService
{
    private readonly InvernaderoDbContext _db;

    public RiegoService(InvernaderoDbContext db) => _db = db;

    public async Task<RiegoDto> CrearAsync(CrearRiegoDto dto, CancellationToken ct = default)
    {
        var riego = new Riego
        {
            InvernaderoId = dto.InvernaderoId,
            Zona = dto.Zona?.Trim(),
            FechaHoraInicio = (dto.FechaHoraInicio ?? DateTime.UtcNow).ToUniversalTime(),
            DuracionSegundos = dto.DuracionSegundos!.Value,
            VolumenMl = dto.VolumenMl,
            Origen = dto.Origen.Trim().ToLowerInvariant(),
            HumedadDetectada = dto.HumedadDetectada,
            Umbral = dto.Umbral,
            LecturaSensorId = dto.LecturaSensorId
        };

        var existe = await _db.Riegos.AnyAsync(r =>
            r.InvernaderoId == riego.InvernaderoId &&
            r.Zona == riego.Zona &&
            r.FechaHoraInicio == riego.FechaHoraInicio, ct);

        if (existe)
            throw new RegistroDuplicadoException(MensajeDuplicado(riego));

        _db.Riegos.Add(riego);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ErroresBd.EsDuplicado(ex))
        {
            // Dos peticiones iguales al mismo tiempo: el índice único lo detecta
            throw new RegistroDuplicadoException(MensajeDuplicado(riego));
        }

        return ADto(riego);
    }

    public async Task<IReadOnlyList<RiegoDto>> ObtenerAsync(FiltroRiegosDto filtro, CancellationToken ct = default)
    {
        var query = _db.Riegos.AsNoTracking().AsQueryable();

        if (filtro.InvernaderoId.HasValue)
            query = query.Where(r => r.InvernaderoId == filtro.InvernaderoId);

        if (!string.IsNullOrWhiteSpace(filtro.Zona))
            query = query.Where(r => r.Zona == filtro.Zona);

        if (!string.IsNullOrWhiteSpace(filtro.Origen))
        {
            var origen = filtro.Origen.Trim().ToLowerInvariant();
            query = query.Where(r => r.Origen == origen);
        }

        if (filtro.Desde.HasValue)
            query = query.Where(r => r.FechaHoraInicio >= filtro.Desde.Value.ToUniversalTime());

        if (filtro.Hasta.HasValue)
            query = query.Where(r => r.FechaHoraInicio <= filtro.Hasta.Value.ToUniversalTime());

        var riegos = await query
            .OrderByDescending(r => r.FechaHoraInicio)
            .Take(filtro.Limite)
            .ToListAsync(ct);

        return riegos.Select(ADto).ToList();
    }

    public async Task<RiegoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var riego = await _db.Riegos.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return riego is null ? null : ADto(riego);
    }

    private static string MensajeDuplicado(Riego r) =>
        $"Ya existe un riego en la zona '{r.Zona ?? "sin zona"}' que inició en {r.FechaHoraInicio:O}.";

    private static RiegoDto ADto(Riego r) => new()
    {
        Id = r.Id,
        InvernaderoId = r.InvernaderoId,
        Zona = r.Zona,
        FechaHoraInicio = r.FechaHoraInicio,
        DuracionSegundos = r.DuracionSegundos,
        VolumenMl = r.VolumenMl,
        Origen = r.Origen,
        HumedadDetectada = r.HumedadDetectada,
        Umbral = r.Umbral,
        LecturaSensorId = r.LecturaSensorId
    };
}
