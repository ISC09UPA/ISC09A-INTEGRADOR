using System.Text.Json;
using AlmacenInteligente.Domain.Common;

namespace AlmacenInteligente.Infrastructure.Mensajeria;

/// <summary>
/// Evento de dominio guardado en la misma transacción que el cambio que lo produjo. Lo entrega después
/// <see cref="ProcesadorOutbox"/>: así un correo nunca sale por un cambio que no se confirmó, y si el correo
/// falla el cambio no se pierde.
/// </summary>
public sealed class MensajeOutbox
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Nombre completo del tipo del evento (p. ej. AlmacenInteligente.Domain.Productos.ProductoAprobado).</summary>
    public string Tipo { get; init; } = null!;

    /// <summary>El evento serializado en JSON.</summary>
    public string Contenido { get; init; } = null!;

    public DateTimeOffset OcurrioEn { get; init; }
    public DateTimeOffset? ProcesadoEn { get; set; }
    public int Intentos { get; set; }
    public DateTimeOffset? ProximoIntentoEn { get; set; }
    public string? UltimoError { get; set; }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static MensajeOutbox Desde(IDomainEvent evento) => new()
    {
        Tipo = evento.GetType().FullName!,
        Contenido = JsonSerializer.Serialize(evento, evento.GetType(), Json),
        OcurrioEn = evento.OcurrioEn,
    };

    /// <summary>Null si el tipo ya no existe en el dominio (p. ej. se renombró el evento).</summary>
    public IDomainEvent? LeerEvento() =>
        typeof(IDomainEvent).Assembly.GetType(Tipo) is { } tipo && typeof(IDomainEvent).IsAssignableFrom(tipo)
            ? (IDomainEvent?)JsonSerializer.Deserialize(Contenido, tipo, Json)
            : null;
}
