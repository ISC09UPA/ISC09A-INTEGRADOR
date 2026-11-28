# Contexto para el embebido (Raspberry Pi) del Almacén inteligente

Este documento explica a quien programe la Raspberry (persona o IA) **cómo encaja el embebido en todo el sistema**
y **qué tiene que mandar y recibir** para que funcione con la API. Léelo completo antes de cambiar `script.py`.

---

## 1. El sistema completo

```
 Raspberry Pi 4                         Azure Service Bus (namespace sb-iot-almacen, plan Basic: solo colas)
 (PN532 + webcam + OLED      ──▶ iot-auth-solicitudes ──▶ ┐
  + 3 LEDs + buzzer)         ◀── iot-auth-respuestas  ◀── ┤   API .NET 10 (workers) ──▶ Custom Vision (rostro)
                             ──▶ iot-venta-escaneos   ──▶ ┤                        └──▶ PostgreSQL (inventario)
                             ◀── iot-escaneos-respuestas ◀┘
                                                              ▲
                                        Web Blazor ───────────┤  HTTP + JWT
                                        App móvil Expo ───────┘
```

- **Backend**: `Almacen/Backend` (.NET 10, arquitectura hexagonal, PostgreSQL). Expone la API REST que usan la web y la app.
- **Frontend**: `Almacen/Frontend` (app móvil Expo / React Native).
- **Embebido**: `Almacen/Codigos_raps` (Python en Raspberry Pi 4).

**Regla de oro: el embebido NO habla con la API por HTTP.** Todo pasa por **colas de Azure Service Bus**. La API
escucha las colas, decide (inventario, reconocimiento facial, ventas, recepciones) y responde por otra cola.
El embebido **no lleva inventario**: la fuente de verdad es la base de datos de la API.

---

## 2. Sesión en el embebido (regla principal del ciclo)

El embebido tiene dos estados:

| Estado | Qué acepta | Qué hace con lo demás |
|--------|-----------|------------------------|
| **Sin usuario autenticado** (reposo) | **Solo tarjetas de acceso** (función 1) | Una etiqueta de pieza o paquete se **ignora**: pantalla `Pase su credencial`, pitido largo, LED rojo ~1 s |
| **Con usuario autenticado** | Piezas y paquetes (funciones 3 a 5) y también tarjetas | Como se describe abajo |

- Se entra al estado "con usuario" cuando un acceso responde `permitido: true`. Guarda `usuarioId` y `nombre` de la
  respuesta y una expiración (p. ej. 60 s sin actividad, como `DURACION_SESION` en `script.py`; cada lectura la renueva).
- Pasar otra vez la misma tarjeta cierra la sesión. La sesión también vence sola.
- Todos los mensajes de piezas/paquetes llevan el **`usuarioId`** del usuario en sesión: la API saca el stock del
  **almacén asignado a ese usuario** (cada usuario tiene un almacén y no puede operar otro).

---

## 3. Funciones

| # | Función | Cola | Respuesta |
|---|---------|------|-----------|
| 1 | **Acceso**: tarjeta NFC del usuario + foto del rostro | `iot-auth-solicitudes` → `iot-auth-respuestas` | Sí (máx. 15 s) |
| 2 | **Inicio de sesión de la app móvil**: es la misma función 1, no hay que programar nada extra | — | — |
| 3 | **Venta por pieza**: un agente tiene el *modo inteligente de venta* en la web/app | `iot-venta-escaneos` → `iot-escaneos-respuestas` | Sí (máx. 5 s) |
| 4 | **Recepción por paquete**: un agente tiene el *modo inteligente de recepción* | `iot-venta-escaneos` → `iot-escaneos-respuestas` | Sí (máx. 5 s) |
| 5 | **Comprobar stock**: nadie tiene un modo inteligente activo | `iot-venta-escaneos` → `iot-escaneos-respuestas` | Sí (máx. 5 s) |

Las funciones 3, 4 y 5 son **el mismo mensaje**: el embebido no elige el modo, **la API decide** según lo que esté
activo en la web o la app. La respuesta dice cuál fue (`modo`) y trae el **nivel** para la pantalla y los LEDs.

