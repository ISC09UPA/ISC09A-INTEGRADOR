using LockerSmart.Application.Interfaces;
using LockerSmart.Domain.Entities;
using LockerSmart.Domain.Enums;

namespace LockerSmart.API.Endpoints;


public static class LockerEndpoints
{
    public static void MapLockerEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/lockers").RequireAuthorization();   // cualquiera con sesión puede leer

        g.MapGet("/", async (bool? disponibles, ILockerService s) =>
            Results.Ok(await s.ListarAsync(disponibles ?? false)));

        g.MapGet("/{id:int}", async (int id, ILockerService s) =>
            await s.ObtenerAsync(id) is { } l ? Results.Ok(l) : Results.NotFound());

        g.MapPost("/", async (Locker locker, ILockerService s) =>
        {
            var creado = await s.CrearAsync(locker);
            return Results.Created($"/api/lockers/{creado.Id}", creado);
        }).RequireAuthorization(p => p.RequireRole("Administrador"));

        g.MapPut("/{id:int}/estado", async (int id, LockerEstado estado, ILockerService s) =>
            await s.CambiarEstadoAsync(id, estado) ? Results.NoContent() : Results.NotFound())
        .RequireAuthorization(p => p.RequireRole("Administrador"));
    }
}