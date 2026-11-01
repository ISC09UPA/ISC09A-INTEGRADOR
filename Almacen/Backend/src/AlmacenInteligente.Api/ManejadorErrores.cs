using AlmacenInteligente.Application.CasosDeUso;
using AlmacenInteligente.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AlmacenInteligente.Api;

/// <summary>Traduce las excepciones de los casos de uso a ProblemDetails con el código HTTP que corresponde.</summary>
internal sealed class ManejadorErrores(IProblemDetailsService problemas, ILogger<ManejadorErrores> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (estado, titulo) = ex switch
        {
            NoAutenticadoException => (StatusCodes.Status401Unauthorized, "No autenticado"),
            AccesoDenegadoException => (StatusCodes.Status403Forbidden, "Sin permiso"),
            NoEncontradoException => (StatusCodes.Status404NotFound, "No encontrado"),
            ValidacionException => (StatusCodes.Status422UnprocessableEntity, "Datos inválidos"),
            DomainException => (StatusCodes.Status409Conflict, "Regla de negocio"),
            _ => (StatusCodes.Status500InternalServerError, "Error inesperado"),
        };
        if (estado == StatusCodes.Status500InternalServerError)
            log.LogError(ex, "Error no controlado en {Ruta}", ctx.Request.Path);

        ctx.Response.StatusCode = estado;
        var detalle = new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            Detail = estado == StatusCodes.Status500InternalServerError ? "Ocurrió un error inesperado." : ex.Message,
        };
        if (ex is ValidacionException v)
            detalle.Extensions["errores"] = v.Errores;

        return await problemas.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = detalle, Exception = ex });
    }
}
