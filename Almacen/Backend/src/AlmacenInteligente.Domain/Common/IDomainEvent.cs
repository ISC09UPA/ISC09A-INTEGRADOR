namespace AlmacenInteligente.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OcurrioEn { get; }
}
