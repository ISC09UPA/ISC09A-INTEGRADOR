using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Application.Exceptions;
using InvernaderoInteligente.Application.Interfaces;
using InvernaderoInteligente.Domain.Entities;
using InvernaderoInteligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvernaderoInteligente.Infrastructure.DataAccess;

public sealed class LecturaDao(InvernaderoDbContext db) : ILecturaDao
{
    public Task<bool> ExisteAsync(
        string sensorId,
        string tipo,
        DateTime fechaHora,
        CancellationToken ct = default) =>
        db.Lecturas.AnyAsync(l =>
            l.SensorId == sensorId &&
            l.Tipo == tipo &&
            l.FechaHora == fechaHora, ct);

    public async Task AgregarAsync(LecturaSensor lectura, CancellationToken ct = default)
    {
        db.Lecturas.Add(lectura);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ErroresBd.EsDuplicado(ex))
        {
            throw new RegistroDuplicadoException(
                $"Ya existe una lectura del sensor '{lectura.SensorId}' ({lectura.Tipo}) en {lectura.FechaHora:O}.");
        }
    }

    public async Task<IReadOnlyList<LecturaSensor>> ObtenerAsync(
        FiltroLecturasDto filtro,
        CancellationToken ct = default)
    {
        return await AplicarFiltros(filtro)
            .OrderByDescending(l => l.FechaHora)
            .Take(filtro.Limite)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LecturaSensor>> ObtenerParaExportacionAsync(
        FiltroExportacionLecturasDto filtro,
        CancellationToken ct = default)
    {
        return await AplicarFiltros(filtro)
            .OrderBy(l => l.FechaHora)
            .ThenBy(l => l.Id)
            .ToListAsync(ct);
    }

    public Task<LecturaSensor?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Lecturas.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);

    private IQueryable<LecturaSensor> AplicarFiltros(FiltroExportacionLecturasDto filtro)
    {
        var query = db.Lecturas.AsNoTracking().AsQueryable();

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

        return query;
    }
}
