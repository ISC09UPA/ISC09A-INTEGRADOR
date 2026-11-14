namespace CasaDomotica.Identity.Domain.Interfaces;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
