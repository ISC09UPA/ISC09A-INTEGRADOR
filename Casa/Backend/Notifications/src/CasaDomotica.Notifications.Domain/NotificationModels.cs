using System.Text;
using System.Text.Json;

namespace CasaDomotica.Notifications.Domain;

public static class DeliveryStatuses
{
    public const string Requested = "requested";
    public const string Accepted = "accepted";
    public const string Failed = "failed";
    public const string Unknown = "unknown";
    public const string Delivered = "delivered";

    public static bool IsValid(string? status) =>
        status is Requested or Accepted or Failed or Unknown or Delivered;

    /// <summary>
    /// Only newly created jobs in "requested" state may initiate a provider send.
    /// Ambiguous ("unknown") or already-admitted ("accepted"/"delivered") sends
    /// must never be retried automatically to avoid duplicate emails.
    /// </summary>
    public static bool CanAttemptSend(string status) => status == Requested;
}

public sealed record NotificationEvent(
    Guid EventId,
    Guid CorrelationId,
    Guid HouseId,
    string Type,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset ReceivedAtUtc,
    string Subject,
    string BodyText);

public static class NotificationProjection
{
    private static readonly HashSet<string> AllowedEnvelopeFields = new(StringComparer.Ordinal)
    {
        "schemaVersion", "type", "eventId", "correlationId", "houseId",
        "sourceDeviceId", "occurredAtUtc", "receivedAtUtc", "isSecurityController", "payload"
    };

    public static NotificationEvent Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || Encoding.UTF8.GetByteCount(body) > 128 * 1024)
            throw new InvalidDataException("Invalid notification event size.");

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Invalid notification event object.");

            var properties = root.EnumerateObject().ToArray();
            if (properties.Any(x => !AllowedEnvelopeFields.Contains(x.Name)) ||
                properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                throw new InvalidDataException("Unexpected notification event properties.");

            if (root.GetProperty("schemaVersion").GetInt32() != 1)
                throw new InvalidDataException("Unsupported notification event version.");

            var eventId = root.GetProperty("eventId").GetGuid();
            var correlationId = root.GetProperty("correlationId").GetGuid();
            var houseId = root.GetProperty("houseId").GetGuid();
            var type = RequiredText(root, "type", 64);
            var occurred = root.GetProperty("occurredAtUtc").GetDateTimeOffset();
            var received = root.GetProperty("receivedAtUtc").GetDateTimeOffset();
            var payload = root.GetProperty("payload");

            if (eventId == Guid.Empty || correlationId == Guid.Empty || houseId == Guid.Empty ||
                occurred.Offset != TimeSpan.Zero || received.Offset != TimeSpan.Zero ||
                occurred > received.AddSeconds(30) || payload.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Invalid notification event envelope metadata.");

            if (root.TryGetProperty("sourceDeviceId", out var dev) &&
                dev.ValueKind == JsonValueKind.String &&
                dev.GetString() is { Length: > 128 })
                throw new InvalidDataException("Invalid source device identifier.");

            return type switch
            {
                "SecurityIncidentCreated" => ParseSecurityIncident(eventId, correlationId, houseId, occurred, received, payload),
                "AccessRecorded" => ParseAccessRecorded(eventId, correlationId, houseId, occurred, received, payload),
                "HouseMembershipChanged" => new NotificationEvent(
                    eventId,
                    correlationId,
                    houseId,
                    type,
                    occurred,
                    received,
                    "Cambio de membresía en tu casa",
                    $"Se registró un cambio en la membresía de la casa el {occurred:yyyy-MM-dd HH:mm:ss} UTC."),
                _ => throw new InvalidDataException("Unsupported notification event type.")
            };
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Invalid notification event payload.", error);
        }
    }

    private static NotificationEvent ParseSecurityIncident(
        Guid eventId,
        Guid correlationId,
        Guid houseId,
        DateTimeOffset occurred,
        DateTimeOffset received,
        JsonElement payload)
    {
        var incidentId = payload.GetProperty("incidentId").GetGuid();
        if (incidentId == Guid.Empty)
            throw new InvalidDataException("Invalid incident identifier.");

        var roomId = RequiredText(payload, "roomId", 128);
        return new NotificationEvent(
            eventId,
            correlationId,
            houseId,
            "SecurityIncidentCreated",
            occurred,
            received,
            $"Alerta de seguridad en {roomId}",
            $"Se detectó movimiento con seguridad armada en \"{roomId}\" el {occurred:yyyy-MM-dd HH:mm:ss} UTC (incidente {incidentId:D}).");
    }

    private static NotificationEvent ParseAccessRecorded(
        Guid eventId,
        Guid correlationId,
        Guid houseId,
        DateTimeOffset occurred,
        DateTimeOffset received,
        JsonElement payload)
    {
        var activityId = payload.GetProperty("activityId").GetGuid();
        if (activityId == Guid.Empty)
            throw new InvalidDataException("Invalid activity identifier.");

        var result = RequiredText(payload, "result", 32);
        var room = payload.TryGetProperty("roomId", out var roomElement) && roomElement.ValueKind == JsonValueKind.String
            ? roomElement.GetString()
            : null;
        if (room is { Length: > 128 })
            throw new InvalidDataException("Invalid room identifier.");

        var locationText = string.IsNullOrWhiteSpace(room) ? "puerta principal" : room;
        return new NotificationEvent(
            eventId,
            correlationId,
            houseId,
            "AccessRecorded",
            occurred,
            received,
            $"Actividad de acceso ({result})",
            $"Se registró una operación de acceso ({result}) en \"{locationText}\" el {occurred:yyyy-MM-dd HH:mm:ss} UTC (actividad {activityId:D}).");
    }

    private static string RequiredText(JsonElement obj, string property, int maxLength)
    {
        if (!obj.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Missing string property '{property}'.");
        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength)
            throw new InvalidDataException($"Invalid length for property '{property}'.");
        return text;
    }
}
