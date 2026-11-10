using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CiudadInteligente.Infrastructure.Persistence.Repositories;

/// <summary>Tabla Emergencias con Entity Framework Core (SQLite).</summary>
public sealed class EmergenciaRepository(AppDbContext db) : IEmergenciaRepository
{
    public async Task<int> RegistrarAsync(Emergencia emergencia)
    {
        db.Emergencias.Add(emergencia);
        await db.SaveChangesAsync();
        return emergencia.Id;
    }

    public Task<int> FinalizarActivasAsync(DateTimeOffset finalizadaEn) =>
        db.Emergencias.Where(e => e.FinalizadaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.FinalizadaEn, (DateTimeOffset?)finalizadaEn));

    public async Task<IReadOnlyList<Emergencia>> ObtenerRecientesAsync(int cantidad) =>
        await db.Emergencias.AsNoTracking()
            .OrderByDescending(e => e.Id)
            .Take(cantidad)
            .ToListAsync();
}