### Conexión

- Librería: `pip install azure-servicebus`
- Cadena de conexión de la regla SAS **`embebido`** (Send + Listen) en la variable de entorno `IOT_SERVICEBUS`.
  **Nunca** en el código ni en el repositorio.
- Hora del sistema correcta (NTP): la API descarta mensajes con más de 60 s.

---

## 4. Función 1: Acceso con tarjeta NFC + rostro

### Solicitud → cola `iot-auth-solicitudes`

| Parte del mensaje | Valor |
|-------------------|-------|
| Cuerpo | Bytes **JPEG** de la webcam (máx. 4 MB; recomendado 640×480, rostro de frente) |
| `content_type` | `image/jpeg` |
| `correlation_id` | Un id único por intento (`uuid.uuid4().hex`) |
| Propiedad de aplicación `nfc` | UID de la tarjeta del usuario en hexadecimal (ej. `11E40DB1`). Se aceptan `04:A2:1B:7C`, `04-a2-1b-7c` o `04A21B7C` |
| `time_to_live` | 1 minuto |

### Respuesta ← cola `iot-auth-respuestas`

Mismo `correlation_id`, `subject = "auth"`, cuerpo JSON:

```json
{ "permitido": true, "usuarioId": "01a1620e-...", "nombre": "Ana López", "confianza": 0.97, "motivo": "Acceso permitido" }
```

- **Abre (inicia sesión en el embebido) solo si `permitido` es `true`.** Muestra `nombre` o `motivo`.
- `motivo` posibles: `Acceso permitido`, `Tarjeta no registrada`, `Usuario desactivado`,
  `El rostro no coincide con el de la tarjeta`, `Confianza insuficiente (…)`, `Imagen inválida…`,
  `El modelo de reconocimiento no está publicado`, `Solicitud expirada`, `Servicio no disponible, intenta de nuevo`.
- Espera **máximo 15 s**; sin respuesta: error en pantalla y **no abrir**.
- Abre el receptor **antes** de enviar. La cola es compartida: recibe en *peek-lock* y si el `correlation_id` no es
  el tuyo, `abandon_message`.
- LEDs: permitido → verde ~2 s y 2 pitidos cortos; negado → rojo ~2 s y 1 pitido largo.

Ya no se usa el diccionario `USUARIOS` de `script.py` ni `verificar_segundo_factor()`: los usuarios, sus tarjetas,
sus fotos y su almacén se capturan en la web (*Usuarios*) y la API decide con Azure Custom Vision.

### Función 2: inicio de sesión de la app

La app móvil no pide contraseña: crea una solicitud de 60 s y la persona hace un **acceso normal** en el embebido.
Si sale `permitido`, la API completa el inicio de sesión de la app. Para el embebido es transparente.

---

## 5. Etiquetas que lee el lector

| Etiqueta | Qué trae | Para qué |
|----------|----------|----------|
| **Tarjeta de usuario** | Solo su UID de fábrica (sin texto grabado) | Función 1 |
| **Pieza** (una por unidad física) | UID de fábrica + texto grabado = **familia** (campo "Familia NFC" del producto) | Funciones 3 y 5 |
| **Paquete** de proveedor | Texto grabado = **código proveedor** (campo "Código proveedor" del producto). El UID no importa | Función 4 (y 5) |

El embebido **no necesita distinguir pieza de paquete**: manda el texto grabado en `familia` y la API lo busca como
familia o como código proveedor según el modo activo. Solo distingue **con texto** (pieza o paquete) de
**sin texto** (tarjeta de usuario).

### Leer el texto NDEF de una etiqueta NTAG21x con PN532 (Adafruit)

