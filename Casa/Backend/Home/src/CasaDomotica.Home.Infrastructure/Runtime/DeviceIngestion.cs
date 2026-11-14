using System.Data;
using System.Text.Json;
using CasaDomotica.Home.Application.Commands;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace CasaDomotica.Home.Infrastructure.Runtime;
public sealed class DeviceIngestion(HomeDb db, CaptureVerifier captures)
{
    public async Task IngestAsync(string authenticatedDeviceId, JsonElement envelope, CancellationToken ct)
    {
        var message = DeviceEvents.Parse(envelope);
        var captureETag = message.Type == "CaptureUploaded" ? await captures.VerifyAsync(authenticatedDeviceId, message.Payload, message.OccurredAtUtc, ct) : null;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var device = await db.Devices.SingleOrDefaultAsync(x => x.DeviceId == authenticatedDeviceId && x.Enabled, ct) ?? throw new HomeException("unknownDevice", "Device is not associated with a house.");
        var prior = await db.Inbox.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == message.EventId, ct);
        if (prior is not null)
        {
            if (prior.DeviceId != authenticatedDeviceId) throw new HomeException("invalidEvent", "Event id belongs to another device.");
            await transaction.CommitAsync(ct); return;
        }
        var received = DateTimeOffset.UtcNow;
        // A device clock may be slightly ahead; it must never extend freshness beyond server receipt.
        var observed = message.OccurredAtUtc < received ? message.OccurredAtUtc : received;
        db.Inbox.Add(new() { EventId = message.EventId, DeviceId = authenticatedDeviceId, ReceivedAtUtc = received });
        if (device.LastReportedAtUtc is null || observed > device.LastReportedAtUtc) device.LastReportedAtUtc = observed;
        string businessType;
        if (message.Type == "DeviceStateReported")
        {
            var lightId = message.Payload.GetProperty("lightId").GetString();
            var light = await db.Lights.SingleOrDefaultAsync(x => x.DeviceId == device.DeviceId && x.HouseId == device.HouseId && x.LightId == lightId, ct) ?? throw new HomeException("invalidEvent", "Light does not belong to the reporting device.");
            var snapshot = message.Payload.GetProperty("snapshot"); var revision = message.Payload.GetProperty("stateRevision").GetInt64();
            LightStateProjection.Apply(light, snapshot, revision, observed);
            // Keep the authenticated mode report associated with its actual device for security projection.
            db.Telemetry.Add(new() { EventId = message.EventId, HouseId = device.HouseId, SensorId = device.DeviceId, Type = message.Type, PayloadJson = message.Payload.GetRawText(), OccurredAtUtc = message.OccurredAtUtc, ReceivedAtUtc = received });
            businessType = "DeviceStateChanged";
        }
        else if (message.Type == "CommandResult")
        {
            var id = message.Payload.GetProperty("commandId").GetString();
            var command = await db.Commands.SingleOrDefaultAsync(x => x.CommandId == id && x.DeviceId == device.DeviceId && x.HouseId == device.HouseId, ct) ?? throw new HomeException("invalidEvent", "Result does not belong to a known device order.");
            if (command.Type != message.Payload.GetProperty("type").GetString()) throw new HomeException("invalidEvent", "Command type does not match.");
            var next = message.Payload.GetProperty("status").GetString()!;
            if (command.Status is "pending" or "unknown")
            {
                command.Status = next; command.Active = next == "unknown"; command.ResultJson = message.Payload.GetRawText();
                if (next == "executed" && command.Type is ("SetLight" or "SetLightingMode"))
                {
                    var snapshot = message.Payload.GetProperty("snapshot");
                    var revision = message.Payload.GetProperty("stateRevision").GetInt64();
                    var light = await db.Lights.SingleAsync(x => x.HouseId == command.HouseId && x.LightId == command.LightId, ct);
                    LightStateProjection.Apply(light, snapshot, revision, observed, command.CommandId);
                }
            }
            businessType = "DeviceStateChanged";
        }
        else
        {
            if (message.Type is "MotionDetected" or "AccessRequested")
            {
                var room = message.Payload.GetProperty("roomId").GetString();
                if (!await db.Lights.AnyAsync(x => x.DeviceId == device.DeviceId && x.HouseId == device.HouseId && x.LightId == room, ct)) throw new HomeException("invalidEvent", "Room does not belong to the configured device.");
            }
            db.Telemetry.Add(new() { EventId = message.EventId, HouseId = device.HouseId, SensorId = device.DeviceId, Type = message.Type, PayloadJson = message.Payload.GetRawText(), OccurredAtUtc = message.OccurredAtUtc, ReceivedAtUtc = received });
            businessType = message.Type == "CaptureUploaded" ? "CaptureAvailable" : message.Type;
        }
        // Only a uniquely configured security controller can update the effective
        // mode in the Security projection. Ambiguous configuration fails closed.
        var controllers = await db.Resources.AsNoTracking().Where(x => x.HouseId == device.HouseId && x.Enabled && x.Kind == "security").Select(x => x.DeviceId).Take(2).ToListAsync(ct);
        var isSecurityController = controllers.Count == 1 && controllers[0] == device.DeviceId;
        object outgoingPayload = captureETag is null ? message.Payload : new { captureId = message.Payload.GetProperty("captureId").GetGuid(), blobName = message.Payload.GetProperty("blobName").GetString(), blobETag = captureETag };
        var body = JsonSerializer.Serialize(new { schemaVersion = 1, type = businessType, eventId = message.EventId, correlationId = message.CorrelationId, houseId = device.HouseId, sourceDeviceId = device.DeviceId, occurredAtUtc = message.OccurredAtUtc, receivedAtUtc = received, isSecurityController, payload = outgoingPayload });
        db.Outbox.Add(new() { EventId = message.EventId, Type = businessType, BodyJson = body, CreatedAtUtc = received });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
}
