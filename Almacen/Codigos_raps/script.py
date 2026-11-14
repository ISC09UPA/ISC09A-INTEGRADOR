#!/usr/bin/env python3
"""
Sistema de almacén - Raspberry Pi 4
Lector NFC PN532, pantalla OLED, buzzer y semáforo de LEDs.

Uso:
  python sistema_almacen.py            # usa el stock guardado en stock.json
  python sistema_almacen.py --stock 20 # fija el stock
"""

# ---------------- Librerías ----------------
import argparse
import json
import time
from datetime import datetime
from pathlib import Path

from adafruit_extended_bus import ExtendedI2C
from adafruit_pn532.i2c import PN532_I2C
from gpiozero import LED, Buzzer
from luma.core.interface.serial import i2c as luma_i2c
from luma.core.render import canvas
from luma.oled.device import sh1106, ssd1306
from PIL import ImageFont
from RPLCD.i2c import CharLCD
from smbus2 import SMBus, i2c_msg

# ---------------- Pines (numeración GPIO/BCM) ----------------
PIN_LED_VERDE = 14      # pin físico 8
PIN_LED_AMARILLO = 15   # pin físico 10
PIN_LED_ROJO = 18       # pin físico 12
PIN_BUZZER = 25         # pin físico 22 (señal +), GND en pin 20

# ---------------- Buses I2C por software (ver /boot/firmware/config.txt) ----------------
BUS_NFC = 3        # SDA = GPIO23 (pin 16), SCL = GPIO24 (pin 18)
BUS_PANTALLA = 4   # SDA = GPIO21 (pin 40), SCL = GPIO20 (pin 38)
DIRECCION_NFC = 0x24

# ---------------- Pantalla ----------------
TIPO_PANTALLA = "oled"      # "lcd" (16x2 con módulo I2C) u "oled" (128x64)
DIRECCION_PANTALLA = 0x3C   # LCD: 0x27 o 0x3F | OLED: 0x3C  (compruébala con i2cdetect -y 4)
CHIP_OLED = "ssd1306"       # "ssd1306" (OLED de 0.96") o "sh1106" (muchas OLED de 1.3")

# ---------------- Inventario ----------------
STOCK_INICIAL = 20
UMBRAL_VERDE = 12       # stock > 12        -> verde (alto)
UMBRAL_AMARILLO = 5     # 5 < stock <= 12   -> amarillo (bajo); si no, rojo (crítico)
ARCHIVO_STOCK = Path(__file__).resolve().parent / "stock.json"

# ---------------- Usuarios autorizados (UID de su credencial -> nombre) ----------------
USUARIOS = {
    "11E40DB1": "David",       # tarjeta blanca
}

# ---------------- Etiquetas con función especial ----------------
UID_REABASTECER = "D9198529"   # llavero azul: regresa el stock a STOCK_INICIAL

# ---------------- Tiempos (segundos) ----------------
TIEMPO_ENTRE_LECTURAS = 0.5   # sin ver la etiqueta antes de volver a contarla
TIEMPO_AVISO = 2              # que se muestra un aviso antes de volver a reposo
DURACION_SESION = 60          # sin actividad antes de cerrar la sesión


def registrar_evento(tipo, **datos):
    evento = {"evento": tipo, "fecha": datetime.now().isoformat(timespec="seconds"), **datos}
    print(json.dumps(evento, ensure_ascii=False), flush=True)


def cargar_stock():
    try:
        with open(ARCHIVO_STOCK, encoding="utf-8") as archivo:
            return int(json.load(archivo)["stock"])
    except (FileNotFoundError, KeyError, ValueError, json.JSONDecodeError):
        return STOCK_INICIAL


def guardar_stock(stock):
    # Se escribe en un archivo temporal y luego se reemplaza el original de un solo
    # paso: si se va la luz a mitad de la escritura, stock.json no queda corrupto.
    archivo_temporal = ARCHIVO_STOCK.with_suffix(".tmp")
    with open(archivo_temporal, "w", encoding="utf-8") as archivo:
        json.dump({"stock": stock}, archivo)
    archivo_temporal.replace(ARCHIVO_STOCK)


