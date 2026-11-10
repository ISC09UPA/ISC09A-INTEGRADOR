using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>
/// Corredor de emergencia (prioridad de paso para ambulancias, bomberos, policia).
/// Lo atiende cualquier usuario con sesion: operador o administrador.
/// </summary>
[ApiController]
[Route("api/emergencias")]
public class EmergenciasController(IEmergenciaService servicio, ILogger<EmergenciasController> logger) : ControllerBase
{
    /// <summary>Activa el corredor: el acceso de llegada a verde y los demas a rojo, y lo registra.</summary>
    [HttpPost]
    public async Task<ActionResult<EmergenciaDto>> Solicitar([FromBody] SolicitudEmergenciaDto dto)
    {
        var emergencia = await servicio.SolicitarAsync(dto);
        logger.LogInformation("{Usuario} activo corredor de emergencia: {Unidad}, acceso {Acceso}, {Tipo}",
            User.Identity?.Name, emergencia.Unidad, emergencia.Acceso, emergencia.Tipo);
        return emergencia;
    }

    /// <summary>Termina el corredor; el cruce regresa al ciclo automatico.</summary>
    [HttpPost("fin")]
    public async Task<IActionResult> Finalizar()
    {
        await servicio.FinalizarAsync();
        logger.LogInformation("{Usuario} termino el corredor de emergencia", User.Identity?.Name);
        return NoContent();
    }

    /// <summary>Ultimas emergencias registradas (mas reciente primero).</summary>
    [HttpGet("recientes")]
    public async Task<ActionResult<IReadOnlyList<EmergenciaDto>>> Recientes([FromQuery] int cantidad = 5) =>
        Ok(await servicio.ObtenerRecientesAsync(cantidad));
}
