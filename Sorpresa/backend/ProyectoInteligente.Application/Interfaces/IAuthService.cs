using LockerSmart.Application.DTOs;

namespace LockerSmart.Application.Interfaces;

public interface IAuthService
{
    Task<(AuthResponse? Respuesta, string? Error)> RegistrarAsync(
        RegistroRequest r, byte[]? foto = null, string? contentType = null, CancellationToken cancellationToken = default);
    Task<AuthResponse?> LoginAsync(LoginRequest r, CancellationToken cancellationToken = default);
    Task<FaceLoginResult> LoginConRostroAsync(byte[] foto, CancellationToken cancellationToken = default);
    Task<UsuarioDto?> ObtenerAsync(int id);
}