```python
def leer_texto_ndef(lector, max_paginas=40):
    """Devuelve el primer registro de texto NDEF de una NTAG213/215/216, o None."""
    datos = bytearray()
    for pagina in range(4, 4 + max_paginas):          # los datos de usuario empiezan en la página 4
        bloque = lector.ntag2xx_read_block(pagina)     # 4 bytes por página
        if bloque is None:
            return None
        datos += bloque
        if 0xFE in bloque:                             # fin de TLV
            break
    i = 0
    while i < len(datos):
        tipo = datos[i]
        if tipo == 0x00:                               # TLV nulo
            i += 1
            continue
        if tipo == 0xFE:
            return None
        largo = datos[i + 1]
        cuerpo = datos[i + 2:i + 2 + largo]
        if tipo == 0x03 and len(cuerpo) >= 3:          # mensaje NDEF
            largo_tipo = cuerpo[1]
            corto = cuerpo[0] & 0x10                   # registro corto (SR)
            largo_carga = cuerpo[2] if corto else int.from_bytes(cuerpo[2:6], "big")
            inicio_tipo = 3 if corto else 6
            largo_id = 0
            if cuerpo[0] & 0x08:                       # trae ID: un byte con su largo antes del tipo
                largo_id = cuerpo[inicio_tipo]
                inicio_tipo += 1
            tipo_registro = bytes(cuerpo[inicio_tipo:inicio_tipo + largo_tipo])
            inicio_carga = inicio_tipo + largo_tipo + largo_id
            carga = bytes(cuerpo[inicio_carga:inicio_carga + largo_carga])
            if tipo_registro == b"T" and carga:
                largo_idioma = carga[0] & 0x3F
                return carga[1 + largo_idioma:].decode("utf-8").strip()
            return None
        i += 2 + largo
    return None
```

Para **grabar** el código en las etiquetas puede usarse una app de celular (p. ej. NFC Tools → Escribir → Texto).
Los códigos (familia y código proveedor) son **hexadecimales** (ej. `A1B2C3`), igual que en el catálogo.

---

## 6. Funciones 3, 4 y 5: escaneo de pieza o paquete

### Escaneo → cola `iot-venta-escaneos`

Cuerpo JSON (`content_type = "application/json"`, `time_to_live` 1 min) y un **`correlation_id`** único
(`uuid.uuid4().hex`). Sin `correlation_id` la API procesa el mensaje pero **no responde**.

```json
{ "id": "532B84E5030001", "familia": "A1B2C3", "usuarioId": "01a1620e-..." }
```

| Campo | Reglas |
|-------|--------|
| `id` | UID de fábrica de la etiqueta (`read_passive_target()`), hexadecimal, máx. 32. Se normaliza a mayúsculas sin `:`, `-` ni espacios |
| `familia` | Texto grabado en la etiqueta: familia NFC de la pieza o código proveedor del paquete. Hexadecimal |
| `usuarioId` | El del usuario con sesión en el embebido (respuesta del acceso). Sin él la API no sabe de qué almacén es el stock |

Qué hace la API:

- **Modo venta** (un agente tiene el modo inteligente de venta): la pieza se agrega a su venta y la línea guarda su
  UID. Cada pieza vendida necesita su UID: si el agente cambia la cantidad a mano, captura los UID que faltan.
  Una pieza ya vendida (UID en una venta no cancelada) se rechaza. Una pieza ya escaneada en la misma venta es un
  **duplicado**. Al registrar la venta cada UID queda vendido; si la venta se cancela, regresan al stock.
- **Modo recepción** (un agente tiene el modo inteligente de recepción): el código proveedor identifica el producto
  y en la pantalla del agente sale un modal para decir de cuántas piezas es el paquete.
- **Comprobar stock** (nadie tiene un modo activo): responde la existencia del producto en el almacén del usuario y
  lo anota en la bitácora *Comprobar stock › Consultas del lector* de la web y la app.

### Respuesta ← cola `iot-escaneos-respuestas`

Mismo `correlation_id`, `subject = "escaneo"`, cuerpo JSON:

```json
{ "modo": "consulta", "aceptado": true, "motivo": "Stock bajo", "producto": "TOR-001 · Tornillo",
  "existencia": 4, "minimo": 5, "nivel": "amarillo" }
```

