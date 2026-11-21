using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CasaDomotica.Security.Infrastructure;

public sealed class CaptureCleanup(SecurityDb db, ICaptureContent content, TimeProvider clock)
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var policies = await db.CapturePolicies.AsNoTracking().Where(x => x.RetentionDays != null).ToListAsync(ct);
        foreach (var policy in policies)
        {
            var cutoff = now.AddDays(-policy.RetentionDays!.Value);
            await db.Captures.Where(x => x.HouseId == policy.HouseId && x.DeleteRequestedAtUtc == null && x.ReceivedAtUtc < cutoff && db.CapturePolicies.Any(p => p.HouseId == x.HouseId && p.RetentionDays == policy.RetentionDays)).ExecuteUpdateAsync(update => update.SetProperty(x => x.DeleteRequestedAtUtc, now), ct);
        }
        var rows = await db.Captures.AsNoTracking().Where(x => x.DeleteRequestedAtUtc != null && x.DeletedAtUtc == null && x.DeleteError == null).Take(20).ToListAsync(ct);
        foreach (var row in rows)
        {
            if (await content.DeleteAsync(row, ct))
                await db.Captures.Where(x => x.CaptureId == row.CaptureId && x.DeletedAtUtc == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.DeletedAtUtc, now).SetProperty(x => x.AnalysisText, (string?)null), ct);
            else
                await db.Captures.Where(x => x.CaptureId == row.CaptureId && x.DeletedAtUtc == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.DeleteError, "blobChanged"), ct);
        }
    }
}

public sealed class CaptureCleanupWorker(IServiceScopeFactory scopes, ILogger<CaptureCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<CaptureCleanup>().RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Capture cleanup unavailable: {Type}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
