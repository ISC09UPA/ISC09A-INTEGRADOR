# Contratos de integración · Invernadero Inteligente

> Dueño: Kevin (Infra Azure, CI/CD, Function de ingesta SCRUM-24) · Versión 1.1 · 6 nov 2026
> Si alguien necesita cambiar un campo, lo avisa antes en el grupo: la Function y la API comparten los DTOs
> de `InvernaderoInteligente.Application/DTOs`, así que el CI rompe si alguien los cambia sin avisar.

```
Raspberry (Juan) ─MQTT─▶ IoT Hub ─ruta─▶ Service Bus (cola "telemetria-ingesta") ─▶ Function ProcesarTelemetria (Kevin)
                                                                              │ HTTP POST
                                                                              ▼
                                                              API Telemetría (Danna / Donnovan)
                                                              POST /api/Lecturas · POST /api/Riegos
```

---

## 0. Nombres fijos (NO se modifican)

Estos nombres ya existen en Azure (SCRUM-19) o están amarrados al CI. Si alguien los cambia en su código, se rompe
la conexión. Las **llaves** no van aquí: Kevin las pasa por mensaje privado.

| Qué | Valor | Lo usa |
|---|---|---|
| Grupo de recursos | `rg-invernadero` | Kevin |
| IoT Hub | `iothub-invernadero-kevin` (nivel Free F1) | Juan, Kevin |
| Dispositivo | `rpi-invernadero-01` (clave simétrica) | Juan |
| Ruta IoT Hub → Service Bus | `ruta-telemetria` → endpoint `ep-sb-telemetria` | Kevin |
| Service Bus (namespace) | `sb-invernadero-kevin` | Kevin |
| Cola | `telemetria-ingesta` (máx. 10 entregas, luego dead-letter) | Function |
| Directiva SAS de la Function | `function-listen` (solo Escuchar) | Function |
| Campo raíz del mensaje | `tipo` = `"lectura"` \| `"riego"` | Juan, Function |
| Rutas API | `POST /api/Lecturas`, `POST /api/Riegos` | API, Function |
| Header de seguridad | `X-Api-Key` | API, Function |
| App settings de la Function | `ServiceBusConnection`, `ServiceBusQueueName`, `TelemetriaApi__BaseUrl`, `TelemetriaApi__ApiKey` | Kevin |
| Microservicios (SCRUM-7) | `Telemetria`, `Usuarios`, `AlertasIA` | Back |
| Ramas con CI | `Invernadero-Develop-V1` y `feature/SCRUM-*` | Todos |
| Puerto local de la API | `http://localhost:5010` | Back, Function |

---

## 1. Raspberry → IoT Hub (Juan)

### Reglas generales
- Cada mensaje es **un JSON** con el campo raíz `tipo` = `"lectura"` o `"riego"` (lo mismo que se acordó en SCRUM-19).
- Nombres de campo en **camelCase** exactamente como abajo.
- Fechas en **UTC ISO-8601 con `Z`**: `"2026-11-05T16:30:00Z"`. Usar la hora en que se **midió**, no la del envío
  (importante al sincronizar lo guardado offline en SQLite).
- En el SDK de Python marcar el mensaje:
  ```python
  msg = Message(json.dumps(payload))
  msg.content_type = "application/json"
  msg.content_encoding = "utf-8"
  ```
  (si no, IoT Hub lo manda en base64; la Function lo soporta pero es más fácil depurar en JSON).
- Máximo **200 lecturas por mensaje**. Al reconectar después de estar offline, mandar el historial en varios mensajes.
- `dispositivoId` es opcional (IoT Hub ya lo agrega), pero ponerlo ayuda en los logs. Valor: `rpi-invernadero-01`.
- Si la red bloquea el puerto 8883, conectar con `websockets=True` (MQTT sobre 443).
- IoT Hub está en nivel **gratuito** (tiene límite diario de mensajes): mandar **un mensaje por ciclo** con todas las
  lecturas juntas, no un mensaje por sensor.
- El campo `tipo` de la raíz (`lectura`/`riego`) es distinto del `tipo` de cada lectura (`temperatura`, `luz`…).

### 1.1 Mensaje `lectura` (cada ciclo de medición)
```json
{
  "tipo": "lectura",
  "dispositivoId": "rpi-invernadero-01",
  "invernaderoId": 1,
  "lecturas": [
    { "sensorId": "DHT22-01",  "tipo": "temperatura",    "valor": 24.6, "unidad": "°C",    "fechaHora": "2026-11-05T16:30:00Z" },
    { "sensorId": "DHT22-01",  "tipo": "humedad_aire",   "valor": 61.2, "unidad": "%",     "fechaHora": "2026-11-05T16:30:00Z" },
    { "sensorId": "SUELO-01",  "tipo": "humedad_suelo",  "valor": 34.0, "unidad": "%",     "fechaHora": "2026-11-05T16:30:00Z" },
    { "sensorId": "BH1750-01", "tipo": "luz",            "valor": 820,  "unidad": "lux",   "fechaHora": "2026-11-05T16:30:00Z" },
    { "sensorId": "NIVEL-01",  "tipo": "nivel_deposito", "valor": 72.5, "unidad": "%",     "fechaHora": "2026-11-05T16:30:00Z" },
    { "sensorId": "FLUJO-01",  "tipo": "caudal",         "valor": 0.0,  "unidad": "L/min", "fechaHora": "2026-11-05T16:30:00Z" },
    { "sensorId": "FLOT-01",   "tipo": "flotador_vacio", "valor": 0,    "unidad": "bool",  "fechaHora": "2026-11-05T16:30:00Z" }
  ]
}
```

