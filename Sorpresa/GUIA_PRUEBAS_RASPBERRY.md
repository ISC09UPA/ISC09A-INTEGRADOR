# LockerSmart – preparación del backend para pruebas con Raspberry Pi

## Cambios incluidos

- Se agregó `ComandoDispositivo` y el enum `ComandoEstado` para persistir cada comando enviado.
- `POST /api/dispositivos/rentas/{rentaId}/abrir` ahora registra el comando antes de enviarlo a Service Bus. Actualiza el estado a `Enviado` o `ErrorEnvio`.
- `DeviceEventMessage` admite `commandId` opcional.
- `AzureServiceBusEventsWorker` procesa `ComandoEjecutado` y `ComandoRechazado`, verifica que `CommandId` exista y corresponda al locker, y actualiza el historial.
- Se agregaron endpoints para consultar el estado de un comando y para que administración consulte historial de comandos/eventos.
- Se agregó la migración `20261127020000_AgregarHistorialComandosDispositivo` y se actualizó el snapshot de EF Core.
- Se incluye colección Postman importable.

## 1. Configuración requerida

Desde la carpeta `backend/ProyectoInteligente.API`, configura los valores mediante User Secrets (no pegues secretos en el repositorio ni en Postman):

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena Azure SQL>"
dotnet user-secrets set "AzureServiceBus:ConnectionString" "<cadena Service Bus>"
dotnet user-secrets set "AzureServiceBus:CommandsQueue" "locker-commands"
dotnet user-secrets set "AzureServiceBus:EventsQueue" "locker-events"
dotnet user-secrets set "Jwt:Key" "<clave JWT segura de al menos 32 bytes>"
dotnet user-secrets set "Jwt:Issuer" "LockerSmart"
dotnet user-secrets set "Jwt:Audience" "LockerSmartUsers"
dotnet user-secrets set "Admin:Username" "lockeradmin"
dotnet user-secrets set "Admin:Password" "<contraseña temporal fuerte>"
```

Conserva también las claves ya requeridas por tu configuración de Azure Blob Storage y Azure Face. No reemplaces ni compartas valores reales.

La identidad/conexión de Service Bus usada por el backend necesita permiso de envío en `locker-commands` y recepción/escucha en `locker-events`. La Raspberry necesita recepción/escucha en `locker-commands` y envío en `locker-events`. Si usas cadenas de conexión SAS, asigna permisos mínimos por entidad.

## 2. Compilar y aplicar migración

Abre PowerShell en la carpeta `backend`:

```powershell
dotnet restore .\ProyectoInteligente.slnx
dotnet build .\ProyectoInteligente.slnx
```

La aplicación ya ejecuta `Database.MigrateAsync()` al iniciar. Por eso, al ejecutar el API aplicará la migración pendiente automáticamente. Para inspeccionar antes:

```powershell
dotnet ef migrations list --project .\ProyectoInteligente.Infrastructure --startup-project .\ProyectoInteligente.API
dotnet ef migrations script --project .\ProyectoInteligente.Infrastructure --startup-project .\ProyectoInteligente.API
```

Después inicia:

```powershell
cd .\ProyectoInteligente.API
dotnet run --launch-profile http
```

Base URL local: `http://localhost:5106`.

## 3. Importar Postman

Importa `postman/LockerSmart_Pruebas_Raspberry.postman_collection.json`.

Variables de colección:
- `baseUrl`: `http://localhost:5106`
- `token`: token JWT de la sesión.
- `rentaId`: id de una renta activa.
- `commandId`: lo captura automáticamente la prueba de apertura cuando recibe 202.

Flujo recomendado:
1. Ejecuta `Login facial` usando form-data, clave `Foto` tipo File. Requiere usuario registrado con rostro y Azure Face configurado. La colección guarda `body.token`.
2. Crea una renta con `Crear renta`, o usa una renta activa que ya pertenezca al usuario.
3. Actualiza `rentaId` si utilizaste una renta existente.
4. Ejecuta `Solicitar apertura`. Debe responder `202` y devolver `commandId`.
5. Consulta `Consultar resultado del comando`. Inicialmente el estado será `Enviado`; cambiará cuando la Raspberry publique confirmación.

**No confundas login de contraseña con login facial:** el token de `/api/auth/login` sirve para autenticación de la aplicación, pero no incluye `face_verified=true`; la ruta de apertura lo rechazará con 403. La apertura requiere el token devuelto por `/api/auth/login-rostro`.

## 4. Contratos Service Bus para Raspberry Pi

### Comando que recibe la Raspberry (`locker-commands`)

```json
{
  "commandId": "9d5c4f...",
  "accion": "Abrir",
  "lockerCodigo": "L-01",
  "rentaId": 12,
  "usuarioId": 4,
  "creadoUtc": "2026-11-27T01:00:00Z",
  "motivo": "Acceso autorizado mediante reconocimiento facial"
}
```

