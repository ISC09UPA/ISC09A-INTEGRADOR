# Dispositivo

**Última actualización:** 27 de noviembre de 2026
**Alcance:** agente de Raspberry Pi, controlador de E/S, agente histórico, instalación y QEMU.

---

## 1. Arquitectura del dispositivo

```text
[App] → [Azure Home] ⇄ [Agente] ⇄ USB serie ⇄ [Controlador de E/S]
                                  └─ cámara (CSI en Pi, USB aquí)
```

- La app no habla con el dispositivo.
- Home envía métodos directos por IoT Hub.
- El agente ejecuta contra el controlador de E/S y publica eventos.
- La Raspberry Pi 4B es el host final; el agente también puede correr en laptop durante pruebas.
- El controlador de E/S no habla con Azure; solo con el agente por el puerto serie.

---

## 2. Agente de Raspberry `Device/agent`

| Archivo | Responsabilidad |
|---|---|
| `casa_agent.py` | Entrada, API local y conexión IoT |
| `agent.py` | Métodos directos, idempotencia, ejecución y reporte |
| `store.py` | SQLite: estado, órdenes, outbox, deduplicación |
| `events.py` | Envelopes v1/v2 |
| `serial_link.py` | Protocolo serie, ack, reintento y reconexión |
| `camera.py` | Captura JPEG por v4l2 o CSI |
| `transports.py` | Transporte local o Azure IoT |
| `config.py` | Variables de entorno |

### Comandos

- `SetLight`
- `SetLightingMode`
- `SetSecurityMode`
- `OpenDoor`
- `CaptureImage`

### Eventos

- `DeviceStateReported` v2
- `TelemetryReported`
- `MotionDetected`
- `AccessRequested`
- `CaptureUploaded`
- `CommandResult`

### Estado

- SQLite en `CASA_DB_PATH`.
- Secuencia monotónica por dispositivo.
- Duplicado exacto: 202, sin repetir efecto.
- Mismo `commandId` con otro cuerpo: 400.
- Orden vencida: `expired`.
- Controlador caído: rechazo o `unknown`, nunca éxito inventado.
- Reinicio: órdenes en ejecución pasan a `unknown`.

### Verificación local

```bash
python3 -m venv Casa/.venv-device
Casa/.venv-device/bin/pip install -r Casa/Device/agent/requirements.txt jsonschema referencing
Casa/.venv-device/bin/python Casa/Device/tests/test_agent_local.py
```

Resultado: **41/41**.

Pruebas con hardware:

```bash
Casa/.venv-device/bin/python Casa/Device/tests/test_protocol_local.py
Casa/.venv-device/bin/python Casa/Device/tests/bringup.py
Casa/.venv-device/bin/python Casa/Device/tests/test_agent_hardware.py
```

Bloqueadas hasta tener la Raspberry Pi 4B, el controlador de E/S, la cámara y los sensores.

---

## 3. Protocolo serie

Agente → controlador:

```json
{"t":"ping"}
{"t":"cmd","id":"<uuid>","a":"light","v":true}
{"t":"cmd","id":"<uuid>","a":"door","ms":700}
{"t":"cmd","id":"<uuid>","a":"mode","v":"armed"}
{"t":"enroll"}
{"t":"status"}
```

Controlador → agente:

```json
{"t":"ready","fw":"1.0.0","board":"io-controller","fp":0,"tpl":0}
{"t":"pong","ms":1234}
{"t":"ack","id":"<uuid>","ok":true,"light":1}
{"t":"ack","id":"<uuid>","ok":true,"dup":1,"light":1}
{"t":"err","id":"<uuid>","code":"duplicateChanged"}
{"t":"ev","k":"motion","v":1,"mode":"armed"}
{"t":"ev","k":"finger","ok":1,"tpl":3,"conf":87}
{"t":"st","light":0,"ldr":42,"motion":0,"mode":"disarmed","door":0,"t_c":23.4,"rh":51,"fp":1,"tpl":3}
```

Reglas:

- `ack` se emite después de escribir y releer el pin.
- Un reintento exacto responde `dup:1` sin repetir el efecto.
- El estado se emite cada 5 s; el agente lo agrega y publica v2 cada 30 s.
- Abrir el puerto reinicia el controlador; esperar `ready`.
- Sin mensajes durante 15 s, el controlador emite `link: 0`.

El firmware del controlador de E/S no forma parte de este repositorio.

---

## 4. Instalación en Raspberry Pi

Checklist resumido:

1. Raspberry Pi OS Lite 64-bit Bookworm.
2. Hostname `casa-pi`, usuario `casa`, SSH y WiFi.
3. Copiar el contenido de `Casa/` a `/opt/casa-pi`.
4. Crear `/etc/casa-pi/agent.env` con permisos 600.
5. Conectar el controlador de E/S por USB, la cámara CSI y los sensores con fuente externa.
6. Verificar NTP.
7. Ejecutar `./Casa/Device/deploy/install.sh --check` y luego `sudo ./Casa/Device/deploy/install.sh`.
8. Verificar `systemctl status casa-pi`, `journalctl` y `pi_readiness.py`.

Variables principales:

```text
CASA_DEVICE_ID=casa-pi-01
CASA_LIGHT_ID=sala
CASA_DOOR_ID=puerta
CASA_SERIAL_PORT=/dev/ttyUSB0
CASA_DB_PATH=/var/lib/casa-pi/agent-state.db
CASA_REPORT_INTERVAL_S=30
CASA_TRANSPORT=iot
CASA_IOT_CONNECTION_STRING=...
CASA_CAMERA_BACKEND=auto
CASA_LDR_ENABLED=1
```

No versionar `agent.env` ni cadenas de conexión.

---

## 5. Agente histórico

Ruta: [RaspberryPi](../RaspberryPi/README.md).

- Python, Pi/QEMU, SQLite.
- Simulación y conexión IoT histórica.
- 78/78 pruebas unitarias.
- No emite `DeviceStateReported` v2.
- No implementa `CaptureImage`.
- Se conserva como referencia y no reemplaza al agente de Raspberry.

Verificación:

```bash
Casa/Scripts/verify-agent.sh
```

---

## 6. Raspberry virtual

Ruta: `RaspberryPi/virtual`.

```bash
./Casa/RaspberryPi/virtual/iniciar-raspberry.sh
ssh -p 50022 domotica@127.0.0.1
```

- Emula una Pi 3, no una Pi 4.
- No reproduce GPIO, CSI ni sensores reales.
- Las imágenes son locales y no se versionan.
- No usar como aceptación física.

Detalle en [Raspberry virtual](../RaspberryPi/virtual/README.md).

---

## 7. Pendientes del dispositivo

- Ruta Azure IoT del agente de Raspberry.
- Raspberry Pi 4B, controlador de E/S, cámara CSI y sensores físicos.
- `CaptureImage` contra Azure.
- `StartVideo` no implementado.
- Device twin no implementado en el agente de Raspberry.
- Correlación C2D sin decisión.
- `roomId` de PIR/huella acoplado a `LightId`.
- Aceptación E2E app → Azure → dispositivo.
