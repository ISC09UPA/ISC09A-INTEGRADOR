using AirportParking.Domain.Common;

namespace AirportParking.Domain.Entities;

public class ParkingBay : BaseEntity
{
    public int BayNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TerminalId { get; set; } = "t1-agu-main";
    public string Status { get; set; } = "FREE"; // FREE, OCCUPIED, EV
    public string CurrentPlate { get; set; } = "—";
    public string CurrentVehicle { get; set; } = "Despejado";
    public string SensorDistance { get; set; } = "> 150 cm";
    public DateTime? OccupiedSince { get; set; }
    public decimal RatePerHour { get; set; } = 45.00m;
}
