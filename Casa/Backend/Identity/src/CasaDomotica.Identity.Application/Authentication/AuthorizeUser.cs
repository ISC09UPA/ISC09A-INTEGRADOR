using CasaDomotica.Identity.Domain.Interfaces;
namespace CasaDomotica.Identity.Application.Authentication;
public enum AuthorizationOutcome { Allowed, Denied, SessionRevoked }
public sealed class AuthorizeUser(IUserRepository users, ISessionRepository sessions, IDateTimeProvider clock)
{
    // A role is always resolved from the current membership, never from a client-provided claim.
    private static readonly HashSet<string> MemberActions = ["home:read", "home:control:lights", "security:read", "notifications:read", "assistant:read"];
    private static readonly HashSet<string> AdminActions = ["home:control:security", "home:control:door", "home:control:capture", "security:manage", "notifications:manage", "identity:manage:members"];
    public async Task<AuthorizationOutcome> EvaluateAsync(Guid userId, Guid sessionId, Guid houseId, string action, CancellationToken ct)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty || houseId == Guid.Empty || action is null || !MemberActions.Contains(action) && !AdminActions.Contains(action)) return AuthorizationOutcome.Denied;
        if (!await sessions.IsActiveAsync(sessionId, userId, clock.UtcNow, ct)) return AuthorizationOutcome.SessionRevoked;
        var user = await users.FindByIdAsync(userId, ct);
        return user is { Enabled: true } && user.Memberships.Any(x => x.HouseId == houseId && (x.Role == "admin" && (MemberActions.Contains(action) || AdminActions.Contains(action)) || x.Role == "resident" && MemberActions.Contains(action))) ? AuthorizationOutcome.Allowed : AuthorizationOutcome.Denied;
    }
    public async Task<bool> ExecuteAsync(Guid userId, Guid sessionId, Guid houseId, string action, CancellationToken ct) => await EvaluateAsync(userId, sessionId, houseId, action, ct) == AuthorizationOutcome.Allowed;
}
