using CiudadInteligente.API.Seguridad;
using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>Densidad de trafico que manda la camara desde la Raspberry Pi (o el simulador).</summary>
[ApiController]
[Route("api/trafico")]
public class TraficoController(ITraficoService servicio) : ControllerBase
{
    /// <summary>
    /// Lo llama Embebidos/RaspberryPi/vision/detectar_trafico.py (o
    /// simular_trafico.py) cada pocos segundos con {"vehiculos":[norte, sur, este, oeste]}
    /// y el encabezado X-Api-Key.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Politicas.EnviarConteos)]
    public async Task<IActionResult> Registrar([FromBody] RegistrarConteoDto dto)
    {
        var guardado = await servicio.RegistrarConteoAsync(dto);

        // 204 = todo bien. 202 = el cruce se actualizo pero el conteo no se pudo
        // guardar en la base de datos (el emisor puede reenviarlo en un lote).
        return guardado ? NoContent() : Accepted(new { guardado = false });
    }

    /// <summary>Conteos con fecha para cargar historial; no se mandan al ESP32.</summary>
    [HttpPost("lote")]
    [Authorize(Policy = Politicas.EnviarConteos)]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<object>> RegistrarLote([FromBody] RegistrarLoteConteosDto dto) =>
        Ok(new { guardados = await servicio.RegistrarLoteAsync(dto) });

    /// <summary>Ultimos conteos guardados (mas reciente primero).</summary>
    [HttpGet("historial")]
    public async Task<ActionResult<IReadOnlyList<ConteoTraficoDto>>> Historial([FromQuery] int cantidad = 60) =>
        Ok(await servicio.ObtenerHistorialAsync(cantidad));
}
