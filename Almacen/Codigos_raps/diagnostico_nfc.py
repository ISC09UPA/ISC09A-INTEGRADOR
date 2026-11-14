#!/usr/bin/env python3
"""
Diagnóstico del lector PN532 por I2C, paso a paso.

Habla con el PN532 directamente (sin la librería de Adafruit) para separar
problemas de conexión de problemas de código.

Uso:
  python diagnostico_nfc.py        # bus 3 (GPIO23/GPIO24)
  python diagnostico_nfc.py 1      # bus 1 (pines I2C normales GPIO2/GPIO3)
"""

import glob
import sys
import time
from pathlib import Path

from smbus2 import SMBus, i2c_msg

DIRECCION = 0x24
BUS = int(sys.argv[1]) if len(sys.argv) > 1 else 3
ACK = bytes([0x00, 0x00, 0xFF, 0x00, 0xFF, 0x00])


def titulo(n, texto):
    print(f"\n[{n}] {texto}")


def ok(texto):
    print(f"    OK     {texto}")


def falla(texto):
    print(f"    FALLA  {texto}")


def info(texto):
    print(f"    ·      {texto}")


# ---------------- Protocolo PN532 (crudo) ----------------
def trama(comando, parametros=b""):
    datos = bytes([0xD4, comando]) + bytes(parametros)
    largo = len(datos)
    return (bytes([0x00, 0x00, 0xFF, largo, (-largo) & 0xFF]) + datos
            + bytes([(-sum(datos)) & 0xFF, 0x00]))


def leer(bus, n):
    msg = i2c_msg.read(DIRECCION, n)
    bus.i2c_rdwr(msg)
    return bytes(list(msg))


def esperar_listo(bus, timeout=1.0):
    fin = time.monotonic() + timeout
    while time.monotonic() < fin:
        try:
            if leer(bus, 1)[0] == 0x01:
                return True
        except OSError:
            pass
        time.sleep(0.01)
    return False


def extraer_datos(respuesta):
    """Quita el preámbulo 00..00 FF LEN LCS y devuelve los datos de la trama."""
    i = 0
    while i < len(respuesta) and respuesta[i] == 0x00:
        i += 1
    if i >= len(respuesta) - 2 or respuesta[i] != 0xFF:
        return None
    largo = respuesta[i + 1]
    if (largo + respuesta[i + 2]) & 0xFF != 0:
        return None
    return respuesta[i + 3:i + 3 + largo]


def comando_crudo(bus, comando, parametros=b"", largo_respuesta=8, intentos=5):
    """Envía un comando con reintentos (el PN532 puede estar dormido). Devuelve los datos o None."""
    for intento in range(1, intentos + 1):
        try:
            bus.i2c_rdwr(i2c_msg.write(DIRECCION, trama(comando, parametros)))
        except OSError as e:
            info(f"intento {intento}: el PN532 no aceptó el comando ({e}); se despierta y reintenta")
            time.sleep(0.2)
            continue
        if not esperar_listo(bus):
            info(f"intento {intento}: no llegó la confirmación (ACK)")
            continue
        ack = leer(bus, 7)[1:]
        if ack != ACK:
            info(f"intento {intento}: confirmación inesperada {ack.hex(' ')}")
            continue
        if not esperar_listo(bus):
            info(f"intento {intento}: no llegó la respuesta")
            continue
        datos = extraer_datos(leer(bus, largo_respuesta + 8)[1:])
        if datos and datos[0] == 0xD5 and datos[1] == comando + 1:
            return datos[2:]
        info(f"intento {intento}: respuesta con formato inválido")
    return None


# ---------------- Pasos ----------------
def paso_bus():
    titulo(1, f"¿Existe el bus I2C {BUS}?")
    buses = sorted(glob.glob("/dev/i2c-*"))
    info(f"buses disponibles: {', '.join(buses) or 'ninguno'}")
    if Path(f"/dev/i2c-{BUS}").exists():
        ok(f"/dev/i2c-{BUS} existe")
        return True

    falla(f"/dev/i2c-{BUS} no existe")
    for ruta in ("/boot/firmware/config.txt", "/boot/config.txt"):
        if Path(ruta).exists():
            lineas = [l.strip() for l in Path(ruta).read_text().splitlines()
                      if "i2c" in l and not l.strip().startswith("#")]
            info(f"líneas I2C en {ruta}: {lineas or 'ninguna'}")
    if BUS == 3:
        info("Agrega al final de /boot/firmware/config.txt y reinicia:")
        info("  dtoverlay=i2c-gpio,bus=3,i2c_gpio_sda=23,i2c_gpio_scl=24")
    else:
        info("Activa I2C con sudo raspi-config -> Interface Options -> I2C y reinicia")
    return False


