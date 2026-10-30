using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class Booking : BaseEntity
{
    public string CustomerId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public string ZoneId { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public decimal MontoTotal { get; set; }
    public string Moneda { get; set; } = "MXN";
    public string Estado { get; set; } = "CONFIRMED";
    public string CodigoQr { get; set; } = string.Empty;

    public Customer? Customer { get; set; }
    public Vehicle? Vehicle { get; set; }
}
