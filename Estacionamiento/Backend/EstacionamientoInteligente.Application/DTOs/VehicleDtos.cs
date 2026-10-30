namespace AirportParking.Application.DTOs;

public record VehicleDto(
    string Id,
    string CustomerId,
    string PlacaNormalizada,
    string? Alias,
    string? Marca,
    string? Modelo,
    string? Color,
    bool EsPredeterminado
);

public record RegisterVehicleRequest(
    string Placa,
    string? Alias,
    string? Marca,
    string? Modelo,
    string? Color,
    bool EsPredeterminado = false
);