def paso_escaneo(bus):
    titulo(2, f"Escaneo del bus {BUS} (primero se despierta al PN532, que arranca dormido)")
    for _ in range(10):
        try:
            bus.i2c_rdwr(i2c_msg.read(DIRECCION, 1))
            break
        except OSError:
            time.sleep(0.1)
    vistos = set()
    for pasada in range(1, 4):
        encontrados = []
        for direccion in range(0x03, 0x78):
            try:
                bus.i2c_rdwr(i2c_msg.read(direccion, 1))
                encontrados.append(direccion)
            except OSError:
                pass
        vistos.update(encontrados)
        info(f"pasada {pasada}: {', '.join(hex(d) for d in encontrados) or 'nada'}")
        time.sleep(0.3)

    if DIRECCION in vistos:
        ok("el PN532 respondió en 0x24")
        return True
    if vistos:
        falla("hay dispositivos en el bus, pero ninguno en 0x24: revisa el switch del módulo")
    else:
        falla("nada responde en el bus: revisa cables SDA/SCL (prueba intercambiarlos), "
              "VCC a 3.3 V y que el GND del módulo esté unido al GND de la Raspberry")
    return False


def paso_firmware(bus):
    titulo(3, "Comando GetFirmwareVersion directo al chip (sin librería)")
    datos = comando_crudo(bus, 0x02, largo_respuesta=6)
    if datos and len(datos) >= 4:
        ok(f"PN532 responde: IC 0x{datos[0]:02X}, firmware {datos[1]}.{datos[2]}")
        return True
    falla("el chip no completó el comando")
    return False


def paso_libreria():
    titulo(4, "Librería de Adafruit tal como la usaba el script original")
    try:
        from adafruit_extended_bus import ExtendedI2C
        from adafruit_pn532.i2c import PN532_I2C
    except ImportError as e:
        falla(f"falta instalar librerías: {e}")
        return None
    try:
        pn532 = PN532_I2C(ExtendedI2C(BUS), debug=False)
        _, version, revision, _ = pn532.firmware_version
        ok(f"la librería funciona a la primera (firmware {version}.{revision})")
    except Exception as e:
        falla(f"{type(e).__name__}: {e}")

    titulo(5, "Librería de Adafruit despertando al chip y con reintentos (script corregido)")
    for intento in range(1, 6):
        try:
            with SMBus(BUS) as bus:
                for _ in range(10):
                    try:
                        bus.i2c_rdwr(i2c_msg.read(DIRECCION, 1))
                        break
                    except OSError:
                        time.sleep(0.1)
            pn532 = PN532_I2C(ExtendedI2C(BUS), debug=False)
            _, version, revision, _ = pn532.firmware_version
            pn532.SAM_configuration()
            ok(f"conectado en el intento {intento} (firmware {version}.{revision})")
            return pn532
        except Exception as e:
            info(f"intento {intento}: {type(e).__name__}: {e}")
            time.sleep(0.5)
    falla("la librería no pudo iniciar el PN532")
    return None


def paso_etiqueta(pn532):
    titulo(6, "Lectura de etiqueta: acerca una tarjeta al lector (15 s)")
    fin = time.monotonic() + 15
    while time.monotonic() < fin:
        try:
            uid = pn532.read_passive_target(timeout=0.5)
        except Exception:
            continue
        if uid:
            ok(f"etiqueta leída: {bytes(uid).hex().upper()}")
            return True
    falla("no se leyó ninguna etiqueta (acércala al centro de la antena, sobre el lado impreso)")
    return False


def main():
    print(f"Diagnóstico PN532 · bus I2C {BUS} · dirección 0x{DIRECCION:02X}")
    if not paso_bus():
        return
    with SMBus(BUS) as bus:
        visto = paso_escaneo(bus)
        firmware = paso_firmware(bus)
    if not visto and not firmware:
        print("\nConclusión: el sensor no responde a nivel eléctrico. Es conexión, alimentación o switch, no el código.")
        return
    pn532 = paso_libreria()
    if pn532:
        paso_etiqueta(pn532)
        print("\nConclusión: el sensor funciona. Usa el sistema_almacen.py corregido.")
    else:
        print("\nConclusión: el chip responde a comandos directos, pero la librería falla. Mándame esta salida.")


if __name__ == "__main__":
    main()
