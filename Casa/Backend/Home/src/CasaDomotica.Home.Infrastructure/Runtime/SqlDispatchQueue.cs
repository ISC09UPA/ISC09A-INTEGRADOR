using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace CasaDomotica.Home.Infrastructure.Runtime;

public sealed class SqlDispatchQueue(HomeDb db) : IDispatchQueue
{
    public async Task ExpireAsync(DateTimeOffset now, CancellationToken ct)
    {
        var timedOut = await db.Commands.AsNoTracking().Where(x => x.Status == "pending" && x.ExpiresAtUtc < now).Take(20).ToListAsync(ct);
        foreach (var row in timedOut)
        {
            var unclaimed = row.DispatchedAtUtc is null;
            var status = unclaimed ? "expired" : "unknown";
            var result = Outcome(row, status, status, "The order expired without a confirmed result.");
            // Recheck dispatch state atomically. A concurrent worker may have claimed the row after our read.
            var match = db.Commands.Where(x => x.CommandId == row.CommandId && x.Status == "pending" && x.ExpiresAtUtc < now);
            match = unclaimed ? match.Where(x => x.DispatchedAtUtc == null) : match.Where(x => x.DispatchedAtUtc != null);
            await match.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, status).SetProperty(x => x.Active, status == "unknown").SetProperty(x => x.ResultJson, result), ct);
        }
    }
    public Task<CommandRow?> NextAsync(DateTimeOffset now, CancellationToken ct) => db.Commands.AsNoTracking().Where(x => x.Status == "pending" && x.DispatchedAtUtc == null && x.ExpiresAtUtc > now).OrderBy(x => x.RequestedAtUtc).FirstOrDefaultAsync(ct);
    public Task<bool> HasPendingAsync(CancellationToken ct) => db.Commands.AnyAsync(x => x.Status == "pending", ct);
    public async Task<bool> ClaimAsync(string id, DateTimeOffset now, CancellationToken ct) => await db.Commands.Where(x => x.CommandId == id && x.Status == "pending" && x.DispatchedAtUtc == null && x.ExpiresAtUtc > now).ExecuteUpdateAsync(update => update.SetProperty(x => x.DispatchedAtUtc, now), ct) == 1;
    public async Task CompleteAsync(CommandRow command, string status, string code, string reason, bool onlyIfUnclaimed, CancellationToken ct)
    {
        var result = Outcome(command, status, code, reason);
        var match = db.Commands.Where(x => x.CommandId == command.CommandId && x.Status == "pending");
        if (onlyIfUnclaimed) match = match.Where(x => x.DispatchedAtUtc == null);
        await match.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, status).SetProperty(x => x.Active, status == "unknown").SetProperty(x => x.ResultJson, result), ct);
    }
    private static string Outcome(CommandRow command, string status, string code, string reason) => JsonSerializer.Serialize(new { commandId = command.CommandId, type = command.Type, status, reasonCode = code, reason, snapshot = (object?)null, stateRevision = (long?)null, correlationId = command.CorrelationId });
}