| Campo | Tipo | Obligatorio | Regla |
|---|---|---|---|
| `sensorId` | string | sí | máx. 50 caracteres, fijo por sensor físico |
| `tipo` | string | sí | uno del catálogo de abajo (máx. 30) |
| `valor` | número | sí | `flotador_vacio`: `1` = vacío, `0` = con agua |
| `unidad` | string | sí | la del catálogo (máx. 10) |
| `fechaHora` | string UTC | recomendado | si falta, se usa la hora del servidor |
| `invernaderoId` | int | no | si falta en la lectura, se copia el de la raíz |

**Catálogo de `tipo` / `unidad`** (alineado con la app de Edward):

| tipo | unidad | Sensor |
|---|---|---|
| `temperatura` | `°C` | DHT22 |
| `humedad_aire` | `%` | DHT22 |
| `humedad_suelo` | `%` | capacitivo + ADS1115 |
| `luz` | `lux` | BH1750 / LDR |
| `nivel_deposito` | `%` | sensor de nivel |
| `caudal` | `L/min` | caudalímetro |
| `flotador_vacio` | `bool` | flotador (0/1) |

Un `tipo` fuera del catálogo se guarda pero queda un warning; si se necesita uno nuevo, avisar para agregarlo.

### 1.2 Mensaje `riego` (cada vez que termina un riego)
```json
{
  "tipo": "riego",
  "dispositivoId": "rpi-invernadero-01",
  "invernaderoId": 1,
  "riego": {
    "zona": "Zona A",
    "fechaHoraInicio": "2026-11-05T16:31:00Z",
    "duracionSegundos": 45,
    "origen": "automatico",
    "humedadDetectada": 28.5,
    "umbral": 30.0,
    "volumenMl": 250
  }
}
```

| Campo | Tipo | Obligatorio | Regla |
|---|---|---|---|
| `duracionSegundos` | int | sí | 1 – 86400 |
| `origen` | string | sí | `automatico` o `manual` |
| `fechaHoraInicio` | string UTC | recomendado | |
| `zona` | string | no | máx. 50 (zona/planta regada) |
| `humedadDetectada` | número | solo automático | humedad de suelo que disparó el riego |
| `umbral` | número | solo automático | umbral que se cruzó |
| `volumenMl` | número | **sí, mandarlo ya** | ver pendiente API #2; hasta que la API lo agregue se ignora |

---

## 2. API Telemetría (Danna / Donnovan) → lo que la Function necesita

**Ya existe y funciona** (auditado en `Invernadero-Develop-V1`): `LecturasController` y `RiegosController`
con sus servicios en Infrastructure. La Function usa exactamente esto:

| Método | Ruta | Body | Respuesta que espera la Function |
|---|---|---|---|
| POST | `/api/Lecturas` | `CrearLecturaDto` (una lectura) | `201 Created` + `LecturaDto` |
| POST | `/api/Riegos` | `CrearRiegoDto` | `201 Created` + `RiegoDto` |

Cómo interpreta la Function cada código:

| Código de la API | Qué hace la Function |
|---|---|
| 2xx | OK, sigue |
| **409 Conflict** | "ya existía" → lo toma como éxito (idempotencia) |
| 400 / 422 / otro 4xx | dato inválido → mensaje a dead-letter, no reintenta |
| 401 / 403 | API key mal configurada → reintenta |
| 404, 408, 429, 5xx, timeout | temporal → Service Bus reintenta |

### Pendientes para la API (pedidos de Kevin)

1. **Idempotencia (prioridad alta).** Si un mensaje con 7 lecturas falla en la 5ª, Service Bus lo reintenta y las
   primeras 4 se duplicarían. Pedido:
   - Índice **único** en `LecturasSensor (SensorId, Tipo, FechaHora)` y en `Riegos (InvernaderoId, Zona, FechaHoraInicio)`.
   - Si el POST choca con el índice, responder **`409 Conflict`** (no 500).
2. **`VolumenMl` en riegos.** El objetivo del proyecto pide registrar "cuánta agua" y la app ya muestra `volumenMl`.
   Agregar `public double? VolumenMl { get; set; }` a `Riego`, `CrearRiegoDto` y `RiegoDto`. La Function lo empieza
   a mandar sola en cuanto exista (comparte el DTO), sin cambios de mi lado.
3. **Origen `ia`.** La app (Edward) maneja `origen: 'ia'`, pero la API solo acepta `automatico|manual`. Decidir entre
   todos: o se agrega `ia` al regex de `CrearRiegoDto.Origen` o la app deja de usarlo.
4. **API key (para despliegue).** Cuando la API esté en App Service, validar el header **`X-Api-Key`** en los POST.
   La Function ya lo manda si se configura `TelemetriaApi__ApiKey`. En local se deja vacío.
5. **Limpieza menor.** Borrar `WeatherForecastController`/`WeatherForecast.cs` y actualizar `InvernaderoInteligente.API.http`
   con ejemplos de `/api/Lecturas` y `/api/Riegos`.
6. **Opcional (fase 2).** `POST /api/Lecturas/lote` que reciba `List<CrearLecturaDto>` en una transacción; reduciría
   7 llamadas HTTP a 1 por mensaje. No es necesario para la entrega.

Nada de esto cambia rutas ni nombres existentes; la Function ya funciona hoy contra la API tal como está.
