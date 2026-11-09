import time

from smbus2 import SMBus, i2c_msg

# Dirección y comandos del BH1750
DIRECCION = 0x23           # 0x5C si el pin ADDR está en alto
ENCENDER = 0x01
MODO_CONTINUO_ALTA = 0x10  # resolución de 1 lx, nueva medición cada 120 ms
REINTENTO_S = 10           # cada cuánto se reintenta conectar si no responde


# Sensor de luz
class SensorLuz:
    # Abre el bus I2C y pone el sensor a medir; si no responde, el sistema sigue sin luz
    def __init__(self, bus=1, direccion=DIRECCION):
        self._numero_bus = bus
        self._direccion = direccion
        self._bus = None
        self._ultimo_intento = 0.0
        self._conectar()
        if self._bus is None:
            print(f"!! Luz (BH1750) no responde: se reintenta cada {REINTENTO_S} s")

    # Intenta abrir el bus y configurar el sensor
    def _conectar(self):
        self._ultimo_intento = time.monotonic()
        try:
            self._bus = SMBus(self._numero_bus)
            self._bus.write_byte(self._direccion, ENCENDER)
            self._bus.write_byte(self._direccion, MODO_CONTINUO_ALTA)
        except OSError:  # sin sensor en el bus o I2C apagado
            self.cerrar()

    # Lee 2 bytes y los convierte a lux (None si falla o no está conectado)
    def leer(self):
        if self._bus is None:
            if time.monotonic() - self._ultimo_intento < REINTENTO_S:
                return None
            self._conectar()
            if self._bus is None:
                return None
            print(">> Luz (BH1750) conectada")
            return None  # la primera medición tarda 120 ms; se lee en la siguiente vuelta
        lectura = i2c_msg.read(self._direccion, 2)
        try:
            self._bus.i2c_rdwr(lectura)
        except OSError:
            # Se desconectó: al volver pierde la configuración, así que se reconfigura
            self.cerrar()
            return None
        alto, bajo = list(lectura)
        return ((alto << 8) | bajo) / 1.2

    # Cierra el bus I2C
    def cerrar(self):
        if self._bus is not None:
            self._bus.close()
            self._bus = None
