namespace CasaDomotica.Home.Domain.Interfaces;
public sealed record UserContext(Guid UserId, Guid SessionId);
public sealed class HomeException(string code, string message) : Exception(message) { public string Code { get; } = code; }
public sealed record StoredCommand(string CommandId, Guid HouseId, string LightId, string DeviceId, string Type, string PayloadJson, string RequestHash, string IdempotencyKey, long Sequence, DateTimeOffset RequestedAtUtc, DateTimeOffset ExpiresAtUtc, Guid CorrelationId, string Status, string? ResultJson);
public sealed record StoredLight(string LightId, bool LightOn, string LightingMode, long StateRevision, DateTimeOffset AppliedAtUtc, DateTimeOffset? StateReportedAtUtc, StoredCommand? LastCommand, StoredCommand? PendingCommand);
public sealed record StoredStatus(Guid HouseId, DateTimeOffset UpdatedAtUtc, IReadOnlyList<StoredLight> Lights);
public interface IUserAuthorizer
{
    Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct);
}
public interface IHomeRuntime
{
    Task<StoredStatus?> StatusAsync(Guid houseId, CancellationToken ct);
    Task<StoredCommand?> FindAsync(string commandId, CancellationToken ct);
    Task<StoredCommand?> FindByKeyAsync(Guid houseId, string key, CancellationToken ct);
    Task<StoredCommand> AdmitAsync(UserContext user, Guid houseId, string lightId, string type, string payloadJson, string requestHash, string key, Guid correlationId, DateTimeOffset requestedAt, DateTimeOffset expiresAt, CancellationToken ct);
}
public interface IDeviceDispatcher { Task<bool> DispatchNextAsync(CancellationToken ct); }
public interface ICapabilityRuntime
{
    Task<StoredCommand> AdmitCapabilityAsync(UserContext user, Guid houseId, string resourceId, string type, string normalizedPayload, string requestHash, string key, Guid correlationId, DateTimeOffset requestedAt, DateTimeOffset expiresAt, CancellationToken ct);
}

public interface IStateNotifier { Task NotifyAsync(Guid houseId, CancellationToken ct); }
