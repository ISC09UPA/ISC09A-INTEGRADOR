namespace CiudadInteligente.Application.Interfaces;

/// <summary>Algo que paso en el cruce y que se publica a la cola de Azure Service Bus.</summary>
/// <param name="Tipo">Ej. "emergencia.solicitada", "emergencia.finalizada".</param>
public record EventoCruce(string Tipo, DateTimeOffset Fecha, IReadOnlyDictionary<string, object?> Datos);

/// <summary>
/// Puerto para publicar eventos del cruce (Azure Service Bus). Nunca lanza:
/// si la cola no esta disponible, el evento se pierde pero el semaforo sigue
/// funcionando (la seguridad del cruce no depende de la cola).
/// </summary>
public interface IPublicadorEventos
{
    Task PublicarAsync(EventoCruce evento, CancellationToken cancelacion = default);
}
