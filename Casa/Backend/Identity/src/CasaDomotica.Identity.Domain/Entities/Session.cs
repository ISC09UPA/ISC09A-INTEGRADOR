namespace CasaDomotica.Identity.Domain.Entities;

public sealed class Session
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
