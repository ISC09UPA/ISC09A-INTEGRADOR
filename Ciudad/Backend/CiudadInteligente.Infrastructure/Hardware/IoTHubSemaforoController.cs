using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventHubs.Consumer;
using CiudadInteligente.Application.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Infrastructure.Hardware;

/// <summary>
/// El ESP32 esta conectado por USB a la Raspberry Pi y la Raspberry se conecta
/// por su cuenta a Azure IoT Hub (Embebidos/RaspberryPi/agente_iot). Este
/// backend puede correr en cualquier lugar con internet: no necesita la IP de
/// la Raspberry ni estar en su misma red.
///
///   Comandos  -> direct method "comando" con {"comando":"SET:1:VERDE"} por la
///                API REST de IoT Hub; la Raspberry contesta con lo que dijo el
///                ESP32 (200 OK:..., 400 ERR:..., 503 sin serial, 504 sin respuesta).
///   Estado    <- telemetria de la Raspberry leida del endpoint compatible con
///                Event Hubs: {"tipo":"est","linea":"EST:0:TODOS_ROJO:500"} al
///                iniciar cada ciclo y {"tipo":"vivo"} como latido. El resto del
///                ciclo lo calcula SemaforoControllerBase con TiemposFirmware.
///
/// Configuracion ("Esp32:Transporte": "IoTHub"):
///   Esp32:IoTHub:DeviceId                  (appsettings.json)  raspberry-cruce04
///   Esp32:IoTHub:GrupoConsumidor           (appsettings.json)  $Default
///   Esp32:IoTHub:ServiceConnectionString   (dotnet user-secrets, NO en el repo)
///   Esp32:IoTHub:EventHubConnectionString  (dotnet user-secrets, NO en el repo)
/// </summary>
public sealed class IoTHubSemaforoController : SemaforoControllerBase, IDisposable
{
    private const string ApiVersion = "2021-04-12";
    private const string NombreMetodo = "comando";

    /// <summary>La Raspberry manda algo al menos cada ~50 s; sin noticias en 90 s = desconectada.</summary>
    private static readonly TimeSpan VentanaVivo = TimeSpan.FromSeconds(90);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly ILogger<IoTHubSemaforoController> _logger;
    private readonly CancellationTokenSource _cts = new();

    private readonly string _host;
    private readonly string _politica;
    private readonly string _llave;
    private readonly string _deviceId;
    private readonly string _eventHubConexion;
    private readonly string _grupoConsumidor;

    private long _ultimoContactoTicks;
    private volatile bool _serialEnRaspberry;

    public IoTHubSemaforoController(IConfiguration config, ILogger<IoTHubSemaforoController> logger)
    {
        _logger = logger;

        // En cmd.exe las comillas simples se guardan como parte del valor: se quitan aqui.
        var servicio = SinComillas(config["Esp32:IoTHub:ServiceConnectionString"]);
        _eventHubConexion = SinComillas(config["Esp32:IoTHub:EventHubConnectionString"]);
        _deviceId = config["Esp32:IoTHub:DeviceId"] ?? "raspberry-cruce04";
        _grupoConsumidor = config["Esp32:IoTHub:GrupoConsumidor"] ?? EventHubConsumerClient.DefaultConsumerGroupName;

        if (string.IsNullOrWhiteSpace(servicio) || string.IsNullOrWhiteSpace(_eventHubConexion))
        {
            throw new InvalidOperationException(
                "Transporte IoTHub sin credenciales: faltan Esp32:IoTHub:ServiceConnectionString y/o " +
                "Esp32:IoTHub:EventHubConnectionString. Ponlas con 'dotnet user-secrets set' " +
                "(ver Embebidos/README.md, seccion Azure IoT Hub).");
        }

        var partes = ParsearCadena(servicio);
        _host = Requerido(partes, "HostName");
        _politica = Requerido(partes, "SharedAccessKeyName");
        _llave = Requerido(partes, "SharedAccessKey");

        _ = Task.Run(() => LeerTelemetriaAsync(_cts.Token));
    }

