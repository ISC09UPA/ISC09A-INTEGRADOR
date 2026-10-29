using Microsoft.AspNetCore.Mvc;
using Telemetria.DTOs;
using Telemetria.Services;

namespace Telemetria.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RiegosController : ControllerBase
{
    private readonly IRiegoService _service;

    public RiegosController(IRiegoService service) => _service = service;

    /// <summary>Registra un riego (manual o automático por umbral).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RiegoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RiegoDto>> Post([FromBody] CrearRiegoDto dto, CancellationToken ct)
    {
        var creado = await _service.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(GetPorId), new { id = creado.Id }, creado);
    }

    /// <summary>Consulta el historial de riegos (más recientes primero) con filtros opcionales.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RiegoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RiegoDto>>> Get([FromQuery] FiltroRiegosDto filtro, CancellationToken ct)
    {
        var riegos = await _service.ObtenerAsync(filtro, ct);
        return Ok(riegos);
    }

    /// <summary>Consulta un riego por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RiegoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiegoDto>> GetPorId(int id, CancellationToken ct)
    {
        var riego = await _service.ObtenerPorIdAsync(id, ct);
        return riego is null ? NotFound() : Ok(riego);
    }
}