def despertar_pn532(intentos=10):
    # El PN532 arranca dormido y no contesta al primer contacto por I2C. La librería
    # de Adafruit solo lo intenta una vez (en Raspberry Pi espera los pines RSTO y P32
    # conectados), así que antes de usarla se le "toca" hasta que responda.
    with SMBus(BUS_NFC) as bus:
        for _ in range(intentos):
            try:
                bus.i2c_rdwr(i2c_msg.read(DIRECCION_NFC, 1))
                return True
            except OSError:
                time.sleep(0.1)
    return False


def iniciar_nfc(intentos=5):
    if not Path(f"/dev/i2c-{BUS_NFC}").exists():
        raise RuntimeError(
            f"no existe el bus /dev/i2c-{BUS_NFC}: falta la linea "
            f"dtoverlay=i2c-gpio,bus={BUS_NFC},i2c_gpio_sda=23,i2c_gpio_scl=24 "
            "en /boot/firmware/config.txt (y reiniciar)"
        )

    ultimo_error = None
    for _ in range(intentos):
        try:
            despertar_pn532()
            lector = PN532_I2C(ExtendedI2C(BUS_NFC), debug=False)
            _, version, revision, _ = lector.firmware_version
            lector.SAM_configuration()
            return lector, f"{version}.{revision}"
        except Exception as error:
            ultimo_error = error
            time.sleep(0.5)
    raise RuntimeError(f"{type(ultimo_error).__name__}: {ultimo_error}")


def leer_uid(lector, timeout=0.1):
    try:
        uid = lector.read_passive_target(timeout=timeout)
    except Exception:
        return None  # lectura incompleta ocasional del bus: se ignora y se vuelve a leer
    return bytes(uid).hex().upper() if uid else None


class Pantalla:
    # Si la pantalla no responde, el sistema sigue funcionando y los mensajes
    # solo se ven en la consola.

    def __init__(self, tipo, bus, direccion):
        self.tipo = tipo
        self.disp = None
        try:
            if tipo == "lcd":
                self.disp = CharLCD("PCF8574", direccion, port=bus, cols=16, rows=2)
                self.disp.clear()
            elif tipo == "oled":
                # Se le pasa un SMBus propio para que luma envíe la imagen en bloques
                # de 32 bytes. Por defecto manda los 1024 bytes de golpe, y en un bus
                # I2C por software eso falla con [Errno 5] Input/output error.
                conexion = luma_i2c(bus=SMBus(bus), address=direccion)
                modelo_oled = sh1106 if CHIP_OLED == "sh1106" else ssd1306
                self.disp = modelo_oled(conexion)
                self.fuente = ImageFont.load_default()
            else:
                raise ValueError(f"tipo de pantalla desconocido: {tipo}")
        except Exception as error:
            print(f"[pantalla] no disponible ({error}); los mensajes se mostrarán en consola")
            self.disp = None

    def mostrar(self, linea_superior, linea_inferior=""):
        print(f"[pantalla] {linea_superior} | {linea_inferior}", flush=True)
        if self.disp is None:
            return
        # Hasta 3 intentos: un error aislado del bus no debe dejar la pantalla sin actualizar.
        for intento in range(3):
            es_ultimo_intento = intento == 2
            if self._dibujar(linea_superior, linea_inferior, es_ultimo_intento):
                return
            time.sleep(0.05)

    def _dibujar(self, linea_superior, linea_inferior, avisar_si_falla):
        try:
            if self.tipo == "lcd":
                self.disp.clear()
                self.disp.write_string(linea_superior[:16])
                self.disp.cursor_pos = (1, 0)
                self.disp.write_string(linea_inferior[:16])
            else:
                with canvas(self.disp) as dibujo:
                    dibujo.text((0, 10), linea_superior, fill="white", font=self.fuente)
                    dibujo.text((0, 34), linea_inferior, fill="white", font=self.fuente)
            return True
        except Exception as error:
            if avisar_si_falla:
                print(f"[pantalla] error al escribir: {error}")
            return False

    def apagar(self):
        if self.disp is None:
            return
        try:
            if self.tipo == "lcd":
                self.disp.close(clear=True)
            else:
                self.disp.clear()
        except Exception:
            pass


