# Arquitectura del programa de la Raspberry Pi

**Actualizacion del 26 de octubre de 2026:** se implemento la primera version del agente simulado. Consulta [OPERACION.md](../OPERACION.md) para el alcance ejecutable, instalacion, comandos y funciones aun pendientes. El resto de este documento conserva el diseno objetivo; no todas sus integraciones fisicas y de nube estan implementadas.

Esta carpeta define **solo el software que se ejecutará en la Raspberry Pi**. La misma aplicación Python funcionará en dos modos: `simulado` dentro de QEMU y `real` en la Raspberry física. El modo se elige con configuración; las reglas de la casa no cambian.

## Qué hará la Raspberry

1. Leer luminosidad por zona, movimiento PIR por habitación y resultados del lector de huellas.
2. Capturar imágenes con la cámara de acceso cuando exista una solicitud o un disparador configurado.
3. Ajustar las luces en modo automático o aplicar una orden manual.
4. Activar el mecanismo de la puerta solo ante una orden de apertura válida, vigente y no ejecutada previamente.
5. Publicar lecturas, eventos y resultados; recibir órdenes y configuración por su conexión IoT.
6. Conservar localmente la última configuración aplicada, una cola acotada de eventos y los identificadores de órdenes ejecutadas.

El lector de huellas entrega un identificador de plantilla o resultado de coincidencia. La Raspberry no guarda imágenes de huellas ni concede acceso solo por esa coincidencia: envía el intento para autorización y espera una orden de apertura vigente. Las imágenes de la cámara siguen el mismo principio. Sin conexión o sin una autorización válida, los accesos biométricos quedan cerrados. La iluminación automática local continúa con la última configuración aplicada.

## Un proceso y sus módulos

`casa-pi` será un único proceso Python 3 iniciado por `systemd`. El proceso organiza tareas de lectura, publicación y recepción de órdenes. Un propietario único controla la cámara para evitar que dos tareas intenten usarla a la vez. Los controladores físicos quedan detrás de interfaces, de modo que QEMU use controladores simulados.

```text
device/raspberry-agent/
├── README.md                         diseño y pasos de operación
├── pyproject.toml                    paquete Python y dependencias
├── .env.example                      nombres de variables; sin credenciales reales
├── config/
│   ├── device.example.toml           zonas, habitaciones, intervalos y modo
│   └── hardware.example.toml         mapa de GPIO, buses y módulos, pendiente del hardware
├── src/casa_pi/
│   ├── __main__.py                    entrada del proceso y apagado ordenado
│   ├── settings.py                    carga y validación de configuración
│   ├── models.py                      lecturas, eventos, órdenes y estado aplicado
│   ├── agent.py                       coordina módulos y sus ciclos de vida
│   ├── sensors/
│   │   ├── light.py                   muestreo y validación de luminosidad
│   │   ├── motion.py                  cambios PIR y eliminación de rebotes
│   │   └── fingerprint.py             coincidencia y evento de intento de acceso
│   ├── camera/
│   │   └── manager.py                 captura única y control de uso de cámara
│   ├── control/
│   │   ├── lighting.py                automático/manual e histéresis
│   │   ├── security.py                modo armado y eventos de movimiento
│   │   ├── access.py                  vigencia, duplicados y pulso de apertura
│   │   └── commands.py                despacho y resultado de órdenes remotas
│   ├── hardware/
│   │   ├── ports.py                   contratos de sensores, cámara y actuadores
│   │   ├── simulated.py               entradas y salidas simuladas en QEMU
│   │   └── real/                      controladores físicos por módulo elegido
│   ├── transport/
│   │   ├── iot.py                     conexión, telemetría, métodos y configuración
│   │   └── images.py                  carga autorizada de capturas
│   ├── persistence/
│   │   └── sqlite.py                  configuración, cola y órdenes ejecutadas
│   └── health.py                      estado del proceso y fallos de módulos
├── simulator/
│   ├── inputs.example.json           luz, PIR, huella e imagen de prueba
│   └── control.py                     cambia entradas simuladas desde la consola
├── deploy/
│   └── casa-pi.service                unidad systemd
└── tests/
    ├── test_lighting.py              umbrales y modo manual
    ├── test_access.py                caducidad y órdenes repetidas
    └── test_simulation.py            lectura simulada y eventos
```

