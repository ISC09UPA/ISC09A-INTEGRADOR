from datetime import datetime
from pathlib import Path

from picamera2 import Picamera2

import config


# Cámara de la Raspberry Pi
class Camara:
    # Crea la carpeta de fotos y enciende la cámara
    def __init__(self, carpeta=config.CARPETA_FOTOS):
        self._carpeta = Path(carpeta)
        self._carpeta.mkdir(parents=True, exist_ok=True)
        self._camara = Picamera2()
        self._camara.configure(
            self._camara.create_still_configuration(main={"size": config.FOTO_RESOLUCION}))
        self._camara.start()  # queda encendida para no esperar el arranque en cada foto

    # Toma una foto y la guarda con la fecha como nombre
    def tomar_foto(self):
        ruta = self._carpeta / f"{datetime.now():%Y%m%d_%H%M%S}.jpg"
        self._camara.capture_file(str(ruta))
        return ruta

    # Apaga la cámara
    def cerrar(self):
        self._camara.stop()
        self._camara.close()
