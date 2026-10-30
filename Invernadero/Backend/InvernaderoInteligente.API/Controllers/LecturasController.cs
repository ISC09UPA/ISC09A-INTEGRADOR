using Microsoft.AspNetCore.Mvc;
using Telemetria.DTOs;
using Telemetria.Services;

namespace Telemetria.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LecturasController : ControllerBase
{
    private readonly ILecturaService _service;

    public LecturasController(ILecturaService service) => _service = service;

    /// <summary>Registra una nueva lectura de sensor.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LecturaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LecturaDto>> Post([FromBody] CrearLecturaDto dto, CancellationToken ct)
    {
        var creada = await _service.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(GetPorId), new { id = creada.Id }, creada);
    }

    /// <summary>Consulta lecturas (más recientes primero) con filtros opcionales.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LecturaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LecturaDto>>> Get([FromQuery] FiltroLecturasDto filtro, CancellationToken ct)
    {
        var lecturas = await _service.ObtenerAsync(filtro, ct);
        return Ok(lecturas);
    }

    /// <summary>Consulta una lectura por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LecturaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LecturaDto>> GetPorId(int id, CancellationToken ct)
    {
        var lectura = await _service.ObtenerPorIdAsync(id, ct);
        return lectura is null ? NotFound() : Ok(lectura);
    }
}