    protected override bool EstaConectado =>
        _serialEnRaspberry &&
        DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref _ultimoContactoTicks), TimeSpan.Zero) < VentanaVivo;

    // ------------------------------------------------------------------
    // Comandos: direct method por la API REST de IoT Hub
    // ------------------------------------------------------------------

    protected override async Task EnviarLineaAsync(string comando)
    {
        var url = $"https://{_host}/twins/{Uri.EscapeDataString(_deviceId)}/methods?api-version={ApiVersion}";
        var cuerpo = JsonSerializer.Serialize(new
        {
            methodName = NombreMetodo,
            responseTimeoutInSeconds = 10,
            connectTimeoutInSeconds = 0,
            payload = new { comando }
        });

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };
        solicitud.Headers.TryAddWithoutValidation("Authorization", CrearTokenSas());

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.SendAsync(solicitud);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new HardwareNoDisponibleException(
                "No se pudo contactar a Azure IoT Hub. Revisa la conexion a internet de esta PC.", ex);
        }

        using (respuesta)
        {
            var texto = await respuesta.Content.ReadAsStringAsync();

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                MarcarContacto(false);
                throw new HardwareNoDisponibleException(
                    $"La Raspberry ({_deviceId}) no esta conectada a IoT Hub. Revisa que agente_iot.py este " +
                    "corriendo y tenga internet, y que Esp32:IoTHub:DeviceId sea el nombre correcto.");
            }

            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new HardwareNoDisponibleException(
                    "IoT Hub rechazo las credenciales: revisa Esp32:IoTHub:ServiceConnectionString (politica 'service').");
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                throw new HardwareNoDisponibleException(
                    $"IoT Hub respondio {(int)respuesta.StatusCode} al mandar {comando}: {Recortar(texto)}");
            }

            var (status, respuestaEsp32) = LeerRespuestaMetodo(texto);
            switch (status)
            {
                case 200:
                    MarcarContacto(true);
                    break;
                case 400:
                    MarcarContacto(true);
                    _logger.LogWarning("El ESP32 rechazo {Comando}: {Respuesta}", comando, respuestaEsp32);
                    break;
                case 503:
                    MarcarContacto(false);
                    throw new HardwareNoDisponibleException(
                        "La Raspberry esta conectada a IoT Hub pero no tiene abierto el puerto serial del ESP32.");
                case 504:
                    throw new HardwareNoDisponibleException(
                        "La Raspberry recibio el comando pero el ESP32 no contesto. Revisa el cable USB y que el " +
                        "ESP32 tenga cargado semaforo_v2.ino.");
                default:
                    throw new HardwareNoDisponibleException($"Respuesta inesperada de la Raspberry ({status}).");
            }
        }
    }

    private static (int Status, string? Respuesta) LeerRespuestaMetodo(string json)
    {
        // {"status":200,"payload":{"respuesta":"OK:SET"}}
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            var status = raiz.TryGetProperty("status", out var s) && s.TryGetInt32(out var n) ? n : -1;
            string? texto = null;
            if (raiz.TryGetProperty("payload", out var payload) &&
                payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("respuesta", out var r) &&
                r.ValueKind == JsonValueKind.String)
            {
                texto = r.GetString();
            }
            return (status, texto);
        }
        catch (JsonException)
        {
            return (-1, null);
        }
    }

    /// <summary>Token SAS de la politica de servicio, valido 1 hora (formato de la API REST de IoT Hub).</summary>
    private string CrearTokenSas()
    {
        var expira = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var recurso = Uri.EscapeDataString(_host.ToLowerInvariant());
        using var hmac = new HMACSHA256(Convert.FromBase64String(_llave));
        var firma = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{recurso}\n{expira}")));
        return $"SharedAccessSignature sr={recurso}&sig={Uri.EscapeDataString(firma)}&se={expira}&skn={Uri.EscapeDataString(_politica)}";
    }

    // ------------------------------------------------------------------
    // Estado: telemetria de la Raspberry (endpoint compatible con Event Hubs)
    // ------------------------------------------------------------------

    private async Task LeerTelemetriaAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var consumidor = new EventHubConsumerClient(_grupoConsumidor, _eventHubConexion);
                _logger.LogInformation("Escuchando telemetria de {Dispositivo} en IoT Hub", _deviceId);

                await foreach (var evento in consumidor.ReadEventsAsync(startReadingAtEarliestEvent: false, cancellationToken: ct))
                {
                    var datos = evento.Data;
                    if (datos is null) continue;

                    if (datos.SystemProperties.TryGetValue("iothub-connection-device-id", out var origen) &&
                        !string.Equals(origen?.ToString(), _deviceId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ProcesarMensaje(datos.EventBody.ToString());
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Se perdio la lectura de telemetria de IoT Hub; reintento en 5 s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void ProcesarMensaje(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            var tipo = raiz.TryGetProperty("tipo", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

            if (tipo == "est" && raiz.TryGetProperty("linea", out var l) && l.GetString() is { } linea)
            {
                MarcarContacto(true);
                ProcesarTextoRecibido(linea + "\n");
            }
            else if (tipo == "vivo")
            {
                MarcarContacto(raiz.TryGetProperty("serial", out var s) && s.ValueKind == JsonValueKind.True);
            }
        }
        catch (JsonException)
        {
            _logger.LogDebug("Mensaje de telemetria que no es JSON: {Mensaje}", Recortar(json));
        }
    }

    private void MarcarContacto(bool serialAbierto)
    {
        _serialEnRaspberry = serialAbierto;
        Interlocked.Exchange(ref _ultimoContactoTicks, DateTimeOffset.UtcNow.UtcTicks);
    }

    // ------------------------------------------------------------------

    private static Dictionary<string, string> ParsearCadena(string cadena)
    {
        // HostName=x.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey=abc==
        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parte in cadena.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var igual = parte.IndexOf('=');
            if (igual > 0) resultado[parte[..igual]] = parte[(igual + 1)..];
        }
        return resultado;
    }

    private static string Requerido(Dictionary<string, string> partes, string clave) =>
        partes.TryGetValue(clave, out var valor) && !string.IsNullOrWhiteSpace(valor)
            ? valor
            : throw new InvalidOperationException(
                $"Esp32:IoTHub:ServiceConnectionString no trae '{clave}'. Copiala completa de " +
                "'az iot hub connection-string show --policy-name service'.");

    private static string SinComillas(string? valor) =>
        (valor ?? string.Empty).Trim().Trim('\'', '"').Trim();

    private static string Recortar(string texto) => texto.Length <= 300 ? texto : texto[..300] + "...";

    public void Dispose()
    {
        _cts.Cancel();
        _http.Dispose();
        _cts.Dispose();
    }
}
