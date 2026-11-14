using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
namespace CasaDomotica.Home.Application.Commands;
public sealed class HomeOperations(IHomeRuntime store, IUserAuthorizer authorizer, TimeProvider? clock = null)
{
    public async Task<StoredStatus?> StatusAsync(UserContext user, Guid houseId, CancellationToken ct)
    {
        await Authorize(user, houseId, "home:read", Guid.NewGuid(), ct);
        return await store.StatusAsync(houseId, ct);
    }
    public async Task<StoredCommand> SendAsync(UserContext user, Guid houseId, string lightId, string type, JsonElement payload, string key, Guid correlationId, CancellationToken ct)
    {
        var requested = (clock ?? TimeProvider.System).GetUtcNow();
        if (houseId == Guid.Empty || correlationId == Guid.Empty || !Guid.TryParse(key, out var id) || id == Guid.Empty || string.IsNullOrWhiteSpace(lightId) || lightId.Length > 128 || payload.ValueKind != JsonValueKind.Object || payload.EnumerateObject().Count() != 1) throw new HomeException("invalidPayload", "Invalid command request.");
        string normalized;
        if (type == "SetLight" && payload.TryGetProperty("on", out var on) && on.ValueKind is JsonValueKind.True or JsonValueKind.False) normalized = JsonSerializer.Serialize(new { on = on.GetBoolean() });
        else if (type == "SetLightingMode" && payload.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String && mode.GetString() is "auto" or "manual") normalized = JsonSerializer.Serialize(new { mode = mode.GetString() });
        else throw new HomeException("invalidPayload", "Unsupported lighting command.");
        await Authorize(user, houseId, "home:control:lights", correlationId, ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{lightId}\n{type}\n{normalized}")));
        return await store.AdmitAsync(user, houseId, lightId, type, normalized, hash, key, correlationId, requested, requested.AddSeconds(30), ct);
    }
    public async Task<StoredCommand?> CommandAsync(UserContext user, string id, CancellationToken ct)
    {
        var command = await store.FindAsync(id, ct);
        if (command is null) return null;
        await Authorize(user, command.HouseId, "home:read", Guid.NewGuid(), ct); return command;
    }
    public async Task<StoredCommand?> ByKeyAsync(UserContext user, Guid houseId, string key, CancellationToken ct)
    {
        await Authorize(user, houseId, "home:read", Guid.NewGuid(), ct); return await store.FindByKeyAsync(houseId, key, ct);
    }
    private async Task Authorize(UserContext user, Guid houseId, string action, Guid correlation, CancellationToken ct)
    {
        if (!await authorizer.CheckAsync(user, houseId, action, correlation, ct)) throw new HomeException("forbidden", "The user is not authorized for this house.");
    }
}
