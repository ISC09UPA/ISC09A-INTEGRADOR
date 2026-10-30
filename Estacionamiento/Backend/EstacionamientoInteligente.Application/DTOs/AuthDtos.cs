namespace AirportParking.Application.DTOs;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string Nombre, string Email, string Password, string Telefono, string? Placa = null, string? MarcaModelo = null);

public record AuthResponse(string Token, string CustomerId, string Nombre, string Email, string Role);
