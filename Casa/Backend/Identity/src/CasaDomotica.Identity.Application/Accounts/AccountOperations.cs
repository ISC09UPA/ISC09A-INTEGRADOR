using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using CasaDomotica.Identity.Domain.Interfaces;

namespace CasaDomotica.Identity.Application.Accounts;

public sealed record HouseMembersView(IReadOnlyList<HouseMemberSummary> Members, IReadOnlyList<InvitationSummary> Invitations);
public sealed record CreatedInvitationView(InvitationSummary Invitation, string InvitationToken);
public sealed record AcceptedInvitationView(Guid HouseId, Guid UserId, string Role, bool CreatedNewUser);
public sealed record AccountError(string Code, string Message, int StatusCode);

public sealed class AccountOperations(
    IAccountStore store,
    IUserRepository users,
    IPasswordHasher passwords,
    IDateTimeProvider clock,
    IAccountEmailSender emailSender)
{
    private readonly string _dummyHash = passwords.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    public static bool TryNormalizeEmail(string? raw, out string trimmed, out string normalized)
    {
        trimmed = (raw ?? string.Empty).Trim();
        normalized = trimmed.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 254) return false;
        return MailAddress.TryCreate(trimmed, out var address) && address.Address == trimmed;
    }

    public static bool IsValidPassword(string? password) =>
        password is not null && password.Length is >= 14 and <= 128;

    public static string HashOpaqueToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw.Trim())));

    private static string GenerateOpaqueToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task<(HouseMembersView? Value, AccountError? Error)> ListMembersAsync(Guid actorUserId, Guid houseId, CancellationToken ct)
    {
        if (!await IsAdminAsync(actorUserId, houseId, ct))
            return (null, new("forbidden", "Administrative permission is required for house members.", 403));

        var (members, invitations) = await store.ListHouseMembersAsync(houseId, ct);
        return (new(members, invitations), null);
    }

    public async Task<(HouseMemberSummary? Value, AccountError? Error)> GetMemberAsync(Guid actorUserId, Guid houseId, Guid memberUserId, CancellationToken ct)
    {
        if (!await IsAdminAsync(actorUserId, houseId, ct))
            return (null, new("forbidden", "Administrative permission is required for house members.", 403));

        var member = await store.GetHouseMemberAsync(houseId, memberUserId, ct);
        return member is null
            ? (null, new("notFound", "Member not found in this house.", 404))
            : (member, null);
    }

    public async Task<(CreatedInvitationView? Value, AccountError? Error)> CreateInvitationAsync(
        Guid actorUserId,
        Guid houseId,
        string? rawEmail,
        CancellationToken ct)
    {
        if (!await IsAdminAsync(actorUserId, houseId, ct))
            return (null, new("forbidden", "Administrative permission is required to invite residents.", 403));

        if (!TryNormalizeEmail(rawEmail, out var email, out var normalized))
            return (null, new("badRequest", "Provide a valid email address.", 400));

        var houseName = await store.GetHouseNameAsync(houseId, ct);
        if (houseName is null)
            return (null, new("notFound", "House not found.", 404));

        var now = clock.UtcNow;
        var expiresAt = now.AddHours(72);
        var token = GenerateOpaqueToken();
        var invitation = await store.CreateInvitationAsync(
            houseId,
            email,
            normalized,
            HashOpaqueToken(token),
            actorUserId,
            now,
            expiresAt,
            ct);

        await emailSender.SendInvitationAsync(email, houseName, token, expiresAt, ct);
        return (new(invitation, token), null);
    }

    public async Task<AccountError?> RevokeInvitationAsync(Guid actorUserId, Guid houseId, Guid invitationId, CancellationToken ct)
    {
        if (!await IsAdminAsync(actorUserId, houseId, ct))
            return new("forbidden", "Administrative permission is required to revoke invitations.", 403);

        var revoked = await store.RevokeInvitationAsync(houseId, invitationId, clock.UtcNow, ct);
        return revoked ? null : new("notFound", "Pending invitation not found.", 404);
    }

    public async Task<AccountError?> RemoveResidentAsync(Guid actorUserId, Guid houseId, Guid memberUserId, CancellationToken ct)
    {
        if (!await IsAdminAsync(actorUserId, houseId, ct))
            return new("forbidden", "Administrative permission is required to remove members.", 403);

        if (actorUserId == memberUserId)
            return new("conflict", "An administrator cannot remove their own membership.", 409);

        var (removed, code, message, status) = await store.RemoveResidentMemberAsync(houseId, memberUserId, clock.UtcNow, ct);
        return removed ? null : new(code ?? "conflict", message ?? "Could not remove member.", status);
    }

    public async Task<(AcceptedInvitationView? Value, AccountError? Error)> AcceptInvitationAsync(
        string? rawToken,
        string? rawEmail,
        string? password,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Trim().Length is < 16 or > 256)
            return (null, new("badRequest", "Invalid invitation token.", 400));

        if (!TryNormalizeEmail(rawEmail, out _, out var normalizedEmail))
            return (null, new("badRequest", "Provide a valid email address.", 400));

        var existing = await users.FindByEmailAsync(normalizedEmail, ct);
        if (existing is null)
        {
            if (!IsValidPassword(password))
                return (null, new("badRequest", "Use a password between 14 and 128 characters.", 400));
        }
        else if (string.IsNullOrEmpty(password))
        {
            return (null, new("invalidCredential", "Existing accounts must confirm their current password.", 401));
        }

        var outcome = await store.AcceptInvitationAsync(
            HashOpaqueToken(rawToken),
            normalizedEmail,
            password,
            (hash, plain) => passwords.Verify(hash, plain),
            passwords.Hash,
            clock.UtcNow,
            ct);

        if (!outcome.Accepted || outcome.HouseId is null || outcome.UserId is null)
            return (null, new(outcome.ErrorCode, outcome.ErrorMessage, outcome.StatusCode));

        return (new(outcome.HouseId.Value, outcome.UserId.Value, "resident", outcome.CreatedNewUser), null);
    }

    public async Task<AccountError?> ChangePasswordAsync(
        Guid actorUserId,
        string? currentPassword,
        string? newPassword,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(currentPassword) || !IsValidPassword(newPassword))
            return new("badRequest", "Use a new password between 14 and 128 characters.", 400);

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            return new("badRequest", "New password must differ from current password.", 400);

        var updated = await store.ChangePasswordAsync(
            actorUserId,
            currentPassword,
            passwords.Hash(newPassword!),
            (hash, plain) => passwords.Verify(hash, plain),
            clock.UtcNow,
            ct);

        return updated ? null : new("invalidCredential", "Current password is invalid.", 401);
    }

    public async Task<AccountError?> RequestPasswordResetAsync(string? rawEmail, CancellationToken ct)
    {
        if (!TryNormalizeEmail(rawEmail, out var email, out var normalized))
            return new("badRequest", "Provide a valid email address.", 400);

        var user = await users.FindByEmailAsync(normalized, ct);
        if (user is null || !user.Enabled || !user.EmailVerified)
        {
            // Equal-cost verification work to avoid timing differences that reveal account existence.
            _ = passwords.Verify(_dummyHash, "anti-enumeration-probe");
            return null;
        }

        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(15);
        var token = GenerateOpaqueToken();
        await store.CreatePasswordResetTokenAsync(user.Id, HashOpaqueToken(token), now, expiresAt, ct);
        await emailSender.SendPasswordResetAsync(email, token, expiresAt, ct);
        return null;
    }

    public async Task<AccountError?> ConfirmPasswordResetAsync(
        string? rawToken,
        string? rawEmail,
        string? newPassword,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Trim().Length is < 16 or > 256)
            return new("badRequest", "Invalid reset token.", 400);

        if (!TryNormalizeEmail(rawEmail, out _, out var normalizedEmail))
            return new("badRequest", "Provide a valid email address.", 400);

        if (!IsValidPassword(newPassword))
            return new("badRequest", "Use a new password between 14 and 128 characters.", 400);

        var (confirmed, code, message, status) = await store.ConfirmPasswordResetAsync(
            HashOpaqueToken(rawToken),
            normalizedEmail,
            passwords.Hash(newPassword!),
            clock.UtcNow,
            ct);

        return confirmed ? null : new(code ?? "requestExpired", message ?? "Reset token is invalid or expired.", status);
    }

    private async Task<bool> IsAdminAsync(Guid userId, Guid houseId, CancellationToken ct)
    {
        var role = await store.GetUserHouseRoleAsync(userId, houseId, ct);
        return string.Equals(role, "admin", StringComparison.Ordinal);
    }
}