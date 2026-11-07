#!/usr/bin/env python3
"""
ESTACIONAMIENTO INTELIGENTE - AEROPUERTO - Programa principal (Raspberry Pi 4B / 5)

Flujo:
 1. ENTRADA: el sensor IR de la caseta detecta el auto -> semaforo verde,
    rojo apagado, se abre la pluma de entrada.
 2. CAMARA + AZURE: la camara de la entrada toma la foto, Azure AI Vision lee
    la placa y se busca en la base policial (tabla PlacasReportadas).
    Si la camara no esta conectada o Azure no lee la placa, el operador la
    escribe en el teclado.
      - Reportada -> EMERGENCIA: buzzer, se bajan las dos plumas,
        LED de emergencia encendido. El auto queda retenido.
      - No reportada -> se le asigna espacio.
 3. ESPACIO: si aparto en la aplicacion (tabla Reservaciones) va a su espacio;
    si no, se le asigna uno libre que no este apartado por otra persona.
 4. El sensor IR de cada espacio detecta el auto -> la app lo ve OCUPADO
    (tabla Espacios) y se enciende el LED rojo del espacio. Si el auto no es
    el asignado a ese espacio -> aviso de espacio incorrecto.
 5. SALIDA: el auto deja el espacio -> la app lo ve LIBRE. Al llegar a la
    caseta, el sensor IR de salida lo detecta, la camara de salida confirma la placa, se registra placa, espacio y hora
    de salida (tabla Salidas) y se abre la pluma de salida.

LED de emergencia: va conectado directo al boton (se prende al presionarlo);
       la Raspberry no lo controla.
Desarmar emergencia: escribir D y Enter en la terminal.
Boton (solo si tambien se conecta al pin 40):
       presion corta = abrir entrada manual, mantener 3 s = activar / desarmar emergencia

SENSORES:
  - 2 INFRARROJOS: ENTRADA (abre la pluma de entrada) y SALIDA (abre la de salida).
  - 4 ULTRASONICOS HC-SR04, uno por espacio: si el auto esta a menos de
    UMBRAL_CM el espacio esta OCUPADO (LED rojo + app).
    El ECHO da 5 V: pasa por un divisor 1 kOhm / 2 kOhm antes de llegar al GPIO.

Conexiones (BCM -> pin fisico):
  Servo ENTRADA senal GPIO18 (pin 12)      Servo SALIDA senal GPIO12 (pin 32)
  IR ENTRADA OUT  GPIO24 (pin 18)          IR SALIDA OUT      GPIO25 (pin 22)
  ULTRASONICO 1  TRIG GPIO17 (pin 11)  ECHO GPIO27 (pin 13)   LED ESPACIO 1 GPIO5  (pin 29)
  ULTRASONICO 2  TRIG GPIO22 (pin 15)  ECHO GPIO23 (pin 16)   LED ESPACIO 2 GPIO6  (pin 31)
  ULTRASONICO 3  TRIG GPIO10 (pin 19)  ECHO GPIO9  (pin 21)   LED ESPACIO 3 GPIO26 (pin 37)
  ULTRASONICO 4  TRIG GPIO11 (pin 23)  ECHO GPIO8  (pin 24)   LED ESPACIO 4 GPIO4  (pin 7)
  SEMAFORO ENTRADA: verde GPIO19 (pin 35)   rojo GPIO13 (pin 33)
  SEMAFORO SALIDA:  verde GPIO14 (pin 8)    rojo GPIO15 (pin 10)
  Buzzer (con 220 Ohm) GPIO20 (pin 38)      Boton (opcional) GPIO21 (pin 40)
  Boton           GPIO21 (pin 40)
  Camara: Raspberry Pi Camera en el puerto CAMERA (cable plano) o webcams USB.
  Teclado: respaldo de placa.

Ejecutar: python3 smart_parking.py      Detener: Ctrl + C
"""
import select
import sys
import threading
from datetime import datetime
from time import sleep, monotonic

