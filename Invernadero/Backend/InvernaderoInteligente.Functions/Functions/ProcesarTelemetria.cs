using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using InvernaderoInteligente.Functions.Models;
using InvernaderoInteligente.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace InvernaderoInteligente.Functions.Functions;

/// <summary>
/// SCRUM-24: lee los mensajes que IoT Hub enruta al Service Bus y los guarda en Telemetría
/// llamando a la API (POST /api/Lecturas y POST /api/Riegos).
///
/// Flujo: Raspberry ─MQTT─▶ IoT Hub ─ruta─▶ Service Bus ─▶ esta Function ─HTTP─▶ API Telemetría
/// </summary>
public class ProcesarTelemetria(ITelemetriaApiClient api, ILogger<ProcesarTelemetria> logger)
{
    /// <summary>Propiedad que IoT Hub agrega a cada mensaje enrutado con el id del dispositivo.</summary>
    private const string PropiedadDispositivoIoTHub = "iothub-connection-device-id";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    [Function(nameof(ProcesarTelemetria))]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueueName%", Connection = "ServiceBusConnection", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage mensaje,
        ServiceBusMessageActions acciones,
        CancellationToken ct)
    {
        logger.LogInformation("Mensaje {MessageId} recibido (intento {Intento}).",
            mensaje.MessageId, mensaje.DeliveryCount);

        try
        {
            var datos = Leer(mensaje);
            await ProcesarAsync(datos, mensaje.MessageId, ct);
            await acciones.CompleteMessageAsync(mensaje, ct);
        }
        catch (MensajeInvalidoException ex)
        {
            // Error permanente: directo a dead-letter para revisarlo sin bloquear la cola.
            logger.LogWarning("Mensaje {MessageId} inválido, se manda a dead-letter: {Motivo}",
                mensaje.MessageId, ex.Message);

            await acciones.DeadLetterMessageAsync(
                mensaje,
                deadLetterReason: "MensajeInvalido",
                deadLetterErrorDescription: Recortar(ex.Message, 1000),
                cancellationToken: ct);
        }
        // Cualquier otra excepción (API caída, timeout, 5xx) sube: el mensaje no se completa,
        // Service Bus lo vuelve a entregar y, al pasar MaxDeliveryCount, lo manda a dead-letter.
    }

    private async Task ProcesarAsync(MensajeTelemetria datos, string messageId, CancellationToken ct)
    {
        var dispositivo = datos.DispositivoId ?? "desconocido";

        var tipoRecibido = datos.Tipo ?? datos.TipoMensaje;

        switch (TiposMensaje.Normalizar(tipoRecibido))
        {
            case TiposMensaje.Lectura:
                if (datos.Lecturas is not { Count: > 0 })
                    throw new MensajeInvalidoException("El mensaje de tipo 'lectura' no trae el arreglo 'lecturas'.");

                if (datos.Lecturas.Count > MensajeTelemetria.MaxLecturasPorMensaje)
                    throw new MensajeInvalidoException(
                        $"El mensaje trae {datos.Lecturas.Count} lecturas; el máximo es {MensajeTelemetria.MaxLecturasPorMensaje}.");

                // 1) Validar todo el lote antes de mandar nada a la API.
                foreach (var lectura in datos.Lecturas)
                {
                    lectura.InvernaderoId ??= datos.InvernaderoId;
                    Validar(lectura);

                    if (!TiposLectura.EsConocido(lectura.Tipo))
                        logger.LogWarning("Tipo de lectura fuera del catálogo: '{Tipo}' (sensor {Sensor}, mensaje {MessageId}).",
                            lectura.Tipo, lectura.SensorId, messageId);
                }

                // 2) Guardar. Si la API responde 409 en un reintento, esa lectura ya estaba.
                int creadas = 0, duplicadas = 0;
                foreach (var lectura in datos.Lecturas)
                {
                    var resultado = await api.RegistrarLecturaAsync(lectura, ct);
                    if (resultado == ResultadoRegistro.Creado) creadas++; else duplicadas++;
                }

                logger.LogInformation(
                    "Mensaje {MessageId}: {Creadas} lecturas guardadas, {Duplicadas} ya existían (dispositivo {Dispositivo}).",
                    messageId, creadas, duplicadas, dispositivo);
                break;

            case TiposMensaje.Riego:
                if (datos.Riego is null)
                    throw new MensajeInvalidoException("El mensaje de tipo 'riego' no trae el objeto 'riego'.");

                datos.Riego.InvernaderoId ??= datos.InvernaderoId;
                if (string.IsNullOrWhiteSpace(datos.Riego.Origen))
                    throw new MensajeInvalidoException("El riego no trae 'origen' ('automatico' o 'manual').");
                datos.Riego.Origen = datos.Riego.Origen.Trim().ToLowerInvariant();
                Validar(datos.Riego);

                var res = await api.RegistrarRiegoAsync(datos.Riego, ct);

                logger.LogInformation(
                    "Mensaje {MessageId}: riego {Origen} de {Duracion}s {Estado} (dispositivo {Dispositivo}).",
                    messageId, datos.Riego.Origen, datos.Riego.DuracionSegundos,
                    res == ResultadoRegistro.Creado ? "guardado" : "ya existía", dispositivo);
                break;

            default:
                throw new MensajeInvalidoException(
                    $"tipo desconocido: '{tipoRecibido}'. Se esperaba 'lectura' o 'riego'.");
        }
    }

    /// <summary>Convierte el cuerpo del mensaje en <see cref="MensajeTelemetria"/>.</summary>
    private static MensajeTelemetria Leer(ServiceBusReceivedMessage mensaje)
    {
        var texto = ObtenerTexto(mensaje);

        MensajeTelemetria? datos;
        try
        {
            datos = JsonSerializer.Deserialize<MensajeTelemetria>(texto, OpcionesJson);
        }
        catch (JsonException ex)
        {
            throw new MensajeInvalidoException($"JSON inválido: {ex.Message}");
        }

        if (datos is null)
            throw new MensajeInvalidoException("El mensaje viene vacío.");

        // Si la Raspberry manda el tipo como propiedad del mensaje en lugar de dentro del JSON.
        if (string.IsNullOrWhiteSpace(datos.Tipo) && string.IsNullOrWhiteSpace(datos.TipoMensaje))
        {
            if (mensaje.ApplicationProperties.TryGetValue("tipo", out var tipo)
                || mensaje.ApplicationProperties.TryGetValue("tipoMensaje", out tipo))
                datos.Tipo = tipo?.ToString();
        }

        // IoT Hub siempre agrega el id del dispositivo que mandó el mensaje.
        if (string.IsNullOrWhiteSpace(datos.DispositivoId)
            && mensaje.ApplicationProperties.TryGetValue(PropiedadDispositivoIoTHub, out var dispositivo))
        {
            datos.DispositivoId = dispositivo?.ToString();
        }

        return datos;
    }

    /// <summary>
    /// IoT Hub entrega el cuerpo en base64 si el dispositivo no marca contentType=application/json
    /// y contentEncoding=utf-8. Aquí se aceptan ambos casos.
    /// </summary>
    private static string ObtenerTexto(ServiceBusReceivedMessage mensaje)
    {
        var texto = mensaje.Body.ToString().Trim();
        if (texto.StartsWith('{'))
            return texto;

        try
        {
            var decodificado = Encoding.UTF8.GetString(Convert.FromBase64String(texto)).Trim();
            if (decodificado.StartsWith('{'))
                return decodificado;
        }
        catch (FormatException)
        {
            // No era base64; se reporta abajo como inválido.
        }

        throw new MensajeInvalidoException("El cuerpo del mensaje no es JSON.");
    }

    /// <summary>Aplica los mismos DataAnnotations que la API para no gastar llamadas en datos inválidos.</summary>
    private static void Validar(object dto)
    {
        var errores = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), errores, validateAllProperties: true))
        {
            var detalle = string.Join("; ", errores.Select(e => e.ErrorMessage));
            throw new MensajeInvalidoException($"{dto.GetType().Name} inválido: {detalle}");
        }
    }

    private static string Recortar(string texto, int max) => texto.Length <= max ? texto : texto[..max];
}
