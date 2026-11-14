using LockerSmart.Application.Interfaces;
using LockerSmart.Domain.Entities;
using LockerSmart.Domain.Enums;
using LockerSmart.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LockerSmart.Infrastructure.Services;

public class RentaService(AppDbContext db) : IRentaService
{
    public async Task<Renta?> CrearAsync(
        int usuarioId,
        double pesoDeclaradoKg,
        int duracionHoras)
    {
        if (pesoDeclaradoKg <= 0 || duracionHoras <= 0)
            return null;

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        var candidatos = await db.Lockers
            .Where(l => l.Estado == LockerEstado.Disponible && l.PesoMaximoKg >= pesoDeclaradoKg)
            .OrderBy(l => l.PesoMaximoKg)
            .ThenBy(l => l.Codigo)
            .Select(l => l.Id)
            .ToListAsync();

        Locker? locker = null;
        foreach (var lockerId in candidatos)
        {
            var actualizados = await db.Lockers
                .Where(l => l.Id == lockerId && l.Estado == LockerEstado.Disponible)
                .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Estado, LockerEstado.Ocupado));
            if (actualizados == 1)
            {
                locker = await db.Lockers.FirstAsync(l => l.Id == lockerId);
                break;
            }
        }

        if (locker is null)
        {
            await tx.RollbackAsync();
            return null;
        }

        var inicio = DateTime.UtcNow;

        var renta = new Renta
        {
            UsuarioId = usuarioId,
            LockerId = locker.Id,
            Inicio = inicio,
            Fin = inicio.AddHours(duracionHoras),
            PesoDeclaradoKg = pesoDeclaradoKg,
            Estado = RentaEstado.Activa
        };

        db.Rentas.Add(renta);

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return renta;
    }

    public Task<Renta?> ObtenerAsync(int id) =>
        db.Rentas.FirstOrDefaultAsync(r => r.Id == id);

    public Task<List<Renta>> ListarPorUsuarioAsync(int usuarioId) =>
        db.Rentas
            .Where(r => r.UsuarioId == usuarioId)
            .OrderByDescending(r => r.Inicio)
            .ToListAsync();

    public Task<List<Renta>> ListarActivasAsync() =>
        db.Rentas
            .Where(r => r.Estado == RentaEstado.Activa ||
                        r.Estado == RentaEstado.Excedida)
            .OrderBy(r => r.Fin)
            .ToListAsync();

    public async Task<bool> FinalizarAsync(
        int id,
        double? pesoFinalKg)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var renta = await db.Rentas
            .FirstOrDefaultAsync(r => r.Id == id);

        if (renta is null ||
            renta.Estado == RentaEstado.Finalizada)
            return false;

        if (pesoFinalKg.HasValue && pesoFinalKg.Value < 0)
            return false;

        var locker = await db.Lockers
            .FirstOrDefaultAsync(l => l.Id == renta.LockerId);

        if (locker is null)
            return false;

        renta.FechaFinalizacion = DateTime.UtcNow;
        renta.PesoFinalKg = pesoFinalKg;
        renta.Estado = RentaEstado.Finalizada;

        locker.Estado = LockerEstado.Disponible;
        locker.PesoActualKg = 0;

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return true;
    }

    public async Task<int> ActualizarVencidasAsync()
    {
        var ahora = DateTime.UtcNow;

        var vencidas = await db.Rentas
            .Where(r => r.Estado == RentaEstado.Activa &&
                        r.Fin <= ahora)
            .ToListAsync();

        foreach (var renta in vencidas)
            renta.Estado = RentaEstado.Excedida;

        await db.SaveChangesAsync();

        return vencidas.Count;
    }
}