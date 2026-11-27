# Arquitectura

**Última actualización:** 27 de noviembre de 2026
**Alcance:** visión vigente del sistema implementado y del dispositivo en evaluación.

---

## 1. Visión general

Casa Domótica integra:

- Una maqueta física con luces, movimiento, puerta, cámara y biometría.
- Una app Android/web en React Native + Expo.
- Cinco servicios de negocio en .NET 10.
- Azure Functions para ingesta y notificaciones.
- Azure SQL, Blob privado, Service Bus, SignalR, IoT Hub, Key Vault, Vision, Face, ACS Email y Azure OpenAI.
- Un agente Python que corre en una Raspberry Pi 4B y se comunica con Home por IoT Hub.

La Raspberry Pi 4B no habla directamente con la app. La app habla con las APIs; Home es el único servicio que envía órdenes al dispositivo por IoT Hub.

```text
┌──────────────┐       HTTPS/Bearer       ┌──────────────────┐
│ App Expo     │ ───────────────────────> │ Identity         │
│ React Native │                          ├──────────────────┤
│ TypeScript   │ ───────────────────────> │ Home             │
│              │                          ├──────────────────┤
│              │ ───────────────────────> │ Security         │
│              │                          ├──────────────────┤
│              │ ───────────────────────> │ Notifications    │
│              │                          ├──────────────────┤
│              │ ───────────────────────> │ Assistant        │
│              │ <──── SignalR ────────── │ Home /hubs/home  │
└──────────────┘                          └────────┬─────────┘
                                                   │
                         ┌─────────────────────────┼─────────────────────────┐
                         │                         │                         │
                         ▼                         ▼                         ▼
                    IoT Hub                  Service Bus                 Blob privado
                         │                         │                         │
                         ▼                         ▼                         ▼
                 Raspberry Pi 4B         Security / Notifications     capturas / video
```

---

## 2. Servicios

### 2.1 Identity

- Login por correo y contraseña.
- Refresh rotatorio, logout y revocación de sesiones.
- Casas, membresías y roles `admin`/`resident`.
- Invitaciones de residentes y recuperación de contraseña.
- JWT ES256 con audiencias por servicio.
- Emite tokens de servicio para llamadas internas.
- No hay registro público ni login social.

Ruta: `Backend/Identity`.

### 2.2 Home

- Casas, dispositivos y recursos.
- Estado de luces y frescura de reportes.
- Órdenes manuales/automáticas con idempotencia y vencimiento.
- Seguridad, puerta, captura y video.
- Telemetría y estado del dispositivo.
- Inbox/outbox y publicación a Service Bus.
- SignalR para invalidar estado en la app.
- Ubicación de la casa.

Ruta: `Backend/Home` y `Backend/Home.Functions`.

### 2.3 Security

- Actividad, accesos e incidentes.
- Consumo de `security-v1` desde Service Bus.
- Capturas privadas en Blob y análisis con Azure Vision.
- Preferencias de captura/análisis.
- Personas autorizadas y sesiones biométricas.
- Face API para rostro, con bloqueo si no hay aprobación.
- Retención y eliminación de datos biométricos.

Ruta: `Backend/Security`.

### 2.4 Notifications

- Preferencias por casa.
- Verificación de destinatario.
- Envío de correo de prueba idempotente.
- Estado de entrega: `requested`, `accepted`, `failed`, `unknown`, `delivered`.
- Consumidor `notifications-v1` de Service Bus.
- ACS Email como proveedor.

Ruta: `Backend/Notifications`.

### 2.5 Assistant

- Consultas de solo lectura sobre Home y Security.
- Historial máximo de 6 turnos.
- Contexto minimizado y fecha UTC.
- Sin herramientas de control.
- Respuesta honesta cuando no hay reportes.
- Azure OpenAI a través de identidad administrada.

Ruta: `Backend/Assistant`.

### 2.6 Home.Functions

- Trigger de Event Hub de IoT Hub.
- Valida eventos, los envía a Home y conserva los inválidos en `iot-quarantine`.
- Consumer group exclusivo `home-functions-v1`.

Ruta: `Backend/Home.Functions`.

---

## 3. Flujos principales

### 3.1 Login

```text
App → Identity /auth/login
Identity → SQL (usuario, hash, sesión, refresh)
Identity → App (accessToken, refreshToken)
App → APIs con Bearer
```

### 3.2 Orden de luz

```text
App → Home /houses/{id}/lights/{lightId}/commands
Home → SQL (orden pendiente, secuencia, idempotencia)
Home → IoT Hub método directo SetLight
Dispositivo → aplica y responde
Dispositivo → IoT Hub CommandResult
Home.Functions → Home /internal/device-events
Home → SQL (resultado, estado)
Home → SignalR stateChanged
App → vuelve a consultar REST
```

Una respuesta 202 solo confirma admisión; la ejecución se confirma con `CommandResult`.

### 3.3 Telemetría

```text
Dispositivo → IoT Hub → Home.Functions → Home
Home → SQL HomeTelemetry
App → Home /telemetry
```

