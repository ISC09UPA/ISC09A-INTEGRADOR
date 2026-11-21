#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Cruce Inteligente - agente de Azure IoT Hub en la Raspberry Pi
================================================================

Reemplaza a puente_serial.py cuando el backend usa "Esp32:Transporte": "IoTHub".
La Raspberry ya no espera conexiones ni necesita que nadie sepa su IP: ELLA se
conecta hacia afuera a Azure IoT Hub (puerto 8883, MQTT sobre TLS) y por esa
conexion le llegan los comandos.

    Backend (PC) --HTTPS: direct method "comando"--> Azure IoT Hub --> agente_iot.py --USB--> ESP32
    Backend (PC) <--lee la telemetria (Event Hubs)-- Azure IoT Hub <-- agente_iot.py <--USB-- ESP32

Comandos (direct method "comando", payload {"comando": "SET:1:VERDE"}):
    se escriben tal cual al ESP32 y se contesta con lo que el ESP32 respondio:
        200 {"respuesta": "OK:SET"}           el ESP32 acepto el comando
        400 {"respuesta": "ERR:..."}          el ESP32 lo rechazo
        504 {"respuesta": null}               el ESP32 no contesto en 2 s
        503 {"respuesta": null}               el puerto serial no esta abierto

Varios comandos juntos (direct method "comandos", payload {"comandos": [...]}):
    el backend manda en UN solo viaje a Azure lo que antes eran varios (detener
    todo = MODO:MANUAL + 4 SET). Se ejecutan en orden aqui mismo, a milisegundos
    uno del otro. Contesta 200 si todos dieron OK:, 400 si alguno dio ERR:, y
    503/504 (deteniendose ahi) si se pierde la comunicacion con el ESP32.

Telemetria (mensajes de la Raspberry a IoT Hub), REDUCIDA a proposito:
    El ESP32 imprime ~17 lineas EST: por ciclo. Subirlas todas serian ~60,000
    mensajes al dia y el plan gratuito (F1) de IoT Hub permite 8,000. Por eso
    solo se suben:
      - EST:0:TODOS_ROJO   (1 por ciclo: el backend calcula el resto del ciclo
                            con los mismos tiempos que el firmware)
      - EST:n:PARPADEO     solo si llego ANTES de tiempo (un peaton presiono el
                            boton y recorto el verde), para corregir el calculo
      - {"tipo":"vivo"}    si no se ha mandado nada en 50 s (en manual o
                            ambulancia el ESP32 no imprime EST:), para que el
                            backend sepa que la Raspberry sigue conectada
    Con el ciclo de tiempos fijos (~21 s) son ~4,100 mensajes al dia.
    Cada linea EST: lleva "t" = cuando la imprimio el ESP32 (ms desde 1970, reloj
    de la Raspberry), para que el backend alinee el ciclo con los LED aunque el
    mensaje tarde en llegarle.

Uso (en la Raspberry Pi):
    python3 -m venv ~/venv-iot
    ~/venv-iot/bin/pip install azure-iot-device pyserial
    export IOTHUB_DEVICE_CONNECTION_STRING='HostName=...;DeviceId=raspberry-cruce04;SharedAccessKey=...'
    ~/venv-iot/bin/python agente_iot.py --serial /dev/ttyUSB0 --verbose

    (la cadena se obtiene con:
     az iot hub device-identity connection-string show -n <hub> -d raspberry-cruce04)
