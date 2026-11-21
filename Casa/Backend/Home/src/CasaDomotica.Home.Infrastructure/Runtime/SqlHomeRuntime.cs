using System.Data;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace CasaDomotica.Home.Infrastructure.Runtime;
public sealed class SqlHomeRuntime(HomeDb db, TimeProvider? clock = null) : IHomeRuntime, ICapabilityRuntime
{
    public static StoredCommand Map(CommandRow row) => new(row.CommandId, row.HouseId, row.LightId, row.DeviceId, row.Type, row.PayloadJson, row.RequestHash, row.IdempotencyKey, row.Sequence, row.RequestedAtUtc, row.ExpiresAtUtc, row.CorrelationId, row.Status, row.ResultJson);
    public async Task<StoredCommand?> FindAsync(string commandId, CancellationToken ct)
    {
        var row = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.CommandId == commandId, ct);
        if (row is null) return null;
        return Map(await ExpireOverdueAsync(row, (clock ?? TimeProvider.System).GetUtcNow(), ct));
    }
    public async Task<StoredCommand?> FindByKeyAsync(Guid houseId, string key, CancellationToken ct, DateTimeOffset? clientExpiresAtUtc = null)
    {
        if (clientExpiresAtUtc is { } invalid && invalid.Offset != TimeSpan.Zero)
            throw new HomeException("invalidPayload", "The client request deadline is invalid.");
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var row = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct);
        if (row is not null) return Map(await ExpireOverdueAsync(row, now, ct));
        if (await db.ExpiredCommandKeys.AsNoTracking().AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct))
            throw new HomeException("requestExpired", "The request expired before admission.");
        if (clientExpiresAtUtc is { } deadline && deadline <= now)
            return await PersistExpiredKeyAsync(houseId, key, deadline, now, ct);
        return null;
    }
    private async Task<StoredCommand> PersistExpiredKeyAsync(Guid houseId, string key, DateTimeOffset deadline, DateTimeOffset now, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var existing = await db.Commands.SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct);
                if (existing is not null)
                {
                    await transaction.CommitAsync(ct);
                    return Map(await ExpireOverdueAsync(existing, now, ct));
                }
                if (!await db.ExpiredCommandKeys.AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct))
                {
                    db.ExpiredCommandKeys.Add(new ExpiredCommandKeyRow { HouseId = houseId, IdempotencyKey = key, ClientExpiresAtUtc = deadline, ExpiredAtUtc = now });
                    await db.SaveChangesAsync(ct);
                }
                await transaction.CommitAsync(ct);
                throw new HomeException("requestExpired", "The request expired before admission.");
            }
            catch (Exception ex) when (IsSqlRaceOrDeadlock(ex))
            {
                db.ChangeTracker.Clear();
                var afterRace = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct);
                if (afterRace is not null) return Map(await ExpireOverdueAsync(afterRace, now, ct));
                if (await db.ExpiredCommandKeys.AsNoTracking().AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct))
                    throw new HomeException("requestExpired", "The request expired before admission.");
                if (attempt < 4 && IsDeadlock(ex)) { await Task.Delay(15, ct); continue; }
                if (attempt == 4) throw;
            }
        }
        throw new HomeException("requestExpired", "The request expired before admission.");
    }
    private async Task<CommandRow> ExpireOverdueAsync(CommandRow row, DateTimeOffset now, CancellationToken ct)
    {
        if (row.Status != "pending" || row.ExpiresAtUtc > now) return row;
        var unclaimed = row.DispatchedAtUtc is null;
        var status = unclaimed ? "expired" : "unknown";
        var result = JsonSerializer.Serialize(new { commandId = row.CommandId, type = row.Type, status, reasonCode = status, reason = "The order expired without a confirmed result.", snapshot = (object?)null, stateRevision = (long?)null, correlationId = row.CorrelationId });
        var match = db.Commands.Where(x => x.CommandId == row.CommandId && x.Status == "pending");
        match = unclaimed ? match.Where(x => x.DispatchedAtUtc == null) : match.Where(x => x.DispatchedAtUtc != null);
        await match.ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, status).SetProperty(x => x.Active, status == "unknown").SetProperty(x => x.ResultJson, result), ct);
        return await db.Commands.AsNoTracking().SingleAsync(x => x.CommandId == row.CommandId, ct);
    }
    public async Task<StoredStatus?> StatusAsync(Guid houseId, CancellationToken ct)
    {
        var reported = await db.Devices.AsNoTracking().Where(x => x.HouseId == houseId && x.Enabled && x.LastReportedAtUtc != null).MaxAsync(x => x.LastReportedAtUtc, ct);
        if (reported is null) return null;
        var lights = await db.Lights.AsNoTracking().Where(x => x.HouseId == houseId && x.AppliedAtUtc != null && x.LightOn != null && x.LightingMode != null).ToListAsync(ct);
        var commandIds = lights.Select(x => x.LastCommandId).Where(x => x != null).ToList();
        var commands = await db.Commands.AsNoTracking().Where(x => x.HouseId == houseId && (x.Active || commandIds.Contains(x.CommandId))).ToListAsync(ct);
        return new(houseId, reported.Value, lights.Select(light => new StoredLight(light.LightId, light.LightOn!.Value, light.LightingMode!, light.StateRevision, light.AppliedAtUtc!.Value, light.StateReportedAtUtc, commands.Where(x => x.CommandId == light.LastCommandId).Select(Map).FirstOrDefault(), commands.Where(x => x.LightId == light.LightId && x.Active).Select(Map).FirstOrDefault())).ToList());
    }
    public async Task<StoredCommand> AdmitAsync(UserContext user, Guid houseId, string lightId, string type, string payloadJson, string requestHash, string key, Guid correlationId, DateTimeOffset requestedAt, DateTimeOffset expiresAt, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var prior = await db.Commands.SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct);
                if (prior is not null)
                {
                    if (prior.RequestHash != requestHash) throw new HomeException("duplicateChanged", "The idempotency key was already used with another request.");
                    await transaction.CommitAsync(ct); return Map(prior);
                }
                if (await db.ExpiredCommandKeys.AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct))
                    throw new HomeException("requestExpired", "The request expired before admission.");
                var light = await db.Lights.SingleOrDefaultAsync(x => x.HouseId == houseId && x.LightId == lightId, ct) ?? throw new HomeException("notFound", "Light is not configured.");
                var device = await db.Devices.SingleAsync(x => x.DeviceId == light.DeviceId, ct);
                if (!device.Enabled || device.HouseId != houseId) throw new HomeException("notFound", "Device is not available for this house.");
                if (await db.Commands.AnyAsync(x => x.HouseId == houseId && x.LightId == lightId && x.Active, ct)) throw new HomeException("pendingCommand", "Reconcile the outstanding order before submitting another.");
                var now = (clock ?? TimeProvider.System).GetUtcNow();
                if (expiresAt <= now) throw new HomeException("requestExpired", "The request expired before admission.");
                if (device.LastReportedAtUtc is null || device.LastReportedAtUtc < now.AddMinutes(-1)) throw new HomeException("deviceOffline", "No recent device report is available.");
                if (light.StateReportedAtUtc is null || light.StateReportedAtUtc < now.AddMinutes(-1)) throw new HomeException("deviceOffline", "No recent confirmed light state is available.");
                if (type == "SetLight" && light.LightingMode != "manual") throw new HomeException("manualModeRequired", "Confirm manual mode before controlling the light.");
                device.LastSequence++;
                var row = new CommandRow { CommandId = Guid.NewGuid().ToString(), HouseId = houseId, LightId = lightId, DeviceId = device.DeviceId, Type = type, PayloadJson = payloadJson, RequestHash = requestHash, IdempotencyKey = key, Sequence = device.LastSequence, UserId = user.UserId, SessionId = user.SessionId, CorrelationId = correlationId, RequestedAtUtc = requestedAt, ExpiresAtUtc = expiresAt };
                db.Commands.Add(row); await db.SaveChangesAsync(ct);
                var commitNow = (clock ?? TimeProvider.System).GetUtcNow();
                if (expiresAt <= commitNow || await db.ExpiredCommandKeys.AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct))
                    throw new HomeException("requestExpired", "The request expired before admission.");
                await transaction.CommitAsync(ct); return Map(row);
            }
            catch (Exception ex) when (IsSqlRaceOrDeadlock(ex))
            {
                var resolved = await TryResolveAdmitRaceAsync(houseId, lightId, key, requestHash, ct);
                if (resolved is not null) return resolved;
                if (attempt < 4 && IsDeadlock(ex)) { await Task.Delay(15, ct); continue; }
                throw;
            }
        }
        throw new HomeException("unavailable", "Home storage unavailable.");
    }
    public async Task<StoredCommand> AdmitCapabilityAsync(UserContext user, Guid houseId, string resourceId, string type, string normalizedPayload, string requestHash, string key, Guid correlationId, DateTimeOffset requestedAt, DateTimeOffset expiresAt, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var prior = await db.Commands.SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct);
                if (prior is not null)
                {
                    if (prior.RequestHash != requestHash) throw new HomeException("duplicateChanged", "The idempotency key was already used with another request.");
                    await transaction.CommitAsync(ct); return Map(prior);
                }
                if (await db.ExpiredCommandKeys.AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct))
                    throw new HomeException("requestExpired", "The request expired before admission.");
                var expectedKind = type switch { "SetSecurityMode" => "security", "OpenDoor" => "door", "CaptureImage" => "camera", _ => throw new HomeException("invalidPayload", "Unsupported command.") };
                var resource = await db.Resources.SingleOrDefaultAsync(x => x.HouseId == houseId && x.ResourceId == resourceId && x.Kind == expectedKind && x.Enabled, ct) ?? throw new HomeException("notFound", "Resource is not configured for this house.");
                var device = await db.Devices.SingleAsync(x => x.DeviceId == resource.DeviceId, ct);
                if (!device.Enabled || device.HouseId != houseId) throw new HomeException("notFound", "Device is not available for this house.");
                if (await db.Commands.AnyAsync(x => x.HouseId == houseId && x.LightId == resourceId && x.Active, ct)) throw new HomeException("pendingCommand", "Reconcile the outstanding order before submitting another.");
                var now = (clock ?? TimeProvider.System).GetUtcNow();
                if (expiresAt <= now) throw new HomeException("requestExpired", "The request expired before admission.");
                if (device.LastReportedAtUtc is null || device.LastReportedAtUtc < now.AddMinutes(-1)) throw new HomeException("deviceOffline", "No recent device report is available.");
                var payload = normalizedPayload;
                var commandId = Guid.NewGuid().ToString();
                if (type == "OpenDoor") payload = JsonSerializer.Serialize(new { doorId = resource.DeviceResourceId, durationMs = 1000 });
                if (type == "CaptureImage")
                {
                    var captureId = Guid.NewGuid();
                    payload = JsonSerializer.Serialize(new { captureId });
                    db.CaptureJobs.Add(new() { CaptureId = captureId, HouseId = houseId, DeviceId = device.DeviceId, CommandId = commandId, CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(5) });
                }
                device.LastSequence++;
                var row = new CommandRow { CommandId = commandId, HouseId = houseId, LightId = resourceId, DeviceId = device.DeviceId, Type = type, PayloadJson = payload, RequestHash = requestHash, IdempotencyKey = key, Sequence = device.LastSequence, UserId = user.UserId, SessionId = user.SessionId, CorrelationId = correlationId, RequestedAtUtc = requestedAt, ExpiresAtUtc = expiresAt };
                db.Commands.Add(row); await db.SaveChangesAsync(ct);
                var commitNow = (clock ?? TimeProvider.System).GetUtcNow();
                if (expiresAt <= commitNow || await db.ExpiredCommandKeys.AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct))
                    throw new HomeException("requestExpired", "The request expired before admission.");
                await transaction.CommitAsync(ct); return Map(row);
            }
            catch (Exception ex) when (IsSqlRaceOrDeadlock(ex))
            {
                var resolved = await TryResolveAdmitRaceAsync(houseId, resourceId, key, requestHash, ct);
                if (resolved is not null) return resolved;
                if (attempt < 4 && IsDeadlock(ex)) { await Task.Delay(15, ct); continue; }
                throw;
            }
        }
        throw new HomeException("unavailable", "Home storage unavailable.");
    }
    private async Task<StoredCommand?> TryResolveAdmitRaceAsync(Guid houseId, string targetId, string key, string requestHash, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var existing = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash) throw new HomeException("duplicateChanged", "The idempotency key was already used with another request.");
            return Map(existing);
        }
        if (await db.ExpiredCommandKeys.AsNoTracking().AnyAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct))
            throw new HomeException("requestExpired", "The request expired before admission.");
        if (await db.Commands.AsNoTracking().AnyAsync(x => x.HouseId == houseId && x.LightId == targetId && x.Active, ct))
            throw new HomeException("pendingCommand", "Reconcile the outstanding order before submitting another.");
        return null;
    }
    private static bool IsSqlRaceOrDeadlock(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is DbUpdateException or SqlException) return true;
        }
        return false;
    }
    private static bool IsDeadlock(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is SqlException { Number: 1205 }) return true;
        }
        return false;
    }
}
