using System.Security.Claims;
using LockerSmart.Application.DTOs;
using LockerSmart.Application.Interfaces;

namespace LockerSmart.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost("/registro", async (RegistroRequest r, IAuthService s) =>
        {
            var (res, error) = await s.RegistrarAsync(r);
            return res is null ? Results.BadRequest(new { error }) : Results.Ok(res);
        });

        g.MapPost("/login", async (LoginRequest r, IAuthService s) =>
            await s.LoginAsync(r) is { } res ? Results.Ok(res) : Results.Unauthorized());

        g.MapGet("/me", async (ClaimsPrincipal p, IAuthService s) =>
        {
            var id = int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await s.ObtenerAsync(id) is { } u ? Results.Ok(u) : Results.NotFound();
        }).RequireAuthorization();
    }
}