La Raspberry debe tratar `commandId` como clave de idempotencia: si recibe de nuevo el mismo comando, no debe accionar dos veces el relevador; debe volver a publicar el resultado original. No energices directamente una cerradura desde un GPIO.

### Confirmación de apertura exitosa (`locker-events`)

```json
{
  "eventId": "evt-uuid-unico",
  "commandId": "9d5c4f...",
  "lockerCodigo": "L-01",
  "tipo": "ComandoEjecutado",
  "timestampUtc": "2026-11-27T01:00:03Z",
  "puertaCerrada": false,
  "detalle": "Relevador activado; sensor confirma puerta abierta"
}
```

### Confirmación rechazada (`locker-events`)

```json
{
  "eventId": "evt-uuid-unico-2",
  "commandId": "9d5c4f...",
  "lockerCodigo": "L-01",
  "tipo": "ComandoRechazado",
  "timestampUtc": "2026-11-27T01:00:03Z",
  "puertaCerrada": true,
  "detalle": "Puerta bloqueada o sensor no confirmó apertura"
}
```

`eventId` debe ser único por evento; `commandId` debe ser el mismo que llegó en el comando. Los nombres de propiedades se envían en camelCase. El tipo debe ser exactamente `ComandoEjecutado` o `ComandoRechazado` (se compara sin distinguir mayúsculas/minúsculas).

### Telemetría y devolución

Los eventos de telemetría conservan el contrato anterior: `eventId`, `lockerCodigo`, `tipo`, `timestampUtc`, `pesoKg`, `puertaCerrada`, `presenciaDetectada`, `rentaId` y `detalle`. La devolución sigue requiriendo `tipo: "DevolucionConfirmada"`, `rentaId`, `pesoKg`, `puertaCerrada: true` y `presenciaDetectada: false`.

## 5. Endpoints de consulta agregados

- `GET /api/dispositivos/comandos/{commandId}`: propietario del comando o administrador.
- `GET /api/dispositivos/comandos?cantidad=50`: solo administrador.
- `GET /api/dispositivos/eventos?cantidad=50`: solo administrador.

`cantidad` se limita entre 1 y 200.

## 6. Pruebas de aceptación antes de conectar actuadores

| Prueba | Acción | Resultado esperado |
|---|---|---|
| API-01 | Login con contraseña y abrir | 403 (no contiene `face_verified`) |
| API-02 | Login facial y abrir con renta propia activa | 202 + `commandId` |
| API-03 | Consultar comando inmediatamente | Estado `Enviado` |
| SB-01 | Raspberry recibe comando | JSON contiene `commandId`, `accion`, `lockerCodigo` |
| SB-02 | Publicar `ComandoEjecutado` | Comando pasa a `Ejecutado` |
| SB-03 | Publicar `ComandoRechazado` | Comando pasa a `Rechazado` |
| SB-04 | Repetir mismo `eventId` | No duplica ni reprocesa el evento |
| SB-05 | Confirmar con `commandId` inexistente | Mensaje a dead-letter como evento no procesable |
| API-04 | Usuario distinto consulta comando ajeno | 403 |
| API-05 | Usuario consulta comando inexistente | 404 |

Para probar Service Bus antes de tener sensores conectados, usa Azure Portal/Service Bus Explorer o un pequeño simulador Python que consuma `locker-commands` y publique los eventos de ejemplo en `locker-events`. No simules `ComandoEjecutado` como confirmación física definitiva en el informe: solo valida el circuito de mensajería.

## 7. Consideraciones de seguridad y hardware

- No guardes connection strings, JWT keys ni contraseñas en Git, ZIP público, Postman o scripts compartidos.
- GPIO de Raspberry Pi trabaja a 3.3 V y no tolera 5 V; nunca conectes cerradura ni bobina directamente a GPIO. Usa driver/relé adecuado, fuente externa y protección eléctrica dimensionada al actuador.
- Antes de una prueba física, verifica polaridad, tensión/corriente de la cerradura, aislamiento, estado de reposo del relevador y lectura de puerta. Empieza con fuente y actuador desacoplados del mecanismo.
- El estado `Ejecutado` significa que el backend recibió la confirmación que emitió el dispositivo; la calidad de esa confirmación depende de que el firmware compruebe realmente el sensor de puerta.

## Nota de validación

Los archivos fuente y la colección Postman se prepararon sobre `LockerSmart_backend_revision_1.zip`. En este entorno no está instalado el SDK de .NET (`dotnet` no está disponible), por lo que no fue posible ejecutar `dotnet build`, generar el SQL mediante EF ni validar contra tu Azure SQL/Service Bus. Ejecuta los comandos de la sección 2 en tu equipo antes de conectar el actuador. Si `dotnet build` informa errores, comparte el texto del error sin secretos y se corrigen sobre esta misma versión.
