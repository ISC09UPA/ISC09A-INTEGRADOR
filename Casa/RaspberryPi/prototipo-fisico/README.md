# Prototipo físico: Raspberry Pi 4

Prototipo independiente del agente `casa-pi`: menú local con PIR, DHT11, LED, buzzer y servo opcionales; detección provisional de rostro y telemetría opcional a IoT Hub. Las lecturas reales y la cámara deben verificarse en el montaje. No utilizar la detección de rostro como control de acceso real: autoriza cualquier rostro detectado, sin comprobar identidad.

Pines BCM previstos: PIR 16 (físico 36), DHT11 DATA 5 (físico 29), servo 18, buzzer 23, LED rojo 24 y LED blanco 25. Verifica cableado, niveles de 3.3 V para GPIO, alimentación y resistencias antes de probar. El servo necesita fuente adecuada, masa común y calibración. Las salidas se activan solo con `--salidas`. La puerta representa la última orden, no una posición medida.

## Simulación

```sh
python3 casa.py --modo simulacion
```

La opción 6 alterna presencia simulada. La alarma empieza desarmada y se activa mientras hay movimiento. El clima simulado no representa sensores físicos.

## Raspberry Pi

Instala gpiozero, Adafruit Blinka y Adafruit CircuitPython DHT en un entorno compatible. Enciende la Raspberry solo después de revisar las conexiones. Para observar los sensores sin activar salidas:

```sh
python3 sensores.py --sensor ambos
python3 casa.py --modo raspberry
```

Los dos programas usan los mismos GPIO: no los ejecutes simultáneamente. Una salida ya verificada se habilita, por ejemplo, con `--salidas blanco`; los nombres disponibles son `rojo`, `blanco`, `buzzer` y `servo`. La opción 7 intenta detectar rostro con `rpicam-still` y OpenCV instalado en `.venv-camara`; si se habilitó el servo, envía la orden de abrir y la opción 3 lo cierra. No se ha confirmado la captura en el hardware actual.

## Telemetría a IoT Hub

Instala `azure-iot-device` en el entorno de Python. Define `IOTHUB_DEVICE_CONNECTION_STRING` con la cadena de conexión del **dispositivo** y usa `--azure` con cualquiera de los modos; `--intervalo 30` controla los segundos entre mensajes (mínimo 5). El programa puede leer `.iot_device_connection` local si no existe la variable; ese archivo está ignorado por Git y nunca se debe compartir.

Los mensajes JSON incluyen fecha UTC, indicador de simulación, movimiento, estado de la alarma, temperatura, humedad y resultado del DHT. El envío corre en segundo plano; si falla o se llena la cola, se descarta el mensaje y se reintenta con una medición posterior. No hay almacenamiento persistente ni garantía de entrega al salir. No se envían imágenes ni se reciben órdenes desde Azure.
