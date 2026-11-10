using CiudadInteligente.Application.DTOs;

namespace CiudadInteligente.Application.Services;

/// <summary>Inicio de sesion y cuenta propia (lo que usa AuthController).</summary>
public interface IAuthService
{
    Task<SesionDto> IniciarSesionAsync(LoginDto dto);

    Task<UsuarioDto> ObtenerAsync(long usuarioId);

    Task CambiarContrasenaAsync(long usuarioId, CambiarContrasenaDto dto);
}
