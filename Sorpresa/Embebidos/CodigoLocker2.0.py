import time
import threading
import RPi.GPIO as GPIO

# ---------- Importación de Picamera2 ----------
try:
    from picamera2 import Picamera2
    PICAMERA_OK = True
except ImportError:
    PICAMERA_OK = False
    print("AVISO: Librería 'picamera2' no instalada.")

# ---------- Pines BCM ----------
PIN_RELE     = 22  # Control del Cerrojo 12V (Pin físico 15) [VCC a 3.3V]
PIN_PUERTA   = 17  # Sensor Magnético MC-38 (Pin físico 11)
PIN_VERDE    = 16  # LED Libre (Pin físico 36)
PIN_AMARILLO = 21  # LED Alerta (Pin físico 40)
PIN_ROJO     = 20  # LED Ocupado (Pin físico 38)
PIN_TRIG     = 23  # HC-SR04 Trigger (Pin físico 16)
PIN_ECHO     = 24  # HC-SR04 Echo con divisor (Pin físico 18)

# ---------- Configuración RPi.GPIO (Anti-Busy Fix) ----------
GPIO.setmode(GPIO.BCM)
GPIO.setwarnings(False)

# Liberar los pines por si quedaron retenidos por una ejecución fallida anterior
pines = [PIN_RELE, PIN_PUERTA, PIN_VERDE, PIN_AMARILLO, PIN_ROJO, PIN_TRIG, PIN_ECHO]
for p in pines:
    try:
        GPIO.cleanup(p)
    except:
        pass

# Inicialización segura de Pines
GPIO.setup(PIN_RELE, GPIO.OUT, initial=GPIO.HIGH)
GPIO.setup(PIN_VERDE, GPIO.OUT, initial=GPIO.LOW)
GPIO.setup(PIN_AMARILLO, GPIO.OUT, initial=GPIO.LOW)
GPIO.setup(PIN_ROJO, GPIO.OUT, initial=GPIO.LOW)
GPIO.setup(PIN_PUERTA, GPIO.IN, pull_up_down=GPIO.PUD_UP)
GPIO.setup(PIN_TRIG, GPIO.OUT, initial=GPIO.LOW)
GPIO.setup(PIN_ECHO, GPIO.IN)

# ---------- Parámetros del Sistema ----------
TIEMPO_RENTA  = 60.0  # Duración de la renta en segundos
AVISO_ANTES   = 15.0  # Tiempo previo para encender LED amarillo
PERIODO_MSG   = 2.0   # Intervalo de refresco en consola
DIST_VACIO_CM = 15.0  # Umbral para considerar espacio libre en cm

# ---------- Variables de Estado ----------
rentado = False
abierto = False
armado  = False
inicio_renta = 0.0
ultimo_msg = 0.0

# ---------- Funciones Hardware ----------
def leds(v, a, r):
    """Controla el estado de los LEDs."""
    GPIO.output(PIN_VERDE, GPIO.HIGH if v else GPIO.LOW)
    GPIO.output(PIN_AMARILLO, GPIO.HIGH if a else GPIO.LOW)
    GPIO.output(PIN_ROJO, GPIO.HIGH if r else GPIO.LOW)

def accionar_cerrojo():
    """Envía pulso de 1.5s en LOW (0V) al GPIO 22 para conmutar el relé."""
    global abierto, armado
    print("\n[RELÉ - GPIO 22] Desbloqueando cerrojo... (¡CLIC!)")
    GPIO.output(PIN_RELE, GPIO.LOW)   # Enciende el relé
    time.sleep(1.5)                   # Mantiene el cerrojo liberado
    GPIO.output(PIN_RELE, GPIO.HIGH)  # Desactiva el relé
    print("[RELÉ - GPIO 22] Cerrojo en reposo.")
    abierto = True
    armado = False

def capturar_y_autenticar_faceid():
    """Cuenta 3 segundos, toma la fotografía con Picamera2 y simula Azure."""
    if not PICAMERA_OK:
        print("ERROR: Picamera2 no disponible.")
        return False

    print("\n[FACE ID]  Preparando cámara... Por favor ponte frente al lente.")
   
    # Temporizador de 3 segundos
    for i in range(3, 0, -1):
        print(f"   [CÁMARA] Capturando en {i}...")
        time.sleep(1.0)

    nombre_foto = "captura_faceid.jpg"
    print("   [CÁMARA]  Capturando fotografía...")
   
    try:
        picam2 = Picamera2()
        picam2.start()
        time.sleep(0.5)  # Ajuste rápido de brillo/exposición
       
        picam2.capture_file(nombre_foto)
        picam2.stop()
        picam2.close()
        print(f" [CÁMARA] Imagen guardada como '{nombre_foto}'.")
    except Exception as e:
        print(f" ERROR con Picamera2: {e}")
        return False

    # --- SIMULACIÓN DE AZURE FACE API ---
    print("[AZURE AI] 🌐 Validando características faciales en el servidor...")
    time.sleep(1.0)
   
    auth_exitosa = True  # Simulación de respuesta positiva
   
    if auth_exitosa:
        print(" [AZURE AI] Identidad confirmada. Acceso concedido.")
        return True
    else:
        print(" [AZURE AI] Usuario no reconocido. Acceso denegado.")
        return False

def leer_distancia_cm():
    """Lectura de distancia mediante el sensor HC-SR04."""
    GPIO.output(PIN_TRIG, GPIO.HIGH)
    time.sleep(0.00001)
    GPIO.output(PIN_TRIG, GPIO.LOW)

    inicio_pulso = time.time()
    fin_pulso = time.time()

    timeout = time.time() + 0.1
    while GPIO.input(PIN_ECHO) == 0:
        inicio_pulso = time.time()
        if inicio_pulso > timeout:
            return 999.0

    while GPIO.input(PIN_ECHO) == 1:
        fin_pulso = time.time()
        if fin_pulso > timeout:
            return 999.0

    duracion = fin_pulso - inicio_pulso
    distancia = (duracion * 34300) / 2
    return distancia

