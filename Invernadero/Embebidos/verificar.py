import sys
import time

import config

# Guarda si cada prueba pasó o falló
resultados = []


# Ejecuta una prueba e imprime OK o FALLA
def probar(nombre, prueba):
    try:
        print(f"[ OK  ] {nombre}: {prueba()}")
        resultados.append(True)
    except Exception as e:
        print(f"[FALLA] {nombre}: {type(e).__name__}: {e}")
        resultados.append(False)


# Revisa que la placa sea una Raspberry Pi 4
def placa():
    with open("/proc/device-tree/model") as f:
        modelo = f.read().strip("\x00")
    if "Raspberry Pi 4" not in modelo:
        raise RuntimeError(f"se esperaba una Raspberry Pi 4 y es: {modelo}")
    return modelo


# Revisa que los pines GPIO se puedan usar
def gpio():
    from gpiozero import Device
    Device.ensure_pin_factory()
    fabrica = type(Device.pin_factory).__name__
    if fabrica not in ("LGPIOFactory", "RPiGPIOFactory"):
        raise RuntimeError(f"gpiozero usa {fabrica}; instala python3-lgpio")
    return fabrica


# Revisa que el ADS1115 y el BH1750 respondan por I2C
def i2c():
    from smbus2 import SMBus
    esperados = {0x48: "ADS1115", 0x23: "BH1750"}
    faltan = []
    with SMBus(1) as bus:
        for direccion, nombre in esperados.items():
            try:
                bus.read_byte(direccion)
            except OSError:
                faltan.append(f"{nombre} ({direccion:#04x})")
    if faltan:
        raise RuntimeError("no responden: " + ", ".join(faltan))
    return "ADS1115 y BH1750 responden"


# Lee la humedad del suelo y su voltaje
def humedad():
    from sensores import adc
    from sensores.humedad_suelo import SensorHumedad
    voltaje = adc.canal(config.ADS1115_CANAL_HUMEDAD).voltage
    porcentaje = SensorHumedad().leer()
    return f"{porcentaje} % ({voltaje:.2f} V; úsalo para calibrar HUMEDAD_V_SECO / MOJADO)"


# Lee la luz
def luz():
    from sensores.luz import SensorLuz
    sensor = SensorLuz()
    time.sleep(0.2)  # primera medición: 120 ms
    lux = sensor.leer()
    sensor.cerrar()
    if lux is None:
        raise RuntimeError("sin lectura")
    return f"{lux:.0f} lx"


# Lee la temperatura, con varios intentos
def temperatura():
    from sensores.temperatura import SensorTemperatura
    sensor = SensorTemperatura(config.PIN_DHT22)
    try:
        for _ in range(5):  # el DHT22 falla seguido
            if sensor.actualizar() is not None:
                return f"{sensor.temperatura:.1f} °C"
            time.sleep(2.1)
    finally:
        sensor.cerrar()
    raise RuntimeError("5 lecturas fallidas; revisa cableado y resistencia pull-up de 10k")


# Revisa el flotador del depósito
def flotador():
    from sensores.nivel_agua import SensorNivel
    sensor = SensorNivel()
    nivel = sensor.porcentaje()
    estado = "VACIO (cerrado)" if sensor.flotador_vacio else "OK (abierto)"
    return estado if nivel is None else f"{estado}, nivel {nivel} %"


# Cuenta los pulsos del caudalímetro durante 5 s
def caudal():
    from sensores.caudal import Caudalimetro
    sensor = Caudalimetro(config.PIN_CAUDALIMETRO)
    print("        Pasa agua o sopla por el caudalímetro durante 5 s...")
    time.sleep(5)
    if sensor.pulsos == 0:
        raise RuntimeError("0 pulsos (si no pasó agua es normal; si pasó, revisa el cable y el divisor)")
    return f"{sensor.pulsos} pulsos = {sensor.mililitros(sensor.pulsos):.1f} mL"


# Toma una foto de prueba
def camara():
    from sensores.camara import Camara
    cam = Camara()
    try:
        return f"foto guardada en {cam.tomar_foto()}"
    finally:
        cam.cerrar()


# Abre la base de datos y cuenta lo pendiente de enviar
def base_datos():
    from almacenamiento.db_local import TABLAS, BaseLocal
    db = BaseLocal()
    try:
        conteo = {tabla: len(db.pendientes(tabla, limite=10**9)) for tabla in TABLAS}
    finally:
        db.cerrar()
    return f"{config.RUTA_DB_LOCAL} abierta; pendientes de enviar: {conteo}"


# Prende LEDs, ventilador y bomba unos segundos
def actuadores():
    from actuadores.bomba import Bomba
    from actuadores.indicadores import Indicadores
    from actuadores.ventilador import Ventilador
    # LEDs, uno por uno
    indicadores = Indicadores()
    for nombre in ("funcionando", "error", "regando"):
        print(f"        Encendiendo {nombre} 1 s")
        salida = getattr(indicadores, nombre)
        salida.on()
        time.sleep(1)
        salida.off()
    # Ventilador
    ventilador = Ventilador()
    print("        Ventilador 2 s")
    ventilador.encender()
    time.sleep(2)
    ventilador.apagar()
    # Bomba, solo si el usuario confirma
    if input("        ¿Probar la bomba 2 s? Debe haber agua en el depósito (s/n): ").lower() == "s":
        bomba = Bomba()
        bomba.encender()
        time.sleep(2)
        bomba.apagar()
    return "revisa que cada salida haya respondido"


# Corre todas las pruebas (actuadores solo con --actuadores)
if __name__ == "__main__":
    probar("Placa", placa)
    probar("GPIO", gpio)
    probar("I2C", i2c)
    probar("Humedad (ADS1115)", humedad)
    probar("Luz (BH1750)", luz)
    probar("Temperatura (DHT22)", temperatura)
    probar("Flotador", flotador)
    probar("Caudalímetro", caudal)
    probar("Cámara", camara)
    probar("Base de datos", base_datos)
    if "--actuadores" in sys.argv:
        probar("Actuadores", actuadores)
    print(f"\n{sum(resultados)}/{len(resultados)} pruebas correctas")
    sys.exit(0 if all(resultados) else 1)
