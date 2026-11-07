import time

from gpiozero import DigitalInputDevice

import config


# Mide el agua que pasa contando pulsos
class Caudalimetro:
    # Configura el pin y los contadores
    def __init__(self, pin_bcm, pulsos_por_litro=config.PULSOS_POR_LITRO):
        # La salida del sensor va a 5 V: usar divisor de voltaje antes del GPIO (3.3 V)
        self._entrada = DigitalInputDevice(pin_bcm, pull_up=False)
        self._entrada.when_activated = self._contar_pulso  # flanco de subida
        self._pulsos_por_litro = pulsos_por_litro
        self.pulsos = 0
        self.caudal_lmin = 0.0
        self._pulsos_previos = 0
        self._ultimo_calculo = time.monotonic()

    # Suma un pulso cada vez que gira la turbina
    def _contar_pulso(self):
        self.pulsos += 1

    # Calcula el caudal en L/min cada segundo
    def actualizar(self):
        ahora = time.monotonic()
        transcurrido = ahora - self._ultimo_calculo
        if transcurrido < 1:
            return self.caudal_lmin
        pulsos = self.pulsos
        hz = (pulsos - self._pulsos_previos) / transcurrido
        self.caudal_lmin = hz * 60 / self._pulsos_por_litro
        self._pulsos_previos = pulsos
        self._ultimo_calculo = ahora
        return self.caudal_lmin

    # Convierte pulsos a mililitros
    def mililitros(self, pulsos):
        return pulsos / self._pulsos_por_litro * 1000