import warnings
from gpiozero import (AngularServo, DigitalInputDevice, DistanceSensor, LED,
                      Button)

warnings.filterwarnings("ignore", module="gpiozero")   # avisos de "sin eco", etc.

import camaras
from zumbador import Zumbador
import nube

# ---------------- Ajustes ----------------
ESPACIOS = [1, 2, 3, 4]        # espacios del estacionamiento
CONFIRMAR_S = 0.5              # la deteccion debe durar esto (evita falsos)
ESPERA_CIERRE_S = 2            # tras pasar el auto, espera antes de cerrar
TIEMPO_EMERGENCIA_S = 3        # mantener el boton este tiempo = emergencia
SENSOR_DETECTA_EN_BAJO = True  # la mayoria de los IR mandan 0 al detectar
ANG_CERRADA, ANG_ABIERTA = 0, 90
# Camara de cada caseta: "picam" = camara de la Raspberry (cable plano);
# un numero (0, 1...) = webcam USB. Con una sola camara, las dos casetas usan "picam".
CAMARAS = {"entrada": "picam", "salida": "picam"}
DESARME_TECLADO = True         # en emergencia, escribir D + Enter desarma

PIN_IR_ENTRADA, PIN_IR_SALIDA = 24, 25
PIN_ULTRA_ESPACIO = {1: (17, 27), 2: (22, 23), 3: (10, 9), 4: (11, 8)}   # (TRIG, ECHO)
UMBRAL_CM = 8                  # mas cerca que esto = espacio OCUPADO (ajustalo a tu maqueta)
PIN_LED_ESPACIO = {1: 5, 2: 6, 3: 26, 4: 4}

# ---------------- Hardware ----------------
def crear_servo(pin):
    return AngularServo(pin, min_angle=0, max_angle=180,
                        min_pulse_width=0.0005, max_pulse_width=0.0025,
                        initial_angle=None)


def crear_ir(pin):
    return DigitalInputDevice(pin, pull_up=SENSOR_DETECTA_EN_BAJO)


class SensorEspacio:
    """Ultrasonico HC-SR04 de un espacio. is_active = hay auto (mas cerca que UMBRAL_CM)."""
    def __init__(self, trig, echo):
        self.sensor = DistanceSensor(echo=echo, trigger=trig, max_distance=1,
                                     queue_len=3, partial=True)

    @property
    def cm(self):
        return self.sensor.distance * 100

    @property
    def is_active(self):
        return self.cm < UMBRAL_CM


servo_entrada = crear_servo(18)
servo_salida = crear_servo(12)
ir_entrada = crear_ir(PIN_IR_ENTRADA)
ir_salida = crear_ir(PIN_IR_SALIDA)
sensor_espacio = {e: SensorEspacio(t, ec) for e, (t, ec) in PIN_ULTRA_ESPACIO.items()}
led_espacio = {e: LED(p) for e, p in PIN_LED_ESPACIO.items()}
verde = LED(19)               # semaforo de la entrada
rojo = LED(13)
verde_salida = LED(14)        # semaforo de la salida
rojo_salida = LED(15)
buzzer = Zumbador(20)          # suena con buzzer activo o pasivo
boton = Button(21, pull_up=True, bounce_time=0.1, hold_time=TIEMPO_EMERGENCIA_S)

# ---------------- Estado ----------------
datos = nube.Almacen()
emergencia = threading.Event()
pedir_cambio_emergencia = threading.Event()
pedir_apertura_manual = threading.Event()
boton_mantenido = False
asignaciones = {}          # placa -> espacio, autos que entraron y no se han estacionado
estacionados = {}          # espacio -> placa, autos estacionados
hacia_salida = {}          # placa -> espacio, autos que dejaron su espacio


def ahora():
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


# ---------------- Actuadores ----------------
def mover(servo, angulo):
    """Mueve el servo y suelta la senal para que no vibre."""
    servo.angle = angulo
    sleep(0.6)
    servo.angle = None


