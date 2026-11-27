# Contratos

**Última actualización:** 27 de noviembre de 2026
**Fuente formal:** `Casa/contracts/v1`, `Casa/contracts/v2`

---

## 1. Principios

- HTTP 202 significa **admisión pendiente**, no ejecución.
- La ejecución física solo se confirma con un `CommandResult` autenticado.
- Las órdenes tienen `commandId`, `commandSequence`, vencimiento e idempotencia.
- Los eventos llevan `eventId` y `correlationId`.
- La app distingue solicitado, ejecutado, rechazado, vencido, desconocido y fallido.
- No se inventan lecturas ni estados.
- Los contratos son la fuente de verdad para backend, app y dispositivo.

---

## 2. Contratos HTTP

| Servicio | OpenAPI |
|---|---|
| Identity | `Casa/contracts/v1/openapi/identity-api.json` |
| Home | `Casa/contracts/v1/openapi/home-api.json` |
| Security | `Casa/contracts/v1/openapi/security-api.json` |
| Notifications | `Casa/contracts/v1/openapi/notifications-api.json` |
| Assistant | `Casa/contracts/v1/openapi/assistant-api.json` |

Rutas principales:

### Identity

- `POST /api/v1/identity/auth/login`
- `POST /api/v1/identity/auth/refresh`
- `POST /api/v1/identity/auth/logout`
- `GET /api/v1/identity/me`
- `GET /api/v1/identity/.well-known/jwks.json`
- Miembros, invitaciones, aceptación, cambio y recuperación de contraseña.

### Home

- `GET /api/v1/houses/{houseId}/status`
- `GET /api/v1/houses/{houseId}/resources`
- `POST /api/v1/houses/{houseId}/lights/{lightId}/commands`
- `POST /api/v1/houses/{houseId}/resources/{resourceId}/commands`
- `GET /api/v1/commands/{commandId}`
- `GET /api/v1/houses/{houseId}/commands/by-key/{key}`
- `GET /api/v1/houses/{houseId}/telemetry`
- `GET /api/v1/houses/{houseId}/security`
- `GET/PUT /api/v1/houses/{houseId}/location`
- Video: `/video/sessions/...`
- Internos: `/api/v1/internal/device-events`, `/internal/captures/...`, `/internal/video/...`

### Security

- `GET /api/v1/security/houses/{houseId}/mode`
- `GET /api/v1/security/houses/{houseId}/activity`
- `GET /api/v1/security/houses/{houseId}/incidents`
- `GET /api/v1/security/houses/{houseId}/captures`
- Preferencias, personas, enrolamientos y diagnósticos.

### Notifications

- `GET/PUT /api/v1/notifications/houses/{houseId}/preferences`
- `POST /api/v1/notifications/houses/{houseId}/recipient/verify`
- `POST /api/v1/notifications/houses/{houseId}/recipient/confirm`
- `POST /api/v1/notifications/houses/{houseId}/test-email`
- `GET /api/v1/notifications/houses/{houseId}/deliveries/{deliveryId}`

### Assistant

- `POST /api/v1/assistant/houses/{houseId}/ask`
- `GET /api/v1/assistant/houses/{houseId}/diagnostics/model`

---

## 3. Autenticación

### Usuario

- JWT firmado por Identity con ES256.
- Claims: `sub`, `sid`, `jti`, `scope`.
- Audiencias explícitas por servicio.
- Expiración corta.
- Refresh rotatorio de un solo uso.
- Logout revoca la familia.

### Servicio

- Assertion ES256 firmada con clave privada P-256.
- `client_id`: `home`, `security`, `notifications` o `assistant`.
- Identity valida firma, emisor, sujeto, audiencia `Identity.TokenEndpoint`, `jti` y TTL ≤ 60 s.
- Emite token con audiencia `Identity.Internal`.
- Cada servicio solo puede consultar sus acciones.

### Acciones

- `home:read`
- `home:manage`
- `home:control:lights`
- `home:control:security`
- `home:control:door`
- `home:control:capture`
- `security:read`
- `security:manage`
- `notifications:read`
- `notifications:manage`
- `assistant:read`

Los residentes controlan luces; el administrador controla puerta, seguridad, capturas, miembros y avisos.

---

## 4. Órdenes

Flujo:

```text
App → Home → SQL (pending)
Home → IoT Hub método directo
Dispositivo → aplica
Dispositivo → CommandResult
Home.Functions → Home
Home → SQL (terminal)
Home → SignalR stateChanged
```

Estados:

- `pending`
- `executed`
- `rejected`
- `expired`
- `unknown`
- `failed`

Reglas:

- Misma clave y mismo cuerpo devuelven la misma orden.
- Misma clave y otro cuerpo devuelven conflicto.
- Una orden reclamada no se reenvía.
- Un resultado ambiguo no se convierte en éxito.
- `executed` requiere snapshot y `stateRevision` (salvo `StartVideo`).
- `rejected`, `expired`, `unknown` y `failed` requieren `reasonCode`.

---

## 5. Eventos del dispositivo

### v1

- `TelemetryReported`
- `MotionDetected`
- `AccessRequested`
- `CaptureUploaded`
- `CommandResult`

### v2

- `DeviceStateReported`

`DeviceStateReported` incluye:

```json
{
  "schemaVersion": 2,
  "type": "DeviceStateReported",
  "eventId": "...",
  "correlationId": "...",
  "occurredAtUtc": "...",
  "payload": {
    "lightId": "sala",
    "stateRevision": 3,
    "securityModeApplied": "disarmed",
    "snapshot": {
      "lightOn": true,
      "lightingMode": "manual",
      "source": "device",
      "appliedAtUtc": "..."
    }
  }
}
```

Home exige frescura de 60 s para admitir luces.

---

## 6. SignalR

- Hub: `/hubs/home`.
- Negociación: `POST /hubs/home/negotiate?negotiateVersion=1&houseId={houseId}`.
- Requiere JWT y `home:read`.
- Eventos:
  - `stateChanged`: invalida estado local.
  - `permissionRevoked`: limpia cachés y revalida `/identity/me`.
- El cliente no recibe estados sensibles por SignalR; reconcilia por REST.

---

## 7. Captura

- `CaptureImage` crea un trabajo y un ticket.
- Ticket: `captureId`, `uploadUrl`, `uploadToken`, `uploadExpiresAtUtc`.
- La clave se guarda solo como hash.
- Subida binaria `image/jpeg`/`image/png`, máximo 5 MiB.
- Cabecera `X-Capture-Token`.
- No se sobrescribe un archivo.
- Repetir el mismo contenido recupera el resultado; contenido distinto da conflicto.
- `CaptureUploaded` confirma metadatos después de validar el Blob.

---

## 8. Video

- `StartVideo` crea una sesión breve y exclusiva por casa.
- Ticket de productor con `sessionId`, `uploadUrl`, `uploadToken`, expiración.
- Segmentos MPEG-TS secuenciales y acotados.
- Playlist HLS generada por el servidor.
- Reproducción autenticada.
- Cierre, expiración, revocación y limpieza.
- El agente de Raspberry **no implementa `StartVideo`** todavía.

---

## 9. Contrato del dispositivo

### Nuevo agente

- `SetLight`
- `SetLightingMode`
- `SetSecurityMode`
- `OpenDoor`
- `CaptureImage`
- `DeviceStateReported` v2
- `CommandResult`
- `MotionDetected`
- `AccessRequested`
- `CaptureUploaded`

### Brechas conocidas

- `StartVideo` no implementado.
- Device twin no implementado en el agente de Raspberry.
- `correlationId` no viaja en el método directo; el agente genera uno nuevo.
- `MotionDetected.roomId` y `AccessRequested.roomId` usan `lightId`.
- La captura y el video físicos quedan para Etapa 11.

---

## 10. Validación

```bash
cd Casa/contracts/v1
../.venv/bin/python tests/validate_contracts.py
../.venv/bin/python tests/validate_fixtures.py
../.venv/bin/python tests/matrix_check.py
../.venv/bin/python tests/adversarial_audit.py
```

Resultado actual:

- 60/60 esquemas y OpenAPI.
- 121/121 fixtures.
- 75/75 vínculos.
- 6/6 adversariales rechazados.

---

## 11. Archivos

- `Casa/contracts/v1/schemas/` — 55 esquemas.
- `Casa/contracts/v2/schemas/device-state-reported.json`.
- `Casa/contracts/v1/fixtures/` — 121 fixtures.
- `Casa/contracts/v1/openapi/` — cinco APIs.
- `Casa/contracts/v1/tests/` — validadores y matriz.
