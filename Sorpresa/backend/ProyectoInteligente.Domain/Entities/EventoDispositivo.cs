namespace LockerSmart.Domain.Entities;

/// <summary>Registro idempotente de eventos recibidos desde Raspberry Pi mediante Service Bus.</summary>
public class EventoDispositivo
{
    public int Id { get; set; }
    public string EventId { get; set; } = "";
    public string LockerCodigo { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime RecibidoUtc { get; set; } = DateTime.UtcNow;
}
