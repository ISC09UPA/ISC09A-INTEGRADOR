using CasaDomotica.Identity.Domain.Entities;
namespace CasaDomotica.Identity.Domain.Interfaces;
public interface ISessionRepository
{
    Task CreateAsync(Session session, string refreshHash, CancellationToken ct);
    Task<Session?> RotateAsync(string refreshHash, string replacementHash, DateTimeOffset now, CancellationToken ct);
    Task RevokeByRefreshAsync(string refreshHash, CancellationToken ct);
    Task<bool> IsActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken ct);
}