class Almacen:
    def __init__(self, stock_forzado=None):
        self.led_verde = LED(PIN_LED_VERDE)
        self.led_amarillo = LED(PIN_LED_AMARILLO)
        self.led_rojo = LED(PIN_LED_ROJO)
        self.buzzer = Buzzer(PIN_BUZZER)
        self.pantalla = Pantalla(TIPO_PANTALLA, BUS_PANTALLA, DIRECCION_PANTALLA)

        if stock_forzado is not None:
            self.stock = stock_forzado
            guardar_stock(self.stock)
        else:
            self.stock = cargar_stock()

        self.lector_nfc = None
        self.ultima_etiqueta = None
        self.momento_ultima_lectura = 0.0
        self.sesion = None      # {"uid", "nombre", "expira"} mientras hay alguien con sesión

    def iniciar(self):
        self.pantalla.mostrar("Iniciando...")
        try:
            self.lector_nfc, version_firmware = iniciar_nfc()
        except Exception as error:
            self.detener_por_error(f"PN532 no detectado en el bus I2C {BUS_NFC}: {error}. "
                                   "Ejecuta diagnostico_nfc.py para ver la causa")

        registrar_evento("info", mensaje="Sistema listo", firmware=version_firmware, stock=self.stock)
        self.buzzer.beep(on_time=0.08, off_time=0.08, n=2)
        self.actualizar_leds()
        self.mostrar_reposo()

    def detener_por_error(self, mensaje):
        # LED rojo parpadeando = lector no encontrado. El programa se queda aquí hasta Ctrl+C.
        registrar_evento("error", mensaje=mensaje)
        self.pantalla.mostrar("Error lector NFC", "Revisar conexion")
        self.led_verde.off()
        self.led_amarillo.off()
        self.led_rojo.blink(on_time=0.2, off_time=0.2)
        while True:
            time.sleep(1)

    def ciclo_principal(self):
        while True:
            if self.sesion and time.monotonic() > self.sesion["expira"]:
                self.cerrar_sesion("Sesion expirada")

            uid = leer_uid(self.lector_nfc)
            if uid is None:
                continue

            # Mientras la misma etiqueta siga encima del lector se renueva el tiempo y no
            # se cuenta otra vez; para volver a contarla hay que retirarla un momento.
            ahora = time.monotonic()
            misma_etiqueta = uid == self.ultima_etiqueta
            if misma_etiqueta and ahora - self.momento_ultima_lectura < TIEMPO_ENTRE_LECTURAS:
                self.momento_ultima_lectura = ahora
                continue
            self.ultima_etiqueta = uid

            if uid in USUARIOS:
                self.procesar_credencial(uid)
            elif self.sesion is None:
                self.denegar_acceso(uid)
            else:
                self.sesion["expira"] = time.monotonic() + DURACION_SESION
                if uid == UID_REABASTECER:
                    self.reabastecer(uid)
                else:
                    self.retirar_articulo(uid)

            # Se toma el tiempo después de procesar: los avisos en pantalla tardan unos
            # segundos y la etiqueta puede seguir encima del lector.
            self.momento_ultima_lectura = time.monotonic()

    def procesar_credencial(self, uid):
        nombre = USUARIOS[uid]
        if self.sesion and self.sesion["uid"] == uid:
            self.cerrar_sesion("Sesion cerrada")
            return

        if not self.verificar_segundo_factor(uid):
            self.buzzer.beep(on_time=0.8, off_time=0, n=1)
            registrar_evento("acceso_denegado", usuario=nombre, uid=uid, motivo="segundo factor")
            self.mostrar_aviso("Acceso denegado", nombre)
            return

        self.sesion = {"uid": uid, "nombre": nombre,
                       "expira": time.monotonic() + DURACION_SESION}
        self.buzzer.beep(on_time=0.08, off_time=0.08, n=2)
        registrar_evento("inicio_sesion", usuario=nombre, uid=uid)
        self.mostrar_aviso("Acceso concedido", f"Hola {nombre}")

    def verificar_segundo_factor(self, uid):
        # Segundo paso de la doble autenticación. Por ahora siempre aprueba; aquí va el
        # reconocimiento facial: devolver True solo si el rostro es del dueño de `uid`.
        return True

    def cerrar_sesion(self, motivo):
        registrar_evento("cierre_sesion", usuario=self.sesion["nombre"], motivo=motivo)
        self.sesion = None
        self.buzzer.beep(on_time=0.08, off_time=0, n=1)
        self.mostrar_aviso(motivo)

    def denegar_acceso(self, uid):
        self.buzzer.beep(on_time=0.8, off_time=0, n=1)
        registrar_evento("acceso_denegado", uid=uid, motivo="sin sesion")
        self.mostrar_aviso("Acceso denegado", "Pase su credencial")

    def retirar_articulo(self, uid):
        usuario = self.sesion["nombre"]
        if self.stock == 0:
            self.buzzer.beep(on_time=0.8, off_time=0, n=1)
            registrar_evento("sin_stock", usuario=usuario, uid=uid)
            self.mostrar_aviso("Sin stock", "Reponer articulos")
            return

        self.stock -= 1
        guardar_stock(self.stock)
        self.actualizar_leds()
        self.buzzer.beep(on_time=0.15, off_time=0, n=1)
        registrar_evento("cambio_inventario", usuario=usuario, uid=uid, stock=self.stock,
                         nube="enviando_a_azure")
        self.mostrar_aviso("Retiro registrado", f"{self.stock} articulos")

    def reabastecer(self, uid):
        self.stock = STOCK_INICIAL
        guardar_stock(self.stock)
        self.actualizar_leds()
        self.buzzer.beep(on_time=0.08, off_time=0.08, n=3)
        registrar_evento("reabastecimiento", usuario=self.sesion["nombre"], uid=uid,
                         stock=self.stock, nube="enviando_a_azure")
        self.mostrar_aviso("Stock repuesto", f"{self.stock} articulos")

    def actualizar_leds(self):
        self.led_verde.off()
        self.led_amarillo.off()
        self.led_rojo.off()
        if self.stock > UMBRAL_VERDE:
            self.led_verde.on()
        elif self.stock > UMBRAL_AMARILLO:
            self.led_amarillo.on()
        else:
            self.led_rojo.on()

    def mostrar_reposo(self):
        if self.sesion:
            self.pantalla.mostrar(f"Hola {self.sesion['nombre']}", f"{self.stock} articulos")
        else:
            self.pantalla.mostrar("Pase su credencial", f"{self.stock} articulos")

    def mostrar_aviso(self, linea_superior, linea_inferior=""):
        self.pantalla.mostrar(linea_superior, linea_inferior)
        time.sleep(TIEMPO_AVISO)
        self.mostrar_reposo()

    def apagar_todo(self):
        for dispositivo in (self.led_verde, self.led_amarillo, self.led_rojo, self.buzzer):
            dispositivo.off()
        self.pantalla.apagar()


def main():
    parser = argparse.ArgumentParser(description="Sistema de almacén con NFC, pantalla, buzzer y LEDs")
    parser.add_argument("--stock", type=int, help="fija la cantidad de artículos")
    opciones = parser.parse_args()

    almacen = Almacen(stock_forzado=opciones.stock)
    try:
        almacen.iniciar()
        almacen.ciclo_principal()
    except KeyboardInterrupt:
        print("\nPrograma detenido.")
    finally:
        almacen.apagar_todo()


if __name__ == "__main__":
    main()