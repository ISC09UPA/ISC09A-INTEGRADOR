using CasaDomotica.Identity.Domain.Entities;

namespace CasaDomotica.Identity.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}
