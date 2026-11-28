using System.Text.Json;
using AlmacenInteligente.Application.Iot;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlmacenInteligente.Infrastructure.Iot;

/// <summary>Sección "Iot" de la configuración: conexión a Service Bus y nombres de las colas.</summary>
public sealed class OpcionesServiceBusIot
{
    public string? ConnectionString { get; init; }
    public string ColaSolicitudesAuth { get; init; } = "iot-auth-solicitudes";
    public string ColaRespuestasAuth { get; init; } = "iot-auth-respuestas";
    public string ColaEscaneosVenta { get; init; } = "iot-venta-escaneos";

    /// <summary>Respuesta a cada pieza (venta o comprobación de stock) para la pantalla y los LEDs del embebido.</summary>
    public string ColaRespuestasEscaneos { get; init; } = "iot-escaneos-respuestas";

    /// <summary>Mensajes más viejos ya no tienen a nadie esperando (el embebido espera ~15 s): se descartan.</summary>
    public int AntiguedadMaximaSegundos { get; init; } = 60;

    public bool Configurada => !string.IsNullOrWhiteSpace(ConnectionString);
}

/// <summary>Base de los workers: arranca un ServiceBusProcessor y lo detiene con la aplicación.</summary>
internal abstract class WorkerServiceBus(IServiceProvider servicios, OpcionesServiceBusIot opciones, ILogger log) : BackgroundService
{
    protected OpcionesServiceBusIot Opciones { get; } = opciones;
    protected ILogger Log { get; } = log;
    protected IServiceScopeFactory Scopes { get; } = servicios.GetRequiredService<IServiceScopeFactory>();

    protected abstract string Cola { get; }

    protected abstract Task ProcesarAsync(ProcessMessageEventArgs args);

    protected virtual Task AlIniciarAsync(ServiceBusClient cliente) => Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var cliente = servicios.GetService<ServiceBusClient>();
        if (cliente is null)
        {
            Log.LogWarning("Iot:ConnectionString está vacío: no se procesa la cola {Cola}. La aplicación sigue funcionando sin el embebido.", Cola);
            return;
        }

        try
        {
            await AlIniciarAsync(cliente);
            await using var procesador = cliente.CreateProcessor(Cola, new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 2,
            });
            procesador.ProcessMessageAsync += ProcesarAsync;
            procesador.ProcessErrorAsync += e =>
            {
                Log.LogWarning(e.Exception, "Error de Service Bus en la cola {Cola} ({Origen}).", Cola, e.ErrorSource);
                return Task.CompletedTask;
            };
            await procesador.StartProcessingAsync(ct);
            Log.LogInformation("Escuchando la cola {Cola}.", Cola);
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
            }
            await procesador.StopProcessingAsync(CancellationToken.None);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Una cadena mal escrita no debe tumbar la API: se registra y el resto sigue funcionando.
            Log.LogError(ex, "No se pudo iniciar el worker de la cola {Cola}.", Cola);
        }
    }

    protected bool EsViejo(ServiceBusReceivedMessage m) =>
        DateTimeOffset.UtcNow - m.EnqueuedTime > TimeSpan.FromSeconds(Opciones.AntiguedadMaximaSegundos);
}

/// <summary>
/// Cola iot-auth-solicitudes: cuerpo JPEG de la webcam y la propiedad "nfc" con el UID de la tarjeta.
/// Responde en iot-auth-respuestas con el mismo CorrelationId. Las fallas transitorias se reintentan
/// (abandonar) hasta <see cref="MaxIntentos"/>; después se responde negado para no dejar al embebido esperando.
/// </summary>
internal sealed class ProcesadorAccesosIot(IServiceProvider servicios, OpcionesServiceBusIot opciones, ILogger<ProcesadorAccesosIot> log)
    : WorkerServiceBus(servicios, opciones, log)
{
    public const int MaxIntentos = 3;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private ServiceBusSender? _respuestas;

    protected override string Cola => Opciones.ColaSolicitudesAuth;

    protected override Task AlIniciarAsync(ServiceBusClient cliente)
    {
        _respuestas = cliente.CreateSender(Opciones.ColaRespuestasAuth);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        if (_respuestas is not null)
            await _respuestas.DisposeAsync();
    }

    protected override async Task ProcesarAsync(ProcessMessageEventArgs args)
    {
        var m = args.Message;
        var nfc = m.ApplicationProperties.TryGetValue("nfc", out var valor) ? valor?.ToString() : null;

        RespuestaAccesoIot respuesta;
        if (EsViejo(m))
        {
            // Nunca abrir con una solicitud vieja: la persona ya no está esperando frente al lector.
            Log.LogWarning("Solicitud de acceso {Id} descartada por antigua ({Encolada:O}).", m.MessageId, m.EnqueuedTime);
            respuesta = new RespuestaAccesoIot(false, null, null, 0, "Solicitud expirada");
        }
        else
        {
            try
            {
                await using var scope = Scopes.CreateAsyncScope();
                respuesta = await scope.ServiceProvider.GetRequiredService<ServicioAccesoIot>()
                    .EvaluarAsync(nfc, m.Body.ToArray(), args.CancellationToken);
            }
            catch (Exception ex) when (m.DeliveryCount < MaxIntentos && !args.CancellationToken.IsCancellationRequested)
            {
                Log.LogWarning(ex, "Falló la solicitud de acceso {Id} (intento {Intento}); se reintenta.", m.MessageId, m.DeliveryCount);
                await args.AbandonMessageAsync(m, cancellationToken: CancellationToken.None);
                return;
            }
            catch (Exception ex) when (!args.CancellationToken.IsCancellationRequested)
            {
                Log.LogError(ex, "La solicitud de acceso {Id} falló {Intentos} veces; se responde negada.", m.MessageId, m.DeliveryCount);
                respuesta = new RespuestaAccesoIot(false, null, null, 0, "Servicio no disponible, intenta de nuevo");
            }
        }

        var salida = new ServiceBusMessage(BinaryData.FromObjectAsJson(respuesta, Json))
        {
            CorrelationId = m.CorrelationId ?? m.MessageId,
            Subject = "auth",
            ContentType = "application/json",
            TimeToLive = TimeSpan.FromMinutes(2),
        };
        await _respuestas!.SendMessageAsync(salida, args.CancellationToken);
        await args.CompleteMessageAsync(m, args.CancellationToken);
        Log.LogInformation("Acceso NFC {Nfc}: {Resultado} ({Motivo}, confianza {Confianza:P0}).",
            nfc, respuesta.Permitido ? "permitido" : "negado", respuesta.Motivo, respuesta.Confianza);
    }
}

