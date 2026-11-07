using CiudadInteligente.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CiudadInteligente.API.Middleware;

/// <summary>
/// Convierte las excepciones de la capa Application en respuestas claras:
///   ReglaNegocioException          -> 400 (dato invalido, verdes en conflicto...)
///   HardwareNoDisponibleException  -> 503 (no hay comunicacion con el ESP32)
///   cualquier otra                 -> 500
/// La app movil muestra el campo "detail" tal cual al usuario.
/// </summary>
public sealed class ManejadorErrores(IProblemDetailsService problemas, ILogger<ManejadorErrores> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        var (status, titulo) = excepcion switch
        {
            ReglaNegocioException => (StatusCodes.Status400BadRequest, "Solicitud invalida"),
            HardwareNoDisponibleException => (StatusCodes.Status503ServiceUnavailable, "ESP32 no disponible"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(excepcion, "Error no controlado en {Ruta}", contexto.Request.Path);
        }
        else
        {
            logger.LogWarning("{Titulo}: {Mensaje}", titulo, excepcion.Message);
        }

        contexto.Response.StatusCode = status;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Ocurrio un error inesperado en el servidor."
                    : excepcion.Message
            }
        });
    }
}
