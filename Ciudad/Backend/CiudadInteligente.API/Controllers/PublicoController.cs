using CiudadInteligente.API.Seguridad;
using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CiudadInteligente.API.Controllers;

/// <summary>
/// Lo que puede ver cualquier persona sin cuenta (rol "ciudadano"): el estado
/// en vivo del cruce y el trafico. Solo lectura. Todo lo demas (control,
/// emergencias, usuarios, historial) sigue pidiendo sesion.
/// </summary>
[ApiController]
[Route("api/publico")]
[AllowAnonymous]
[EnableRateLimiting(Politicas.LimitePublico)]
public class PublicoController(ISemaforoService semaforo, IEstadisticasService estadisticas) : ControllerBase
{
    /// <summary>Colores de los 4 accesos, fase, cuenta regresiva y vehiculos por acceso.</summary>
    [HttpGet("cruce")]
    public ActionResult<EstadoPublicoDto> Cruce() => EstadoPublicoDto.De(semaforo.ObtenerEstado());

    /// <summary>Trafico ahora, prediccion a 15/30/60 min, flujo por hora y mejor hora para pasar.</summary>
    [HttpGet("trafico")]
    public async Task<ActionResult<TraficoPublicoDto>> Trafico() =>
        TraficoPublicoDto.De(await estadisticas.ObtenerResumenAsync(7));
}
