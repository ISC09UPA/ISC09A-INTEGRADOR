using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaDomotica.Security.Domain;

namespace CasaDomotica.Security.Application;

public interface ISecurityEventStore
{
    // Insert inbox, projection and resulting outbox in one transaction.
    Task StoreAsync(HouseEvent message, ProjectionChange change, string bodyHash, CancellationToken ct);
}

public sealed class SecurityEvents(ISecurityEventStore store)
{
    public async Task IngestAsync(string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body) || Encoding.UTF8.GetByteCount(body) > 128 * 1024) throw new InvalidDataException("Invalid event size.");
        HouseEvent message;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Invalid event schema.");
            string[] allowed = ["schemaVersion", "type", "eventId", "correlationId", "houseId", "sourceDeviceId", "occurredAtUtc", "receivedAtUtc", "isSecurityController", "payload"];
            var properties = root.EnumerateObject().ToArray();
            if (properties.Any(x => !allowed.Contains(x.Name)) || properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length ||
                root.TryGetProperty("isSecurityController", out var controller) && controller.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new InvalidDataException("Invalid event properties.");
            message = new HouseEvent(root.GetProperty("eventId").GetGuid(), root.GetProperty("correlationId").GetGuid(),
                root.GetProperty("houseId").GetGuid(), root.GetProperty("sourceDeviceId").GetString()!, root.GetProperty("type").GetString()!,
                root.GetProperty("occurredAtUtc").GetDateTimeOffset(), root.GetProperty("receivedAtUtc").GetDateTimeOffset(), root.GetProperty("payload").Clone(),
                root.TryGetProperty("isSecurityController", out var isController) && isController.ValueKind == JsonValueKind.True);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Invalid security event.", e);
        }
        var change = SecurityProjection.Apply(message);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        await store.StoreAsync(message, change, hash, ct);
    }
}
