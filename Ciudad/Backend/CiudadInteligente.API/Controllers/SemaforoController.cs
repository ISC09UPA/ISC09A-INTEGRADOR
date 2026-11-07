using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>Estado en vivo del cruce y control de modos / colores.</summary>
[ApiController]
[Route("api/semaforo")]
public class SemaforoController(ISemaforoService servicio) : ControllerBase
{
    /// <summary>Estado actual: modo, fase, colores y vehiculos por acceso.</summary>
    [HttpGet("estado")]
    public ActionResult<EstadoCruceDto> Estado() => servicio.ObtenerEstado();

    /// <summary>Cambia a "adaptativo", "fijo" o "manual".</summary>
    [HttpPost("modo")]
    public async Task<ActionResult<EstadoCruceDto>> CambiarModo([FromBody] CambiarModoDto dto) =>
        await servicio.CambiarModoAsync(dto);

    /// <summary>Aplica colores manuales a uno o mas accesos (valida conflictos Norte-Sur / Este-Oeste).</summary>
    [HttpPost("secuencia")]
    public async Task<ActionResult<EstadoCruceDto>> AplicarSecuencia([FromBody] SecuenciaManualDto dto) =>
        await servicio.AplicarSecuenciaAsync(dto);

    /// <summary>Pasa a manual con los 4 accesos en rojo.</summary>
    [HttpPost("detener")]
    public async Task<ActionResult<EstadoCruceDto>> Detener() => await servicio.DetenerTodoAsync();
}
