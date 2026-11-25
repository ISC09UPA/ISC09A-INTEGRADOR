namespace CasaDomotica.Identity.Domain.Interfaces;

public sealed record HouseMemberSummary(Guid UserId, string Email, string Role, bool Enabled, bool EmailVerified);

public sealed record InvitationSummary(
    Guid Id,
    Guid HouseId,
    string Email,
    string Role,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record AcceptInvitationOutcome(
    bool Accepted,
    string ErrorCode,
    string ErrorMessage,
    int StatusCode,
    Guid? HouseId = null,
    Guid? UserId = null,
    bool CreatedNewUser = false);

public sealed record PasswordResetRecord(
    Guid UserId,
    string Email,
    string PasswordHash,
    bool Enabled,
    bool EmailVerified,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? UsedAtUtc);

public interface IAccountEmailSender
{
    Task SendInvitationAsync(string recipientEmail, string houseName, string token, DateTimeOffset expiresAtUtc, CancellationToken ct);
    Task SendPasswordResetAsync(string recipientEmail, string token, DateTimeOffset expiresAtUtc, CancellationToken ct);
}

public interface IAccountStore
{
    Task<string?> GetUserHouseRoleAsync(Guid userId, Guid houseId, CancellationToken ct);
    Task<(IReadOnlyList<HouseMemberSummary> Members, IReadOnlyList<InvitationSummary> Invitations)> ListHouseMembersAsync(Guid houseId, CancellationToken ct);
    Task<HouseMemberSummary?> GetHouseMemberAsync(Guid houseId, Guid memberUserId, CancellationToken ct);
    Task<string?> GetHouseNameAsync(Guid houseId, CancellationToken ct);
    Task<InvitationSummary> CreateInvitationAsync(
        Guid houseId,
        string email,
        string normalizedEmail,
        string tokenHash,
        Guid createdByUserId,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct);
    Task<bool> RevokeInvitationAsync(Guid houseId, Guid invitationId, DateTimeOffset now, CancellationToken ct);
    Task<(bool Removed, string? ErrorCode, string? ErrorMessage, int StatusCode)> RemoveResidentMemberAsync(
        Guid houseId,
        Guid memberUserId,
        DateTimeOffset now,
        CancellationToken ct);
    Task<AcceptInvitationOutcome> AcceptInvitationAsync(
        string tokenHash,
        string normalizedEmail,
        string? password,
        Func<string, string, bool> verifyPassword,
        Func<string, string> hashPassword,
        DateTimeOffset now,
        CancellationToken ct);
    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPasswordHash,
        Func<string, string, bool> verifyPassword,
        DateTimeOffset now,
        CancellationToken ct);
    Task CreatePasswordResetTokenAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct);
    Task<(bool Confirmed, string? ErrorCode, string? ErrorMessage, int StatusCode)> ConfirmPasswordResetAsync(
        string tokenHash,
        string normalizedEmail,
        string newPasswordHash,
        DateTimeOffset now,
        CancellationToken ct);
}