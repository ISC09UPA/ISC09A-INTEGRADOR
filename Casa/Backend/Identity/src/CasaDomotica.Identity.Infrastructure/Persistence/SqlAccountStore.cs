using System.Data;
using CasaDomotica.Identity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CasaDomotica.Identity.Infrastructure.Persistence;

public sealed class SqlAccountStore(IdentityDb db) : IAccountStore
{
    public async Task<string?> GetUserHouseRoleAsync(Guid userId, Guid houseId, CancellationToken ct) =>
        await db.Memberships.AsNoTracking()
            .Where(x => x.UserId == userId && x.HouseId == houseId)
            .Select(x => x.Role)
            .SingleOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<HouseMemberSummary> Members, IReadOnlyList<InvitationSummary> Invitations)> ListHouseMembersAsync(Guid houseId, CancellationToken ct)
    {
        var members = await (
            from membership in db.Memberships.AsNoTracking()
            join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.HouseId == houseId
            orderby membership.Role, user.Email
            select new HouseMemberSummary(user.Id, user.Email, membership.Role, user.Enabled, user.EmailVerified)
        ).ToListAsync(ct);

        var rawInvites = (await db.Invitations.AsNoTracking()
            .Where(x => x.HouseId == houseId)
            .ToListAsync(ct))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var invitations = rawInvites.Select(x => ToInvitationSummary(x, now)).ToList();
        return (members, invitations);
    }

    public async Task<HouseMemberSummary?> GetHouseMemberAsync(Guid houseId, Guid memberUserId, CancellationToken ct) =>
        await (
            from membership in db.Memberships.AsNoTracking()
            join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.HouseId == houseId && membership.UserId == memberUserId
            select new HouseMemberSummary(user.Id, user.Email, membership.Role, user.Enabled, user.EmailVerified)
        ).SingleOrDefaultAsync(ct);

    public Task<string?> GetHouseNameAsync(Guid houseId, CancellationToken ct) =>
        db.Houses.AsNoTracking().Where(x => x.Id == houseId).Select(x => x.Name).SingleOrDefaultAsync(ct);

    public async Task<InvitationSummary> CreateInvitationAsync(
        Guid houseId,
        string email,
        string normalizedEmail,
        string tokenHash,
        Guid createdByUserId,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct)
    {
        var row = new InvitationRow
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = "resident",
            TokenHash = tokenHash,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc
        };
        db.Invitations.Add(row);
        await db.SaveChangesAsync(ct);
        return ToInvitationSummary(row, now);
    }

    public async Task<bool> RevokeInvitationAsync(Guid houseId, Guid invitationId, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.Invitations.SingleOrDefaultAsync(x => x.Id == invitationId && x.HouseId == houseId, ct);
        if (row is null || row.AcceptedAtUtc is not null || row.RevokedAtUtc is not null)
            return false;

        row.RevokedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(bool Removed, string? ErrorCode, string? ErrorMessage, int StatusCode)> RemoveResidentMemberAsync(
        Guid houseId,
        Guid memberUserId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var membership = await db.Memberships.SingleOrDefaultAsync(x => x.HouseId == houseId && x.UserId == memberUserId, ct);
        if (membership is null)
        {
            await tx.CommitAsync(ct);
            return (false, "notFound", "Member not found in this house.", 404);
        }

        if (string.Equals(membership.Role, "admin", StringComparison.Ordinal))
        {
            await tx.CommitAsync(ct);
            return (false, "conflict", "Administrator memberships cannot be revoked through resident management.", 409);
        }

        db.Memberships.Remove(membership);
        await db.SaveChangesAsync(ct);

        await RevokeUserSessionsAsync(memberUserId, now, ct);
        await tx.CommitAsync(ct);
        return (true, null, null, 204);
    }

    public async Task<AcceptInvitationOutcome> AcceptInvitationAsync(
        string tokenHash,
        string normalizedEmail,
        string? password,
        Func<string, string, bool> verifyPassword,
        Func<string, string> hashPassword,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invite = await db.Invitations.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
        if (invite is null || invite.RevokedAtUtc is not null || invite.AcceptedAtUtc is not null)
        {
            await tx.CommitAsync(ct);
            return new(false, "invalidCredential", "Invitation token is invalid or has already been used.", 401);
        }

        if (invite.ExpiresAtUtc <= now)
        {
            await tx.CommitAsync(ct);
            return new(false, "requestExpired", "Invitation token has expired.", 422);
        }

        if (!string.Equals(invite.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
        {
            await tx.CommitAsync(ct);
            return new(false, "forbidden", "Invitation was issued for a different email address.", 403);
        }

        var existingUser = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);
        Guid userId;
        bool createdNew;

        if (existingUser is null)
        {
            if (string.IsNullOrEmpty(password) || password.Length is < 14 or > 128)
            {
                await tx.CommitAsync(ct);
                return new(false, "badRequest", "Use a password between 14 and 128 characters.", 400);
            }

            userId = Guid.NewGuid();
            db.Users.Add(new UserRow
            {
                Id = userId,
                Email = invite.Email,
                NormalizedEmail = invite.NormalizedEmail,
                PasswordHash = hashPassword(password),
                Enabled = true,
                EmailVerified = true
            });
            createdNew = true;
        }
        else
        {
            if (!existingUser.Enabled || string.IsNullOrEmpty(password) || !verifyPassword(existingUser.PasswordHash, password))
            {
                await tx.CommitAsync(ct);
                return new(false, "invalidCredential", "Existing accounts must verify their current password.", 401);
            }

            // Never reset or overwrite an existing user's password on invitation acceptance.
            userId = existingUser.Id;
            createdNew = false;
        }

        var alreadyMember = await db.Memberships.AnyAsync(x => x.UserId == userId && x.HouseId == invite.HouseId, ct);
        if (!alreadyMember)
        {
            db.Memberships.Add(new MembershipRow
            {
                UserId = userId,
                HouseId = invite.HouseId,
                Role = "resident"
            });
        }

        invite.AcceptedAtUtc = now;
        invite.AcceptedByUserId = userId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, string.Empty, string.Empty, 200, invite.HouseId, userId, createdNew);
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPasswordHash,
        Func<string, string, bool> verifyPassword,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.Enabled || !verifyPassword(user.PasswordHash, currentPassword))
        {
            await tx.CommitAsync(ct);
            return false;
        }

        user.PasswordHash = newPasswordHash;
        await db.SaveChangesAsync(ct);
        await RevokeUserSessionsAsync(userId, now, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task CreatePasswordResetTokenAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct)
    {
        db.PasswordResetTokens.Add(new PasswordResetTokenRow
        {
            TokenHash = tokenHash,
            UserId = userId,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<(bool Confirmed, string? ErrorCode, string? ErrorMessage, int StatusCode)> ConfirmPasswordResetAsync(
        string tokenHash,
        string normalizedEmail,
        string newPasswordHash,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var reset = await db.PasswordResetTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
        if (reset is null || reset.UsedAtUtc is not null)
        {
            await tx.CommitAsync(ct);
            return (false, "invalidCredential", "Password reset token is invalid or has already been used.", 401);
        }

        if (reset.ExpiresAtUtc <= now)
        {
            await tx.CommitAsync(ct);
            return (false, "requestExpired", "Password reset token has expired.", 422);
        }

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == reset.UserId, ct);
        if (user is null || !user.Enabled || !user.EmailVerified ||
            !string.Equals(user.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
        {
            await tx.CommitAsync(ct);
            return (false, "invalidCredential", "Password reset token does not match an active verified account.", 401);
        }

        reset.UsedAtUtc = now;
        user.PasswordHash = newPasswordHash;
        await db.SaveChangesAsync(ct);
        await RevokeUserSessionsAsync(user.Id, now, ct);
        await tx.CommitAsync(ct);
        return (true, null, null, 204);
    }

    private Task RevokeUserSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.Sessions
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, now), ct);

    private static InvitationSummary ToInvitationSummary(InvitationRow row, DateTimeOffset now)
    {
        var status = row.RevokedAtUtc is not null ? "revoked"
            : row.AcceptedAtUtc is not null ? "accepted"
            : row.ExpiresAtUtc <= now ? "expired"
            : "pending";
        return new InvitationSummary(
            row.Id,
            row.HouseId,
            row.Email,
            row.Role,
            status,
            row.CreatedAtUtc,
            row.ExpiresAtUtc,
            row.AcceptedAtUtc,
            row.RevokedAtUtc);
    }
}