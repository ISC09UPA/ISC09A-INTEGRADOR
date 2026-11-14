using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;

namespace CasaDomotica.Home.Application.Commands;
public sealed class CapabilityOperations(ICapabilityRuntime store, IUserAuthorizer authorizer, TimeProvider clock)
{
    public async Task<StoredCommand> SendAsync(UserContext user, Guid houseId, string resourceId, string type, JsonElement payload, string key, Guid correlationId, DateTimeOffset clientExpiresAtUtc, CancellationToken ct)
    {
        var requested = clock.GetUtcNow();
        if (houseId == Guid.Empty || correlationId == Guid.Empty || !Guid.TryParse(key, out var parsedKey) || parsedKey == Guid.Empty || string.IsNullOrWhiteSpace(resourceId) || resourceId.Length > 128 || payload.ValueKind != JsonValueKind.Object) throw new HomeException("invalidPayload", "Invalid command request.");
        var props = payload.EnumerateObject().ToArray();
        if (props.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != props.Length) throw new HomeException("invalidPayload", "Duplicate command fields are not allowed.");
        string action, normalized;
        switch (type)
        {
            case "SetSecurityMode" when props.Length == 1 && payload.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String && mode.GetString() is "armed" or "disarmed":
                action = "home:control:security"; normalized = JsonSerializer.Serialize(new { mode = mode.GetString() }); break;
            case "OpenDoor" when props.Length == 0:
                action = "home:control:door"; normalized = "{}"; break;
            case "CaptureImage" when props.Length == 0:
                action = "home:control:capture"; normalized = "{}"; break;
            default: throw new HomeException("invalidPayload", "Unsupported command or payload.");
        }
        var maximumDeadline = requested.AddSeconds(type == "OpenDoor" ? 10 : 30);
        if (clientExpiresAtUtc.Offset != TimeSpan.Zero || clientExpiresAtUtc > maximumDeadline.AddSeconds(30)) throw new HomeException("invalidPayload", "The client request deadline is invalid.");
        var expires = clientExpiresAtUtc < maximumDeadline ? clientExpiresAtUtc : maximumDeadline;
        if (!await authorizer.CheckAsync(user, houseId, action, correlationId, ct)) throw new HomeException("forbidden", "The user is not authorized for this house.");
        // The store checks the deadline inside its serializable transaction, after looking up
        // the idempotency key. This lets a late duplicate recover an existing order safely.
        var canonical = JsonSerializer.Serialize(new { resourceId, type, payload = normalized });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return await store.AdmitCapabilityAsync(user, houseId, resourceId, type, normalized, hash, parsedKey.ToString("D"), correlationId, requested, expires, ct);
    }
}
