# Embebidos — ESP32 + Raspberry Pi

```
App móvil (Frontend/mobile) ──HTTP :5240──▶ Backend .NET (tu PC)
                                              │  TCP :5090
Cámara ─▶ vision/detectar_trafico.py ─HTTP──▶ │   (o vision/simular_trafico.py)
          (Raspberry Pi)                      ▼
                              puente_serial/puente_serial.py (Raspberry Pi)
                                              │  USB serial 115200
                                              ▼
                              ESP32/semaforo_v2 (LEDs + botones peatonales)
```

| Carpeta | Corre en | Qué hace |
|---|---|---|
| `ESP32/semaforo_v2/` | ESP32 (Arduino IDE) | Ciclo de 4 accesos con millis(), botones peatonales, modo manual, ambulancia, verde según densidad (`TRAFICO:`), reporta cada fase (`EST:`). |
| `RaspberryPi/puente_serial/` | Raspberry Pi | Pasa los comandos del backend (TCP, misma red) al ESP32 (USB) y regresa las respuestas. |
| `RaspberryPi/agente_iot/` | Raspberry Pi | Lo mismo, pero por **Azure IoT Hub**: sin IPs ni misma red. Reemplaza al puente. |
| `RaspberryPi/vision/` | Raspberry Pi o PC | Cuenta vehículos por acceso con la cámara (OpenCV, u opcional Azure Custom Vision) y los manda a `POST /api/trafico`. `simular_trafico.py` inventa esos conteos para probar sin maqueta. |
| `RaspberryPi/semaforo_gpio/` | Raspberry Pi | Versión de respaldo/práctica: el mismo ciclo con LEDs directo en los GPIO, sin ESP32. |

## Fase de pruebas (backend y app en la PC)

1. **ESP32**: abre `ESP32/semaforo_v2/semaforo_v2.ino` en Arduino IDE, súbelo y conecta el ESP32 por USB a la **Raspberry Pi**.
2. **Raspberry Pi** (misma red WiFi que la PC):
   ```bash
   sudo apt install -y python3-serial    # pyserial (en Raspberry Pi OS, pip da "externally-managed-environment")
   sudo usermod -aG dialout $USER        # permiso para el puerto USB (cierra sesion y vuelve a entrar)
   cd Embebidos/RaspberryPi/puente_serial
   ls /dev/ttyUSB* /dev/ttyACM*          # para ver el puerto del ESP32
   python3 puente_serial.py --serial /dev/ttyUSB0 --verbose
   hostname -I                           # IP de la Raspberry
   ```
3. **Backend (PC)**: en `Backend/CiudadInteligente.API/appsettings.json` pon la IP de la Raspberry en `Esp32:RaspberryPi:Host`, y luego:
   ```bash
   cd Backend/CiudadInteligente.API
   dotnet run --launch-profile http
   ```
   La primera vez, Windows pregunta si permite el acceso en redes privadas: acepta (puerto 5240). La base de datos `ciudad_inteligente.db` (SQLite) se crea sola.
4. **App (PC)**: en `Frontend/mobile/src/constants/api.ts` pon la IP de tu PC (`ipconfig`, adaptador Wi-Fi) y:
   ```bash
   cd Frontend/mobile
   npx expo start
   ```
5. **Cámara (opcional, Raspberry Pi)**:
   ```bash
   cd Embebidos/RaspberryPi/vision
   sudo apt install -y python3-opencv python3-numpy
   export CRUCE_API_KEY='<la llave de Dispositivos:ApiKey>'
   python3 detectar_trafico.py --backend-url http://<IP-DE-TU-PC>:5240/api/trafico
   ```

## Login, roles y llaves (una vez, en la PC)

La app pide iniciar sesión. Hay dos roles:

| Rol | Puede |
|---|---|
| **administrador** | Todo: cambiar el modo, colores manuales, detener el cruce, dar de alta y baja usuarios |
| **operador** | Ver el cruce y las estadísticas, y activar/terminar corredores de emergencia |

