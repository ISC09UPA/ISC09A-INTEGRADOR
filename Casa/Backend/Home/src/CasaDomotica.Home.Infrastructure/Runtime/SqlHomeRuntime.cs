using System.Data;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace CasaDomotica.Home.Infrastructure.Runtime;
public sealed class SqlHomeRuntime(HomeDb db) : IHomeRuntime, ICapabilityRuntime
{
    public static StoredCommand Map(CommandRow row) => new(row.CommandId, row.HouseId, row.LightId, row.DeviceId, row.Type, row.PayloadJson, row.RequestHash, row.IdempotencyKey, row.Sequence, row.RequestedAtUtc, row.ExpiresAtUtc, row.CorrelationId, row.Status, row.ResultJson);
    public async Task<StoredCommand?> FindAsync(string commandId, CancellationToken ct) { var row = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.CommandId == commandId, ct); return row is null ? null : Map(row); }
    public async Task<StoredCommand?> FindByKeyAsync(Guid houseId, string key, CancellationToken ct) { var row = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct); return row is null ? null : Map(row); }
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
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await db.Commands.SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct);
        if (prior is not null)
        {
            if (prior.RequestHash != requestHash) throw new HomeException("duplicateChanged", "The idempotency key was already used with another request.");
            await transaction.CommitAsync(ct); return Map(prior);
        }
        var light = await db.Lights.SingleOrDefaultAsync(x => x.HouseId == houseId && x.LightId == lightId, ct) ?? throw new HomeException("notFound", "Light is not configured.");
        var device = await db.Devices.SingleAsync(x => x.DeviceId == light.DeviceId, ct);
        if (!device.Enabled || device.HouseId != houseId) throw new HomeException("notFound", "Device is not available for this house.");
        if (await db.Commands.AnyAsync(x => x.HouseId == houseId && x.LightId == lightId && x.Active, ct)) throw new HomeException("pendingCommand", "Reconcile the outstanding order before submitting another.");
        if (expiresAt <= DateTimeOffset.UtcNow) throw new HomeException("requestExpired", "The request expired before admission.");
        if (device.LastReportedAtUtc is null || device.LastReportedAtUtc < DateTimeOffset.UtcNow.AddMinutes(-1)) throw new HomeException("deviceOffline", "No recent device report is available.");
        if (light.StateReportedAtUtc is null || light.StateReportedAtUtc < DateTimeOffset.UtcNow.AddMinutes(-1)) throw new HomeException("deviceOffline", "No recent confirmed light state is available.");
        if (type == "SetLight" && light.LightingMode != "manual") throw new HomeException("manualModeRequired", "Confirm manual mode before controlling the light.");
        var now = DateTimeOffset.UtcNow;
        device.LastSequence++;
        if (expiresAt <= now) throw new HomeException("requestExpired", "The request expired before admission.");
        var row = new CommandRow { CommandId = Guid.NewGuid().ToString(), HouseId = houseId, LightId = lightId, DeviceId = device.DeviceId, Type = type, PayloadJson = payloadJson, RequestHash = requestHash, IdempotencyKey = key, Sequence = device.LastSequence, UserId = user.UserId, SessionId = user.SessionId, CorrelationId = correlationId, RequestedAtUtc = requestedAt, ExpiresAtUtc = expiresAt };
        db.Commands.Add(row); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Map(row);
    }
    public async Task<StoredCommand> AdmitCapabilityAsync(UserContext user, Guid houseId, string resourceId, string type, string normalizedPayload, string requestHash, string key, Guid correlationId, DateTimeOffset requestedAt, DateTimeOffset expiresAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await db.Commands.SingleOrDefaultAsync(x => x.HouseId == houseId && x.IdempotencyKey == key, ct);
        if (prior is not null)
        {
            if (prior.RequestHash != requestHash) throw new HomeException("duplicateChanged", "The idempotency key was already used with another request.");
            await transaction.CommitAsync(ct); return Map(prior);
        }
        var expectedKind = type switch { "SetSecurityMode" => "security", "OpenDoor" => "door", "CaptureImage" => "camera", _ => throw new HomeException("invalidPayload", "Unsupported command.") };
        var resource = await db.Resources.SingleOrDefaultAsync(x => x.HouseId == houseId && x.ResourceId == resourceId && x.Kind == expectedKind && x.Enabled, ct) ?? throw new HomeException("notFound", "Resource is not configured for this house.");
        var device = await db.Devices.SingleAsync(x => x.DeviceId == resource.DeviceId, ct);
        if (!device.Enabled || device.HouseId != houseId) throw new HomeException("notFound", "Device is not available for this house.");
        if (await db.Commands.AnyAsync(x => x.HouseId == houseId && x.LightId == resourceId && x.Active, ct)) throw new HomeException("pendingCommand", "Reconcile the outstanding order before submitting another.");
        var now = DateTimeOffset.UtcNow;
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
        db.Commands.Add(row); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Map(row);
    }
}
