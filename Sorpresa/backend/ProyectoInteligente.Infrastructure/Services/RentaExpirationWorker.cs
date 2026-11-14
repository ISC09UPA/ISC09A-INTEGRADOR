using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LockerSmart.Domain.Enums;
using LockerSmart.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LockerSmart.Infrastructure.Services;

/// <summary>Marca automáticamente como excedidas las rentas cuyo horario terminó.</summary>
public sealed class RentaExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<RentaExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ahora = DateTime.UtcNow;
                var vencidas = await db.Rentas.Where(r => r.Estado == RentaEstado.Activa && r.Fin <= ahora).ToListAsync(stoppingToken);
                foreach (var renta in vencidas) renta.Estado = RentaEstado.Excedida;
                if (vencidas.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Se marcaron {Count} rentas como excedidas.", vencidas.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error actualizando rentas vencidas."); }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