La cámara y el simulador no inician sesión: mandan el encabezado `X-Api-Key` con una llave de dispositivo,
que solo sirve para enviar conteos. Todo va en `dotnet user-secrets` (fuera del repo):

```cmd
cd Backend\CiudadInteligente.API
dotnet user-secrets set "Jwt:Clave" "<32 o más caracteres al azar>"
dotnet user-secrets set "Dispositivos:ApiKey" "<llave para la cámara y el simulador>"
dotnet user-secrets set "Usuarios:ContrasenaAdmin" "<contraseña de admin>"
dotnet user-secrets set "Usuarios:ContrasenaOperador" "<contraseña de operador>"
```

La primera vez que arranca, el backend crea los usuarios `admin` y `operador` con esas contraseñas. Si no las
pusiste, las inventa y las muestra **una vez** en la consola. Después se crean más usuarios desde la app
(Control → Usuarios). Sin `Jwt:Clave` el login funciona, pero cada vez que reinicias el backend hay que
volver a iniciar sesión.

## Simulador de tráfico (mientras no está la maqueta)

`RaspberryPi/vision/simular_trafico.py` hace lo mismo que la cámara pero con conteos inventados: horas pico a
las 8:00 y 18:30 entre semana, menos tráfico en fin de semana, la avenida (Norte/Sur) más cargada que la calle.
Solo usa la librería estándar de Python, así que corre en la PC o en la Raspberry.

```cmd
cd Embebidos\RaspberryPi\vision
set CRUCE_API_KEY=<la llave de Dispositivos:ApiKey>
python simular_trafico.py --historial 14 --en-vivo
```

- `--historial 14` sube 14 días de conteos (uno cada 5 min) de golpe, sin mandarlos al ESP32.
- `--en-vivo` manda un conteo cada 10 s, igual que la cámara (en modo adaptativo cambia los verdes del ESP32).
  Con IoT Hub el backend solo le manda `TRAFICO:` al ESP32 cuando cambia algún tiempo de verde, y máximo cada
  30 s, para no gastar el límite de mensajes.
- Para empezar de cero: detén el backend, borra `Backend/CiudadInteligente.API/ciudad_inteligente.db` y
  vuelve a arrancarlo (también se vuelven a crear los usuarios).

En la app, la pestaña **Estadísticas** muestra el tráfico actual contra lo normal, la predicción a 15/30/60 min,
el flujo por hora (horas pico y mejor hora para pasar), el flujo por día y el comparativo de espera del
temporizador fijo contra el adaptativo.

## Con Azure IoT Hub (backend y app en tu PC, Raspberry en cualquier red)

```
App (celular) ──IP de tu PC :5240──▶ Backend (PC) ──HTTPS: direct method──▶ Azure IoT Hub
                                         ▲                                        │
                                         └──── telemetría (Event Hubs) ◀───────── │
                                                                                  ▼
                                               agente_iot.py (Raspberry, se conecta HACIA AFUERA) ──USB──▶ ESP32
```

La Raspberry ya no necesita IP conocida ni estar en tu red: ella abre la conexión a IoT Hub.
La app todavía usa la IP de tu PC porque el backend sigue corriendo ahí.

### 1. Crear el IoT Hub (una vez, en tu PC con Azure CLI)
```bash
az login
az extension add --name azure-iot
az group create -n rg-ciudad -l centralus
az iot hub create -n ciudad-iot-isc09a -g rg-ciudad --sku F1 --partition-count 2
az iot hub device-identity create -n ciudad-iot-isc09a -d raspberry-cruce04
```
`ciudad-iot-isc09a` debe ser único en todo Azure; si ya existe usa otro nombre (en todos los comandos).

### 2. Raspberry: el agente
```bash
az iot hub device-identity connection-string show -n ciudad-iot-isc09a -d raspberry-cruce04   # (en tu PC) copia la cadena
```
En la Raspberry (detén antes `puente_serial.py`):
```bash
python3 -m venv ~/venv-iot
~/venv-iot/bin/pip install azure-iot-device pyserial
cd ~/Embebidos/RaspberryPi/agente_iot
export IOTHUB_DEVICE_CONNECTION_STRING='HostName=ciudad-iot-isc09a.azure-devices.net;DeviceId=raspberry-cruce04;SharedAccessKey=...'
~/venv-iot/bin/python agente_iot.py --serial /dev/ttyUSB0 --verbose
```
Debe decir `conectado a IoT Hub` y `agente listo`.