def puerta_cerrada():
    """Devuelve True si el imán está pegado al sensor MC-38."""
    return GPIO.input(PIN_PUERTA) == GPIO.LOW

# ---------- Lógica del Locker ----------
def rentar():
    global rentado, armado, inicio_renta
    if rentado:
        print("\n El locker ya se encuentra rentado.")
        return
    rentado = True
    armado = False
    inicio_renta = time.time()
    leds(False, False, True)  # LED Rojo = Ocupado
    print("\n RENTA INICIADA.")
    print("  -> Presiona 'A' para abrir con QR.")
    print("  -> Presiona 'V' para abrir con FaceID (Toma foto en 3s).")

def abrir_con_qr():
    if not rentado:
        print("\n Primero inicia la renta con 'R'.")
    elif (time.time() - inicio_renta) > TIEMPO_RENTA:
        print("\n ACCESO BLOQUEADO: Tiempo de renta excedido.")
    else:
        print("\n QR VÁLIDO.")
        accionar_cerrojo()

def abrir_con_faceid():
    if not rentado:
        print("\n Primero inicia la renta con 'R'.")
    elif (time.time() - inicio_renta) > TIEMPO_RENTA:
        print("\n ACCESO BLOQUEADO: Tiempo de renta excedido.")
    else:
        if capturar_y_autenticar_faceid():
            accionar_cerrojo()

def intentar_cerrar():
    global armado, abierto
    if not rentado:
        print("\n No hay una renta activa.")
        return
   
    if not puerta_cerrada():
        print("\n SENSOR PUERTA: La puerta sigue ABIERTA. Junta el imán para asegurar.")
        return
   
    abierto = False
    armado = True
    print("\n LOCKER ASEGURADO. Monitoreo de seguridad activo.")

def finalizar():
    global rentado
    if not rentado:
        print("\n No hay renta activa para finalizar.")
        return
   
    accionar_cerrojo()
    time.sleep(0.5)
   
    dist = leer_distancia_cm()
    print(f"[ULTRASÓNICO] Distancia detectada: {dist:.1f} cm")
   
    if dist > DIST_VACIO_CM:
        rentado = False
        leds(True, False, False)  # LED Verde = Libre
        print("\n RENTA FINALIZADA. Locker libre y disponible.")
    else:
        print("\n ALERTA: Aún hay objetos dentro. Retíralos y presiona 'F'.")

def mostrar_estado():
    est_puerta = "CERRADA (Imán pegado)" if puerta_cerrada() else "ABIERTA (Imán separado)"
    est_cerrojo = "ABIERTA" if abierto else "BLOQUEADA"
    dist = leer_distancia_cm()
    est_renta = "ACTIVA" if rentado else "LIBRE"
   
    print("\n" + "="*60)
    print(f" Renta: {est_renta} | Cerrojo: {est_cerrojo}")
    print(f" Puerta: {est_puerta}")
    print(f" Sensor Ultrasónico: {dist:.1f} cm")
    print("="*60)

# ---------- Hilo de Supervisión ----------
def supervisar():
    global ultimo_msg
    transcurrido = time.time() - inicio_renta
    alerta = False
    aviso = False
   
    imprimir = (time.time() - ultimo_msg) >= PERIODO_MSG
    if imprimir:
        ultimo_msg = time.time()

    if transcurrido > TIEMPO_RENTA:
        alerta = True
        if imprimir: print("\n ALERTA: Tiempo de renta agotado.")
    elif transcurrido > (TIEMPO_RENTA - AVISO_ANTES):
        aviso = True
        if imprimir: print(f"\n AVISO: Quedan {int(TIEMPO_RENTA - transcurrido)} s de renta.")

    if armado and not puerta_cerrada():
        alerta = True
        if imprimir:
            print("\n ALERTA DE SEGURIDAD: ¡Puerta forzada sin autorización!")

    if alerta:
        leds(False, False, True)    # Rojo
    elif aviso:
        leds(False, True, False)   # Amarillo

# ---------- Interfaz de Consola ----------
def escuchar_terminal():
    while True:
        c = input().strip().upper()
        if c == 'R': rentar()
        elif c == 'A': abrir_con_qr()
        elif c == 'V': abrir_con_faceid()
        elif c == 'C': intentar_cerrar()
        elif c == 'F': finalizar()
        elif c == 'S': mostrar_estado()

def setup():
    leds(True, False, False)
    print("\n--- SISTEMA LOCKER INTEGRADO (PIN 22 - FIX GPIO BUSY) ---")
    print("Comandos disponibles:")
    print("  [R] -> Iniciar Renta")
    print("  [A] -> Abrir Cerrojo con QR")
    print("  [V] -> Abrir Cerrojo con FaceID (3s + Foto Picamera2)")
    print("  [C] -> Intentar Cerrar (Verifica Sensor Magnético)")
    print("  [F] -> Finalizar Renta (Comprueba ultrasónico)")
    print("  [S] -> Mostrar Estado de Sensores\n")

if __name__ == '__main__':
    setup()
   
    hilo = threading.Thread(target=escuchar_terminal, daemon=True)
    hilo.start()
   
    try:
        while True:
            if rentado:
                supervisar()
            time.sleep(0.05)
    except KeyboardInterrupt:
        GPIO.cleanup()
        print("\nPrograma finalizado de forma limpia.")