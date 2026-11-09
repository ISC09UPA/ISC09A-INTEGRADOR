import time

import config
from sensores import adc

# Cada cuánto se reintenta conectar el ADS1115 si no responde
REINTENTO_S = 10


# Sensor de humedad del suelo
class SensorHumedad:
    # Usa un canal del ADS1115; si no responde, el sistema sigue sin humedad
    def __init__(self):
        self._canal = None
        self._ultimo_intento = 0.0
        self._conectar()
        if self._canal is None:
            print(f"!! Humedad (ADS1115) no responde: se reintenta cada {REINTENTO_S} s")

    # Intenta abrir el canal del ADS1115
    def _conectar(self):
        self._ultimo_intento = time.monotonic()
        try:
            self._canal = adc.canal(config.ADS1115_CANAL_HUMEDAD)
        except Exception:  # ValueError si no hay chip en 0x48, OSError si I2C está apagado
            self._canal = None

    # Lee el voltaje y lo convierte a % (None si falla o no está conectado)
    def leer(self):
        if self._canal is None:
            if time.monotonic() - self._ultimo_intento < REINTENTO_S:
                return None
            self._conectar()
            if self._canal is None:
                return None
            print(">> Humedad (ADS1115) conectada")
        try:
            voltaje = self._canal.voltage
        except OSError:
            return None
        return adc.a_porcentaje(voltaje, config.HUMEDAD_V_SECO, config.HUMEDAD_V_MOJADO)
