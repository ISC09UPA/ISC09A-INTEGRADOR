using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class Stay : BaseEntity
{
    public string? BookingId { get; set; }
    public string PlacaNormalizada { get; set; } = string.Empty;
    public string CarrilEntrada { get; set; } = string.Empty;
    public DateTime FechaIngreso { get; set; }
    public string? CarrilSalida { get; set; }
    public DateTime? FechaSalida { get; set; }
    public decimal MontoCalculado { get; set; }
    public string EstadoCobro { get; set; } = "PENDING";

    public Booking? Booking { get; set; }
}
