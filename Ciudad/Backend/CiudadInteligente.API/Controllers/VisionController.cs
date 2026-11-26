using CiudadInteligente.API.Seguridad;
using CiudadInteligente.Application.DTOs;
using CiudadInteligente.Application.Interfaces;
using CiudadInteligente.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Controllers;

/// <summary>
/// Foto del cruce -> conteo por acceso (Azure Custom Vision) -> mismo flujo que
/// POST api/trafico (actualiza el verde adaptativo y guarda el historial).
/// Sirve para mandar fotos desde la nube sin correr el script de vision local.
/// </summary>
[ApiController]
[Route("api/vision")]
public class VisionController(IServicioVision vision, ITraficoService trafico) : ControllerBase
{
    /// <summary>multipart/form-data con el campo "imagen" (jpg/png, hasta 4 MB) y X-Api-Key.</summary>
    [HttpPost("conteo")]
    [Authorize(Policy = Politicas.EnviarConteos)]
    [RequestSizeLimit(4_500_000)]
    public async Task<IActionResult> Conteo(IFormFile imagen, CancellationToken ct)
    {
        if (!vision.EstaConfigurado)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Vision no configurada",
                detail: "Faltan Vision:PredictionUrl y Vision:PredictionKey.");
        }

        if (imagen is null || imagen.Length == 0 || imagen.Length > 4_000_000)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Solicitud invalida", detail: "Manda una imagen de hasta 4 MB en el campo \"imagen\".");
        }

        await using var flujo = imagen.OpenReadStream();
        var vehiculos = await vision.ContarPorAccesoAsync(flujo, ct);
        var guardado = await trafico.RegistrarConteoAsync(new RegistrarConteoDto(vehiculos));
        return Ok(new { vehiculos, guardado });
    }
}
