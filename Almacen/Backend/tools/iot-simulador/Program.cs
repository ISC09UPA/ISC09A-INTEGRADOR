// Simulador del embebido (lector NFC + webcam) para probar sin el hardware.
//
//   dotnet run --project tools/iot-simulador -- auth <nfc> <ruta.jpg>
//   dotnet run --project tools/iot-simulador -- escanear <uid> <familia>
//
// La cadena de conexión se lee de la variable de entorno IOT_SERVICEBUS (o de --conexion "<cadena>").
// Usa la regla SAS "embebido" del namespace sb-iot-almacen (Send + Listen).

using System.Text.Json;
using Azure.Messaging.ServiceBus;

const string ColaSolicitudes = "iot-auth-solicitudes";
const string ColaRespuestas = "iot-auth-respuestas";
const string ColaEscaneos = "iot-venta-escaneos";
var espera = TimeSpan.FromSeconds(15);

var argumentos = args.ToList();
string? conexion = Environment.GetEnvironmentVariable("IOT_SERVICEBUS");
var i = argumentos.IndexOf("--conexion");
if (i >= 0 && i + 1 < argumentos.Count)
{
    conexion = argumentos[i + 1];
    argumentos.RemoveRange(i, 2);
}

if (argumentos.Count == 0 || string.IsNullOrWhiteSpace(conexion))
    return Ayuda(string.IsNullOrWhiteSpace(conexion) ? "Falta la cadena de conexión (IOT_SERVICEBUS o --conexion)." : null);

await using var cliente = new ServiceBusClient(conexion);
switch (argumentos[0].ToLowerInvariant())
{
    case "auth" when argumentos.Count == 3:
        return await AutenticarAsync(argumentos[1], argumentos[2]);
    case "escanear" when argumentos.Count == 3:
        return await EscanearAsync(argumentos[1], argumentos[2]);
    default:
        return Ayuda("Comando no reconocido.");
}

async Task<int> AutenticarAsync(string nfc, string ruta)
{
    if (!File.Exists(ruta))
        return Ayuda($"No existe el archivo {ruta}.");

    var correlacion = Guid.NewGuid().ToString("N");
    // El receptor se abre antes de enviar para no perder una respuesta rápida.
    await using var receptor = cliente.CreateReceiver(ColaRespuestas);
    await using var emisor = cliente.CreateSender(ColaSolicitudes);
    var mensaje = new ServiceBusMessage(await File.ReadAllBytesAsync(ruta))
    {
        CorrelationId = correlacion,
        ContentType = "image/jpeg",
        TimeToLive = TimeSpan.FromMinutes(1),
    };
    mensaje.ApplicationProperties["nfc"] = nfc;
    await emisor.SendMessageAsync(mensaje);
    Console.WriteLine($"Solicitud enviada (CorrelationId {correlacion}). Esperando respuesta hasta {espera.TotalSeconds:0} s…");

    // Todas las respuestas llegan a la misma cola: las que no son nuestras se liberan para su dueño.
    var limite = DateTimeOffset.UtcNow + espera;
    while (DateTimeOffset.UtcNow < limite)
    {
        var restante = limite - DateTimeOffset.UtcNow;
        var respuesta = await receptor.ReceiveMessageAsync(restante < TimeSpan.FromSeconds(2) ? restante : TimeSpan.FromSeconds(2));
        if (respuesta is null)
            continue;
        if (respuesta.CorrelationId != correlacion)
        {
            await receptor.AbandonMessageAsync(respuesta);
            continue;
        }
        await receptor.CompleteMessageAsync(respuesta);
        var json = JsonDocument.Parse(respuesta.Body.ToMemory()).RootElement;
        Console.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
        var permitido = json.TryGetProperty("permitido", out var p) && p.GetBoolean();
        Console.WriteLine(permitido ? "ACCESO PERMITIDO" : "ACCESO NEGADO");
        return permitido ? 0 : 2;
    }
    Console.Error.WriteLine("No llegó respuesta a tiempo. ¿La API está corriendo con Iot:ConnectionString?");
    return 3;
}

// id = UID de fábrica de la etiqueta (una por pieza física); familia = código NFC grabado en el producto del catálogo.
async Task<int> EscanearAsync(string id, string familia)
{
    await using var emisor = cliente.CreateSender(ColaEscaneos);
    await emisor.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(new { id, familia }))
    {
        ContentType = "application/json",
        TimeToLive = TimeSpan.FromMinutes(1),
    });
    Console.WriteLine($"Pieza {id} ({familia}) enviada. Si alguien tiene el modo inteligente activo, aparecerá en su venta (o el aviso de por qué no).");
    return 0;
}

static int Ayuda(string? error)
{
    if (error is not null)
        Console.Error.WriteLine(error);
    Console.WriteLine("""
        Uso:
          iot-simulador auth <nfc> <ruta.jpg>   Envía tarjeta + foto y espera la respuesta (15 s).
          iot-simulador escanear <uid> <familia>
                                                Envía el escaneo de una pieza: UID de su etiqueta NFC
                                                (p. ej. 532B84E5030001) y familia = código NFC del producto.
        Opciones:
          --conexion "<cadena>"                 Cadena de Service Bus (por defecto, variable IOT_SERVICEBUS).
        """);
    return 1;
}