### 3. Backend: credenciales (NO van en appsettings.json ni en el repo)
```bash
cd Backend/CiudadInteligente.API
dotnet user-secrets init
dotnet user-secrets set "Esp32:IoTHub:ServiceConnectionString"  "<salida de: az iot hub connection-string show -n ciudad-iot-isc09a --policy-name service -o tsv>"
dotnet user-secrets set "Esp32:IoTHub:EventHubConnectionString" "<salida de: az iot hub connection-string show -n ciudad-iot-isc09a --default-eventhub -o tsv>"
```
En `appsettings.json` cambia `"Transporte": "RaspberryPi"` por `"Transporte": "IoTHub"` y corre
`dotnet run --launch-profile http`. Para regresar a la red local, vuelve a poner `"RaspberryPi"`.

### Mensajes y plan gratuito (F1: 8,000 mensajes/día)
El agente NO sube cada línea `EST:` (serían ~60,000/día). Sube solo el inicio de cada ciclo, el
`PARPADEO` cuando un peatón recorta el verde y un latido cada 50 s si no hubo nada más; el backend
calcula el resto del ciclo con los tiempos de `Hardware/TiemposFirmware.cs` (los mismos del `.ino`).
Con tiempos fijos son ~4,100 mensajes/día más los comandos de la app. Si cambias un tiempo en el
`.ino`, cámbialo también en `TiemposFirmware.cs`.

Para ver lo que llega a IoT Hub: `az iot hub monitor-events -n ciudad-iot-isc09a -d raspberry-cruce04`.

### Retraso con IoT Hub
Cada comando viaja PC → Azure → Raspberry → ESP32 y regresa (~0.3–0.6 s con el hub en Brazil South).
Para que no se note:
- **App:** muestra el resultado en cuanto tocas el botón; si el comando falla, regresa sola y muestra el error.
- **Backend:** "Detener todo", la secuencia manual y el cambio de modo viajan en **un solo** mensaje
  (antes era uno por comando: hasta 5 viajes), y la conexión HTTPS con Azure se mantiene abierta.
- **Agente:** responde al instante (antes esperaba hasta 0.2 s por respuesta del ESP32) y cada `EST:`
  lleva la hora en que ocurrió, para que el ciclo de la app vaya al mismo tiempo que los LED.
  Ten la hora de Windows sincronizada (Configuración → Hora → *Sincronizar ahora*).

Si cambias `agente_iot.py`, vuelve a copiarlo a la Raspberry (`scp`) y reinicia el agente **y** el backend.

## Producción (todo en la Raspberry Pi)

Conecta el ESP32 por USB a la Raspberry, corre ahí el backend y cambia en `appsettings.json`
`"Transporte": "Serial"` con `"Puerto": "/dev/ttyUSB0"`. El puente TCP ya no hace falta.

## Protocolo serial (115200 baudios, una línea por comando)

| De backend a ESP32 | Efecto |
|---|---|
| `MODO:AUTO` / `MODO:MANUAL` | Ciclo automático / control manual |
| `SET:<1-4>:VERDE\|AMARILLO\|ROJO` | Color de un acceso (solo en manual) |
| `AMBULANCIA:<1-4>` / `AMBULANCIA:FIN` | Corredor de emergencia / terminar |
| `TRAFICO:<n>,<s>,<e>,<o>` | Vehículos por acceso → verde de 1.5 s + 0.3 s por vehículo (máx. 5 s) |

| De ESP32 a backend | Significado |
|---|---|
| `OK:...` / `ERR:...` | Respuesta a cada comando |
| `EST:<0-4>:<fase>:<ms>` | Cambio de fase del ciclo (0 = todos en rojo) |

Accesos: 1 Norte, 2 Sur, 3 Este, 4 Oeste.
