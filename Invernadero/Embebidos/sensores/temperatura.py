import time

import adafruit_dht
import board

# Límites del DHT22
INTERVALO_S = 2       # el DHT22 no acepta más de una lectura cada 2 s
FALLOS_PARA_ERROR = 3  # el DHT22 falla seguido; solo es error tras 3 fallos seguidos


# Sensor de temperatura DHT22
class SensorTemperatura:
    # Prepara el sensor en el pin indicado
    def __init__(self, pin_bcm):
        pin = getattr(board, f"D{pin_bcm}")
        try:
            self._dht = adafruit_dht.DHT22(pin)  # usa el programa libgpiod_pulsein de Blinka
        except Exception:
            # Si ese programa no arranca (p. ej. versión de libgpiod distinta), leer por software
            self._dht = adafruit_dht.DHT22(pin, use_pulseio=False)
        self.temperatura = None  # °C; None hasta la primera lectura buena o si hay error
        self._fallos = 0
        self._ultima_lectura = None

    # True si el sensor lleva varios fallos seguidos
    @property
    def error(self):
        return self._fallos >= FALLOS_PARA_ERROR

    # Lee la temperatura cada 2 s y cuenta los fallos
    def actualizar(self):
        ahora = time.monotonic()
        if self._ultima_lectura is not None and ahora - self._ultima_lectura < INTERVALO_S:
            return self.temperatura
        self._ultima_lectura = ahora
        try:
            temperatura = self._dht.temperature
        except RuntimeError:  # checksum o sensor sin contestar
            temperatura = None
        if temperatura is None:
            self._fallos += 1
            if self.error:
                self.temperatura = None
        else:
            self.temperatura = temperatura
            self._fallos = 0
        return self.temperatura

    # Libera el sensor
    def cerrar(self):
        self._dht.exit()
