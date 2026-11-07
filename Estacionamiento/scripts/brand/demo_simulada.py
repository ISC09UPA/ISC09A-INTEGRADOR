#!/usr/bin/env python3
"""
demo_simulada.py - Ejecuta smart_parking.py con los sensores y el boton
SIMULADOS desde el teclado (pines virtuales de gpiozero). Sirve para probar
toda la logica sin tener el circuito armado. Corre en la Raspberry Pi o en
cualquier computadora con Python 3.

Instalar (si no es Raspberry Pi):  pip install gpiozero opencv-python requests
Ejecutar:                          python3 demo_simulada.py

Comandos (escribe y presiona Enter):
  e PLACA   un auto llega a la caseta (entrada o salida) con esa placa
            ejemplo:  e ABC123D
  o         un auto se estaciona en el espacio 4 (sensor del espacio)
  l         el auto deja el espacio 4
  b         presion corta del boton (abrir entrada)
  m         mantener el boton 3 s (activar / desarmar emergencia)
  q         salir
"""
import os
import queue
import threading
import time

os.environ["OPENCV_LOG_LEVEL"] = "ERROR"   # oculta avisos de camaras no conectadas

from gpiozero import Device
from gpiozero.pins.mock import MockFactory, MockPWMPin

Device.pin_factory = MockFactory(pin_class=MockPWMPin)   # pines virtuales

import nube                      # noqa: E402  (despues de los pines virtuales)
import smart_parking as sp       # noqa: E402

placas = queue.Queue()


class _SinCamara:
    """Simula que no hay camaras conectadas: la placa se pide por teclado."""
    def __init__(self, *args):
        pass

    def isOpened(self):
        return False

    def release(self):
        pass


sp.cv2.VideoCapture = _SinCamara
nube.placa_por_teclado = lambda momento: (
    lambda p: (print(f"  Placa del auto en la {momento} (teclado): {p}"), nube.normalizar(p))[1]
)(placas.get())

pin = Device.pin_factory.pin
SENSOR_ENTRADA, SENSOR_ESPACIO, BOTON = pin(24), pin(22), pin(21)
for p in (SENSOR_ENTRADA, SENSOR_ESPACIO, BOTON):
    p.drive_high()               # reposo: sensores sin deteccion, boton suelto


def pulso_entrada():
    SENSOR_ENTRADA.drive_low()   # el IR manda 0 cuando detecta
    time.sleep(1.5)
    SENSOR_ENTRADA.drive_high()


def comandos():
    print(__doc__.split("Comandos")[1])
    while True:
        try:
            linea = input().strip()
        except EOFError:
            break
        if not linea:
            continue
        cmd, *resto = linea.split()
        cmd = cmd.lower()
        # cada comando espera a que el sistema termine antes de leer el siguiente
        if cmd == "e" and resto:
            placas.put(resto[0])
            threading.Thread(target=pulso_entrada, daemon=True).start()
            time.sleep(11)
        elif cmd == "o":
            SENSOR_ESPACIO.drive_low(); time.sleep(2)
        elif cmd == "l":
            SENSOR_ESPACIO.drive_high(); time.sleep(2)
        elif cmd == "b":
            BOTON.drive_low(); time.sleep(0.3); BOTON.drive_high(); time.sleep(6)
        elif cmd == "m":
            BOTON.drive_low(); time.sleep(sp.TIEMPO_EMERGENCIA_S + 0.3); BOTON.drive_high()
            time.sleep(2)
        elif cmd == "q":
            raise SystemExit
        else:
            print("Comando no valido. Usa: e PLACA, o, l, b, m, q")


if __name__ == "__main__":
    print("=== DEMO CON SENSORES SIMULADOS (pines virtuales) ===")
    threading.Thread(target=sp.main, daemon=True).start()
    time.sleep(6)                # deja terminar el autodiagnostico
    try:
        comandos()
    except (KeyboardInterrupt, SystemExit):
        pass
    print("\nDemo terminada.")
