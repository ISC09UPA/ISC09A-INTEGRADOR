using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LockerSmart.Application.DTOs;
using LockerSmart.Domain.Enums;
using LockerSmart.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LockerSmart.Infrastructure.Services;

/// <summary>Consume eventos de Raspberry Pi y refleja el estado en SQL.</summary>
public sealed class AzureServiceBusEventsWorker(
    IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<AzureServiceBusEventsWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxIntentosProcesamiento = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = configuration["AzureServiceBus:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            logger.LogWarning("Service Bus no está configurado. El consumidor de eventos no se inició.");
            return;
        }

        var queue = configuration["AzureServiceBus:EventsQueue"] ?? "locker-events";
        await using var client = new ServiceBusClient(connection);
        await using var processor = client.CreateProcessor(queue, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
        });
        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Error consumiendo eventos de Service Bus. Entity: {Entity}", args.EntityPath);
            return Task.CompletedTask;
        };
        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { await processor.StopProcessingAsync(CancellationToken.None); }
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<DeviceEventMessage>(args.Message.Body.ToString(), JsonOptions);
            if (evt is null || string.IsNullOrWhiteSpace(evt.EventId) ||
                string.IsNullOrWhiteSpace(evt.LockerCodigo) || string.IsNullOrWhiteSpace(evt.Tipo))
                throw new JsonException("El evento requiere EventId, LockerCodigo y Tipo.");

            if (evt.PesoKg.HasValue && (!double.IsFinite(evt.PesoKg.Value) || evt.PesoKg.Value < 0))
                throw new JsonException("PesoKg debe ser un número finito mayor o igual a cero.");
            if (evt.TimestampUtc.HasValue && evt.TimestampUtc.Value > DateTime.UtcNow.AddMinutes(5))
                throw new JsonException("TimestampUtc no puede estar más de cinco minutos en el futuro.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.EventosDispositivo.AnyAsync(x => x.EventId == evt.EventId))
            {
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            var locker = await db.Lockers.FirstOrDefaultAsync(x => x.Codigo == evt.LockerCodigo);
            if (locker is null) throw new InvalidOperationException($"No existe locker {evt.LockerCodigo}.");

            // Las confirmaciones siempre deben corresponder a un comando emitido por este backend.
            if (evt.Tipo.Equals("ComandoEjecutado", StringComparison.OrdinalIgnoreCase) ||
                evt.Tipo.Equals("ComandoRechazado", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(evt.CommandId))
                    throw new JsonException("La confirmación requiere CommandId.");
                var command = await db.ComandosDispositivo.FirstOrDefaultAsync(
                    x => x.CommandId == evt.CommandId, CancellationToken.None);
                if (command is null || command.LockerCodigo != evt.LockerCodigo)
                    throw new InvalidOperationException("CommandId inexistente o asociado a otro locker.");
                if (command.Estado is ComandoEstado.Ejecutado or ComandoEstado.Rechazado)
                {
                    // La confirmación repetida con otro EventId no debe revertir el resultado original.
                    db.EventosDispositivo.Add(new LockerSmart.Domain.Entities.EventoDispositivo
                    {
                        EventId = evt.EventId, LockerCodigo = evt.LockerCodigo, Tipo = evt.Tipo,
                        PayloadJson = args.Message.Body.ToString(), RecibidoUtc = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                    await args.CompleteMessageAsync(args.Message);
                    return;
                }
                command.Estado = evt.Tipo.Equals("ComandoEjecutado", StringComparison.OrdinalIgnoreCase)
                    ? ComandoEstado.Ejecutado : ComandoEstado.Rechazado;
                command.RespuestaDispositivo = evt.Detalle is null ? null :
                    (evt.Detalle.Length > 1000 ? evt.Detalle[..1000] : evt.Detalle);
                command.ActualizadoUtc = DateTime.UtcNow;
            }

            // La telemetría puede actualizarse, pero nunca debe sacar un locker de mantenimiento.
            if (evt.PesoKg.HasValue) locker.PesoActualKg = evt.PesoKg.Value;
            if (evt.PuertaCerrada.HasValue) locker.PuertaCerrada = evt.PuertaCerrada.Value;

            if (evt.Tipo.Equals("Falla", StringComparison.OrdinalIgnoreCase))
            {
                locker.Estado = LockerEstado.Mantenimiento;
            }
            else if (evt.Tipo.Equals("DevolucionConfirmada", StringComparison.OrdinalIgnoreCase))
            {
                if (!evt.RentaId.HasValue || evt.RentaId.Value <= 0)
                    throw new JsonException("La devolución requiere un RentaId válido.");
                if (!evt.PesoKg.HasValue)
                    throw new JsonException("La devolución requiere un peso final válido.");
                if (evt.PuertaCerrada != true || evt.PresenciaDetectada != false)
                    throw new JsonException("La devolución requiere puerta cerrada y ausencia de objetos o personas.");
                if (locker.Estado == LockerEstado.Mantenimiento)
                    throw new JsonException("No se puede liberar un locker en mantenimiento; requiere revisión administrativa.");

                var renta = await db.Rentas.FirstOrDefaultAsync(r => r.Id == evt.RentaId.Value &&
                    r.LockerId == locker.Id && (r.Estado == RentaEstado.Activa || r.Estado == RentaEstado.Excedida));
                if (renta is null)
                    throw new JsonException("No existe una renta activa o excedida que coincida con el locker y RentaId.");

                renta.Estado = RentaEstado.Finalizada;
                renta.FechaFinalizacion = (evt.TimestampUtc ?? DateTime.UtcNow).ToUniversalTime();
                renta.PesoFinalKg = evt.PesoKg.Value;
                locker.Estado = LockerEstado.Disponible;
                locker.PesoActualKg = 0;
            }
            else if (evt.Tipo.Equals("PresenciaDetectada", StringComparison.OrdinalIgnoreCase) &&
                     evt.PresenciaDetectada == true && locker.Estado == LockerEstado.Disponible)
            {
                locker.Estado = LockerEstado.Ocupado;
            }

            db.EventosDispositivo.Add(new LockerSmart.Domain.Entities.EventoDispositivo
            {
                EventId = evt.EventId, LockerCodigo = evt.LockerCodigo, Tipo = evt.Tipo,
                PayloadJson = args.Message.Body.ToString(), RecibidoUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            await args.CompleteMessageAsync(args.Message);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Evento inválido; se enviará a dead-letter.");
            await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Evento no procesable; se enviará a dead-letter.");
            await args.DeadLetterMessageAsync(args.Message, "InvalidEvent", ex.Message);
        }
        catch (Exception ex)
        {
            if (args.Message.DeliveryCount >= MaxIntentosProcesamiento)
            {
                logger.LogError(ex, "Evento {MessageId} agotó {Intentos} intentos; se enviará a dead-letter.",
                    args.Message.MessageId, MaxIntentosProcesamiento);
                await args.DeadLetterMessageAsync(args.Message, "ProcessingFailed", "Se agotaron los intentos de procesamiento.");
                return;
            }
            logger.LogError(ex, "Error transitorio procesando evento {MessageId}. Intento {Intento}/{Maximo}; se reintentará.",
                args.Message.MessageId, args.Message.DeliveryCount, MaxIntentosProcesamiento);
            await args.AbandonMessageAsync(args.Message);
        }
    }
}
