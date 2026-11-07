using InvernaderoInteligente.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvernaderoInteligente.API.Middleware;

/// <summary>
/// Manejo de errores global: convierte las excepciones en respuestas ProblemDetails.
/// RegistroDuplicadoException → 409 Conflict; cualquier otra → 500 sin exponer detalles internos.
/// </summary>
public class ManejadorErrores(IProblemDetailsService problemDetails, ILogger<ManejadorErrores> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, titulo) = ex switch
        {
            RegistroDuplicadoException => (StatusCodes.Status409Conflict, "Registro duplicado"),
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Error no controlado en {Metodo} {Ruta}", http.Request.Method, http.Request.Path);
        else
            logger.LogInformation("{Titulo} en {Ruta}: {Detalle}", titulo, http.Request.Path, ex.Message);

        http.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "Ocurrió un error inesperado. Revisa los logs del servidor."
                    : ex.Message
            }
        });
    }
}
