#!/usr/bin/env python3
"""
AeroPark · Simulador Rápido de Raspberry Pi (Edge Controller)
Simula: Servomotor de Pluma (PWM), Sensores Ultrasónicos (4 Cajones),
Lazo Inductivo, Semáforos y Sistema de Emergencia (Enclavamiento y LEDs Rojos).
No requiere hardware físico ni librerías externas complejas.
"""

import time
import json
import threading
try:
    import urllib.request
    HAS_URLLIB = True
except ImportError:
    HAS_URLLIB = False


BACKEND_URL = "http://localhost:5000/api/v1"

state = {
    "pluma_angulo": 0.0,            # 0.0° (Cerrada) a 90.0° (Abierta)
    "pwm_duty_ms": 1.0,             # 1.0ms (Cerrada) a 2.0ms (Abierta)
    "rele_enclavamiento": False,    # True = Corta energía a pluma (bloqueo total)
    "emergencia_activa": False,     # Código Rojo activo
    "semaforo": "ROJO",             # "VERDE" o "ROJO"
    "leds_rojos_estrobos": False,   # Balizas rojas a 3.2 Hz
    "lazo_inductivo": False,        # Auto detectado en carril
    "cajones": [
        {"id": "bay-01", "nombre": "Cajón 01", "distancia_cm": 18.0, "ocupado": True,  "placa": "ABC-123-D"},
        {"id": "bay-02", "nombre": "Cajón 02", "distancia_cm": 180.0, "ocupado": False, "placa": "—"},
        {"id": "bay-03", "nombre": "Cajón 03 (EV)", "distancia_cm": 22.0, "ocupado": True,  "placa": "JLM-456-E"},
        {"id": "bay-04", "nombre": "Cajón 04", "distancia_cm": 16.0, "ocupado": True,  "placa": "XYZ-789-A"},
    ]
}


def notificar_backend_barrera(comando, motivo="Simulador Raspberry Pi"):
    """Envía la orden al endpoint del backend si está encendido."""
    if not HAS_URLLIB:
        return
    try:
        url = f"{BACKEND_URL}/access/barrier"
        payload = json.dumps({"laneId": "carril-01", "command": comando, "reason": motivo}).encode('utf-8')
        req = urllib.request.Request(url, data=payload, headers={'Content-Type': 'application/json'})
        urllib.request.urlopen(req, timeout=1.0)
    except Exception:
        pass  # Modo autónomo local si el backend no está corriendo


def mover_pluma(abrir: bool):
    """Controla el servomotor PWM respetando el enclavamiento de emergencia."""
    if state["rele_enclavamiento"] or state["emergencia_activa"]:
        print("\n❌ [BLOQUEO FÍSICO]: No se puede mover la pluma. Sistema enclavado por EMERGENCIA.")
        return False

    if abrir:
        print("\n⚙️  [GPIO 18 PWM 50Hz]: Enviando pulso 2.0ms -> Levantando pluma a 90°...")
        state["pluma_angulo"] = 90.0
        state["pwm_duty_ms"] = 2.0
        state["semaforo"] = "VERDE"
        notificar_backend_barrera("OPEN", "Paso autorizado en carril")
    else:
        print("\n⚙️  [GPIO 18 PWM 50Hz]: Enviando pulso 1.0ms -> Bajando pluma a 0°...")
        state["pluma_angulo"] = 0.0
        state["pwm_duty_ms"] = 1.0
        state["semaforo"] = "ROJO"
        notificar_backend_barrera("CLOSE", "Cierre de carril")
    return True

def activar_emergencia():
    """Activa Código Rojo: Enclava pluma (0°), prende estrobos rojos y alerta C5."""
    state["emergencia_activa"] = True
    state["rele_enclavamiento"] = True
    state["pluma_angulo"] = 0.0
    state["pwm_duty_ms"] = 0.0  
    state["semaforo"] = "ROJO ESTROBOSCÓPICO"
    state["leds_rojos_estrobos"] = True
    notificar_backend_barrera("EMERGENCY_LOCK", "Paro de emergencia activado desde caseta")
    print("\n🚨🚨🚨 [CÓDIGO ROJO ACTIVADO] 🚨🚨🚨")
    print("🔒 Enclavamiento de relé activado: Subida y cerrado mecánicamente bloqueados.")
    print("💡 Balizas y LEDs rojos estroboscópicos encendidos a 3.2 Hz.")
    print("📡 Alerta transmitida al C5 / Fiscalía y Guardia Nacional.")

