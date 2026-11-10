using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CiudadInteligente.Infrastructure.Persistence.Repositories;

/// <summary>Tabla ConteosTrafico con Entity Framework Core (SQLite).</summary>
public sealed class ConteoTraficoRepository(AppDbContext db) : IConteoTraficoRepository
{
    public async Task GuardarAsync(ConteoTrafico conteo)
    {
        db.ConteosTrafico.Add(conteo);
        await db.SaveChangesAsync();
    }

    public async Task GuardarVariosAsync(IReadOnlyList<ConteoTrafico> conteos)
    {
        if (conteos.Count == 0) return;

        // SaveChanges manda todo en una sola transaccion
        db.ConteosTrafico.AddRange(conteos);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ConteoTrafico>> ObtenerRecientesAsync(int cantidad) =>
        await db.ConteosTrafico.AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Take(cantidad)
            .ToListAsync();

    public async Task<IReadOnlyList<ConteoTrafico>> ObtenerDesdeAsync(DateTimeOffset desde) =>
        // las fechas se guardan como texto ISO en UTC: comparar texto = comparar fechas
        await db.ConteosTrafico.AsNoTracking()
            .Where(c => c.RegistradoEn >= desde)
            .OrderBy(c => c.RegistradoEn)
            .ToListAsync();
}
