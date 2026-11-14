using System.Security.Claims;
using LockerSmart.Application.DTOs;
using LockerSmart.Application.Interfaces;
using LockerSmart.Domain.Enums;
using LockerSmart.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LockerSmart.API.Endpoints;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dispositivos").RequireAuthorization();
        group.MapPost("/rentas/{rentaId:int}/abrir", async (
            int rentaId, ClaimsPrincipal principal, AppDbContext db, IDeviceMessagingService messaging, CancellationToken ct) =>
        {
            // La apertura física solo se autoriza con un JWT emitido tras reconocimiento facial.
            if (principal.FindFirstValue("face_verified") != "true")
                return Results.Forbid();
            if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();
            var renta = await db.Rentas.FirstOrDefaultAsync(r => r.Id == rentaId, ct);
            if (renta is null) return Results.NotFound();
            if (renta.UsuarioId != userId && !principal.IsInRole("Administrador")) return Results.Forbid();
            if (renta.Estado is not (RentaEstado.Activa or RentaEstado.Excedida))
                return Results.Conflict(new { error = "La renta no está activa." });
            var locker = await db.Lockers.FirstOrDefaultAsync(l => l.Id == renta.LockerId, ct);
            if (locker is null) return Results.NotFound();
            var command = new DeviceCommandMessage(Guid.NewGuid().ToString("N"), "Abrir", locker.Codigo, renta.Id, userId, DateTime.UtcNow, "Acceso autorizado mediante reconocimiento facial");
            await messaging.EnviarComandoAsync(command, ct);
            return Results.Accepted(value: new { commandId = command.CommandId, locker = locker.Codigo, status = "Enviado a Service Bus" });
        });
    }
}
