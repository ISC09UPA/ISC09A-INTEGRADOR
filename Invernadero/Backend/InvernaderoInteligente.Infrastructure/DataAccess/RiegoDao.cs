using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Application.Exceptions;
using InvernaderoInteligente.Application.Interfaces;
using InvernaderoInteligente.Domain.Entities;
using InvernaderoInteligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvernaderoInteligente.Infrastructure.DataAccess;

public sealed class RiegoDao(InvernaderoDbContext db) : IRiegoDao
{
    public Task<bool> ExisteAsync(
        int? invernaderoId,
        string? zona,
        DateTime fechaHoraInicio,
        CancellationToken ct = default) =>
        db.Riegos.AnyAsync(r =>
            r.InvernaderoId == invernaderoId &&
            r.Zona == zona &&
            r.FechaHoraInicio == fechaHoraInicio, ct);

    public async Task AgregarAsync(Riego riego, CancellationToken ct = default)
    {
        db.Riegos.Add(riego);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ErroresBd.EsDuplicado(ex))
        {
            throw new RegistroDuplicadoException(
                $"Ya existe un riego en la zona '{riego.Zona ?? "sin zona"}' que inició en {riego.FechaHoraInicio:O}.");
        }
    }

    public async Task<IReadOnlyList<Riego>> ObtenerAsync(
        FiltroRiegosDto filtro,
        CancellationToken ct = default)
    {
        var query = db.Riegos.AsNoTracking().AsQueryable();

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

        return await query
            .OrderByDescending(r => r.FechaHoraInicio)
            .Take(filtro.Limite)
            .ToListAsync(ct);
    }

    public Task<Riego?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Riegos.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
}
