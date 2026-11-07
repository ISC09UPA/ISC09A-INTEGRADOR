#!/usr/bin/env python3
"""
prueba_camara.py - Prueba de captura de las camaras del Estacionamiento inteligente.
Corre en la Raspberry Pi o en cualquier computadora con camara (laptop).

  1. Muestra la imagen de la camara en una ventana.
  2. ESPACIO: toma la foto y la guarda en la carpeta fotos/ (igual que
     smart_parking.py). Si hay Azure AI Vision, intenta leer la placa.
  3. Q: salir.

Instalar (una vez):  pip install opencv-python requests
Ejecutar:            python3 prueba_camara.py picam  (camara de la Raspberry)
                     python3 prueba_camara.py 0      (webcam USB 0)

Necesita nube.py y config_azure.py en la misma carpeta.
"""
import platform
import sys
from datetime import datetime

import cv2

import nube


class _PiCam:
    """Camara de la Raspberry con la misma forma que cv2.VideoCapture."""
    def __init__(self):
        import camaras
        self.c = camaras
        self.ok = camaras.capturar("picam") is not None

    def isOpened(self):
        return self.ok

    def read(self):
        cuadro = self.c.capturar("picam")
        return cuadro is not None, cuadro

    def release(self):
        self.c.cerrar()


def abrir_camara(indice):
    if indice == "picam":
        return _PiCam()
    if platform.system() == "Windows":
        cam = cv2.VideoCapture(indice, cv2.CAP_DSHOW)   # abre mas rapido en Windows
    else:
        cam = cv2.VideoCapture(indice)
    cam.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cam.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
    return cam


def guardar(cuadro, indice):
    ahora = datetime.now()
    ok, jpg = cv2.imencode(".jpg", cuadro, [cv2.IMWRITE_JPEG_QUALITY, 90])
    if not ok:
        print("No se pudo codificar la imagen.")
        return
    foto = jpg.tobytes()
    destino = nube.guardar_foto(foto, f"{ahora:%Y%m%d_%H%M%S}_prueba_cam{indice}.jpg")
    alto, ancho = cuadro.shape[:2]
    print(f"[{ahora:%Y-%m-%d %H:%M:%S}] Foto tomada ({ancho}x{alto}) -> {destino}")
    if nube.USA_AZURE_VISION:
        try:
            lineas = nube.leer_texto_azure(foto)
            print(f"  Azure leyo: {lineas} -> placa: {nube.extraer_placa(lineas)}")
        except Exception as e:
            print(f"  Azure AI Vision no respondio ({e}).")


def main():
    arg = sys.argv[1] if len(sys.argv) > 1 else "picam"
    indice = int(arg) if arg.isdigit() else "picam"
    print("=== PRUEBA DE CAMARA - ESTACIONAMIENTO INTELIGENTE ===")
    print(f"Sistema: {platform.system()} | Camara: {indice} | Lectura de placas: {nube.modo()[1]}")
    cam = abrir_camara(indice)
    if not cam.isOpened():
        print(f"ERROR: no se encontro la camara {indice}.")
        return
    print("Camara lista. En la ventana: ESPACIO = tomar foto, Q = salir.")
    while True:
        ok, cuadro = cam.read()
        if not ok:
            print("ERROR: la camara no entrego imagen.")
            break
        vista = cuadro.copy()
        cv2.putText(vista, "ESPACIO: tomar foto   Q: salir", (20, 40),
                    cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 255, 0), 2)
        cv2.imshow("Estacionamiento inteligente - camara", vista)
        tecla = cv2.waitKey(1) & 0xFF
        if tecla == ord(" "):
            guardar(cuadro, indice)       # se guarda la imagen sin el texto
        elif tecla in (ord("q"), ord("Q"), 27):
            break
    cam.release()
    cv2.destroyAllWindows()
    print("Prueba terminada.")


if __name__ == "__main__":
    main()
