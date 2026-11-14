using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CasaDomotica.Home.Infrastructure.Runtime;

public sealed class VideoCleanup(HomeDb db, IPrivateVideoStore storage, TimeProvider clock)
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var active = await db.VideoSessions.AsNoTracking().Where(x => x.Active).Take(100).ToListAsync(ct);
        foreach (var session in active)
        {
            var commandStatus = await db.Commands.AsNoTracking().Where(x => x.CommandId == session.CommandId).Select(x => x.Status).SingleOrDefaultAsync(ct);
            var reason = session.ExpiresAtUtc <= now ? "expired" : commandStatus is null or "failed" or "rejected" or "expired" ? "failed" : null;
            if (reason is not null)
            {
                await db.VideoSessions.Where(x => x.SessionId == session.SessionId && x.Active).ExecuteUpdateAsync(u => u.SetProperty(x => x.Active, false).SetProperty(x => x.ClosedAtUtc, now).SetProperty(x => x.CloseReason, reason), ct);
                if (reason == "expired") await ExpireCommandAsync(session.CommandId, ct);
            }
        }
        var closed = await db.VideoSessions.AsNoTracking().Where(x => !x.Active && x.ClosedAtUtc != null && db.VideoSegments.Any(s => s.SessionId == x.SessionId && s.DeletedAtUtc == null && s.CleanupError == null)).Take(100).ToListAsync(ct);
        var eligible = closed.Where(x => x.ClosedAtUtc <= now.AddSeconds(-30)).ToArray();
        if (eligible.Length == 0) return;
        foreach (var session in eligible) await ExpireCommandAsync(session.CommandId, ct);
        var sessionIds = eligible.Select(x => x.SessionId).ToArray();
        var segments = await db.VideoSegments.AsNoTracking().Where(x => sessionIds.Contains(x.SessionId) && x.DeletedAtUtc == null && x.CleanupError == null).Take(40).ToListAsync(ct);
        foreach (var segment in segments)
        {
            // A timed-out upload may still be completing remotely. Keep ambiguous
            // segments long enough for the bounded upload attempt to settle.
            if (segment.State != "ready" && segment.CreatedAtUtc > now.AddMinutes(-2)) continue;
            try
            {
                if (await storage.DeleteAsync(segment, ct))
                    await db.VideoSegments.Where(x => x.SessionId == segment.SessionId && x.Sequence == segment.Sequence && x.DeletedAtUtc == null).ExecuteUpdateAsync(u => u.SetProperty(x => x.DeletedAtUtc, now), ct);
                else
                {
                    // A changed object is permanent evidence of an integrity mismatch.
                    // Leave transient storage failures retryable on the next cycle.
                    await db.VideoSegments.Where(x => x.SessionId == segment.SessionId && x.Sequence == segment.Sequence && x.DeletedAtUtc == null).ExecuteUpdateAsync(u => u.SetProperty(x => x.CleanupError, "blobChanged"), ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                // Transient storage failure: keep DeletedAtUtc/CleanupError null so the
                // next cleanup cycle retries instead of abandoning private data.
            }
        }
    }
    private async Task ExpireCommandAsync(string commandId, CancellationToken ct)
    {
        var command = await db.Commands.SingleOrDefaultAsync(x => x.CommandId == commandId && (x.Status == "pending" || x.Status == "unknown"), ct);
        if (command is null) return;
        // A closed or expired video session must not leave the camera resource locked.
        command.Status = "expired"; command.Active = false;
        command.ResultJson = JsonSerializer.Serialize(new { commandId = command.CommandId, type = command.Type, status = "expired", reasonCode = "expired", reason = "Video session ended before confirmation.", snapshot = (object?)null, stateRevision = (long?)null, correlationId = command.CorrelationId });
        await db.SaveChangesAsync(ct);
    }
}
public sealed class VideoCleanupWorker(IServiceScopeFactory scopes, ILogger<VideoCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<VideoCleanup>().RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Video cleanup unavailable: {Type}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
