using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>
/// Analisis del trafico: flujo por hora y dia, horas pico, mejor hora para
/// pasar, prediccion a corto plazo y comparativo fijo vs. adaptativo.
/// Cualquier usuario con sesion.
/// </summary>
[ApiController]
[Route("api/estadisticas")]
public class EstadisticasController(IEstadisticasService servicio) : ControllerBase
{
    /// <summary>Todo junto, de los ultimos <paramref name="dias"/> dias (1-30, por defecto 7).</summary>
    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenEstadisticasDto>> Resumen([FromQuery] int dias = 7) =>
        await servicio.ObtenerResumenAsync(dias);

    /// <summary>Solo el promedio por hora del dia.</summary>
    [HttpGet("flujo-por-hora")]
    public async Task<ActionResult<IReadOnlyList<FlujoHoraDto>>> FlujoPorHora([FromQuery] int dias = 7) =>
        Ok((await servicio.ObtenerResumenAsync(dias)).FlujoPorHora);

    /// <summary>Solo la prediccion a 15, 30 y 60 minutos.</summary>
    [HttpGet("prediccion")]
    public async Task<ActionResult<PrediccionDto>> Prediccion([FromQuery] int dias = 7) =>
        (await servicio.ObtenerResumenAsync(dias)).Prediccion;

    /// <summary>Solo el comparativo de espera: temporizador fijo vs. algoritmo adaptativo.</summary>
    [HttpGet("comparativo")]
    public async Task<ActionResult<ComparativoDto>> Comparativo([FromQuery] int dias = 7) =>
        (await servicio.ObtenerResumenAsync(dias)).Comparativo;
}
