namespace LockerSmart.Application.DTOs;

public record RegistroRequest(string Nombre, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record UsuarioDto(int Id, string Nombre, string Email, string Rol, bool TieneRostro, string? FotoUrl);
public record AuthResponse(string Token, UsuarioDto Usuario);
public record FaceLoginResult(AuthResponse? Respuesta, string? Error);
