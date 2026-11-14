# Operacion del agente casa-pi

La version 0.2 implementa el agente Linux en modo simulado con transporte Azure IoT Hub. Hay configuracion TOML, luz automatica con histeresis y manual, armado, eventos de movimiento, intentos de huella simulados, validacion de ordenes, cierre temporizado de puerta, SQLite y servicio systemd. La lectura de luz es un porcentaje de prueba, no lux calibrados.

## Instalacion actual

- Codigo: `/opt/casa-pi`.
- Configuracion: `/etc/casa-pi/device.toml` y `hardware.toml`.
- Datos: `/var/lib/casa-pi/agent.db`.
- Entradas: `/var/lib/casa-pi/simulator.json`.
- Salidas: `/var/lib/casa-pi/outputs.json`.
- Salud: `/var/lib/casa-pi/health.json`.
- Credenciales, solo si se habilita Azure: `/etc/casa-pi/secrets.env`.

El servicio se ejecuta como domotica en la VM actual. La unidad restringe escrituras al directorio de datos. Al desplegar en otro equipo hay que adaptar el usuario y los permisos. El modo real falla explicitamente hasta implementar sus controladores.

## Controles locales de simulacion

```bash
sudo systemctl status casa-pi
journalctl -u casa-pi -n 30 --no-pager
casa-pi-sim status
casa-pi-sim sensor --light 10
casa-pi-sim sensor --light 90
casa-pi-sim command SetLight --on true
casa-pi-sim command SetLightingMode --mode auto
casa-pi-sim command SetSecurityMode --mode armed
casa-pi-sim sensor --motion on
casa-pi-sim sensor --motion off
casa-pi-sim sensor --fingerprint demo-1
casa-pi-sim sensor --fingerprint none
casa-pi-sim command OpenDoor --duration-ms 1500
```

`OpenDoor` en esta consola es una inyeccion local de prueba y solo se procesa en modo simulado. No representa reconocimiento biometrico ni autorizacion del backend. El modo real no lee la carpeta de ordenes locales. No se expone un servidor HTTP.

Las ordenes llevan commandId, deviceId, issuedAtUtc, expiresAtUtc y payload. OpenDoor exige ademas attemptId y doorId. Ventana maxima: 30 segundos. Pulso maximo configurado: 3000 ms. Un ID ya ejecutado o desconocido no repite la accion; reutilizarlo con otro contenido se rechaza. Una apertura activa no se prolonga con otra orden. Al reiniciar se desactiva la salida de puerta y las ejecuciones incompletas quedan unknown. En una orden de apertura, executed significa que se inicio el pulso, no que un sensor confirmo la posicion fisica de la puerta.

Para que una apertura pueda ejecutarse el reloj debe aparecer sincronizado en timedatectl. La validacion vuelve a comprobar vigencia inmediatamente antes de actuar. La simulacion tiene cierre por software; el montaje real requerira un controlador adecuado y proteccion de tiempo independiente del proceso.

Los eventos pendientes tienen un limite configurable de 1000. Al agotarlo se descartan primero los resúmenes entregados/antiguos, conservando prioridad para incidentes. No se promete retencion ilimitada. Los IDs de ordenes se conservan para deduplicacion; vigilar el disco en uso sostenido.

## Conexion Azure y funciones pendientes

El transporte activo es `azure`, con el dispositivo `raspberry-sim` del hub `i9n-karma-iothub`. El SDK 2.14.0 y sus dependencias estan instalados en `/opt/casa-pi/vendor`; el inventario de versiones y hashes esta en `deploy/azure-dependencies.json`. La credencial de dispositivo se carga desde `/etc/casa-pi/secrets.env` (root, 600); no esta incluida en los fuentes.

El agente recibe SetLight, SetSecurityMode, SetLightingMode y OpenDoor como metodos directos. HTTP 202 confirma que la orden entro en la cola; el evento CommandResult y las propiedades reportadas `casaPi.lastCommand` comunican la ejecucion o el rechazo posterior. El estado reportado incluye lightOn, lightingMode, securityModeApplied y doorPulseActive. No debe interpretarse doorPulseActive como posicion fisica medida de una puerta.

Los mensajes de SQLite solo se marcan entregados despues de la confirmacion del SDK. La red se procesa en un hilo separado; los sensores, el control de salidas y SQLite pertenecen al hilo principal. La cola en memoria es limitada y reintenta con espera creciente. Una entrega ambigua puede producir eventos duplicados: el consumidor debe deduplicar eventId.

La configuracion persistente llega en `properties.desired.agentConfiguration`, con lightOnBelow, lightOffAbove, reportSeconds y lightingMode. Se validan rangos y se confirma appliedVersion/observedVersion en `properties.reported.casaPi.configuration`. Una version vieja se ignora y una invalida conserva los valores aplicados. Este canal no permite aperturas ni cambios del modo de seguridad. La configuracion valida y su version se guardan juntas en SQLite.

Quedan por implementar cargas autorizadas a Blob, captura real/CSI, reconocimiento facial, integracion de huella fisica y WebRTC. camera/manager.py permite leer una imagen de prueba bajo exclusion mutua; transport/images.py rechaza explicitamente una carga aun no implementada. Las clases fisicas no accionan GPIO. Esta conexion de dispositivo no implica que las APIs .NET ni la app movil esten construidas.

## Pruebas

```bash
cd /opt/casa-pi
PYTHONPATH=src:vendor python3 -m unittest discover -s tests -v
```

Incluyen luz/histeresis, entradas invalidas, deteccion de flanco, huella sin apertura, canal no confiable, destino, caducidad, reloj, duracion, duplicados, reinicio durante ejecucion, persistencia y cola acotada. Las pruebas de software no certifican hardware.

## Evidencia del 26 de octubre de 2026

La suite actual pasa 24 pruebas de software. La instalación original se verificó con IoT Hub y con una instancia aislada sin red. Los archivos de evidencia de aquella ejecución permanecen en la copia original del proyecto; no se incluyen como datos de ejecución en este paquete.

## Simular y observar telemetría

Consulta [SIMULACION_AZURE.md](SIMULACION_AZURE.md) para generar valores ficticios desde la Raspberry y ver los eventos recibidos en Azure desde la Mac.

## Intervalo de telemetría

El 28 de octubre se ajustó `reportSeconds` a 10 mediante la configuración deseada de IoT Hub; la Raspberry confirmó la versión aplicada. La lectura local de sensores continúa cada segundo. Consulta `SIMULACION_AZURE.md` para el monitor con formato y el límite de mensajes del nivel gratuito.