La estructura anterior es el **plano de implementación**. Ya existe una base ejecutable en modo simulado; las pruebas se concentran en `tests/test_agent.py`. Los controladores `hardware/real/` se concretarán cuando se elijan los modelos de sensor de luz, lector de huellas, cámara y mecanismo de puerta. Si el sensor de luz es una LDR analógica, su controlador necesitará un convertidor analógico digital; un sensor digital no lo necesita.

## Configuración y datos en el sistema operativo

| Ruta en la Raspberry | Uso |
|---|---|
| `/opt/casa-pi/` | Código y entorno virtual de Python. |
| `/etc/casa-pi/device.toml` | Modo `simulado`/`real`, identificadores, zonas e intervalos. |
| `/etc/casa-pi/hardware.toml` | Pines y buses de los módulos físicos, cuando existan. |
| `/etc/casa-pi/secrets.env` | Credenciales del dispositivo, con permisos restringidos; fuera del repositorio. |
| `/var/lib/casa-pi/agent.db` | SQLite: configuración aplicada, eventos pendientes y órdenes ya ejecutadas. |
| `/var/lib/casa-pi/simulator.json` | Estado de sensores y referencia de imagen solo en modo simulado. |
| `journalctl -u casa-pi` | Registros del proceso; sin secretos ni imágenes. |

`systemd` iniciará el agente al encender la Pi y lo reiniciará si el proceso falla. Se evitará crear un servidor HTTP público en la Raspberry: las entradas remotas llegan por la conexión saliente del dispositivo.

## Flujo local de cada dispositivo

| Entrada o salida | Acción en la Raspberry |
|---|---|
| Luz | Muestrear cada intervalo, descartar valores inválidos y aplicar modo automático con dos umbrales para evitar parpadeo. En modo manual, conservar el valor ordenado. |
| PIR | Registrar el cambio a movimiento, asociarlo a la habitación y generar un evento cuando la seguridad efectiva esté armada. Limitar repeticiones del mismo movimiento. |
| Huella | Leer el resultado del módulo, generar un intento con identificador y vencimiento; esperar autorización antes de accionar la puerta. |
| Cámara | Tomar una captura por solicitud, intento de acceso o regla habilitada; compartir el dispositivo mediante el administrador de cámara. En QEMU se usa una imagen de prueba. |
| Luces | Cambiar la salida física o simulada y publicar el estado realmente aplicado. |
| Cerradura | Comprobar `commandId`, vencimiento y duración máxima; accionar un pulso y registrar el resultado. Una orden repetida no produce otro pulso. |

Los intervalos iniciales propuestos son 1 segundo para luz y PIR y 30 segundos para un resumen periódico de telemetría. Los eventos de movimiento, huella y resultado de órdenes se publican al ocurrir. Son valores de partida configurables, no requisitos de la rúbrica.

## Límites del simulador y secuencia de construcción

QEMU ejecuta el mismo sistema y proceso, pero **no reproduce físicamente** un PIR, lector de huellas, cámara ni GPIO de la Pi 4. `simulator/control.py` cambiará las entradas de prueba; `hardware/simulated.py` registrará el estado de luces y cerradura. El ensayo de pines, voltajes y tiempos reales quedará para la Raspberry física.

Orden de construcción del agente: (1) configuración, modelos y adaptadores simulados; (2) luz, PIR y salidas; (3) cola SQLite y conexión IoT; (4) huella y cámara; (5) controladores físicos; (6) servicio `systemd` y pruebas completas. Ninguna etapa requiere guardar imágenes de huellas en el agente.
