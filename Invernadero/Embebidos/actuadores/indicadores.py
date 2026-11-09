from gpiozero import LED

import config


# LEDs de estado del sistema
class Indicadores:
    # Verde: funcionando, rojo: error, azul: regando
    def __init__(self):
        self.funcionando = LED(config.PIN_LED_VERDE)
        self.error = LED(config.PIN_LED_ROJO)
        self.regando = LED(config.PIN_LED_AZUL)

    # Apaga todos los LEDs
    def apagar_todo(self):
        for salida in (self.funcionando, self.error, self.regando):
            salida.off()
