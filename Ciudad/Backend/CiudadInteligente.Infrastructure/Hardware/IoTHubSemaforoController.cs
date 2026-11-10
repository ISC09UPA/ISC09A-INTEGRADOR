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
///                Varios comandos juntos (detener todo, secuencia manual, cambio
///                de modo) van en UN solo direct method "comandos" con
///                {"comandos":[...]}: un viaje a Azure en vez de uno por comando.
///   Estado    <- telemetria de la Raspberry leida del endpoint compatible con
///                Event Hubs: {"tipo":"est","linea":"EST:0:TODOS_ROJO:500","t":ms}
///                al iniciar cada ciclo y {"tipo":"vivo"} como latido. El resto
///                del ciclo lo calcula SemaforoControllerBase con TiemposFirmware,
///                a partir del momento "t" en que el ESP32 imprimio la linea.
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
    private const string NombreMetodoLote = "comandos";

    /// <summary>La Raspberry manda algo al menos cada ~50 s; sin noticias en 90 s = desconectada.</summary>
    private static readonly TimeSpan VentanaVivo = TimeSpan.FromSeconds(90);

    /// <summary>Cada cuanto se toca IoT Hub para que la conexion HTTPS siga abierta.</summary>
    private static readonly TimeSpan IntervaloConexionViva = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Un TRAFICO: al ESP32 cada 30 s como maximo (el ciclo dura ~20 s): son
    /// hasta 2,880 mensajes al dia del limite de 8,000 del plan F1, aunque la
    /// camara o el simulador manden un conteo cada pocos segundos.
    /// </summary>
    protected override TimeSpan IntervaloMinimoTrafico => TimeSpan.FromSeconds(30);

    // Una sola conexion HTTPS reutilizada: abrirla (DNS + TCP + TLS hasta Azure)
    // cuesta ~0.5 s extra. .NET la cierra tras 1 min sin uso; aqui se le da mas
    // tiempo y MantenerConexionAsync la mantiene ocupada.
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(30),
    })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };
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
    /// <summary>false si la Raspberry tiene un agente_iot.py viejo sin el metodo "comandos".</summary>
    private volatile bool _agenteAceptaLotes = true;

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
        _ = Task.Run(() => MantenerConexionAsync(_cts.Token));
    }

    protected override bool EstaConectado =>
        _serialEnRaspberry &&
        DateTimeOffset.UtcNow - new DateTimeOffset(Interlocked.Read(ref _ultimoContactoTicks), TimeSpan.Zero) < VentanaVivo;

    // ------------------------------------------------------------------
    // Comandos: direct method por la API REST de IoT Hub
    // ------------------------------------------------------------------

    protected override async Task EnviarLineaAsync(string comando)
    {
        var (status, payload) = await InvocarMetodoAsync(NombreMetodo, new { comando }, comando);
        InterpretarStatus(status, comando, LeerTexto(payload, "respuesta"));
    }

    /// <summary>Varios comandos en un solo direct method "comandos" (un solo viaje a Azure).</summary>
    protected override async Task EnviarLineasAsync(IReadOnlyList<string> comandos)
    {
        if (comandos.Count == 1 || !_agenteAceptaLotes)
        {
            await base.EnviarLineasAsync(comandos);
            return;
        }

        var descripcion = string.Join(" | ", comandos);
        var (status, payload) = await InvocarMetodoAsync(NombreMetodoLote, new { comandos }, descripcion);

        if (status == 404)
        {
            // agente_iot.py viejo: no conoce "comandos". Se mandan uno por uno.
            _agenteAceptaLotes = false;
            _logger.LogWarning(
                "La Raspberry tiene un agente_iot.py sin el metodo \"comandos\"; se mandan uno por uno " +
                "(mas lento). Copia de nuevo Embebidos/RaspberryPi/agente_iot a la Raspberry.");
            await base.EnviarLineasAsync(comandos);
            return;
        }

        InterpretarStatus(status, descripcion, LeerTexto(payload, "respuesta"));
    }

    /// <summary>
    /// Llama un direct method de la Raspberry por la API REST de IoT Hub.
    /// Regresa el status que contesto agente_iot.py y su payload. Los errores
    /// de red, credenciales o Raspberry desconectada salen como excepcion.
    /// </summary>
    private async Task<(int Status, JsonElement? Payload)> InvocarMetodoAsync(string metodo, object payload, string descripcion)
    {
        var url = $"https://{_host}/twins/{Uri.EscapeDataString(_deviceId)}/methods?api-version={ApiVersion}";
        var cuerpo = JsonSerializer.Serialize(new
        {
            methodName = metodo,
            responseTimeoutInSeconds = 10,
            connectTimeoutInSeconds = 0,
            payload
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
                    $"IoT Hub respondio {(int)respuesta.StatusCode} al mandar {descripcion}: {Recortar(texto)}");
            }

            return LeerRespuestaMetodo(texto);
        }
    }

    private void InterpretarStatus(int status, string descripcion, string? respuestaEsp32)
    {
        switch (status)
        {
            case 200:
                MarcarContacto(true);
                break;
            case 400:
                MarcarContacto(true);
                _logger.LogWarning("El ESP32 rechazo {Comando}: {Respuesta}", descripcion, respuestaEsp32);
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

    private static (int Status, JsonElement? Payload) LeerRespuestaMetodo(string json)
    {
        // {"status":200,"payload":{"respuesta":"OK:SET"}}
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            var status = raiz.TryGetProperty("status", out var s) && s.TryGetInt32(out var n) ? n : -1;
            JsonElement? payload = raiz.TryGetProperty("payload", out var p) ? p.Clone() : null;
            return (status, payload);
        }
        catch (JsonException)
        {
            return (-1, null);
        }
    }

    private static string? LeerTexto(JsonElement? payload, string propiedad) =>
        payload is { ValueKind: JsonValueKind.Object } p &&
        p.TryGetProperty(propiedad, out var valor) &&
        valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    /// <summary>
    /// Toca IoT Hub cada 40 s (una peticion sin credenciales: la rechaza al
    /// instante y no cuenta en el limite de mensajes) para que la conexion
    /// HTTPS siga abierta y el siguiente comando no pague el tiempo de abrirla.
    /// </summary>
    private async Task MantenerConexionAsync(CancellationToken ct)
    {
        var url = $"https://{_host}/devices/{Uri.EscapeDataString(_deviceId)}?api-version={ApiVersion}";
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var respuesta = await _http.GetAsync(url, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No se pudo tocar IoT Hub para mantener la conexion");
            }

            try
            {
                await Task.Delay(IntervaloConexionViva, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
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

                    ProcesarMensaje(datos.EventBody.ToString(), datos.EnqueuedTime);
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

    private void ProcesarMensaje(string json, DateTimeOffset encoladoEn)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            var tipo = raiz.TryGetProperty("tipo", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

            if (tipo == "est" && raiz.TryGetProperty("linea", out var l) && l.GetString() is { } linea)
            {
                MarcarContacto(true);
                ProcesarTextoRecibido(linea + "\n", MomentoDeLaLinea(raiz, encoladoEn));
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

    /// <summary>
    /// Cuando imprimio el ESP32 la linea: el "t" que pone agente_iot.py (reloj
    /// de la Raspberry) si cuadra con la hora en que IoT Hub recibio el mensaje
    /// (reloj de Azure); si no, esa hora de Azure, que llega ~0.1 s despues.
    /// </summary>
    private static DateTimeOffset MomentoDeLaLinea(JsonElement raiz, DateTimeOffset encoladoEn)
    {
        if (raiz.TryGetProperty("t", out var t) && t.TryGetInt64(out var ms))
        {
            var enRaspberry = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            var diferencia = encoladoEn - enRaspberry;
            if (diferencia >= TimeSpan.FromSeconds(-1) && diferencia <= TimeSpan.FromSeconds(3))
            {
                return enRaspberry;
            }
        }
        return encoladoEn;
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
