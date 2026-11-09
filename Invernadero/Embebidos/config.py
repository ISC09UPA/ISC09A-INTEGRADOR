import os

from dotenv import load_dotenv

load_dotenv()

# Entradas
PIN_DHT22 = 4
PIN_CAUDALIMETRO = 17
PIN_NIVEL_AGUA = 27      # flotador a GND (pull-up interna): cerrado = sin agua
ADS1115_CANAL_HUMEDAD = 0  # el ADS1115 y el BH1750 van por I2C (GPIO 2 y 3)
ADS1115_CANAL_NIVEL = None  # canal del sensor de nivel analógico; None si solo hay flotador

# Salidas
PIN_RELE_BOMBA = 23
PIN_RELE_VENTILADOR = 24
PIN_LED_VERDE = 5
PIN_LED_ROJO = 6
PIN_LED_AZUL = 12
RELES_ACTIVOS_EN_BAJO = False  # True si el módulo de relés enciende con LOW

# Calibración (voltaje que entrega el sensor al ADS1115)
HUMEDAD_V_SECO = 2.8     # sensor capacitivo al aire
HUMEDAD_V_MOJADO = 1.2   # sensor metido en agua
NIVEL_V_VACIO = 0.0
NIVEL_V_LLENO = 3.3

# Riego (ajustar al calibrar con la planta elegida)
HUMEDAD_MINIMA = 35      # % para empezar a regar
HUMEDAD_OBJETIVO = 60    # % para dejar de regar
NIVEL_MINIMO = 10        # % de depósito para considerar "sin agua"
TIEMPO_MAX_BOMBA_S = 10
ESPERA_ENTRE_RIEGOS_S = 300
INTERVALO_LECTURA_S = 60
PULSOS_POR_LITRO = 5880  # YF-S401 (F = 98 × L/min)

# Ventilador con histéresis
TEMP_VENT_ON = 30.0      # °C
TEMP_VENT_OFF = 28.0     # °C

# Cámara
CARPETA_FOTOS = "fotos"
FOTO_RESOLUCION = (1920, 1080)
INTERVALO_FOTO_S = 3600

# Base de datos local
RUTA_DB_LOCAL = os.getenv("RUTA_DB_LOCAL", "riego.db")
