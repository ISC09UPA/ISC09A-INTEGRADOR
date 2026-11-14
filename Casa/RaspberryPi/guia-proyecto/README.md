# Casa domótica: guía sencilla

Esta carpeta reúne accesos al proyecto que está funcionando en esta Raspberry. Los nombres ayudan a encontrar cada parte; los accesos apuntan a las carpetas originales del programa. Si abres uno y cambias un archivo, estarás cambiando el archivo original.

| Carpeta | Qué contiene | Cómo explicarlo |
|---|---|---|
| `1_sensores` | Lecturas de luz, movimiento y huella. | «Recibe información de la casa». |
| `2_control_casa` | Lógica de luz, alarma, puerta y órdenes. | «Decide cómo responder y controla la casa». |
| `3_comunicacion_azure` | Comunicación con IoT Hub e imágenes. | «Conecta la Raspberry con Azure». |
| `4_memoria_local` | Persistencia en SQLite. | «Guarda eventos, configuración y órdenes». |
| `5_camara` | Gestión de cámara. | «Organiza las capturas»; la cámara real sigue pendiente. |
| `6_dispositivos` | Adaptadores simulados y espacio para los físicos. | «Conecta la lógica con sensores y actuadores». |
| `7_simulacion` | Herramientas para cambiar valores de prueba. | «Permite probar sin sensores físicos». |
| `8_configuracion` | Archivos de configuración de ejemplo. | «Muestra los valores con los que se puede configurar el agente». La configuración activa está en `/etc/casa-pi/device.toml`. |
| `9_pruebas` | Pruebas del programa. | «Comprueba reglas y comunicación». |

El programa principal está en `codigo_principal`: `agent.py` coordina las tareas y `__main__.py` inicia el agente. Su ruta real es `/opt/casa-pi/src/casa_pi/`.

## Flujo para explicar

```text
Sensor simulado → Raspberry / casa-pi → Azure IoT Hub
App futura → backend futuro → Azure IoT Hub → Raspberry → dispositivo
```

La Raspberry virtual se ha conectado al Hub y se han probado órdenes y eventos simulados. La app y el backend siguen pendientes. IoT Hub requiere que otros programas procesen sus eventos para guardarlos o enviar avisos.

## Comandos útiles

```bash
cd ~/casa-domotica
ls
cat README.md
probar-azure
casa-pi-sim status
sudo systemctl status casa-pi
```

Presiona `q` para salir de la pantalla del estado del servicio. Para ver más instrucciones: `cat /opt/casa-pi/OPERACION.md`.

Las carpetas originales están en `/opt/casa-pi/`, los datos de funcionamiento en `/var/lib/casa-pi/` y la configuración activa en `/etc/casa-pi/`. Esta vista no incluye accesos a las credenciales. Cámara, huella y dispositivos físicos requieren implementación y pruebas adicionales.
