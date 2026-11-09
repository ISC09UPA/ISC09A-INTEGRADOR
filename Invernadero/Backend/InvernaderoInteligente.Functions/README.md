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

## Configuración en Azure (Function App)

| App setting | Valor |
|---|---|
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `ServiceBusConnection` | Cadena de la directiva SAS `function-listen` de `sb-invernadero-kevin` |
| `ServiceBusQueueName` | `telemetria-ingesta` |
| `TelemetriaApi__BaseUrl` | URL del App Service de Telemetría |
| `TelemetriaApi__ApiKey` | Llave compartida con la API (header `X-Api-Key`) |

.NET 10 en Linux requiere el plan **Flex Consumption**.
