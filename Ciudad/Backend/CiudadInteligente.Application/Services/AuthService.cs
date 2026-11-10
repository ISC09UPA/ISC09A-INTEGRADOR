using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Exceptions;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Mapping;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Application.Services;

public class AuthService(
    IUsuarioRepository usuarios,
    IHasherContrasenas hasher,
    IGeneradorToken tokens,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<SesionDto> IniciarSesionAsync(LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Usuario) || string.IsNullOrEmpty(dto.Contrasena))
        {
            logger.LogWarning("Intento de inicio de sesion sin usuario o sin contrasena");
            throw new CredencialesInvalidasException("Escribe tu usuario y tu contraseña.");
        }

        var usuario = await usuarios.ObtenerPorNombreAsync(dto.Usuario.Trim());

        // mismo mensaje si el usuario no existe o la contrasena esta mal:
        // asi no se puede averiguar que usuarios existen
        if (usuario is null || !hasher.Verificar(dto.Contrasena, usuario.HashContrasena))
        {
            // se registra el usuario escrito (no la contrasena) para detectar intentos de adivinarla
            logger.LogWarning("Inicio de sesion fallido para el usuario {Usuario}", dto.Usuario.Trim());
            throw new CredencialesInvalidasException();
        }

        var (token, expira) = tokens.Generar(usuario);
        logger.LogInformation("Inicio de sesion: {Usuario} ({Rol})", usuario.NombreUsuario, usuario.Rol);
        return new SesionDto(token, expira, usuario.ADto());
    }

    public async Task<UsuarioDto> ObtenerAsync(long usuarioId)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId)
            ?? throw new CredencialesInvalidasException("Tu usuario ya no existe; inicia sesión de nuevo.");
        return usuario.ADto();
    }

    public async Task CambiarContrasenaAsync(long usuarioId, CambiarContrasenaDto dto)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId)
            ?? throw new CredencialesInvalidasException("Tu usuario ya no existe; inicia sesión de nuevo.");

        if (!hasher.Verificar(dto.Actual ?? string.Empty, usuario.HashContrasena))
        {
            logger.LogWarning("Cambio de contrasena rechazado (contrasena actual incorrecta) para {Usuario}", usuario.NombreUsuario);
            throw new ReglaNegocioException("La contraseña actual no es correcta.");
        }

        ReglasUsuario.ValidarContrasena(dto.Nueva);
        await usuarios.ActualizarContrasenaAsync(usuarioId, hasher.Hash(dto.Nueva));
        logger.LogInformation("Contrasena cambiada por {Usuario}", usuario.NombreUsuario);
    }
}
