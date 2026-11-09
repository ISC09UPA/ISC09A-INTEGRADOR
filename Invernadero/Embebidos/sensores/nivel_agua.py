from gpiozero import Button

import config
from sensores import adc


# Nivel de agua del depósito
class SensorNivel:
    # Flotador y, si existe, sensor analógico
    def __init__(self):
        self._flotador = Button(config.PIN_NIVEL_AGUA, pull_up=True)
        self._canal = None
        if config.ADS1115_CANAL_NIVEL is not None:
            self._canal = adc.canal(config.ADS1115_CANAL_NIVEL)

    # True si el depósito está vacío
    @property
    def flotador_vacio(self):
        return self._flotador.is_pressed  # cerrado = sin agua

    # Nivel en % (None si no hay sensor analógico)
    def porcentaje(self):
        if self._canal is None:
            return None
        try:
            voltaje = self._canal.voltage
        except OSError:
            return None
        return adc.a_porcentaje(voltaje, config.NIVEL_V_VACIO, config.NIVEL_V_LLENO)
