using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>Densidad de trafico que manda la camara desde la Raspberry Pi.</summary>
[ApiController]
[Route("api/trafico")]
public class TraficoController(ITraficoService servicio) : ControllerBase
{
    /// <summary>
    /// Lo llama Embebidos/RaspberryPi/vision/detectar_trafico.py cada pocos
    /// segundos con {"vehiculos":[norte, sur, este, oeste]}.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarConteoDto dto)
    {
        await servicio.RegistrarConteoAsync(dto);
        return NoContent();
    }

    /// <summary>Ultimos conteos guardados (mas reciente primero).</summary>
    [HttpGet("historial")]
    public async Task<ActionResult<IReadOnlyList<ConteoTraficoDto>>> Historial([FromQuery] int cantidad = 60) =>
        Ok(await servicio.ObtenerHistorialAsync(cantidad));
}
