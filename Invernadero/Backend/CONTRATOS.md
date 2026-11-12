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

## Capas del Backend

- `Domain/Entities` contiene `LecturaSensor` y `Riego`, las entidades persistidas.
- `Application/DTOs` define los contratos HTTP de entrada, salida y filtros; los controladores y la Function usan esos DTOs, no exponen entidades.
- `Application/Interfaces` declara servicios y DAOs. Los servicios convierten DTOs a entidades y entidades a DTOs.
- `Infrastructure/DataAccess` implementa los DAOs con `InvernaderoDbContext` y Entity Framework Core; los proveedores configurados son SQLite local y SQL Server/Azure SQL.
- `Infrastructure/Services` aplica las operaciones de aplicación y usa los DAOs para consultar y guardar.

## Speech (solo desarrollo)

`POST /api/speech/receive` acepta `multipart/form-data` con los campos `audio` y `transcription`.
Solo está habilitado cuando la API se ejecuta en `Development`; fuera de ese entorno responde `404`.
Valida que el audio no esté vacío, sea AAC/M4A/MP4/WAV/WebM y no exceda 15 MB, y que la transcripción
no supere 5000 caracteres. Responde `202 Accepted` con `status: "received"`, `azureSpeechConfigured: false`
y `persisted: false`. No guarda el audio ni la transcripción y no realiza reconocimiento; la conexión con
Azure Speech queda pendiente. El cliente no debe enviar la API key privada de telemetría.

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
| Variable con la llave compartida | `TelemetriaApi__ApiKey` | API, Function |
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
| `origen` | string | sí | `automatico`, `manual` o `ia` |
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

### Situación del contrato API

1. **Idempotencia — implementada.** Índices únicos en `LecturasSensor (SensorId, Tipo, FechaHora)` y
   `Riegos (InvernaderoId, Zona, FechaHoraInicio)`; un duplicado devuelve `409 Conflict`.
2. **`VolumenMl` — implementado.** Se guarda y devuelve mediante `CrearRiegoDto`, `Riego` y `RiegoDto`.
3. **Origen `ia` — implementado.** `CrearRiegoDto.Origen` acepta `automatico`, `manual` e `ia`.
4. **API key — implementada para despliegue.** API y Function comparten `TelemetriaApi__ApiKey`.
   En la API es obligatoria fuera de Development y protege los `POST /api/Lecturas` y `POST /api/Riegos`;
   las solicitudes inválidas reciben `401`. En local Development se puede dejar sin configurar.
5. **Limpieza menor pendiente.** Borrar `WeatherForecastController`/`WeatherForecast.cs`.
6. **Opcional (fase 2).** `POST /api/Lecturas/lote` que reciba `List<CrearLecturaDto>` en una transacción; reduciría
   7 llamadas HTTP a 1 por mensaje. No es necesario para la entrega.

### Exportación CSV para el conjunto de datos

`GET /api/Lecturas/exportar.csv` exporta todas las lecturas coincidentes, ordenadas por fecha ascendente.
Acepta los filtros opcionales `sensorId`, `tipo`, `invernaderoId`, `desde` y `hasta`; las fechas deben ser UTC ISO-8601.
El archivo UTF-8 (con BOM) contiene las columnas `id,sensorId,tipo,valor,unidad,invernaderoId,fechaHora`.
Los números usan punto decimal y las fechas se escriben en UTC ISO-8601, independientemente de la cultura del servidor.
Los campos se escapan como CSV cuando contienen comas, comillas o saltos de línea.

El catálogo de tipos/unidades de la sección 1.1 define las variables de telemetría disponibles. Los registros de riego
también contienen `humedadDetectada`, `umbral`, `duracionSegundos` y `volumenMl`. El repositorio aún no contiene un
modelo entrenado ni resultados de evaluación; no se reportan MAE u otras métricas hasta ejecutar y evaluar el modelo
con los datos reales y el endpoint acordado.

### Configuración de la API

| Variable | Uso | Valor local |
|---|---|---|
| `ConnectionStrings__Invernadero` | Base SQLite local o cadena de Azure SQL | Opcional; sin definir usa `Data Source=invernadero.db` |
| `Database__Provider` | Proveedor de base de datos | `SqlServer` obligatorio en producción; Development usa SQLite por defecto |
| `TelemetriaApi__ApiKey` | Llave compartida para autorizar POST desde la Function | Vacía en Development; obligatoria en producción |
| `Cors__AllowedOrigins` | Orígenes web autorizados, separados por coma | URL exacta del sitio web Expo publicado; localhost en Development |

En el App Service, configurar `ConnectionStrings__Invernadero` con la cadena ADO.NET de `db-invernadero`
(servidor `srv-invernadero-kevin`), `Database__Provider=SqlServer` y `TelemetriaApi__ApiKey` como ajustes
protegidos de la aplicación.
La Function App necesita `ServiceBusConnection`, `ServiceBusQueueName=telemetria-ingesta`,
`TelemetriaApi__BaseUrl` (URL HTTPS del App Service) y la misma `TelemetriaApi__ApiKey`.
La app Expo solo necesita `EXPO_PUBLIC_TELEMETRIA_API_URL` (URL HTTPS pública de la API); no debe contener
`TelemetriaApi__ApiKey` ni secretos. CORS aplica al cliente web, no a Android/iOS nativos; en producción
configurar `Cors__AllowedOrigins` con el origen exacto del sitio Expo Web. El origen se mantiene pendiente hasta
que el frontend Web tenga una URL de publicación; para desarrollo local, agregar el origen que muestre Expo
(normalmente `http://localhost:8081`) al App Service.
No pegar credenciales en este archivo ni en `appsettings.json`. En producción la API falla al iniciar si falta
la conexión SQL o el proveedor explícito, para evitar que las escrituras terminen por error en SQLite local.
En Development con SQLite, la API crea el esquema local con `EnsureCreated`. Azure SQL se actualiza con
migraciones EF Core como paso explícito del despliegue; la API no modifica el esquema de producción al arrancar.
Para agregar una migración desde `Invernadero/Backend`:

```powershell
dotnet ef migrations add NombreDescriptivo `
  --project InvernaderoInteligente.Infrastructure `
  --startup-project InvernaderoInteligente.API `
  --context InvernaderoDbContext `
  --output-dir Data/Migrations

dotnet ef database update `
  --project InvernaderoInteligente.Infrastructure `
  --startup-project InvernaderoInteligente.API `
  --context InvernaderoDbContext
```

Los comandos usan SQL Server por defecto para generar y aplicar migraciones. Configura
`ConnectionStrings__Invernadero` para apuntar a la base prevista antes de ejecutar `database update`.
Para una base ya provisionada, verifica primero su esquema y registra una línea base compatible; no apliques
la migración inicial a ciegas porque intenta crear las tablas e índices.

Configurar los valores sensibles en App Service/Function App o en el gestor local de secretos; no guardarlos en
`appsettings*.json`, `local.settings.example.json` ni en Git. La tabla de nombres de Azure no contiene las llaves SAS
ni valores secretos que Kevin debe proporcionar por un canal privado.

### Estado de la integración de IA

El recurso Custom Vision Prediction existe en South Central US (F0), pero aún no hay una iteración entrenada
y publicada; por ello la app no puede solicitar clasificaciones todavía. El workspace `ml-invernadero` de Azure
Machine Learning tampoco tiene un endpoint de inferencia. No configurar las llaves de predicción en Expo:
cuando el modelo esté listo, integrar la inferencia detrás de la API/Function y mantener las credenciales
solo en configuración protegida del servidor.
