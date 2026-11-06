using LockerSmart.Application.DTOs;

namespace LockerSmart.Application.Interfaces;

public interface IAuthService
{
    Task<(AuthResponse? Respuesta, string? Error)> RegistrarAsync(RegistroRequest r);
    Task<AuthResponse?> LoginAsync(LoginRequest r);
    Task<UsuarioDto?> ObtenerAsync(int id);
}
