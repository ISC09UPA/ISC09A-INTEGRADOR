from gpiozero import OutputDevice

import config


# Bomba de agua conectada a un relé
class Bomba:
    # Configura el pin del relé, empieza apagada
    def __init__(self):
        self._rele = OutputDevice(config.PIN_RELE_BOMBA,
                                  active_high=not config.RELES_ACTIVOS_EN_BAJO,
                                  initial_value=False)

    # True si la bomba está encendida
    @property
    def encendida(self):
        return self._rele.is_active

    # Enciende la bomba
    def encender(self):
        self._rele.on()

    # Apaga la bomba
    def apagar(self):
        self._rele.off()
