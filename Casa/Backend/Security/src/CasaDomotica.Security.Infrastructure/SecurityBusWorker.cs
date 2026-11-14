using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using CasaDomotica.Security.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CasaDomotica.Security.Infrastructure;

public sealed class SecurityBusWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<SecurityBusWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = configuration["ServiceBus:Namespace"] ?? throw new InvalidOperationException("Security Service Bus namespace is required.");
        await using var bus = new ServiceBusClient(host, new DefaultAzureCredential(), new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets });
        await using var receiver = bus.CreateProcessor("house-events-v1", "security-v1", new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 1, MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5) });
        await using var sender = bus.CreateSender("house-events-v1");
        receiver.ProcessMessageAsync += async args =>
        {
            try
            {
                var body = args.Message.Body.ToString();
                var messageId = GetEventId(body);
                if (!string.Equals(messageId, args.Message.MessageId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Message identifier mismatch.");
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SecurityEvents>().IngestAsync(body, args.CancellationToken);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            }
            catch (InvalidDataException error)
            {
                logger.LogWarning("Invalid security event retained in the dead-letter queue: {Type}", error.GetType().Name);
                await args.DeadLetterMessageAsync(args.Message, "invalidSecurityEvent", "Event rejected by the security contract.", args.CancellationToken);
            }
        };
        receiver.ProcessErrorAsync += args => { logger.LogWarning("Security event processing unavailable: {Type}", args.Exception.GetType().Name); return Task.CompletedTask; };
        await receiver.StartProcessingAsync(stoppingToken);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<SecurityDb>();
                    var rows = await db.Outbox.AsNoTracking().Where(x => x.PublishedAtUtc == null).OrderBy(x => x.CreatedAtUtc).Take(20).ToListAsync(stoppingToken);
                    foreach (var row in rows)
                    {
                        using var body = JsonDocument.Parse(row.BodyJson);
                        var output = new ServiceBusMessage(row.BodyJson) { MessageId = row.EventId.ToString(), Subject = row.Type, ContentType = "application/json", CorrelationId = body.RootElement.GetProperty("correlationId").GetString() };
                        output.ApplicationProperties["type"] = row.Type;
                        await sender.SendMessageAsync(output, stoppingToken);
                        await db.Outbox.Where(x => x.EventId == row.EventId && x.PublishedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.PublishedAtUtc, DateTimeOffset.UtcNow), stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error) { logger.LogWarning("Security event publication unavailable: {Type}", error.GetType().Name); }
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        finally { await receiver.StopProcessingAsync(CancellationToken.None); }
    }

    private static string GetEventId(string body)
    {
        try { using var document = JsonDocument.Parse(body); return document.RootElement.GetProperty("eventId").GetGuid().ToString(); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new InvalidDataException("Invalid event identifier.", error); }
    }
}
