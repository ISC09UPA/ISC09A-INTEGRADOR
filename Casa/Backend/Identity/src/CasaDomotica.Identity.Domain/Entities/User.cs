using CasaDomotica.Identity.Domain.ValueObjects;

namespace CasaDomotica.Identity.Domain.Entities;

public sealed class User
{
    public Guid Id { get; init; }
    public string PasswordHash { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public string Email { get; init; } = string.Empty;
    public IReadOnlyCollection<HouseMembership> Memberships { get; init; } = Array.Empty<HouseMembership>();
}