def desactivar_emergencia():
    """Desarma el protocolo de emergencia y libera los seguros."""
    state["emergencia_activa"] = False
    state["rele_enclavamiento"] = False
    state["semaforo"] = "ROJO"
    state["leds_rojos_estrobos"] = False
    state["pwm_duty_ms"] = 1.0
    notificar_backend_barrera("CLOSE", "Emergencia restablecida por supervisor")
    print("\n✅ [SISTEMA RESTABLECIDO]: Enclavamiento liberado. Pluma y sensores en operación estándar.")

def probar_azure():
    """Consulta el estado en vivo de Azure Cloud Services."""
    if not HAS_URLLIB:
        print("Módulo urllib no disponible.")
        return
    try:
        url = f"{BACKEND_URL}/azure/status"
        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=6.0) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            print("\n" + "="*65)
            print(" ☁️  ESTADO EN VIVO DE AZURE CLOUD SERVICES (CONECTIVIDAD LIVE)")
            print("="*65)
            print(f" • Estado Global       : 🟢 {data.get('status')}")
            services = data.get('services', {})
            cv = services.get('computerVision', {})
            acs = services.get('communicationServices', {})
            sb = services.get('serviceBus', {})
            blob = services.get('blobStorage', {})
            print(f" • Computer Vision     : 🟢 {cv.get('status')} ({cv.get('region')} · OCR LPR)")
            print(f" • Communication Svcs  : 🟢 {acs.get('status')} (Alertas y Códigos Rojos)")
            print(f" • Service Bus (IoT)   : 🟢 {sb.get('status')} (Namespace: {sb.get('namespace')}, Cola: {sb.get('queue')})")
            print(f" • Blob Storage        : 📁 {blob.get('status')} (Cuenta: {blob.get('account')})")
            print("="*65)
    except Exception as e:
        print(f"\n⚠️ No se pudo conectar con el endpoint de Azure del Backend: {e}")

def simular_ciclo_paso(placa="ABC-123-D"):
    """Simula: Auto llega -> Sensor lo detecta -> LPR y Azure -> Pluma abre -> Auto cruza -> Pluma cierra."""
    if state["emergencia_activa"]:
        print("\n❌ Maniobra rechazada: Sistema enclavado por emergencia.")
        return

    print(f"\n🚗 1. Automóvil aproximándose a caseta (Placa: {placa})...")
    state["lazo_inductivo"] = True
    time.sleep(0.5)

    print("📸 2. Cámara LPR capturando placa y consultando Backend & Azure Service Bus...")
    if HAS_URLLIB:
        try:
            req = urllib.request.Request(
                f"{BACKEND_URL}/access/evaluate",
                data=json.dumps({"laneId": "carril-01", "laneType": "ENTRY", "placa": placa, "confianza": 0.98}).encode('utf-8'),
                headers={'Content-Type': 'application/json'}
            )
            with urllib.request.urlopen(req, timeout=3.0) as resp:
                eval_data = json.loads(resp.read().decode('utf-8'))
                print(f"   ↳ Decisión Backend: {eval_data.get('decision')} | {eval_data.get('motivo')}")
                print(f"   ↳ Telemetría: Evento publicado a Azure Service Bus (estacionamiento-events)")
        except Exception:
            pass
    time.sleep(0.5)

    print("🟢 3. Acceso autorizado. Semáforo en VERDE.")
    mover_pluma(True)
    time.sleep(1.2)

    print("🚗 4. Vehículo cruzando el carril de acceso...")
    state["lazo_inductivo"] = False
    time.sleep(1.0)

    print("🔴 5. Carril despejado. Bajando pluma a 0°.")
    mover_pluma(False)
    print("✅ Ciclo de acceso completado con éxito.\n")

