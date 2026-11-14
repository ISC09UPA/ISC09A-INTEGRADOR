using System.Security.Claims;
using LockerSmart.Application.DTOs;
using LockerSmart.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LockerSmart.API.Endpoints;

public sealed class RegistroForm
{
    [FromForm] public string Nombre { get; set; } = "";
    [FromForm] public string Email { get; set; } = "";
    [FromForm] public string Password { get; set; } = "";
    [FromForm] public IFormFile? Foto { get; set; }
}

public sealed class FaceLoginForm
{
    [FromForm] public IFormFile? Foto { get; set; }
}

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/registro", async (
            [FromForm] RegistroForm form,
            IAuthService service,
            CancellationToken ct) =>
        {
            byte[]? foto = null;
            string? contentType = null;

            if (form.Foto is { Length: > 0 })
            {
                await using var memory = new MemoryStream();
                await form.Foto.CopyToAsync(memory, ct);
                foto = memory.ToArray();
                contentType = form.Foto.ContentType;
            }

            var request = new RegistroRequest(form.Nombre, form.Email, form.Password);
            var (response, error) = await service.RegistrarAsync(
                request, foto, contentType, ct);

            return response is null
                ? Results.BadRequest(new { error })
                : Results.Ok(response);
        })
        .DisableAntiforgery();

        group.MapPost("/login", async (LoginRequest request, IAuthService service, CancellationToken ct) =>
            await service.LoginAsync(request, ct) is { } response
                ? Results.Ok(response)
                : Results.Unauthorized());

        group.MapPost("/login-rostro", async (
            [FromForm] FaceLoginForm form,
            IAuthService service,
            CancellationToken ct) =>
        {
            if (form.Foto is null)
                return Results.BadRequest(new { error = "Debes enviar una fotografía en el campo Foto." });

            await using var memory = new MemoryStream();
            await form.Foto.CopyToAsync(memory, ct);

            var result = await service.LoginConRostroAsync(memory.ToArray(), ct);
            return result.Respuesta is not null
                ? Results.Ok(result.Respuesta)
                : Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .DisableAntiforgery();

        group.MapGet("/me", async (ClaimsPrincipal principal, IAuthService service) =>
        {
            var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claim, out var id)) return Results.Unauthorized();

            return await service.ObtenerAsync(id) is { } usuario
                ? Results.Ok(usuario)
                : Results.NotFound();
        }).RequireAuthorization();

        group.MapGet("/me/foto-url", async (ClaimsPrincipal principal, IAuthService auth, IBlobStorageService blob) =>
        {
            var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claim, out var id)) return Results.Unauthorized();
            var usuario = await auth.ObtenerAsync(id);
            if (usuario is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(usuario.FotoUrl))
                return Results.NotFound(new { error = "El usuario no tiene fotografía almacenada." });
            var url = await blob.CreateReadSasUrlAsync(usuario.FotoUrl, TimeSpan.FromMinutes(5));
            return Results.Ok(new { url, expiraEnMinutos = 5 });
        }).RequireAuthorization();
    }
}
