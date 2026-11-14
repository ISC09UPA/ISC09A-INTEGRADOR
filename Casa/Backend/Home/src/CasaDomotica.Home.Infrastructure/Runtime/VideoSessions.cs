using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Home.Infrastructure.Runtime;

public sealed record VideoSessionView(Guid Id, string Status, DateTimeOffset ExpiresAtUtc, string? CommandId);

public sealed class VideoSessions(HomeDb db, IUserAuthorizer authorizer, TimeProvider clock)
{
    public const int MaximumSessionSeconds = 180;
    public async Task<VideoSessionView> StartAsync(UserContext user, Guid houseId, string resourceId, string key, Guid correlationId, CancellationToken ct)
    {
        if (houseId == Guid.Empty || correlationId == Guid.Empty || string.IsNullOrWhiteSpace(resourceId) || resourceId.Length > 128 || !Guid.TryParse(key, out var parsed) || parsed == Guid.Empty) throw new HomeException("invalidPayload", "Invalid video request.");
        await AuthorizeControlAsync(user, houseId, correlationId, ct);
        var normalizedKey = parsed.ToString("D");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var prior = await db.VideoSessions.SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == normalizedKey, ct);
            if (prior is not null)
            {
                if (prior.UserId != user.UserId || prior.UserSessionId != user.SessionId || prior.ResourceId != resourceId) throw new HomeException("duplicateChanged", "The video request key was already used.");
                var previous = await ViewAsync(prior, ct); await transaction.CommitAsync(ct); return previous;
            }
            if (await db.Commands.AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == normalizedKey, ct)) throw new HomeException("duplicateChanged", "The request key was already used.");
            var now = clock.GetUtcNow();
            var active = await db.VideoSessions.SingleOrDefaultAsync(x => x.HouseId == houseId && x.Active, ct);
            if (active is not null)
            {
                if (active.ExpiresAtUtc > now) throw new HomeException("pendingCommand", "Another video session is active for this house.");
                active.Active = false; active.ClosedAtUtc = now; active.CloseReason = "expired";
            }
            var resource = await db.Resources.SingleOrDefaultAsync(x => x.HouseId == houseId && x.ResourceId == resourceId && x.Kind == "camera" && x.Enabled, ct) ?? throw new HomeException("notFound", "No camera is associated with this house.");
            var device = await db.Devices.SingleOrDefaultAsync(x => x.DeviceId == resource.DeviceId && x.HouseId == houseId && x.Enabled, ct) ?? throw new HomeException("notFound", "The camera device is unavailable.");
            if (device.LastReportedAtUtc is null || device.LastReportedAtUtc < now.AddMinutes(-1)) throw new HomeException("deviceOffline", "The camera has no recent authenticated report.");
            if (await db.Commands.AnyAsync(x => x.HouseId == houseId && x.LightId == resourceId && x.Active, ct)) throw new HomeException("pendingCommand", "Another camera operation is awaiting confirmation.");
            var sessionId = Guid.NewGuid(); var commandId = Guid.NewGuid().ToString();
            var normalized = JsonSerializer.Serialize(new { resourceId, type = "StartVideo" });
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
            device.LastSequence++;
            var session = new VideoSessionRow { SessionId = sessionId, HouseId = houseId, ResourceId = resourceId, DeviceId = device.DeviceId, UserId = user.UserId, UserSessionId = user.SessionId, IdempotencyKey = normalizedKey, CommandId = commandId, CreatedAtUtc = now, ExpiresAtUtc = now.AddSeconds(MaximumSessionSeconds), LastSequence = -1 };
            db.VideoSessions.Add(session);
            db.Commands.Add(new CommandRow { CommandId = commandId, HouseId = houseId, LightId = resourceId, DeviceId = device.DeviceId, Type = "StartVideo", PayloadJson = JsonSerializer.Serialize(new { sessionId }), RequestHash = hash, IdempotencyKey = normalizedKey, Sequence = device.LastSequence, UserId = user.UserId, SessionId = user.SessionId, CorrelationId = correlationId, RequestedAtUtc = now, ExpiresAtUtc = now.AddSeconds(30) });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(sessionId, "connecting", session.ExpiresAtUtc, commandId);
        }
        catch (DbUpdateException)
        {
            // Two concurrent starts can race the filtered unique index. Re-read the
            // durable result instead of reporting a storage outage.
            db.ChangeTracker.Clear();
            var existing = await db.VideoSessions.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == normalizedKey, ct);
            if (existing is not null)
            {
                if (existing.UserId != user.UserId || existing.UserSessionId != user.SessionId || existing.ResourceId != resourceId) throw new HomeException("duplicateChanged", "The video request key was already used.");
                return await ViewAsync(existing, ct);
            }
            if (await db.VideoSessions.AsNoTracking().AnyAsync(x => x.HouseId == houseId && x.Active, ct)) throw new HomeException("pendingCommand", "Another video session is active for this house.");
            throw;
        }
    }
    public async Task<VideoSessionView?> GetAsync(UserContext user, Guid houseId, Guid sessionId, CancellationToken ct)
    {
        await AuthorizeViewAsync(user, houseId, Guid.NewGuid(), ct);
        var row = await db.VideoSessions.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == sessionId && x.HouseId == houseId && x.UserId == user.UserId && x.UserSessionId == user.SessionId, ct);
        return row is null ? null : await ViewAsync(row, ct);
    }
    public async Task<VideoSessionView?> ByKeyAsync(UserContext user, Guid houseId, string key, CancellationToken ct)
    {
        if (!Guid.TryParse(key, out var parsed) || parsed == Guid.Empty) throw new HomeException("invalidPayload", "Invalid video request key.");
        await AuthorizeViewAsync(user, houseId, Guid.NewGuid(), ct);
        var normalized = parsed.ToString("D");
        var row = await db.VideoSessions.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == normalized && x.UserId == user.UserId && x.UserSessionId == user.SessionId, ct);
        return row is null ? null : await ViewAsync(row, ct);
    }
    public async Task<VideoSessionView?> CloseAsync(UserContext user, Guid houseId, Guid sessionId, CancellationToken ct)
    {
        await AuthorizeControlAsync(user, houseId, Guid.NewGuid(), ct);
        var row = await db.VideoSessions.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == sessionId && x.HouseId == houseId && x.UserId == user.UserId && x.UserSessionId == user.SessionId, ct);
        if (row is null) return null;
        var now = clock.GetUtcNow();
        await db.VideoSessions.Where(x => x.SessionId == sessionId && x.Active).ExecuteUpdateAsync(u => u.SetProperty(x => x.Active, false).SetProperty(x => x.ClosedAtUtc, now).SetProperty(x => x.CloseReason, "closed"), ct);
        var command = await db.Commands.SingleOrDefaultAsync(x => x.CommandId == row.CommandId && (x.Status == "pending" || x.Status == "unknown"), ct);
        if (command is not null)
        {
            // Closing the viewer session makes a still-unconfirmed producer order
            // terminal so the camera resource is not locked forever.
            command.Status = "expired"; command.Active = false;
            command.ResultJson = JsonSerializer.Serialize(new { commandId = command.CommandId, type = command.Type, status = "expired", reasonCode = "expired", reason = "Video session closed before confirmation.", snapshot = (object?)null, stateRevision = (long?)null, correlationId = command.CorrelationId });
            await db.SaveChangesAsync(ct);
        }
        return new(sessionId, "ended", row.ExpiresAtUtc, row.CommandId);
    }
    public async Task<VideoSessionRow?> AuthorizedActiveAsync(UserContext user, Guid houseId, Guid sessionId, CancellationToken ct)
    {
        await AuthorizeViewAsync(user, houseId, Guid.NewGuid(), ct);
        var row = await db.VideoSessions.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == sessionId && x.HouseId == houseId && x.UserId == user.UserId && x.UserSessionId == user.SessionId && x.Active, ct);
        return row is null || row.ExpiresAtUtc <= clock.GetUtcNow() ? null : row;
    }
    private async Task AuthorizeControlAsync(UserContext user, Guid houseId, Guid correlation, CancellationToken ct)
    {
        if (!await authorizer.CheckAsync(user, houseId, "home:control:capture", correlation, ct)) throw new HomeException("forbidden", "Starting or closing video requires administrative permission for this house.");
    }
    private async Task AuthorizeViewAsync(UserContext user, Guid houseId, Guid correlation, CancellationToken ct)
    {
        if (!await authorizer.CheckAsync(user, houseId, "home:read", correlation, ct)) throw new HomeException("forbidden", "Viewing video requires membership in this house.");
    }
    private async Task<VideoSessionView> ViewAsync(VideoSessionRow row, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!row.Active || row.ExpiresAtUtc <= now) return new(row.SessionId, row.CloseReason == "failed" ? "failed" : "ended", row.ExpiresAtUtc, row.CommandId);
        var status = await db.Commands.AsNoTracking().Where(x => x.CommandId == row.CommandId).Select(x => x.Status).SingleOrDefaultAsync(ct);
        if (status is "rejected" or "failed" or "expired") return new(row.SessionId, "failed", row.ExpiresAtUtc, row.CommandId);
        var streaming = row.PlaylistJson is not null && row.PlaylistUpdatedAtUtc is not null && row.PlaylistUpdatedAtUtc >= now.AddSeconds(-20);
        return new(row.SessionId, streaming ? "live" : "connecting", row.ExpiresAtUtc, row.CommandId);
    }
}