### 3.4 Seguridad e incidentes

```text
Dispositivo → MotionDetected → Home → Service Bus
Service Bus security-v1 → Security
Security → SQL (actividad, incidente)
App → Security /activity e /incidents
```

### 3.5 Captura

```text
App → Home CaptureImage
Home → ticket (captureId, uploadUrl, uploadToken, expiración)
Dispositivo → POST binario a Home /internal/captures/{id}/upload
Home → valida y guarda en Blob privado
Dispositivo → CaptureUploaded
Security → lista/detalle/contenido autorizado
```

### 3.6 Video

```text
App → Home StartVideo
Home → sesión breve + ticket de productor
Dispositivo → segmentos MPEG-TS + playlist
App → Home playlist/segmentos con Bearer
App → expo-video (HLS)
```

El backend está implementado. El agente de Raspberry todavía no implementa `StartVideo`.

### 3.7 Asistente

```text
App → Assistant /ask
Assistant → Identity /me (autorización de casa)
Assistant → Home/Security (lecturas con el token del usuario)
Assistant → Azure OpenAI
Assistant → App
```

---

## 4. Autenticación y autorización

- Identity firma JWT ES256 con claves P-256.
- Cada servicio fija la clave pública de Identity (`Jwt:PublicKeyPem`).
- Las llamadas internas usan assertions ES256 firmadas por cada servicio.
- Identity valida cliente, firma, emisor, audiencia, `jti` y expiración.
- Cada cliente de servicio tiene una lista limitada de acciones.
- Las decisiones consultan sesión activa y membresía vigente.
- Los residentes controlan luces; el administrador controla puerta, seguridad, capturas y miembros.

Ver [Contratos](CONTRATOS.md) para el detalle.

---

## 5. Datos

| Base | Servicio | Contenido |
|---|---|---|
| `IdentityDb` | Identity | Usuarios, casas, membresías, sesiones, refresh, invitaciones, resets |
| `HomeDb` | Home | Dispositivos, luces, comandos, telemetría, inbox/outbox, capturas, video, ubicación |
| `SecurityDb` | Security | Actividad, incidentes, capturas, preferencias, personas, enrolamientos |
| `NotificationsDb` | Notifications | Preferencias, entregas, inbox/outbox |

Migraciones:

- Identity: `001_identity.sql`, `002_members_and_tokens.sql`.
- Home: `001_home.sql` a `007_house_location.sql`.
- Security: `001_security.sql` a `004_persons_and_biometrics.sql`.
- Notifications: `001_notifications.sql`.

Todas son aditivas y viven en Azure SQL `sql-casa-example`, con bases separadas.

---

## 6. Azure

| Recurso | Uso |
|---|---|
| App Service | Identity, Home, Security, Assistant |
| Function Apps | Home Functions, Notifications |
| Azure SQL | Cuatro bases privadas |
| IoT Hub | Comunicación con el dispositivo |
| Service Bus | Eventos de negocio |
| SignalR | Invalidaciones de estado |
| Storage | Blob privado: capturas, video, cuarentena |
| Key Vault | Secretos y claves |
| Vision | Análisis de capturas |
| Face | Biometría facial, sujeto a aprobación |
| ACS Email | Correo |
| Azure OpenAI | Asistente |
| APIM | Gateway definido, aún sin APIs importadas |

Identidades administradas con permisos mínimos por recurso.

---

## 7. App móvil

- Expo Router.
- Pantallas: login, inicio, luces, seguridad, cámara, actividad, accesos, capturas, personas, miembros, ubicación, preferencias, asistente, configuración.
- Cliente HTTP centralizado en `src/core/api.ts`.
- SignalR propio en `src/core/realtime.ts`.
- Estado global en `src/core/state.tsx`.
- SecureStore en nativo; en web no persiste refresh token.
- Frescura de estado y reconciliación REST antes de habilitar controles.
- `eas.json` con perfiles Android.

Detalle en [App móvil](../Frontend/mobile/README.md).

---

## 8. Dispositivo

### Agente de Raspberry `Device/agent`

- Python.
- Controlador de E/S por USB serie.
- Métodos directos: `SetLight`, `SetLightingMode`, `SetSecurityMode`, `OpenDoor`, `CaptureImage`.
- `DeviceStateReported` v2.
- SQLite para estado, órdenes y outbox.
- Cámara v4l2/CSI.
- Transporte local o Azure IoT.

### Agente histórico

- Python.
- Pi/QEMU.
- Simulación y conexión IoT histórica.
- Se conserva como referencia.

Detalle en [Dispositivo](DISPOSITIVO.md).

---

## 9. Decisiones de diseño

- Sin Docker ni Docker Compose.
- Sin SQLite en el teléfono.
- Sin registro público.
- SQL separado por servicio.
- Sin datos simulados en producción.
- Blob privado para capturas y video.
- SignalR solo para invalidar; la app reconcilia por REST.
- Órdenes físicas con idempotencia, vencimiento y resultado durable.
- La ausencia de hardware se muestra como espera o indisponibilidad, nunca como éxito.
