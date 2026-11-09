import board
import busio

# Importa el ADS1115 según la versión instalada de la librería
try:  # adafruit-circuitpython-ads1x15 3.x
    from adafruit_ads1x15 import ADS1115, AnalogIn, ads1x15
    _ENTRADAS = (ads1x15.Pin.A0, ads1x15.Pin.A1, ads1x15.Pin.A2, ads1x15.Pin.A3)
except ImportError:  # versiones 2.x
    from adafruit_ads1x15.ads1115 import ADS1115, P0, P1, P2, P3
    from adafruit_ads1x15.analog_in import AnalogIn
    _ENTRADAS = (P0, P1, P2, P3)

# El chip se crea una sola vez
_ads = None


# Devuelve un canal (0-3) del ADS1115
def canal(numero):
    global _ads
    if _ads is None:
        _ads = ADS1115(busio.I2C(board.SCL, board.SDA))
    return AnalogIn(_ads, _ENTRADAS[numero])


# Convierte un voltaje a porcentaje (0-100)
def a_porcentaje(voltaje, v_0, v_100):
    porcentaje = (voltaje - v_0) / (v_100 - v_0) * 100
    return int(max(0, min(100, porcentaje)))
