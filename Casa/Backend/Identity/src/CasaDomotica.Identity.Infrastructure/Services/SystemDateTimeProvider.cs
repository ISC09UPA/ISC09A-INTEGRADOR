using CasaDomotica.Identity.Domain.Interfaces;

namespace CasaDomotica.Identity.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
