using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Application.Interfaces;

namespace InvernaderoInteligente.API.Controllers;

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
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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

    /// <summary>Exporta el historial filtrado de lecturas como CSV en orden cronológico.</summary>
    [HttpGet("exportar.csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarCsv([FromQuery] FiltroExportacionLecturasDto filtro, CancellationToken ct)
    {
        var lecturas = await _service.ObtenerParaExportacionAsync(filtro, ct);
        var csv = new StringBuilder();

        AgregarFilaCsv(csv, "id", "sensorId", "tipo", "valor", "unidad", "invernaderoId", "fechaHora");
        foreach (var lectura in lecturas)
        {
            AgregarFilaCsv(
                csv,
                lectura.Id.ToString(CultureInfo.InvariantCulture),
                lectura.SensorId,
                lectura.Tipo,
                lectura.Valor.ToString("R", CultureInfo.InvariantCulture),
                lectura.Unidad,
                lectura.InvernaderoId?.ToString(CultureInfo.InvariantCulture),
                lectura.FechaHora.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }

        var contenido = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();
        var nombre = $"lecturas-{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}.csv";

        return File(contenido, "text/csv; charset=utf-8", nombre);
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

    private static void AgregarFilaCsv(StringBuilder csv, params string?[] valores)
    {
        csv.AppendJoin(',', valores.Select(EscaparCsv)).Append("\r\n");
    }

    private static string EscaparCsv(string? valor)
    {
        valor ??= string.Empty;
        if (valor.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return valor;

        return $"\"{valor.Replace("\"", "\"\"")}\"";
    }
}
