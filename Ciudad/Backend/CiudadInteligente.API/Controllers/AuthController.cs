using CiudadInteligente.API.Seguridad;
using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CiudadInteligente.API.Controllers;

/// <summary>Inicio de sesion y datos de la cuenta propia.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService servicio) : ControllerBase
{
    /// <summary>
    /// Usuario y contrasena -> token JWT (12 h). Maximo 10 intentos por minuto
    /// por IP, para que no se puedan probar contrasenas a lo loco.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(Politicas.LimiteLogin)]
    public async Task<ActionResult<SesionDto>> Login([FromBody] LoginDto dto) =>
        await servicio.IniciarSesionAsync(dto);

    /// <summary>Quien soy (la app lo usa al abrir para revisar que la sesion guardada siga valida).</summary>
    [HttpGet("yo")]
    public async Task<ActionResult<UsuarioDto>> Yo() => await servicio.ObtenerAsync(User.IdUsuario());

    /// <summary>Cambia la contrasena propia (pide la actual).</summary>
    [HttpPost("contrasena")]
    public async Task<IActionResult> CambiarContrasena([FromBody] CambiarContrasenaDto dto)
    {
        await servicio.CambiarContrasenaAsync(User.IdUsuario(), dto);
        return NoContent();
    }
}
