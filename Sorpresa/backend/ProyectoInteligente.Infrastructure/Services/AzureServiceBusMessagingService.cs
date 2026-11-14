using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using LockerSmart.Application.DTOs;
using LockerSmart.Application.Interfaces;

namespace LockerSmart.Infrastructure.Services;

public sealed class AzureServiceBusMessagingService : IDeviceMessagingService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);


    public AzureServiceBusMessagingService(IConfiguration configuration)
    {
        var connection = configuration["AzureServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("Falta AzureServiceBus:ConnectionString en User Secrets.");
        var queue = configuration["AzureServiceBus:CommandsQueue"] ?? "locker-commands";
        _client = new ServiceBusClient(connection);
        _sender = _client.CreateSender(queue);
    }

    public async Task EnviarComandoAsync(DeviceCommandMessage command, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(command, JsonOptions);
        var message = new ServiceBusMessage(BinaryData.FromString(body))
        {
            MessageId = command.CommandId,
            ContentType = "application/json",
            Subject = command.Accion
        };
        message.ApplicationProperties["lockerCodigo"] = command.LockerCodigo;
        await _sender.SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
