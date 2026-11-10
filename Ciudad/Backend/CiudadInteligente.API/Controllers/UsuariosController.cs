using CiudadInteligente.API.Seguridad;
using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>Alta, baja y lista de usuarios. Solo administrador.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Politicas.SoloAdministrador)]
public class UsuariosController(IUsuarioService servicio, ILogger<UsuariosController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar() => Ok(await servicio.ListarAsync());

    /// <summary>Crea un usuario: {"usuario","nombreCompleto","contrasena","rol":"operador"|"administrador"}.</summary>
    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear([FromBody] CrearUsuarioDto dto)
    {
        var creado = await servicio.CrearAsync(dto);
        logger.LogInformation("{Admin} creo el usuario {Usuario} ({Rol})", User.Identity?.Name, creado.Usuario, creado.Rol);
        return Created($"/api/usuarios/{creado.Id}", creado);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id)
    {
        await servicio.EliminarAsync(id, User.IdUsuario());
        logger.LogInformation("{Admin} elimino al usuario con id {Id}", User.Identity?.Name, id);
        return NoContent();
    }
}
