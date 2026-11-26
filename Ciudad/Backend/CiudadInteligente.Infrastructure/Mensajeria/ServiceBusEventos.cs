using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CiudadInteligente.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Infrastructure.Mensajeria;

/// <summary>
/// Configuracion de Azure Service Bus ("ServiceBus" en appsettings):
///   Habilitado        (appsettings.json)  false por defecto
///   Cola              (appsettings.json)  integrador
///   ConnectionString  (dotnet user-secrets / variable de entorno ServiceBus__ConnectionString, NO en el repo)
/// </summary>
public sealed record OpcionesServiceBus(bool Habilitado, string Cola, string ConnectionString)
{
    public bool EstaListo => Habilitado && !string.IsNullOrWhiteSpace(ConnectionString);
}

/// <summary>Publica los eventos del cruce en la cola. Un error al enviar solo se registra.</summary>
public sealed class ServiceBusPublicadorEventos(
    ServiceBusClient cliente,
    OpcionesServiceBus opciones,
    ILogger<ServiceBusPublicadorEventos> logger) : IPublicadorEventos, IAsyncDisposable
{
    private readonly ServiceBusSender _emisor = cliente.CreateSender(opciones.Cola);

    public async Task PublicarAsync(EventoCruce evento, CancellationToken cancelacion = default)
    {
        try
        {
            var mensaje = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(evento))
            {
                MessageId = Guid.NewGuid().ToString(),
                ContentType = "application/json",
                Subject = evento.Tipo
            };
            await _emisor.SendMessageAsync(mensaje, cancelacion);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo publicar el evento {Tipo} en Service Bus", evento.Tipo);
        }
    }

    public ValueTask DisposeAsync() => _emisor.DisposeAsync();
}

/// <summary>Cuando Service Bus esta apagado o sin cadena de conexion: no hace nada.</summary>
public sealed class PublicadorEventosNulo : IPublicadorEventos
{
    public Task PublicarAsync(EventoCruce evento, CancellationToken cancelacion = default) => Task.CompletedTask;
}

/// <summary>
/// Lee la cola y procesa cada evento. De momento los registra en el log y los
/// completa; aqui se engancha lo que se quiera hacer con ellos (bitacora,
/// notificaciones, etc.). Si falla el procesamiento el mensaje se reintenta
/// (hasta 10 veces) y luego va a la cola de mensajes muertos.
/// </summary>
public sealed class ServiceBusConsumidorEventos(
    ServiceBusClient cliente,
    OpcionesServiceBus opciones,
    ILogger<ServiceBusConsumidorEventos> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        await using var procesador = cliente.CreateProcessor(opciones.Cola, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        procesador.ProcessMessageAsync += async args =>
        {
            logger.LogInformation("Service Bus [{Tipo}] {Cuerpo}", args.Message.Subject, args.Message.Body.ToString());
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        };
        procesador.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Error en Service Bus ({Origen})", args.ErrorSource);
            return Task.CompletedTask;
        };

        await procesador.StartProcessingAsync(detener);
        try { await Task.Delay(Timeout.Infinite, detener); }
        catch (OperationCanceledException) { }
        await procesador.StopProcessingAsync(CancellationToken.None);
    }
}
