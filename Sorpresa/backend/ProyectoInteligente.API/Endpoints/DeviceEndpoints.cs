using System.Security.Claims;
using LockerSmart.Application.DTOs;
using LockerSmart.Application.Interfaces;
using LockerSmart.Domain.Enums;
using LockerSmart.Domain.Entities;
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
            // La apertura física exige un token obtenido mediante reconocimiento facial.
            if (principal.FindFirstValue("face_verified") != "true") return Results.Forbid();
            if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();

            var renta = await db.Rentas.FirstOrDefaultAsync(r => r.Id == rentaId, ct);
            if (renta is null) return Results.NotFound(new { error = "No existe la renta." });
            if (renta.UsuarioId != userId && !principal.IsInRole("Administrador")) return Results.Forbid();
            if (renta.Estado is not (RentaEstado.Activa or RentaEstado.Excedida))
                return Results.Conflict(new { error = "La renta no está activa." });

            var locker = await db.Lockers.FirstOrDefaultAsync(l => l.Id == renta.LockerId, ct);
            if (locker is null) return Results.NotFound(new { error = "No existe el locker asignado." });
            if (locker.Estado == LockerEstado.Mantenimiento)
                return Results.Conflict(new { error = "El locker está en mantenimiento y no puede abrirse." });
            if (!locker.PuertaCerrada)
                return Results.Conflict(new { error = "La puerta ya aparece abierta; verifica el estado físico antes de enviar otro comando." });

            var commandId = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;
            var command = new DeviceCommandMessage(
                commandId, "Abrir", locker.Codigo, renta.Id, userId,
                now, "Acceso autorizado mediante reconocimiento facial");
            var record = new ComandoDispositivo
            {
                CommandId = commandId, LockerCodigo = locker.Codigo, RentaId = renta.Id,
                UsuarioId = userId, Accion = command.Accion, Estado = ComandoEstado.Pendiente,
                Motivo = command.Motivo, CreadoUtc = now
            };
            db.ComandosDispositivo.Add(record);
            await db.SaveChangesAsync(ct);
            try
            {
                await messaging.EnviarComandoAsync(command, ct);
                record.Estado = ComandoEstado.Enviado;
                record.ActualizadoUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                record.Estado = ComandoEstado.ErrorEnvio;
                record.RespuestaDispositivo = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                record.ActualizadoUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            return Results.Accepted($"/api/dispositivos/comandos/{commandId}", new
            {
                commandId, locker = locker.Codigo, status = "Enviado",
                message = "El envío no confirma que la Raspberry Pi haya ejecutado la apertura."
            });
        });

        group.MapGet("/comandos/{commandId}", async (
            string commandId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();
            var command = await db.ComandosDispositivo.AsNoTracking()
                .FirstOrDefaultAsync(x => x.CommandId == commandId, ct);
            if (command is null) return Results.NotFound();
            if (command.UsuarioId != userId && !principal.IsInRole("Administrador"))
                return Results.Forbid();
            return Results.Ok(new
            {
                command.CommandId, command.LockerCodigo, command.RentaId, command.Accion,
                estado = command.Estado.ToString(), command.Motivo,
                command.RespuestaDispositivo, command.CreadoUtc, command.ActualizadoUtc
            });
        });

        group.MapGet("/comandos", async (ClaimsPrincipal principal, AppDbContext db,
            int? cantidad, CancellationToken ct) =>
        {
            if (!principal.IsInRole("Administrador")) return Results.Forbid();
            var take = Math.Clamp(cantidad ?? 50, 1, 200);
            var commands = await db.ComandosDispositivo.AsNoTracking()
                .OrderByDescending(x => x.CreadoUtc).Take(take)
                .Select(x => new { x.CommandId, x.LockerCodigo, x.RentaId, x.UsuarioId,
                    x.Accion, estado = x.Estado.ToString(), x.Motivo,
                    x.RespuestaDispositivo, x.CreadoUtc, x.ActualizadoUtc })
                .ToListAsync(ct);
            return Results.Ok(commands);
        });

        group.MapGet("/eventos", async (ClaimsPrincipal principal, AppDbContext db,
            int? cantidad, CancellationToken ct) =>
        {
            if (!principal.IsInRole("Administrador")) return Results.Forbid();
            var take = Math.Clamp(cantidad ?? 50, 1, 200);
            var events = await db.EventosDispositivo.AsNoTracking()
                .OrderByDescending(x => x.RecibidoUtc).Take(take)
                .Select(x => new { x.EventId, x.LockerCodigo, x.Tipo, x.RecibidoUtc, x.PayloadJson })
                .ToListAsync(ct);
            return Results.Ok(events);
        });
    }
}
