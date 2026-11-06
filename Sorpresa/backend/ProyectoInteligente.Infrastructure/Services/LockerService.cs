using LockerSmart.Application.Interfaces;
using LockerSmart.Domain.Entities;
using LockerSmart.Domain.Enums;
using LockerSmart.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LockerSmart.Infrastructure.Services;

public class LockerService(AppDbContext db) : ILockerService
{
    public Task<List<Locker>> ListarAsync(bool soloDisponibles) =>
        db.Lockers
          .Where(l => !soloDisponibles || l.Estado == LockerEstado.Disponible)
          .OrderBy(l => l.Codigo)
          .ToListAsync();

    public Task<Locker?> ObtenerAsync(int id) => db.Lockers.FindAsync(id).AsTask();

    public async Task<Locker> CrearAsync(Locker locker)
    {
        db.Lockers.Add(locker);
        await db.SaveChangesAsync();
        return locker;
    }

    public async Task<bool> CambiarEstadoAsync(int id, LockerEstado estado)
    {
        var l = await db.Lockers.FindAsync(id);
        if (l is null) return false;
        l.Estado = estado;
        await db.SaveChangesAsync();
        return true;
    }
}