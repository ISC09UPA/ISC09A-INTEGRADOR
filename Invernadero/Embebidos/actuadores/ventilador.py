from gpiozero import OutputDevice

import config


# Ventilador conectado a un relé
class Ventilador:
    # Configura el pin del relé, empieza apagado
    def __init__(self):
        self._rele = OutputDevice(config.PIN_RELE_VENTILADOR,
                                  active_high=not config.RELES_ACTIVOS_EN_BAJO,
                                  initial_value=False)

    # True si el ventilador está encendido
    @property
    def encendido(self):
        return self._rele.is_active

    # Enciende si hace calor y apaga cuando baja la temperatura
    def actualizar(self, temperatura):
        if temperatura is None:
            return
        if not self.encendido and temperatura >= config.TEMP_VENT_ON:
            self._rele.on()
        elif self.encendido and temperatura <= config.TEMP_VENT_OFF:
            self._rele.off()

    # Enciende el ventilador
    def encender(self):
        self._rele.on()

    # Apaga el ventilador
    def apagar(self):
        self._rele.off()