/// <summary>
/// Cola iot-venta-escaneos: JSON { "id": "UID de fábrica de la etiqueta", "familia": "código NFC del producto" }.
/// Cada etiqueta es una pieza física. Si alguien tiene el modo inteligente vigente, la pieza va a su venta
/// (o se le avisa por qué no: ya vendida, familia desconocida…). Si nadie lo tiene, es una comprobación de stock.
/// Si el mensaje trae CorrelationId, se responde en iot-escaneos-respuestas con el resultado y el nivel de stock
/// (verde / amarillo / rojo) para la pantalla y los LEDs del embebido.
/// </summary>
internal sealed class ProcesadorEscaneosIot(IServiceProvider servicios, OpcionesServiceBusIot opciones, ILogger<ProcesadorEscaneosIot> log)
    : WorkerServiceBus(servicios, opciones, log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private ServiceBusSender? _respuestas;

    protected override string Cola => Opciones.ColaEscaneosVenta;

    protected override Task AlIniciarAsync(ServiceBusClient cliente)
    {
        _respuestas = cliente.CreateSender(Opciones.ColaRespuestasEscaneos);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        if (_respuestas is not null)
            await _respuestas.DisposeAsync();
    }

    protected override async Task ProcesarAsync(ProcessMessageEventArgs args)
    {
        var m = args.Message;
        if (EsViejo(m))
        {
            // Nadie sigue esperando frente al lector: no se vende ni se responde.
            Log.LogWarning("Escaneo {Id} descartado por antiguo ({Encolado:O}).", m.MessageId, m.EnqueuedTime);
            await args.CompleteMessageAsync(m, args.CancellationToken);
            return;
        }

        string? uid, familia;
        Guid? usuarioId;
        try
        {
            using var json = JsonDocument.Parse(m.Body.ToMemory());
            (uid, familia) = (Texto(json.RootElement, "id"), Texto(json.RootElement, "familia"));
            // Quien tiene sesión en el embebido: de su almacén sale el stock al comprobar.
            usuarioId = Guid.TryParse(Texto(json.RootElement, "usuarioId"), out var u) ? u : null;
        }
        catch (JsonException)
        {
            Log.LogWarning("Escaneo {Id} con cuerpo que no es JSON; se descarta.", m.MessageId);
            await args.CompleteMessageAsync(m, args.CancellationToken);
            return;
        }

        // Si falla la base se abandona; Service Bus lo manda a dead-letter tras su MaxDeliveryCount.
        await using var scope = Scopes.CreateAsyncScope();
        var (resultado, productoId) = await scope.ServiceProvider.GetRequiredService<ServicioEscaneosIot>()
            .RegistrarAsync(uid, familia, args.CancellationToken);
        var stock = scope.ServiceProvider.GetRequiredService<ServicioConsultaStockIot>();
        RespuestaEscaneoIot respuesta;
        if (resultado == ResultadoEscaneo.SinSesion)
        {
            // Nadie está vendiendo con el lector: la pieza sirve para comprobar el stock de su producto.
            respuesta = await stock.ConsultarAsync(uid, familia, usuarioId, args.CancellationToken);
            Log.LogInformation("Comprobación de stock {Uid} ({Familia}): {Motivo}, existencia {Existencia}, nivel {Nivel}.",
                uid, familia, respuesta.Motivo, respuesta.Existencia, respuesta.Nivel);
        }
        else
        {
            respuesta = await stock.RespuestaVentaAsync(resultado, productoId, args.CancellationToken);
            if (resultado == ResultadoEscaneo.Registrado)
                Log.LogInformation("Escaneo NFC {Uid} ({Familia}): producto {Producto} agregado a la venta inteligente.", uid, familia, productoId);
            else
                Log.LogWarning("Escaneo NFC {Uid} ({Familia}) no agregado: {Resultado}.", uid, familia, resultado);
        }

        // Solo responde si el embebido lo pidió (CorrelationId); así un lector viejo que no escucha respuestas no llena la cola.
        if (!string.IsNullOrEmpty(m.CorrelationId))
            await _respuestas!.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(respuesta, Json))
            {
                CorrelationId = m.CorrelationId,
                Subject = "escaneo",
                ContentType = "application/json",
                TimeToLive = TimeSpan.FromMinutes(1),
            }, args.CancellationToken);
        await args.CompleteMessageAsync(m, args.CancellationToken);
    }

    /// <summary>La familia puede venir como texto o como número.</summary>
    private static string? Texto(JsonElement raiz, string propiedad) =>
        raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty(propiedad, out var p)
            ? p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.GetRawText(),
                _ => null,
            }
            : null;
}