def alternar_cajon(num_cajon: int):
    """Simula el sensor ultrasónico HC-SR04 de un cajón."""
    idx = num_cajon - 1
    if 0 <= idx < len(state["cajones"]):
        cajon = state["cajones"][idx]
        cajon["ocupado"] = not cajon["ocupado"]
        if cajon["ocupado"]:
            cajon["distancia_cm"] = 18.5
            cajon["placa"] = "NUE-901-B"
        else:
            cajon["distancia_cm"] = 180.0
            cajon["placa"] = "—"
        print(f"\n🅿️ [{cajon['nombre']}]: Sensor HC-SR04 -> Distancia: {cajon['distancia_cm']} cm | Estado: {'OCUPADO (' + cajon['placa'] + ')' if cajon['ocupado'] else 'LIBRE'}")


def mostrar_dashboard():
    print("\n" + "="*65)
    print(" 🛫 AEROPARK · SIMULADOR EDGE RASPBERRY PI (CONTROLADOR DE CARRIL)")
    print("="*65)
    
    # Estado de la Pluma y Semáforo
    estado_bloqueo = "🔒 ENCLAVADO (BLOQUEADO)" if state["rele_enclavamiento"] else "🟢 OPERATIVO"
    estado_pluma = f"{state['pluma_angulo']}° ({'ABIERTA' if state['pluma_angulo'] > 45 else 'CERRADA'})"
    print(f" • Barrera / Pluma : {estado_pluma} | PWM: {state['pwm_duty_ms']}ms | Relé: {estado_bloqueo}")
    print(f" • Semáforo        : {state['semaforo']}")
    print(f" • Lazo Presencia  : {'◉ AUTO EN CASETA' if state['lazo_inductivo'] else '○ DESPEJADO'}")
    
    # Estado de Emergencia
    if state["emergencia_activa"]:
        print(f" • Emergencia      : 🚨 CÓDIGO ROJO ACTIVO | LEDs Rojos: ENCENDIDOS (3.2 Hz)")
    else:
        print(f" • Emergencia      : ⚪ En espera (Sin alertas)")

    # 4 Cajones IoT
    print("\n [Telemetría 4 Cajones IoT - Sensores Ultrasónicos HC-SR04]:")
    for c in state["cajones"]:
        tag = "🔴 OCUPADO" if c["ocupado"] else "🟢 LIBRE  "
        print(f"   {c['nombre']}: {tag} | Dist: {c['distancia_cm']:>5.1f} cm | Placa: {c['placa']}")
    print("="*65)

def main():
    while True:
        mostrar_dashboard()
        print("\nOpciones de prueba:")
        print(" [1] Simular llegada y paso automático de vehículo")
        print(" [2] Levantar pluma manualmente (90°)")
        print(" [3] Bajar pluma manualmente (0°)")
        print(" [4] Alternar ocupación de Cajón (1 al 4)")
        print(" [A] ☁️ Probar conectividad con la Nube de Azure (/api/v1/azure/status)")
        print(" [E] 🚨 DISPARAR PARO DE EMERGENCIA (Bloqueo + Estrobos Rojos)")
        print(" [R] ✅ Desactivar / Restablecer emergencia")
        print(" [Q] Salir")
        
        opc = input("\nElige una opción: ").strip().upper()
        
        if opc == '1':
            simular_ciclo_paso()
        elif opc == '2':
            mover_pluma(True)
        elif opc == '3':
            mover_pluma(False)
        elif opc == '4':
            c_num = input("Ingresa número de cajón (1-4): ").strip()
            if c_num in ['1', '2', '3', '4']:
                alternar_cajon(int(c_num))
            else:
                print("Cajón inválido.")
        elif opc == 'A':
            probar_azure()
        elif opc == 'E':
            activar_emergencia()
        elif opc == 'R':
            desactivar_emergencia()
        elif opc == 'Q':
            print("\nCerrando simulador Raspberry Pi...")
            break
        else:
            print("Opción no válida.")
        
        time.sleep(0.5)

if __name__ == "__main__":
    main()