| Campo | Tipo | Notas |
|-------|------|-------|
| `modo` | `"venta"` \| `"recepcion"` \| `"consulta"` | Qué hizo la API |
| `aceptado` | bool | Venta/recepción: se agregó. Consulta: se reconoció el producto |
| `motivo` | string | Texto corto para la pantalla |
| `producto` | string \| null | `"REF · descripción"`; null si el código no existe |
| `existencia` | number \| null | Existencia del producto (almacén del usuario) |
| `minimo` | number \| null | Stock mínimo |
| `nivel` | `"verde"` \| `"amarillo"` \| `"rojo"` \| null | **Qué LED prender** |

**Significado de `nivel`:**

- **Consulta** (semáforo de stock, igual que en la web y la app):
  - 🔴 **rojo**: existencia 0 (agotado).
  - 🟡 **amarillo**: hay existencia pero está en o por debajo del mínimo (stock bajo).
  - 🟢 **verde**: arriba del mínimo.
  - `null`: no se reconoció el producto.
- **Venta / recepción** (resultado de la lectura):
  - 🟢 **verde**: agregada.
  - 🟡 **amarillo**: **duplicada** (esa pieza ya se escaneó en esta venta). Es una alerta, no un error.
  - 🔴 **rojo**: rechazada (ya vendida, código no registrado, producto no disponible, UID inválido…).

Valores de `motivo`:

- Consulta: `Stock suficiente`, `Stock bajo`, `Stock agotado`, `Esta pieza ya se vendió`,
  `Producto no disponible para venta`, `Familia no registrada`, `Etiqueta sin familia NFC válida`.
- Venta: `Agregada a la venta`, `Pieza duplicada`, `Pieza ya vendida`, `Producto no disponible`,
  `Familia no registrada`, `UID inválido`.

### Qué mostrar en el hardware

| Situación | Pantalla OLED (2 líneas, ~21 caracteres) | LEDs | Buzzer |
|-----------|-------------------------------------------|------|--------|
| Sin sesión y llega pieza/paquete | `Pase su credencial` | Rojo ~1 s | 1 largo |
| Consulta reconocida | `REF` / `Stock: 4 (min 5)` | El del `nivel` | 1 corto |
| Venta/recepción agregada | `Agregada a venta` / `REF · stock 4` | Verde | 2 cortos |
| Duplicada | `Pieza duplicada` / `REF` | **Amarillo** | 2 cortos rápidos |
| Rechazada | `motivo` / `REF` | Rojo | 1 largo |
| Sin respuesta en 5 s | `Sin respuesta` / `Revisa la red` | Amarillo parpadeando | 1 largo |

Después de ~3 s regresa a reposo: `Pase su credencial` (sin sesión) o `Hola <nombre>` (con sesión).

### Cosas que ya NO hace el embebido

- `stock.json`, `retirar_articulo()`, `reabastecer()`, `UID_REABASTECER`, `UMBRAL_VERDE`/`UMBRAL_AMARILLO` y
  `actualizar_leds()` por stock local: **el inventario lo lleva la API** y los LEDs muestran el `nivel` que responde.
  El stock mínimo se define por producto y almacén en la web/app (*Existencias › Definir mínimo*).

---

## 7. Hardware actual (ver `script.py` y `diagnostico_nfc.py`)

- Raspberry Pi 4.
- Lector **PN532** por I2C por software en el **bus 3** (SDA GPIO23 / pin 16, SCL GPIO24 / pin 18), dirección `0x24`.
  Necesita `dtoverlay=i2c-gpio,bus=3,i2c_gpio_sda=23,i2c_gpio_scl=24` en `/boot/firmware/config.txt`.
  Arranca dormido: `despertar_pn532()` lo "toca" antes de usar la librería de Adafruit.
- Pantalla OLED SSD1306 (o SH1106) 128×64 en el **bus 4** (SDA GPIO21, SCL GPIO20), dirección `0x3C`.
- **3 LEDs**: verde GPIO 14, amarillo GPIO 15, rojo GPIO 18. Buzzer en GPIO 25.
- Webcam USB: **todavía no hay código**; hace falta para la función 1 (p. ej. `opencv-python`:
  `cv2.VideoCapture(0)`, `cv2.imencode(".jpg", frame)`).

