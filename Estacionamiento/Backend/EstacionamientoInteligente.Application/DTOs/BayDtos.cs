namespace AirportParking.Application.DTOs;

public record ParkingBayDto(
    string Id,
    int BayNumber,
    string Name,
    string Status, // free, occupied, ev
    string Plate,
    string Vehicle,
    string SensorDistance,
    string ElapsedTime,
    decimal RatePerHour
);

public record ToggleBayRequest(
    string? NewStatus,
    string? Plate,
    string? Vehicle
);

public record DashboardSummaryDto(
    int TotalBays,
    int FreeBays,
    int OccupiedBays,
    int EvBays,
    string OccupancyPercentage,
    decimal TodayCashlessRevenue,
    string BarrierStatus,
    List<ParkingBayDto> Bays
);