def semaforo(paso):
    """Semaforo de la ENTRADA: verde = pasa, rojo = alto."""
    verde.value = paso
    rojo.value = not paso


def semaforo_salida(paso):
    """Semaforo de la SALIDA: verde = pasa, rojo = alto."""
    verde_salida.value = paso
    rojo_salida.value = not paso


def pitido(veces=1):
    for _ in range(veces):
        buzzer.on(); sleep(0.12); buzzer.off(); sleep(0.12)


def detectado(sensor):
    """True si el sensor detecta algo durante CONFIRMAR_S (evita falsos)."""
    inicio = monotonic()
    while sensor.is_active:
        if monotonic() - inicio >= CONFIRMAR_S:
            return True
        sleep(0.05)
    return False


def esperar_que_pase(sensor):
    """Espera a que el auto deje de tapar el sensor y un poco mas."""
    while sensor.is_active and not emergencia.is_set():
        sleep(0.1)
    sleep(ESPERA_CIERRE_S)


# ---------------- Camaras ----------------
def tomar_foto(momento):
    """Toma una foto con la camara de la entrada o de la salida.
    Regresa los bytes JPG, o None si no hay camara."""
    cuadro = camaras.capturar(CAMARAS[momento])
    if cuadro is None:
        print(f"  Camara de {momento} no disponible: se usara el teclado.")
        return None
    foto = camaras.a_jpg(cuadro)
    if foto is None:
        return None
    destino = nube.guardar_foto(foto, f"{datetime.now():%Y%m%d_%H%M%S}_{momento}.jpg")
    print(f"  Foto guardada: {destino}")
    return foto


def leer_placa(momento):
    """Foto -> Azure AI Vision. Respaldo: el operador escribe la placa."""
    foto = tomar_foto(momento)
    placa = nube.leer_placa(foto, momento)
    print(f"  Placa: {placa or 'NO CAPTURADA'}")
    return placa


# ---------------- Logica de negocio ----------------
def esta_reportada(placa):
    return datos.obtener("PlacasReportadas", placa) is not None


def estado_espacio(espacio):
    e = datos.obtener("Espacios", espacio)
    return e["estado"] if e else "LIBRE"


def apartado_por_otro(espacio, placa):
    for clave, r in datos.listar("Reservaciones"):
        if int(r["espacio"]) == espacio and clave != placa:
            return True
    return False


def asignar_espacio(placa):
    """Regresa (espacio, tiene_reservacion) o (None, False) si esta lleno."""
    reserva = datos.obtener("Reservaciones", placa) if placa else None
    if reserva:
        return int(reserva["espacio"]), True
    ocupados = set(asignaciones.values()) | set(estacionados)
    for e in ESPACIOS:
        if (estado_espacio(e) == "LIBRE" and e not in ocupados
                and not apartado_por_otro(e, placa)):
            return e, False
    return None, False


def activar_emergencia(motivo, placa=None):
    emergencia.set()
    print(f"\n!!! EMERGENCIA: {motivo}")
    semaforo(False)
    semaforo_salida(False)
    buzzer.beep(on_time=0.15, off_time=0.15)
    mover(servo_entrada, ANG_CERRADA)  # se bajan las plumas
    mover(servo_salida, ANG_CERRADA)
    datos.agregar("Eventos", {"tipo": "EMERGENCIA", "motivo": motivo,
                              "placa": placa or "", "hora": ahora()})
    print("    Para desarmar: escribe D y presiona Enter.")


def desarmar_emergencia():
    buzzer.off()
    semaforo(False)
    emergencia.clear()
    datos.agregar("Eventos", {"tipo": "DESARME", "motivo": "operador",
                              "placa": "", "hora": ahora()})
    print("\nEmergencia desarmada por el operador. Operacion normal.")