Conviene conservar de `script.py`: arranque del PN532, `leer_uid()`, la pantalla con reintentos, el filtro anti rebote
(`TIEMPO_ENTRE_LECTURAS`) y el manejo de sesión con expiración.

---

## 8. Código de referencia (Python)

```python
import json, os, time, uuid
from datetime import timedelta
from azure.servicebus import ServiceBusClient, ServiceBusMessage

CONEXION = os.environ["IOT_SERVICEBUS"]
cliente = ServiceBusClient.from_connection_string(CONEXION)   # crear una vez y reutilizar


def _pedir(cola_envio, cola_respuesta, mensaje: ServiceBusMessage, espera_s: int) -> dict | None:
    correlacion = uuid.uuid4().hex
    mensaje.correlation_id = correlacion
    with cliente.get_queue_receiver(cola_respuesta, max_wait_time=espera_s) as receptor, \
         cliente.get_queue_sender(cola_envio) as emisor:
        emisor.send_messages(mensaje)
        for recibido in receptor:                  # termina tras max_wait_time sin mensajes
            if recibido.correlation_id != correlacion:
                receptor.abandon_message(recibido) # es de otro dispositivo
                continue
            receptor.complete_message(recibido)
            return json.loads(b"".join(recibido.body))
    return None


def autenticar(nfc: str, jpeg: bytes) -> dict | None:
    return _pedir("iot-auth-solicitudes", "iot-auth-respuestas", ServiceBusMessage(
        jpeg, content_type="image/jpeg", application_properties={"nfc": nfc},
        time_to_live=timedelta(minutes=1)), espera_s=15)


def enviar_etiqueta(uid: str, texto: str, usuario_id: str) -> dict | None:
    return _pedir("iot-venta-escaneos", "iot-escaneos-respuestas", ServiceBusMessage(
        json.dumps({"id": uid, "familia": texto, "usuarioId": usuario_id}),
        content_type="application/json", time_to_live=timedelta(minutes=1)), espera_s=5)


def mostrar_nivel(almacen, nivel: str | None) -> None:
    """Prende solo el LED del nivel que respondió la API."""
    leds = {"verde": almacen.led_verde, "amarillo": almacen.led_amarillo, "rojo": almacen.led_rojo}
    for led in leds.values():
        led.off()
    if nivel in leds:
        leds[nivel].on()


def procesar_etiqueta(almacen, uid: str, texto: str | None) -> None:
    """Se llama con cada etiqueta nueva (ya pasó el filtro anti rebote)."""
    if texto is None:                                   # tarjeta de usuario
        procesar_tarjeta(almacen, uid)
        return
    if not almacen.sesion:                              # sin usuario: solo se aceptan tarjetas
        mostrar_nivel(almacen, "rojo")
        almacen.buzzer.beep(on_time=0.8, off_time=0, n=1)
        almacen.mostrar_aviso("Pase su credencial")
        return
    almacen.sesion["expira"] = time.monotonic() + 60
    almacen.pantalla.mostrar("Consultando...", uid[-12:])
    r = enviar_etiqueta(uid, texto, almacen.sesion["usuario_id"])
    if r is None:
        almacen.led_amarillo.blink(on_time=0.2, off_time=0.2)
        almacen.buzzer.beep(on_time=0.8, off_time=0, n=1)
        almacen.mostrar_aviso("Sin respuesta", "Revisa la red")
        return
    mostrar_nivel(almacen, r.get("nivel"))
    referencia = (r.get("producto") or "").split(" · ")[0]
    if r["modo"] == "consulta" and r["aceptado"]:
        almacen.buzzer.beep(on_time=0.08, off_time=0, n=1)
        minimo = f" (min {r['minimo']:g})" if r.get("minimo") else ""
        almacen.mostrar_aviso(referencia[:21], f"Stock: {r['existencia']:g}{minimo}"[:21])
    elif r["aceptado"]:
        almacen.buzzer.beep(on_time=0.08, off_time=0.08, n=2)
        almacen.mostrar_aviso("Agregada a venta" if r["modo"] == "venta" else "Paquete leido", referencia[:21])
    elif r.get("nivel") == "amarillo":                  # duplicada: alerta
        almacen.buzzer.beep(on_time=0.05, off_time=0.05, n=2)
        almacen.mostrar_aviso(r["motivo"][:21], referencia[:21])
    else:
        almacen.buzzer.beep(on_time=0.8, off_time=0, n=1)
        almacen.mostrar_aviso(r["motivo"][:21], referencia[:21])


def procesar_tarjeta(almacen, uid: str) -> None:
    if almacen.sesion and almacen.sesion["uid"] == uid:  # la misma tarjeta cierra la sesión
        almacen.cerrar_sesion("Sesion cerrada")
        return
    almacen.pantalla.mostrar("Mire a la camara", "Verificando...")
    r = autenticar(uid, tomar_foto_jpeg())              # tomar_foto_jpeg(): webcam → bytes JPEG
    if r and r["permitido"]:
        almacen.sesion = {"uid": uid, "usuario_id": r["usuarioId"], "nombre": r["nombre"],
                          "expira": time.monotonic() + 60}
        mostrar_nivel(almacen, "verde")
        almacen.buzzer.beep(on_time=0.08, off_time=0.08, n=2)
        almacen.mostrar_aviso("Acceso concedido", f"Hola {r['nombre']}"[:21])
    else:
        mostrar_nivel(almacen, "rojo")
        almacen.buzzer.beep(on_time=0.8, off_time=0, n=1)
        almacen.mostrar_aviso("Acceso denegado", (r["motivo"] if r else "Sin respuesta")[:21])
```