"""
import argparse
import json
import os
import queue
import sys
import threading
import time

import serial  # pyserial

COMANDOS_PERMITIDOS = ("MODO:", "SET:", "AMBULANCIA:", "TRAFICO:")
MAX_COMANDOS_POR_LOTE = 10
ESPERA_RESPUESTA_S = 2.0
SILENCIO_MAXIMO_S = 50.0
# un PARPADEO que llega al menos esto antes de lo esperado = boton peatonal
TOLERANCIA_PARPADEO_S = 0.4
# tope de seguridad para no gastar el limite diario si algo se descontrola
MAX_MENSAJES_POR_MINUTO = 20


def log(msg):
    print(f"[{time.strftime('%H:%M:%S')}] {msg}", flush=True)


def abrir_serial(puerto, baud):
    """Abre el puerto serial sin trabar al ESP32 en reset; reintenta si no esta."""
    while True:
        try:
            ser = serial.Serial()
            ser.port = puerto
            ser.baudrate = baud
            ser.timeout = 0.2
            # DTR/RTS van a EN (reset) y GPIO0 en las placas ESP32: abrir con las
            # dos apagadas para que el ESP32 siga corriendo (ver puente_serial.py)
            ser.dtr = False
            ser.rts = False
            ser.open()
            time.sleep(2.0)
            ser.reset_input_buffer()
            log(f"serial abierto: {puerto} @ {baud} (DTR/RTS apagados)")
            return ser
        except serial.SerialException as exc:
            log(f"no se pudo abrir {puerto} ({exc}); reintento en 3 s")
            time.sleep(3)


class Agente:
    """
    Logica del agente, separada del cliente de IoT Hub para poder probarla:
    `enviar` es cualquier funcion que reciba un dict y lo suba como telemetria.
    """

    def __init__(self, puerto, baud, enviar, verbose=False):
        self.puerto = puerto
        self.baud = baud
        self.enviar = enviar
        self.verbose = verbose
        self.ser = abrir_serial(puerto, baud)
        self.lock_serial = threading.Lock()
        self.lock_comando = threading.Lock()
        self.respuestas = queue.Queue()
        self.ultimo_envio = 0.0
        self.envios_recientes = []
        self.ultimo_verde = None  # (acceso, duracion_s, instante)
        self.ultimo_dato_esp32 = time.time()
        self.avisado_silencio = False

    # ------------------------------------------------------------ telemetria

    def subir(self, cuerpo, forzar=False):
        ahora = time.time()
        self.envios_recientes = [t for t in self.envios_recientes if ahora - t < 60]
        if not forzar and len(self.envios_recientes) >= MAX_MENSAJES_POR_MINUTO:
            log("AVISO: tope de mensajes por minuto alcanzado; se omite uno")
            return
        self.envios_recientes.append(ahora)
        self.ultimo_envio = ahora
        try:
            self.enviar(cuerpo)
            if self.verbose:
                log(f"-> IoT Hub: {json.dumps(cuerpo)}")
        except Exception as exc:  # sin internet: el SDK reintenta la conexion solo
            log(f"no se pudo subir a IoT Hub: {exc}")

    def serial_abierto(self):
        try:
            return self.ser.is_open
        except Exception:
            return False

    def latido_si_hace_falta(self):
        if time.time() - self.ultimo_envio >= SILENCIO_MAXIMO_S:
            self.subir({"tipo": "vivo", "serial": self.serial_abierto()}, forzar=True)

    # ------------------------------------------------- lo que manda el ESP32

    def procesar_linea_esp32(self, texto, leida_en=None):
        leida_en = leida_en or time.time()
        self.ultimo_dato_esp32 = leida_en
        self.avisado_silencio = False

        if texto.startswith("OK:") or texto.startswith("ERR:"):
            log(f"ESP32 -> {texto}")
            self.respuestas.put(texto)
            return

        if not texto.startswith("EST:"):
            if self.verbose:
                log(f"ESP32 -> {texto}")
            return

        if self.verbose:
            log(f"ESP32 -> {texto}")

        partes = texto.split(":")
        if len(partes) != 4:
            return
        _, acceso, fase, ms = partes
        try:
            acceso = int(acceso)
            duracion = int(ms) / 1000.0
        except ValueError:
            return

        momento = int(leida_en * 1000)
        if fase == "TODOS_ROJO":
            # inicio de ciclo: el backend calcula el resto con los tiempos del firmware
            self.subir({"tipo": "est", "linea": texto, "t": momento})
        elif fase == "VERDE":
            self.ultimo_verde = (acceso, duracion, leida_en)
        elif fase == "PARPADEO" and self.ultimo_verde and self.ultimo_verde[0] == acceso:
            _, dur_verde, inicio = self.ultimo_verde
            if leida_en - inicio < dur_verde - TOLERANCIA_PARPADEO_S:
                # el verde se corto antes (boton peatonal): corregir al backend
                self.subir({"tipo": "est", "linea": texto, "t": momento})

    def hilo_lectura(self):
        pendiente = b""
        while True:
            if not self.avisado_silencio and time.time() - self.ultimo_dato_esp32 > 15:
                log(f"AVISO: no recibo nada del ESP32 en {self.puerto} desde hace 15 s "
                    "(normal en modo manual/ambulancia; en automatico revisa el puerto)")
                self.avisado_silencio = True
            try:
                # lo que ya haya llegado (o esperar al primer byte, max 0.2 s):
                # read(256) esperaba hasta juntar 256 bytes y atrasaba cada
                # respuesta OK:/ERR: hasta 0.2 s
                datos = self.ser.read(self.ser.in_waiting or 1)
            except (serial.SerialException, OSError) as exc:
                log(f"se perdio el serial ({exc}); reabriendo...")
                with self.lock_serial:
                    try:
                        self.ser.close()
                    except Exception:
                        pass
                    self.ser = abrir_serial(self.puerto, self.baud)
                continue
            if not datos:
                continue
            leida_en = time.time()
            pendiente += datos
            while b"\n" in pendiente:
                linea, pendiente = pendiente.split(b"\n", 1)
                texto = linea.rstrip(b"\r").decode(errors="replace").strip()
                if texto:
                    self.procesar_linea_esp32(texto, leida_en)

    # ---------------------------------------------- lo que manda el backend

    def ejecutar_comando(self, comando):
        """Escribe el comando al ESP32 y espera su OK:/ERR:. Regresa (status, respuesta)."""
        comando = (comando or "").strip()
        if not comando.startswith(COMANDOS_PERMITIDOS) or "\n" in comando or len(comando) > 64:
            return 400, f"ERR:comando no permitido por el agente: {comando!r}"

        with self.lock_comando:
            while not self.respuestas.empty():
                self.respuestas.get_nowait()

            with self.lock_serial:
                if not self.serial_abierto():
                    return 503, None
                try:
                    self.ser.write(comando.encode("ascii", errors="ignore") + b"\n")
                except (serial.SerialException, OSError) as exc:
                    log(f"no se pudo escribir al ESP32: {exc}")
                    return 503, None
            log(f"PC -> ESP32: {comando}")

            try:
                respuesta = self.respuestas.get(timeout=ESPERA_RESPUESTA_S)
            except queue.Empty:
                log(f"AVISO: el ESP32 no contesto a {comando}")
                return 504, None

        return (200 if respuesta.startswith("OK:") else 400), respuesta

    def ejecutar_lote(self, comandos):
        """Varios comandos seguidos. Regresa (status, [respuestas])."""
        if not isinstance(comandos, list) or not comandos or len(comandos) > MAX_COMANDOS_POR_LOTE:
            return 400, [f"ERR:se esperan de 1 a {MAX_COMANDOS_POR_LOTE} comandos"]
        respuestas = []
        hubo_error = False
        for comando in comandos:
            status, texto = self.ejecutar_comando(comando if isinstance(comando, str) else "")
            respuestas.append(texto)
            if status in (503, 504):
                return status, respuestas  # sin comunicacion con el ESP32: no seguir
            hubo_error = hubo_error or status != 200
        return (400 if hubo_error else 200), respuestas


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--serial", default="/dev/ttyUSB0", help="Puerto serial del ESP32 (default: /dev/ttyUSB0)")
    ap.add_argument("--baud", type=int, default=115200, help="Baudios (default: 115200, igual que el firmware)")
    ap.add_argument("--conexion", default=os.environ.get("IOTHUB_DEVICE_CONNECTION_STRING"),
                    help="Cadena de conexion del DISPOSITIVO (o variable IOTHUB_DEVICE_CONNECTION_STRING)")
    ap.add_argument("--verbose", action="store_true", help="Muestra todas las lineas del ESP32 y cada mensaje subido")
    args = ap.parse_args()

    if not args.conexion:
        print("Falta la cadena de conexion del dispositivo. Ponla en la variable "
              "IOTHUB_DEVICE_CONNECTION_STRING o pasala con --conexion.", file=sys.stderr)
        sys.exit(2)

    from azure.iot.device import IoTHubDeviceClient, Message, MethodResponse

    cliente = IoTHubDeviceClient.create_from_connection_string(args.conexion)

    def enviar(cuerpo):
        mensaje = Message(json.dumps(cuerpo), content_encoding="utf-8", content_type="application/json")
        cliente.send_message(mensaje)

    agente = Agente(args.serial, args.baud, enviar, verbose=args.verbose)

    def al_recibir_metodo(solicitud):
        payload = solicitud.payload if isinstance(solicitud.payload, dict) else {}
        if solicitud.name == "comando":
            status, texto = agente.ejecutar_comando(payload.get("comando"))
            cuerpo = {"respuesta": texto}
        elif solicitud.name == "comandos":
            status, respuestas = agente.ejecutar_lote(payload.get("comandos"))
            cuerpo = {"respuesta": " | ".join(r or "-" for r in respuestas), "respuestas": respuestas}
        else:
            status, cuerpo = 404, {"respuesta": f"ERR:metodo desconocido {solicitud.name}"}
        cliente.send_method_response(MethodResponse.create_from_method_request(solicitud, status, cuerpo))

    def al_cambiar_conexion():
        log("conectado a IoT Hub" if cliente.connected else "desconectado de IoT Hub (reintentando...)")

    cliente.on_method_request_received = al_recibir_metodo
    cliente.on_connection_state_change = al_cambiar_conexion

    log("conectando a Azure IoT Hub...")
    cliente.connect()
    agente.subir({"tipo": "vivo", "serial": agente.serial_abierto()}, forzar=True)

    threading.Thread(target=agente.hilo_lectura, daemon=True).start()
    log("agente listo (Ctrl+C para salir)")
    try:
        while True:
            time.sleep(10)
            agente.latido_si_hace_falta()
    except KeyboardInterrupt:
        log("cerrando agente")
    finally:
        try:
            cliente.shutdown()
        except Exception:
            pass
        try:
            agente.ser.close()
        except Exception:
            pass


if __name__ == "__main__":
    main()
