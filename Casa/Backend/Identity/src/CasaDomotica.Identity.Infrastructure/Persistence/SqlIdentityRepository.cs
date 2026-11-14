using System.Data;
using CasaDomotica.Identity.Domain.Entities;
using CasaDomotica.Identity.Domain.Interfaces;
using CasaDomotica.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
namespace CasaDomotica.Identity.Infrastructure.Persistence;
public sealed class SqlIdentityRepository(IdentityDb db) : IUserRepository, ISessionRepository
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) => await Map(await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedEmail == email.Trim().ToUpperInvariant(), cancellationToken), cancellationToken);
    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => await Map(await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken), cancellationToken);
    private async Task<User?> Map(UserRow? row, CancellationToken ct)
    {
        if (row is null) return null;
        var memberships = await db.Memberships.AsNoTracking().Where(x => x.UserId == row.Id).Select(x => new HouseMembership(x.HouseId, x.Role)).ToListAsync(ct);
        return new User { Id = row.Id, Email = row.Email, PasswordHash = row.PasswordHash, Enabled = row.Enabled, Memberships = memberships };
    }
    public async Task CreateAsync(Session session, string refreshHash, CancellationToken ct)
    {
        db.Sessions.Add(new() { Id = session.Id, UserId = session.UserId, CreatedAt = session.CreatedAt, ExpiresAt = session.ExpiresAt });
        db.RefreshTokens.Add(new() { Hash = refreshHash, SessionId = session.Id, IssuedAt = session.CreatedAt });
        await db.SaveChangesAsync(ct);
    }
    public async Task<Session?> RotateAsync(string refreshHash, string replacementHash, DateTimeOffset now, CancellationToken ct)
    {
        // A serializable transaction retains consumed hashes so replay revokes the whole session family.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var refresh = await db.RefreshTokens.SingleOrDefaultAsync(x => x.Hash == refreshHash, ct);
        if (refresh is null) { await transaction.CommitAsync(ct); return null; }
        var session = await db.Sessions.SingleAsync(x => x.Id == refresh.SessionId, ct);
        if (refresh.UsedAt is not null) { session.RevokedAt = now; await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return null; }
        if (session.RevokedAt is not null || session.ExpiresAt <= now) { await transaction.CommitAsync(ct); return null; }
        refresh.UsedAt = now;
        db.RefreshTokens.Add(new() { Hash = replacementHash, SessionId = session.Id, IssuedAt = now });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new Session { Id = session.Id, UserId = session.UserId, CreatedAt = session.CreatedAt, ExpiresAt = session.ExpiresAt };
    }
    public async Task RevokeByRefreshAsync(string refreshHash, CancellationToken ct)
    {
        var sessionId = await db.RefreshTokens.Where(x => x.Hash == refreshHash).Select(x => (Guid?)x.SessionId).SingleOrDefaultAsync(ct);
        if (sessionId is null) return;
        await db.Sessions.Where(x => x.Id == sessionId).ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
    }
    public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken ct) => db.Sessions.AsNoTracking().AnyAsync(x => x.Id == sessionId && x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now && db.Users.Any(u => u.Id == userId && u.Enabled), ct);
}
