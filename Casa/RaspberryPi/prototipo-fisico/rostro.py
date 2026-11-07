#!/usr/bin/env python3
"""Detecta un rostro en una captura local. No identifica a la persona."""
import os
import subprocess
import sys
import tempfile


def main():
    try:
        import cv2
        cascada = cv2.CascadeClassifier(cv2.data.haarcascades + 'haarcascade_frontalface_default.xml')
        if cascada.empty():
            raise RuntimeError('No se pudo cargar el detector de rostros.')
        with tempfile.TemporaryDirectory(prefix='casa-rostro-') as carpeta:
            foto = os.path.join(carpeta, 'captura.jpg')
            resultado = subprocess.run(
                ['rpicam-still', '-n', '-t', '2000', '--width', '640', '--height', '480', '-o', foto],
                capture_output=True, text=True, timeout=20)
            if resultado.returncode:
                raise RuntimeError('La cámara no pudo capturar: ' + resultado.stderr.strip()[-500:])
            imagen = cv2.imread(foto)
            if imagen is None:
                raise RuntimeError('No se pudo leer la captura.')
            gris = cv2.cvtColor(imagen, cv2.COLOR_BGR2GRAY)
            caras = cascada.detectMultiScale(gris, scaleFactor=1.1, minNeighbors=5, minSize=(60, 60))
            if len(caras):
                print('Rostro detectado. Acceso provisional: no se verificó identidad.')
                return 0
            print('No se detectó un rostro. Acceso no autorizado.')
            return 1
    except (ImportError, OSError, RuntimeError, subprocess.TimeoutExpired) as error:
        print(f'No se pudo comprobar el rostro: {error}', file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
