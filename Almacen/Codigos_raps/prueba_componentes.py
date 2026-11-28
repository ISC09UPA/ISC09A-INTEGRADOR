#!/usr/bin/env python3
"""
Prueba cada componente por separado, antes de correr el sistema completo.

  python prueba_componentes.py leds
  python prueba_componentes.py buzzer
  python prueba_componentes.py pantalla
  python prueba_componentes.py nfc
  python prueba_componentes.py todo
"""

import sys
import time

from gpiozero import LED, Buzzer

import sistema_almacen as sa


def probar_leds():
    for nombre, pin in (("verde", sa.PIN_LED_VERDE), ("amarillo", sa.PIN_LED_AMARILLO),
                        ("rojo", sa.PIN_LED_ROJO)):
        led = LED(pin)
        print(f"LED {nombre} (GPIO{pin}) encendido 1 s")
        led.on()
        time.sleep(1)
        led.off()
        led.close()


def probar_buzzer():
    buzzer = Buzzer(sa.PIN_BUZZER)
    print(f"Buzzer (GPIO{sa.PIN_BUZZER}): 3 pitidos")
    buzzer.beep(on_time=0.2, off_time=0.2, n=3, background=False)
    buzzer.close()


def probar_pantalla():
    pantalla = sa.Pantalla(sa.TIPO_PANTALLA, sa.BUS_PANTALLA, sa.DIRECCION_PANTALLA)
    if pantalla.disp is None:
        print("La pantalla no respondió. Revisa i2cdetect -y 4, TIPO_PANTALLA y DIRECCION_PANTALLA.")
        return
    pantalla.mostrar("Prueba pantalla", "Linea 2 OK")
    time.sleep(3)
    pantalla.apagar()


def probar_nfc():
    try:
        pn532, firmware = sa.iniciar_nfc()
    except Exception as e:
        print(f"PN532 no detectado en el bus I2C {sa.BUS_NFC}: {e}")
        return
    print(f"PN532 listo, firmware {firmware}. Pasa una etiqueta (10 s)...")
    fin = time.monotonic() + 10
    while time.monotonic() < fin:
        uid = sa.leer_uid(pn532, timeout=0.5)
        if uid:
            print(f"Etiqueta leída: {uid}")
            return
    print("No se leyó ninguna etiqueta.")


PRUEBAS = {"leds": probar_leds, "buzzer": probar_buzzer,
           "pantalla": probar_pantalla, "nfc": probar_nfc}

if __name__ == "__main__":
    opcion = sys.argv[1] if len(sys.argv) > 1 else "todo"
    if opcion == "todo":
        for prueba in PRUEBAS.values():
            prueba()
    elif opcion in PRUEBAS:
        PRUEBAS[opcion]()
    else:
        sys.exit(__doc__)
