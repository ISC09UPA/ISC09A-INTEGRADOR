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
            if (evt is null || string.IsNullOrWhiteSpace(evt.EventId) || string.IsNullOrWhiteSpace(evt.LockerCodigo))
                throw new JsonException("El evento requiere EventId y LockerCodigo.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.EventosDispositivo.AnyAsync(x => x.EventId == evt.EventId))
            {
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            var locker = await db.Lockers.FirstOrDefaultAsync(x => x.Codigo == evt.LockerCodigo);
            if (locker is null) throw new InvalidOperationException($"No existe locker {evt.LockerCodigo}.");

            if (evt.PesoKg is < 0) throw new InvalidOperationException("El peso reportado no puede ser negativo.");
            if (evt.PesoKg.HasValue) locker.PesoActualKg = evt.PesoKg.Value;
            if (evt.PuertaCerrada.HasValue) locker.PuertaCerrada = evt.PuertaCerrada.Value;

            if (evt.PresenciaDetectada == true && locker.Estado == LockerEstado.Disponible)
                locker.Estado = LockerEstado.Ocupado;

            if (evt.Tipo.Equals("Falla", StringComparison.OrdinalIgnoreCase))
                locker.Estado = LockerEstado.Mantenimiento;

            // Solo un evento explícito de devolución confirmada puede cerrar una renta y liberar el locker.
            if (evt.Tipo.Equals("DevolucionConfirmada", StringComparison.OrdinalIgnoreCase))
            {
                if (evt.PuertaCerrada != true || evt.PresenciaDetectada != false)
                    throw new InvalidOperationException("DevolucionConfirmada requiere puerta cerrada y presencia no detectada.");
                var query = db.Rentas.Where(r => r.LockerId == locker.Id &&
                    (r.Estado == RentaEstado.Activa || r.Estado == RentaEstado.Excedida));
                if (evt.RentaId.HasValue) query = query.Where(r => r.Id == evt.RentaId.Value);
                var renta = await query.OrderByDescending(r => r.Inicio).FirstOrDefaultAsync();
                if (renta is not null)
                {
                    renta.Estado = RentaEstado.Finalizada;
                    renta.FechaFinalizacion = evt.TimestampUtc?.ToUniversalTime() ?? DateTime.UtcNow;
                    renta.PesoFinalKg = evt.PesoKg;
                    locker.Estado = LockerEstado.Disponible;
                    locker.PesoActualKg = 0;
                }
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
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo procesar evento {MessageId}; se reintentará.", args.Message.MessageId);
            await args.AbandonMessageAsync(args.Message);
        }
    }
}
