namespace AirportParking.Application.DTOs;

public record BookingDto(
    string Id,
    string CustomerId,
    string VehicleId,
    string Placa,
    string Terminal,
    string Zone,
    DateTime FechaInicio,
    DateTime FechaFin,
    decimal MontoTotal,
    string Estado,
    string CodigoQr
);

public record CreateBookingRequest(
    string CustomerId,
    string VehicleId,
    string Terminal,
    string Zone,
    DateTime FechaInicio,
    DateTime FechaFin,
    decimal MontoTotal
);

public record TerminalAvailabilityDto(
    string TerminalId,
    string Name,
    int TotalBays,
    int FreeBays,
    string OccupancyPercentage,
    decimal RatePerHour
);
