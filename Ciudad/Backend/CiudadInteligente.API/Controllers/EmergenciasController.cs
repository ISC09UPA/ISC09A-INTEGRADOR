using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>Corredor de emergencia (prioridad de paso para ambulancias, bomberos, policia).</summary>
[ApiController]
[Route("api/emergencias")]
public class EmergenciasController(IEmergenciaService servicio) : ControllerBase
{
    /// <summary>Activa el corredor: el acceso de llegada a verde y los demas a rojo, y lo registra.</summary>
    [HttpPost]
    public async Task<ActionResult<EmergenciaDto>> Solicitar([FromBody] SolicitudEmergenciaDto dto) =>
        await servicio.SolicitarAsync(dto);

    /// <summary>Termina el corredor; el cruce regresa al ciclo automatico.</summary>
    [HttpPost("fin")]
    public async Task<IActionResult> Finalizar()
    {
        await servicio.FinalizarAsync();
        return NoContent();
    }

    /// <summary>Ultimas emergencias registradas (mas reciente primero).</summary>
    [HttpGet("recientes")]
    public async Task<ActionResult<IReadOnlyList<EmergenciaDto>>> Recientes([FromQuery] int cantidad = 5) =>
        Ok(await servicio.ObtenerRecientesAsync(cantidad));
}
