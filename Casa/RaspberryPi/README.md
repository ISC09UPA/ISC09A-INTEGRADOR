# Raspberry Pi de la casa domótica

Aquí está el agente Python `casa-pi`. Lee sensores, controla luces y seguridad, conserva eventos localmente y se comunica con Azure IoT Hub. La versión actual funciona con entradas y salidas simuladas. La cámara, los sensores físicos y la integración completa con la app y el backend siguen pendientes.

## Encontrar cada parte

| Carpeta | Contenido |
|---|---|
| `src/casa_pi/sensors` | Lecturas de luz, movimiento y huella. |
| `src/casa_pi/control` | Reglas de iluminación, seguridad, apertura y configuración. |
| `src/casa_pi/hardware` | Dispositivos simulados e interfaces para los físicos. |
| `src/casa_pi/transport` | Comunicación con Azure; la carga de imágenes sigue pendiente. |
| `src/casa_pi/persistence` | Configuración, eventos y órdenes guardados con SQLite. |
| `simulator` | Herramientas para simular entradas. |
| `config` | Ejemplos de configuración sin claves. |
| `tests` | Pruebas de comportamiento y del transporte. |
| `deploy` | Ejemplo del servicio Linux y registro de dependencias usadas en la VM. |
| `tools` | Monitor para ver en la Mac los mensajes recibidos en Azure. |

## Probar el código en tu equipo

Desde esta carpeta, con Python 3.9 o superior:

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -e .
python -m unittest discover -s tests -v
```

Para iniciar el agente localmente, copia `config/device.example.toml` a `config/device.local.toml`. En la copia cambia `data_dir` por un directorio escribible, por ejemplo `/tmp/casa-pi-demo` en macOS o Linux, y conserva `transport = "local"` y `mode = "simulado"`.

```bash
python -m casa_pi --config config/device.local.toml
```

Desde otra terminal, con el mismo entorno activo y desde esta carpeta:

```bash
python simulator/control.py --data-dir /tmp/casa-pi-demo status
python simulator/control.py --data-dir /tmp/casa-pi-demo sensor --light 15 --motion off
```

El ejemplo envía resúmenes cada 10 segundos al transporte configurado. En modo local los eventos quedan en SQLite; no se envían a Azure. La lógica de apertura exige un reloj sincronizado y una orden válida. Las pruebas utilizan adaptadores simulados.

## Conexión con Azure

El flujo probado es Raspberry virtual → IoT Hub, con recepción de órdenes y confirmación del estado. IoT Hub requiere que el backend o las Functions procesen los mensajes para llevarlos a una base de datos y a la app; esos pasos todavía no están integrados.

Para habilitar el adaptador Azure se requiere instalar `python -m pip install -e '.[azure]'`, configurar `transport = "azure"` y proporcionar la credencial propia del dispositivo mediante la variable `IOTHUB_DEVICE_CONNECTION_STRING`. `.env.example` muestra solo el nombre de la variable. El programa no carga automáticamente un archivo `.env`.

En la VM existente el servicio carga la credencial desde `/etc/casa-pi/secrets.env`, fuera del código. Cada integrante debe configurar su acceso por separado. No copiar ese archivo al repositorio.

## Guías

- [Operación de la Raspberry virtual existente](OPERACION.md): rutas y comandos de la instalación que usa el usuario `domotica`.
- [Simulación y monitor Azure](SIMULACION_AZURE.md): demostración de sensores ficticios.
- [Arquitectura objetivo](docs/ARQUITECTURA.md): responsabilidades y componentes planeados.
- [Vista sencilla en la VM](guia-proyecto/README.md): explicación por partes. Sus accesos se crearon en la VM; no son necesarios para ejecutar este paquete.

El repositorio contiene fuentes y ejemplos. Los entornos virtuales, dependencias instaladas, credenciales, discos de la VM y datos generados están excluidos con `.gitignore`. El archivo `deploy/casa-pi.service` es un ejemplo de la instalación existente: requiere adaptar usuario y rutas para otro equipo.
