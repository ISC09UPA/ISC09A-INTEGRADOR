using CiudadInteligente.API.Seguridad;
using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>
/// Estado en vivo del cruce (cualquier usuario con sesion) y control de modos /
/// colores (solo administrador).
/// </summary>
[ApiController]
[Route("api/semaforo")]
public class SemaforoController(ISemaforoService servicio, ILogger<SemaforoController> logger) : ControllerBase
{
    /// <summary>Estado actual: modo, fase, colores y vehiculos por acceso.</summary>
    [HttpGet("estado")]
    public ActionResult<EstadoCruceDto> Estado() => servicio.ObtenerEstado();

    /// <summary>Cambia a "adaptativo", "fijo" o "manual".</summary>
    [HttpPost("modo")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    public async Task<ActionResult<EstadoCruceDto>> CambiarModo([FromBody] CambiarModoDto dto)
    {
        var estado = await servicio.CambiarModoAsync(dto);
        logger.LogInformation("{Usuario} cambio el modo del cruce a {Modo}", User.Identity?.Name, dto.Modo);
        return estado;
    }

    /// <summary>Aplica colores manuales a uno o mas accesos (valida conflictos Norte-Sur / Este-Oeste).</summary>
    [HttpPost("secuencia")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    public async Task<ActionResult<EstadoCruceDto>> AplicarSecuencia([FromBody] SecuenciaManualDto dto)
    {
        var estado = await servicio.AplicarSecuenciaAsync(dto);
        logger.LogInformation("{Usuario} aplico colores manuales: {Colores}", User.Identity?.Name,
            string.Join(", ", (dto.Accesos ?? []).Select(a => $"{a.Acceso}={a.Color}")));
        return estado;
    }

    /// <summary>Pasa a manual con los 4 accesos en rojo.</summary>
    [HttpPost("detener")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    public async Task<ActionResult<EstadoCruceDto>> Detener()
    {
        var estado = await servicio.DetenerTodoAsync();
        logger.LogInformation("{Usuario} puso los 4 accesos en rojo (detener)", User.Identity?.Name);
        return estado;
    }
}
