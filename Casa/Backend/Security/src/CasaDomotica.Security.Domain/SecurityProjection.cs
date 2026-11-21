using System.Text.Json;

namespace CasaDomotica.Security.Domain;

// Security consumes only events that Home has authenticated and associated with a house.
// OccurredAt is preserved separately from ReceivedAt: delivery order is not event order.
public sealed record HouseEvent(Guid EventId, Guid CorrelationId, Guid HouseId, string SourceDeviceId,
    string Type, DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc, JsonElement Payload, bool IsSecurityController);

public sealed record ModeObservation(string Mode, DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc, Guid EventId);
public sealed record SecurityActivity(Guid EventId, string Type, string Result, string? RoomId,
    DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc);
public sealed record SecurityIncident(Guid EventId, string RoomId, DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc);
public sealed record SecurityCapture(Guid CaptureId, string BlobName, string BlobETag, DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc);
public sealed record ProjectionChange(ModeObservation? Mode, SecurityActivity? Activity, SecurityIncident? Incident, SecurityCapture? Capture = null);

public static class SecurityProjection
{
    public static ProjectionChange Apply(HouseEvent message)
    {
        if (message.EventId == Guid.Empty || message.CorrelationId == Guid.Empty || message.HouseId == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.SourceDeviceId) || message.SourceDeviceId.Length > 128 ||
            message.OccurredAtUtc.Offset != TimeSpan.Zero || message.ReceivedAtUtc.Offset != TimeSpan.Zero ||
            message.OccurredAtUtc > message.ReceivedAtUtc.AddSeconds(30) || message.Payload.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Invalid security event envelope.");

        switch (message.Type)
        {
            case "DeviceStateChanged":
                // CommandResult also uses DeviceStateChanged but has no security mode.
                if (message.Payload.TryGetProperty("type", out var commandType) && commandType.ValueKind == JsonValueKind.String && commandType.GetString() == "OpenDoor")
                {
                    var status = Text(Required(message.Payload, "status"), 16);
                    if (status is not ("executed" or "rejected" or "expired" or "failed" or "unknown")) throw new InvalidDataException("Invalid door command result.");
                    return new(null, new SecurityActivity(message.EventId, "access", status, null, message.OccurredAtUtc, message.ReceivedAtUtc), null);
                }
                // Only a device explicitly associated with the security resource can own this state.
                if (!message.IsSecurityController || !message.Payload.TryGetProperty("securityModeApplied", out var mode)) return new(null, null, null);
                return new(new ModeObservation(Mode(mode), message.OccurredAtUtc, message.ReceivedAtUtc, message.EventId), null, null);
            case "MotionDetected":
                var effectiveMode = Mode(Required(message.Payload, "securityModeApplied"));
                var room = Text(Required(message.Payload, "roomId"), 128);
                var activity = new SecurityActivity(message.EventId, "motion", effectiveMode, room, message.OccurredAtUtc, message.ReceivedAtUtc);
                // The device's mode at the time of motion determines the incident. A later
                // mode update must not retroactively change that decision.
                return new(null, activity, effectiveMode == "armed" ? new SecurityIncident(message.EventId, room, message.OccurredAtUtc, message.ReceivedAtUtc) : null);
            case "AccessRequested":
                var roomId = Text(Required(message.Payload, "roomId"), 128);
                var attempt = Required(message.Payload, "attemptId");
                if (attempt.ValueKind != JsonValueKind.String || !attempt.TryGetGuid(out var attemptId) || attemptId == Guid.Empty)
                    throw new InvalidDataException("Invalid access attempt.");
                return new(null, new SecurityActivity(message.EventId, "access", "requested", roomId, message.OccurredAtUtc, message.ReceivedAtUtc), null);
            case "CaptureAvailable":
                var capture = Required(message.Payload, "captureId");
                if (capture.ValueKind != JsonValueKind.String || !capture.TryGetGuid(out var captureId) || captureId == Guid.Empty) throw new InvalidDataException("Invalid capture identity.");
                var blob = Text(Required(message.Payload, "blobName"), 1024);
                var prefix = $"{message.HouseId:D}/{message.SourceDeviceId}/{captureId:D}.";
                if (!blob.StartsWith(prefix, StringComparison.Ordinal) || blob[prefix.Length..] is not ("jpg" or "png")) throw new InvalidDataException("Capture does not belong to its house and device.");
                var etag = Text(Required(message.Payload, "blobETag"), 128);
                return new(null, null, null, new SecurityCapture(captureId, blob, etag, message.OccurredAtUtc, message.ReceivedAtUtc));
            case "HouseMembershipChanged":
                return new(null, null, null);
            default:
                throw new InvalidDataException("Unsupported security event.");
        }
    }

    public static bool IsNewer(ModeObservation candidate, ModeObservation current) =>
        candidate.OccurredAtUtc > current.OccurredAtUtc || candidate.OccurredAtUtc == current.OccurredAtUtc && candidate.EventId.CompareTo(current.EventId) > 0;

    private static JsonElement Required(JsonElement payload, string name) => payload.TryGetProperty(name, out var value) ? value : throw new InvalidDataException("Missing security event property.");
    private static string Mode(JsonElement value) => value.ValueKind == JsonValueKind.String && value.GetString() is "armed" or "disarmed" ? value.GetString()! : throw new InvalidDataException("Invalid security mode.");
    private static string Text(JsonElement value, int max) => value.ValueKind == JsonValueKind.String && value.GetString() is { } text && !string.IsNullOrWhiteSpace(text) && text.Length <= max ? text : throw new InvalidDataException("Invalid security event text.");
}