def procesar_entrada():
    print(f"\n>>> [{ahora()}] Auto en la ENTRADA")
    semaforo(True)                      # verde encendido, rojo apagado
    pitido()
    mover(servo_entrada, ANG_ABIERTA)

    placa = leer_placa("entrada") or f"SINPLACA{datetime.now():%H%M%S}"

    if esta_reportada(placa):
        activar_emergencia("placa con reporte policial", placa)
        return

    espacio, reservado = asignar_espacio(placa)
    if espacio is None:
        print("  Estacionamiento LLENO.")
    elif reservado:
        print(f"  Tiene reservacion: dirijase a su espacio {espacio}.")
    else:
        print(f"  Sin reservacion: espacio asignado {espacio}.")
    if espacio is not None:
        asignaciones[placa] = espacio
    datos.agregar("Eventos", {"tipo": "ENTRADA", "placa": placa,
                              "espacio": espacio or 0, "hora": ahora()})

    esperar_que_pase(ir_entrada)
    if emergencia.is_set():
        return
    semaforo(False)
    mover(servo_entrada, ANG_CERRADA)
    print("  Pluma de entrada cerrada.")


def espacio_ocupado(espacio):
    """El sensor del espacio detecto un auto: se verifica que sea el suyo."""
    placa = next((p for p, e in asignaciones.items() if e == espacio), None)
    if placa:
        print(f"\nEspacio {espacio} OCUPADO por {placa} (correcto, es su espacio).")
    else:
        # auto que entro y aun no se estaciona, pero tenia otro espacio
        placa = next(iter(asignaciones), None) or "DESCONOCIDA"
        suyo = asignaciones.get(placa)
        print(f"\nAVISO: el auto {placa} se estaciono en el espacio {espacio}"
              + (f", pero el suyo es el {suyo}." if suyo else "."))
        pitido(3)
        datos.agregar("Eventos", {"tipo": "ESPACIO_INCORRECTO", "placa": placa,
                                  "espacio": espacio, "hora": ahora()})
    asignaciones.pop(placa, None)
    estacionados[espacio] = placa
    led_espacio[espacio].on()
    datos.guardar("Espacios", espacio,
                  {"estado": "OCUPADO", "placa": placa, "desde": ahora()})


def espacio_liberado(espacio):
    """El auto dejo el espacio: la app lo ve LIBRE y va hacia la caseta de salida."""
    placa = estacionados.pop(espacio, None) or \
        (datos.obtener("Espacios", espacio) or {}).get("placa") or "DESCONOCIDA"
    led_espacio[espacio].off()
    datos.guardar("Espacios", espacio,
                  {"estado": "LIBRE", "placa": "", "desde": ahora()})
    hacia_salida[placa] = espacio
    print(f"\nEspacio {espacio} LIBRE. {placa} se dirige a la salida.")


def procesar_salida():
    print(f"\n>>> [{ahora()}] Auto en la caseta de SALIDA")
    placa = leer_placa("salida")
    if placa in hacia_salida:
        espacio = hacia_salida.pop(placa)
        print(f"  Placa confirmada: {placa} salio del espacio {espacio}.")
    elif placa is None and len(hacia_salida) == 1:
        placa, espacio = hacia_salida.popitem()
        print(f"  No se obtuvo la placa; se usa la del espacio: {placa}.")
    else:
        espacio = 0
        placa = placa or "DESCONOCIDA"
        print(f"  Atencion: {placa} no aparece saliendo de ningun espacio.")
    if esta_reportada(placa):
        activar_emergencia("placa con reporte policial en la salida", placa)
        return
    datos.agregar("Salidas", {"placa": placa, "espacio": espacio,
                              "hora_salida": ahora()})
    datos.borrar("Reservaciones", placa)   # la reservacion ya se uso
    print(f"  Salida registrada: {placa}, espacio {espacio}, {ahora()}")
    pitido()
    semaforo_salida(True)               # verde de salida encendido, rojo apagado
    mover(servo_salida, ANG_ABIERTA)
    esperar_que_pase(ir_salida)
    if emergencia.is_set():
        return
    semaforo_salida(False)
    mover(servo_salida, ANG_CERRADA)
    print("  Pluma de salida cerrada.")


