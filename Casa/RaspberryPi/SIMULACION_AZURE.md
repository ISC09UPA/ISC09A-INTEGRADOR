# Simular sensores y ver los datos en Azure

La Raspberry virtual puede simular luz y movimiento. El agente `casa-pi` lee los valores y envía un resumen de telemetría a IoT Hub aproximadamente cada 10 segundos.

## 1. Conectar la Raspberry virtual

Inicia la Raspberry y entra desde la Mac:

```bash
ssh -p 50022 domotica@127.0.0.1
```

Comprueba el agente:

```bash
probar-azure
```

## 2. Abrir el monitor en la Mac

Desde la raíz de este repositorio, en otra terminal de la Mac, ejecuta:

```bash
python3 Casa/RaspberryPi/tools/monitorear-sensores.py
```

Déjalo abierto. Para detenerlo usa Ctrl+C. La Mac debe tener Azure CLI y la extensión `azure-iot`, con sesión iniciada en la suscripción del proyecto. El monitor muestra mensajes nuevos; ábrelo antes de iniciar la simulación.

## 3. Iniciar sensores ficticios

En la terminal de la Raspberry:

```bash
simular-sensores
```

El programa simula tres escenarios: 15 % de luz sin movimiento, 55 % con movimiento y 85 % sin movimiento. Cada uno dura 15 segundos para dar tiempo a que se envíe el resumen; tarda aproximadamente 45 segundos en total. Al terminar, o al pulsar Ctrl+C, restaura las entradas que había antes.

El monitor mostrará mensajes `TelemetryReported` con campos como `lightPercent`, `motion`, `lightOn` y `securityModeApplied`. `lightPercent` es un valor ficticio de 0 a 100, no una medición real en lux. `motion` indica si la entrada simulada de movimiento está activa. El sistema solo produce un evento de alarma por movimiento si la seguridad está armada; el simulador no modifica esa configuración.

## Lo que ya se verificó

Se probó con la Raspberry virtual y el monitor recibió desde `raspberry-sim` una lectura con `lightPercent: 15.0` y `motion: false`. Esto verifica el recorrido de la Raspberry virtual a IoT Hub; no prueba sensores físicos ni que el backend, la base de datos o la app hayan recibido el mensaje. Los eventos `MotionDetected` dependen de que la seguridad esté armada.

## Cuota del nivel gratuito

El nivel F1 tiene un límite de 8,000 mensajes diarios. Un resumen cada 10 segundos durante 24 horas serían 8,640 resúmenes, sin contar otros eventos y operaciones; se puede agotar la cuota antes de terminar el día. Para una prueba corta está bien. Consulta la [documentación de cuotas de IoT Hub](https://learn.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-quotas-throttling).
