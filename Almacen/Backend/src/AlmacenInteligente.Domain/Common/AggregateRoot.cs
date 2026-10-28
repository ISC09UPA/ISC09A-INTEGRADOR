namespace AlmacenInteligente.Domain.Common;

/// <summary>Raíz de agregado: unidad de consistencia que acumula eventos de dominio hasta que se persisten.</summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _eventos = [];

    public IReadOnlyList<IDomainEvent> Eventos => _eventos;

    protected void Emitir(IDomainEvent evento) => _eventos.Add(evento);

    public void LimpiarEventos() => _eventos.Clear();
}