def apertura_manual():
    print(f"\n>>> [{ahora()}] Apertura manual de la entrada (operador)")
    semaforo(True)
    pitido()
    mover(servo_entrada, ANG_ABIERTA)
    sleep(1)
    esperar_que_pase(ir_entrada)
    if not emergencia.is_set():
        semaforo(False)
        mover(servo_entrada, ANG_CERRADA)


def tecla_desarmar():
    """True si en la terminal se escribio D + Enter (sin detener el programa)."""
    if not DESARME_TECLADO:
        return False
    try:
        listo, _, _ = select.select([sys.stdin], [], [], 0)
        if listo:
            return sys.stdin.readline().strip().upper() == "D"
    except (OSError, ValueError):
        pass
    return False


# ---------------- Boton ----------------
def al_mantener_boton():
    global boton_mantenido
    boton_mantenido = True
    pedir_cambio_emergencia.set()


def al_soltar_boton():
    global boton_mantenido
    if boton_mantenido:
        boton_mantenido = False
    elif not emergencia.is_set():
        pedir_apertura_manual.set()


# ---------------- Inicio ----------------
def autodiagnostico():
    datos_modo, vision_modo = nube.modo()
    print("=== ESTACIONAMIENTO INTELIGENTE - AEROPUERTO ===")
    print(f"Datos: {datos_modo} | Lectura de placas: {vision_modo}")
    for led in (verde, rojo, verde_salida, rojo_salida, *led_espacio.values()):
        led.on(); sleep(0.3); led.off()
    pitido()
    mover(servo_entrada, ANG_ABIERTA); mover(servo_entrada, ANG_CERRADA)
    mover(servo_salida, ANG_ABIERTA); mover(servo_salida, ANG_CERRADA)
    for momento, fuente in CAMARAS.items():
        ok = camaras.capturar(fuente) is not None
        print(f"Camara de {momento} ({fuente}): {'OK' if ok else 'no disponible (respaldo: teclado)'}")
    for e in ESPACIOS:                 # estado inicial que vera la app
        if datos.obtener("Espacios", e) is None:
            datos.guardar("Espacios", e, {"estado": "LIBRE", "placa": "", "desde": ahora()})
    print("=== Listo ===\n")


def main():
    autodiagnostico()
    semaforo(False)
    semaforo_salida(False)
    boton.when_held = al_mantener_boton
    boton.when_released = al_soltar_boton

    sleep(1)                           # los ultrasonicos toman sus primeras lecturas
    ocupado_antes = {e: False for e in ESPACIOS}
    for e in ESPACIOS:
        if sensor_espacio[e].is_active:
            ocupado_antes[e] = True
            espacio_ocupado(e)
        else:
            datos.guardar("Espacios", e, {"estado": "LIBRE", "placa": "", "desde": ahora()})

    while True:
        if pedir_cambio_emergencia.is_set():
            pedir_cambio_emergencia.clear()
            if emergencia.is_set():
                desarmar_emergencia()
            else:
                activar_emergencia("boton del operador")

        if emergencia.is_set():
            if tecla_desarmar():
                desarmar_emergencia()
            sleep(0.1)
            continue

        if pedir_apertura_manual.is_set():
            pedir_apertura_manual.clear()
            apertura_manual()

        if detectado(ir_entrada):
            procesar_entrada()

        if detectado(ir_salida):
            procesar_salida()

        for e in ESPACIOS:
            ocupado = sensor_espacio[e].is_active
            if ocupado != ocupado_antes[e]:
                sleep(CONFIRMAR_S)              # confirmar el cambio
                if sensor_espacio[e].is_active == ocupado:
                    ocupado_antes[e] = ocupado
                    if ocupado:
                        espacio_ocupado(e)
                    else:
                        espacio_liberado(e)

        sleep(0.1)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
    finally:
        for d in (verde, rojo, verde_salida, rojo_salida, buzzer,
                  *led_espacio.values()):
            d.off()
        servo_entrada.angle = None
        servo_salida.angle = None
        camaras.cerrar()
        print("\nPrograma terminado.")
