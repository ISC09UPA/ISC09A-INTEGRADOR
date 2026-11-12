# InvernaderoInteligente.Functions (SCRUM-24)

Azure Function (.NET 10, modelo aislado) que lee la cola de Service Bus donde IoT Hub
enruta la telemetría de la Raspberry y la guarda en Telemetría llamando a la API.

```
Raspberry ──MQTT──▶ IoT Hub ──ruta──▶ Service Bus (cola "telemetria-ingesta") ──▶ ProcesarTelemetria ──HTTP──▶ API /api/Lecturas, /api/Riegos
```

**Contrato completo (Raspberry y API): [`../CONTRATOS.md`](../CONTRATOS.md).** Ejemplos de mensajes en `Ejemplos/`.

## Estructura

| Archivo | Qué hace |
|---|---|
| `Functions/ProcesarTelemetria.cs` | Trigger de Service Bus: lee, valida, llama a la API, completa o manda a dead-letter |
| `Services/TelemetriaApiClient.cs` | `HttpClient` tipado a la API; traduce códigos HTTP a éxito / error permanente / temporal |
| `Models/MensajeTelemetria.cs` | Sobre del mensaje + catálogo de tipos de lectura |
| `Models/MensajeInvalidoException.cs` | Error permanente → dead-letter |
| `Program.cs` | DI, Application Insights, configuración del `HttpClient` |

Los DTOs (`CrearLecturaDto`, `CrearRiegoDto`) vienen de `InvernaderoInteligente.Application`: un solo contrato para API y Function.

## Manejo de errores

| Situación | Qué pasa |
|---|---|
| Guardado correcto, o la API responde 409 (ya existía) | El mensaje se completa |
| JSON roto, datos inválidos, >200 lecturas, o la API responde 4xx de datos | Dead-letter inmediato con el motivo |
| API caída, 5xx, 404, 408, 429, 401/403 o timeout | Se reintenta; tras `MaxDeliveryCount` va a dead-letter |

## Correr en local

1. `cp local.settings.example.json local.settings.json` y poner la cadena del Service Bus (no se sube a Git).
2. Levantar Azurite (`azurite --silent --location .azurite`) para `AzureWebJobsStorage`.
3. Levantar la API: `dotnet run --project ../InvernaderoInteligente.API` (`http://localhost:5010`).
4. `func start` dentro de esta carpeta (Azure Functions Core Tools v4).
5. Mandar `Ejemplos/mensaje-lecturas.json` a la cola desde el Service Bus Explorer del portal
   y verificar con `GET http://localhost:5010/api/Lecturas`.

## Prueba local del Backend

Desde PowerShell, ejecutar `../scripts/Test-BackendLocal.ps1`. El script compila y levanta la API con una base SQLite
temporal y una API key ficticia; prueba autenticación, validaciones, persistencia, duplicados, filtros, CSV y riegos.
No requiere Azure ni modifica la base local del proyecto.

Esta prueba cubre el tramo API → base de datos. Para comprobar Raspberry → IoT Hub → Service Bus → Function → API
se necesitan una Raspberry o un emisor autorizado, acceso al namespace y a la cola de Service Bus, Azurite y Azure
Functions Core Tools v4. La plantilla `local.settings.example.json` no contiene una llave SAS real; obtenerla de Kevin
por un canal privado y mantenerla únicamente en `local.settings.json` o en la configuración segura de Azure.

### Verificar el flujo real con Azure

1. Instalar y arrancar Azurite y Azure Functions Core Tools v4.
2. Desde la carpeta `InvernaderoInteligente.Functions`, copiar `local.settings.example.json` como `local.settings.json`
   y configurar allí la cadena SAS real de Service Bus
   que Kevin proporcione, además de `ServiceBusQueueName=telemetria-ingesta`,
   `TelemetriaApi__BaseUrl=http://localhost:5010` y la misma `TelemetriaApi__ApiKey` que se configurará para la API.
   `local.settings.json` está excluido de Git; no compartir ni registrar sus valores.
3. En una terminal PowerShell, establecer `TelemetriaApi__ApiKey` para la API sin escribir la clave en el historial:
   ```powershell
   $secureKey = Read-Host 'API key local' -AsSecureString
   $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
   try {
       $env:TelemetriaApi__ApiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
   }
   finally {
       [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
   }
   dotnet run --project ..\InvernaderoInteligente.API
   ```
4. En otra terminal, desde `InvernaderoInteligente.Functions`, iniciar `func start`.
5. En Service Bus Explorer, enviar un mensaje válido con un `fechaHora` UTC nuevo para evitar un duplicado anterior.
   Confirmar que la Function complete el mensaje y que `GET http://localhost:5010/api/Lecturas?sensorId=DHT22-01`
   devuelva las lecturas recién guardadas. Enviar el mismo mensaje otra vez debe completar el reintento por idempotencia
   (la API responde `409` para cada lectura que ya exista).
6. Repetir con `Ejemplos/mensaje-riego.json` y consultar `GET http://localhost:5010/api/Riegos`. Confirmar que aparece
   el riego con su `volumenMl`. Un JSON inválido se debe mover a dead-letter; una API no disponible debe provocar reintento.

Esta verificación confirma el tramo Function → API → SQLite usando la cola real. Para verificar también
Raspberry → IoT Hub, enviar el ciclo desde el dispositivo autorizado y confirmar su enrutamiento en Service Bus.
La parte de Azure requiere las credenciales y permisos reales del equipo; no se sustituye con las pruebas locales.

## Configuración en Azure (Function App)

| App setting | Valor |
|---|---|
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `ServiceBusConnection` | Cadena de la directiva SAS `function-listen` de `sb-invernadero-kevin` |
| `ServiceBusQueueName` | `telemetria-ingesta` |
| `TelemetriaApi__BaseUrl` | URL del App Service de Telemetría |
| `TelemetriaApi__ApiKey` | Llave compartida con la API (header `X-Api-Key`) |

.NET 10 en Linux requiere el plan **Flex Consumption**.
