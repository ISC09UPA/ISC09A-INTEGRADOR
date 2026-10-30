using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Application.Interfaces;
using InvernaderoInteligente.Domain.Entities;
using InvernaderoInteligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvernaderoInteligente.Infrastructure.Services;

public class LecturaService : ILecturaService
{
    private readonly InvernaderoDbContext _db;

    public LecturaService(InvernaderoDbContext db) => _db = db;

    public async Task<LecturaDto> CrearAsync(CrearLecturaDto dto, CancellationToken ct = default)
    {
        var lectura = new LecturaSensor
        {
            SensorId = dto.SensorId.Trim(),
            Tipo = dto.Tipo.Trim().ToLowerInvariant(),
            Valor = dto.Valor!.Value,
            Unidad = dto.Unidad.Trim(),
            InvernaderoId = dto.InvernaderoId,
            FechaHora = (dto.FechaHora ?? DateTime.UtcNow).ToUniversalTime()
        };

        _db.Lecturas.Add(lectura);
        await _db.SaveChangesAsync(ct);

        return ADto(lectura);
    }

    public async Task<IReadOnlyList<LecturaDto>> ObtenerAsync(FiltroLecturasDto filtro, CancellationToken ct = default)
    {
        var query = _db.Lecturas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.SensorId))
            query = query.Where(l => l.SensorId == filtro.SensorId);

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            var tipo = filtro.Tipo.Trim().ToLowerInvariant();
            query = query.Where(l => l.Tipo == tipo);
        }

        if (filtro.InvernaderoId.HasValue)
            query = query.Where(l => l.InvernaderoId == filtro.InvernaderoId);

        if (filtro.Desde.HasValue)
            query = query.Where(l => l.FechaHora >= filtro.Desde.Value.ToUniversalTime());

        if (filtro.Hasta.HasValue)
            query = query.Where(l => l.FechaHora <= filtro.Hasta.Value.ToUniversalTime());

        var lecturas = await query
            .OrderByDescending(l => l.FechaHora)
            .Take(filtro.Limite)
            .ToListAsync(ct);

        return lecturas.Select(ADto).ToList();
    }

    public async Task<LecturaDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var lectura = await _db.Lecturas.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        return lectura is null ? null : ADto(lectura);
    }

    private static LecturaDto ADto(LecturaSensor l) => new()
    {
        Id = l.Id,
        SensorId = l.SensorId,
        Tipo = l.Tipo,
        Valor = l.Valor,
        Unidad = l.Unidad,
        InvernaderoId = l.InvernaderoId,
        FechaHora = l.FechaHora
    };
}
