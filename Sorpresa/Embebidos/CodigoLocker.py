

import time
import threading
from gpiozero import AngularServo, Button, LED, DistanceSensor, MCP3008 

# ---------- Pines BCM ----------
PIN_SERVO    = 12
PIN_PUERTA   = 17
PIN_VERDE    = 27
PIN_AMARILLO = 22
PIN_ROJO     = 23
PIN_BOTON    = 24
TRIG         = 25
ECHO         = 26

# ---------- Hardware ----------
# Configuracion del servo (ajusta min_pulse_width y max_pulse_width segun tu servo)
cerradura = AngularServo(PIN_SERVO, min_angle=0, max_angle=90, min_pulse_width=0.0005, max_pulse_width=0.0025)
puerta = Button(PIN_PUERTA, pull_up=True)
boton = Button(PIN_BOTON, pull_up=True, bounce_time=0.05)
led_verde = LED(PIN_VERDE)
led_amarillo = LED(PIN_AMARILLO)
led_rojo = LED(PIN_ROJO)
sonar = DistanceSensor(echo=ECHO, trigger=TRIG, max_distance=3.0)
sensor_peso = MCP3008(channel=0) # Requiere habilitar SPI en la Raspberry (sudo raspi-config)

# ---------- Parametros ----------
PESO_MAX_KG_SENSOR = 50.0
LIMITE_RENTA_KG    = 20.0
TOLERANCIA_KG      = 1.0
PESO_VACIO_KG      = 0.5
DIST_VACIO_CM      = 20.0
USAR_SONAR         = False

TIEMPO_RENTA = 60.0  # en segundos (60000 ms)
AVISO_ANTES  = 15.0  # en segundos
PERIODO_MSG  = 1.0   # en segundos

# ---------- Estado ----------
rentado = False
abierto = False
armado  = False
inicio_renta = 0.0
ultimo_msg = 0.0
peso_inicial = 0.0

# ---------- Utilidades ----------
def leer_peso():
    # sensor_peso.value devuelve un float entre 0 y 1
    return sensor_peso.value * PESO_MAX_KG_SENSOR

def leer_distancia_cm():
    # sonar.distance devuelve en metros
    return sonar.distance * 100.0

def locker_vacio():
    peso_ok = leer_peso() < PESO_VACIO_KG
    if not USAR_SONAR:
        return peso_ok
    return peso_ok and leer_distancia_cm() > DIST_VACIO_CM

def abrir():
    global abierto, armado
    cerradura.angle = 90
    abierto = True
    armado = False

def cerrar():
    global abierto
    cerradura.angle = 0
    abierto = False

def leds(verde, amarillo, rojo):
    if verde: led_verde.on()
    else: led_verde.off()
    
    if amarillo: led_amarillo.on()
    else: led_amarillo.off()
    
    if rojo: led_rojo.on()
    else: led_rojo.off()

# ---------- Acciones ----------
def rentar():
    global rentado, armado, inicio_renta, peso_inicial
    if rentado:
        print("El locker ya esta rentado.")
        return
    rentado = True
    armado = False
    inicio_renta = time.time()
    peso_inicial = 0.0
    leds(False, False, False)
    print("Renta iniciada. Use A para abrir con QR.")

def abrir_con_qr():
    if not rentado:
        print("Primero rente el locker (R).")
    elif (time.time() - inicio_renta) > TIEMPO_RENTA:
        print("ACCESO BLOQUEADO: tiempo excedido. Se aplica cargo extra.")
    elif abierto:
        print("El locker ya esta abierto.")
    else:
        abrir()
        print("QR valido. Locker abierto.")

def intentar_cerrar():
    global peso_inicial, armado
    if not rentado:
        print("No hay renta activa.")
        return
    if not abierto:
        print("El locker ya esta cerrado.")
        return
    if not puerta.is_pressed: # is_pressed = pin en LOW (puerta cerrada)
        print("Cierre la puerta (switch) antes de bloquear.")
        return
    
    peso = leer_peso()
    if peso > LIMITE_RENTA_KG:
        print("No se puede cerrar: peso sobre el limite. Retire objetos.")
        return
    
    cerrar()
    peso_inicial = peso
    armado = True
    print(f"Locker cerrado. Peso inicial: {peso:.2f} kg")
    if peso < PESO_VACIO_KG:
        print("AVISO: el locker esta vacio.")

def finalizar():
    global rentado
    if not rentado:
        print("No hay renta activa.")
        return
    
    abrir()
    time.sleep(0.5)
    
    if locker_vacio():
        rentado = False
        cerrar()
        leds(True, False, False)
        print("Locker vacio. Renta finalizada, locker libre.")
    else:
        print("ALERTA: aun hay objetos en el locker. Retirelos y pulse F.")

def mostrar_estado():
    peso = leer_peso()
    estado_puerta = "CERRADA" if puerta.is_pressed else "ABIERTA"
    estado_cerradura = "ABIERTA" if abierto else "BLOQUEADA"
    distancia = leer_distancia_cm()
    estado_renta = "ACTIVA" if rentado else "LIBRE"
    
    print(f"Peso: {peso:.2f} kg | Puerta: {estado_puerta} | Cerradura: {estado_cerradura} | Distancia: {distancia:.1f} cm | Renta: {estado_renta}")

# ---------- Supervision ----------
def supervisar():
    global ultimo_msg
    transcurrido = time.time() - inicio_renta
    peso = leer_peso()
    alerta = False
    aviso = False
    
    imprimir = (time.time() - ultimo_msg) >= PERIODO_MSG
    if imprimir:
        ultimo_msg = time.time()

    # Tiempo
    if transcurrido > TIEMPO_RENTA:
        alerta = True
        if imprimir: print("ALERTA: tiempo excedido. Cargo extra / acceso bloqueado.")
    elif transcurrido > (TIEMPO_RENTA - AVISO_ANTES):
        aviso = True
        if imprimir: print(f"AVISO: quedan {int(TIEMPO_RENTA - transcurrido)} s de renta.")

    # Sobrepeso
    if peso > LIMITE_RENTA_KG:
        alerta = True
        if imprimir: print(f"ALERTA: peso sobre el limite ({peso:.2f} kg).")

    # Vigilancia cerrado
    if armado:
        if abs(peso - peso_inicial) > TOLERANCIA_KG:
            alerta = True
            if imprimir: print("ALERTA: el peso cambio con el locker cerrado.")
        if not puerta.is_pressed:
            alerta = True
            if imprimir: print("ALERTA: puerta abierta sin autorizacion.")

    if alerta:
        leds(False, False, True)
    elif aviso:
        leds(False, True, False)
    else:
        leds(False, False, False)

# ---------- Hilo de Terminal ----------
def escuchar_terminal():
    while True:
        c = input().strip().upper()
        if c == 'R': rentar()
        elif c == 'A': abrir_con_qr()
        elif c == 'C': intentar_cerrar()
        elif c == 'F': finalizar()
        elif c == 'S': mostrar_estado()

# ---------- Principal ----------
def setup():
    cerrar()
    leds(True, False, False)
    # Vincular boton fisico
    boton.when_pressed = intentar_cerrar
    print("Locker libre. Comandos: R A C F S | Botón físico = cerrar")

def loop():
    while True:
        if rentado:
            supervisar()
        time.sleep(0.02) # Para no saturar el CPU

if __name__ == '__main__':
    setup()
    
    # Iniciar lectura de consola en un hilo separado
    hilo_consola = threading.Thread(target=escuchar_terminal, daemon=True)
    hilo_consola.start()
    
    try:
        loop()
    except KeyboardInterrupt:
        print("\nSaliendo...")