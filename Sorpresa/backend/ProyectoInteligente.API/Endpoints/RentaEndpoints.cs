using System.Security.Claims;
using LockerSmart.Application.Interfaces;

namespace LockerSmart.API.Endpoints;

public record CrearRentaRequest(double PesoDeclaradoKg, int DuracionHoras);
public record FinalizarRentaRequest(double? PesoFinalKg);

public static class RentaEndpoints
{
    public static void MapRentaEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/rentas").RequireAuthorization();

        // Crear una renta para el usuario autenticado.
        g.MapPost("/", async (
            CrearRentaRequest request,
            ClaimsPrincipal principal,
            IRentaService service) =>
        {
            var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(claim, out var usuarioId))
                return Results.Unauthorized();

            if (request.PesoDeclaradoKg <= 0 || request.DuracionHoras <= 0)
                return Results.BadRequest("El peso y la duración deben ser mayores a cero.");

            var renta = await service.CrearAsync(
                usuarioId,
                request.PesoDeclaradoKg,
                request.DuracionHoras);

            return renta is null
                ? Results.Conflict("No hay lockers disponibles con capacidad suficiente.")
                : Results.Created($"/api/rentas/{renta.Id}", renta);
        });

        // Consultar las rentas del usuario autenticado.
        g.MapGet("/mis-rentas", async (
            ClaimsPrincipal principal,
            IRentaService service) =>
        {
            var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(claim, out var usuarioId))
                return Results.Unauthorized();

            return Results.Ok(await service.ListarPorUsuarioAsync(usuarioId));
        });

        // Consultar rentas activas: solo administrador.
        g.MapGet("/activas", async (IRentaService service) =>
            Results.Ok(await service.ListarActivasAsync()))
            .RequireAuthorization(p => p.RequireRole("Administrador"));

        // Actualizar rentas vencidas: solo administrador.
        g.MapPost("/actualizar-vencidas", async (IRentaService service) =>
        {
            var cantidad = await service.ActualizarVencidasAsync();
            return Results.Ok(new { actualizadas = cantidad });
        }).RequireAuthorization(p => p.RequireRole("Administrador"));

        // Consultar una renta: propietario o administrador.
        g.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal principal,
            IRentaService service) =>
        {
            var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(claim, out var usuarioId))
                return Results.Unauthorized();

            var renta = await service.ObtenerAsync(id);

            if (renta is null)
                return Results.NotFound();

            if (renta.UsuarioId != usuarioId &&
                !principal.IsInRole("Administrador"))
                return Results.Forbid();

            return Results.Ok(renta);
        });

        // Finalizar una renta: propietario o administrador.
        g.MapPost("/{id:int}/finalizar", async (
            int id,
            FinalizarRentaRequest request,
            ClaimsPrincipal principal,
            IRentaService service) =>
        {
            var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(claim, out var usuarioId))
                return Results.Unauthorized();

            var renta = await service.ObtenerAsync(id);

            if (renta is null)
                return Results.NotFound();

            if (renta.UsuarioId != usuarioId &&
                !principal.IsInRole("Administrador"))
                return Results.Forbid();

            if (request.PesoFinalKg < 0)
                return Results.BadRequest("El peso final no puede ser negativo.");

            var finalizada = await service.FinalizarAsync(
                id,
                request.PesoFinalKg);

            return finalizada
                ? Results.NoContent()
                : Results.Conflict("No fue posible finalizar la renta.");
        });
    }
}