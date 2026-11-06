namespace LockerSmart.Application.DTOs;

public record RegistroRequest(string Nombre, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record UsuarioDto(int Id, string Nombre, string Email, string Rol, bool TieneRostro);
public record AuthResponse(string Token, UsuarioDto Usuario);
