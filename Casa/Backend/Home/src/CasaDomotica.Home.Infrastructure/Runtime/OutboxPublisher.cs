using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace CasaDomotica.Home.Infrastructure.Runtime;
public sealed class BusConnection : IAsyncDisposable
{
    public ServiceBusClient Client { get; }
    public ServiceBusSender Sender { get; }
    public BusConnection(IConfiguration configuration)
    {
        var host = configuration["ServiceBus:Namespace"] ?? throw new InvalidOperationException("Configure the project Service Bus namespace.");
        Client = new ServiceBusClient(host, new DefaultAzureCredential(), new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets });
        Sender = Client.CreateSender("house-events-v1");
    }
    public async ValueTask DisposeAsync() { await Sender.DisposeAsync(); await Client.DisposeAsync(); }
}
public sealed class OutboxPublisher(HomeDb db, BusConnection connection, IStateNotifier notifier)
{
    public async Task<bool> PublishNextAsync(CancellationToken ct)
    {
        var batch = await db.Outbox.AsNoTracking().Where(x => x.PublishedAtUtc == null).OrderBy(x => x.CreatedAtUtc).Take(20).ToListAsync(ct);
        foreach (var record in batch)
        {
            using var document = JsonDocument.Parse(record.BodyJson);
            var message = new ServiceBusMessage(record.BodyJson) { MessageId = record.EventId.ToString(), Subject = record.Type, ContentType = "application/json", CorrelationId = document.RootElement.GetProperty("correlationId").GetString() };
            message.ApplicationProperties["type"] = record.Type;
            await connection.Sender.SendMessageAsync(message, ct);
            await notifier.NotifyAsync(document.RootElement.GetProperty("houseId").GetGuid(), ct);
            await db.Outbox.Where(x => x.EventId == record.EventId && x.PublishedAtUtc == null).ExecuteUpdateAsync(update => update.SetProperty(x => x.PublishedAtUtc, DateTimeOffset.UtcNow), ct);
        }
        return batch.Count == 20;
    }
}