Recomendaciones:

- La autenticación puede tardar hasta 15 s: muestra "Verificando…" y no leas otras etiquetas mientras tanto.
- Si falla la red, reintenta con espera creciente y avisa en pantalla; nunca abras sin respuesta.
- Registra cada evento en consola como ya hace `registrar_evento()`.

---

## 9. Cómo probar

- Simulador del backend (hace lo mismo que el embebido):
  ```bash
  cd Almacen/Backend
  export IOT_SERVICEBUS="Endpoint=sb://sb-iot-almacen.servicebus.windows.net/;SharedAccessKeyName=embebido;SharedAccessKey=..."
  dotnet run --project tools/iot-simulador -- auth 11E40DB1 ./rostro.jpg
  dotnet run --project tools/iot-simulador -- escanear 532B84E5030001 A1B2C3
  ```
- Venta: web o app → *Nueva venta* → almacén → activar **Modo inteligente** → pasar piezas.
- Comprobar stock: con los modos apagados, pasa una pieza. La pantalla y los LEDs deben coincidir con el color de
  *Comprobar stock* en la web o la app, y la consulta aparece sola en *Consultas del lector*.
- La cola `iot-escaneos-respuestas` hay que crearla en el namespace si no existe:
  ```bash
  az servicebus queue create -g rg-almacen --namespace-name sb-iot-almacen -n iot-escaneos-respuestas --default-message-time-to-live PT2M
  ```
- Del lado de la API, `Iot:ConnectionString` (regla `api`) debe estar configurada; vacía = los workers no arrancan.

---

## 10. Checklist para el embebido

- [ ] Leer `IOT_SERVICEBUS` del entorno y crear el `ServiceBusClient`.
- [ ] Webcam: capturar JPEG.
- [ ] Sesión local: sin usuario solo se aceptan tarjetas; con acceso permitido guardar `usuarioId`, `nombre` y expiración.
- [ ] Al leer una etiqueta: UID + texto NDEF. Sin texto → tarjeta (función 1). Con texto → `procesar_etiqueta()`.
- [ ] Mandar `usuarioId` en cada escaneo y mostrar la respuesta en la OLED y el `nivel` en los LEDs
      (verde agregada / amarillo duplicada / rojo rechazada; en consulta, el semáforo de stock).
- [ ] Quitar el stock local y el diccionario `USUARIOS`.
- [ ] Mantener el anti rebote y los reintentos de pantalla.
- [ ] Sin respuesta en el tiempo límite o error de red: avisar y no abrir.
