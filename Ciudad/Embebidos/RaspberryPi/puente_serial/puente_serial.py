#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Cruce Inteligente - puente TCP <-> serial en la Raspberry Pi
==============================================================

Fase de pruebas: el backend .NET y la app corren en la PC, y el ESP32 esta
conectado por USB a la Raspberry Pi. Este script es el "cable largo" entre
los dos:

    Backend (PC)  --TCP:5090-->  puente_serial.py (Raspberry)  --USB-->  ESP32
                  <-------------  (lineas OK:/ERR:/EST:)  <--------------

- Todo lo que llega por TCP (una linea por comando: MODO:AUTO, SET:1:VERDE,
  AMBULANCIA:2, TRAFICO:3,0,5,1 ...) se escribe tal cual al puerto serial.
- Todo lo que el ESP32 escribe por serial (OK:, ERR:, EST:...) se reenvia
  a todos los clientes TCP conectados (normalmente solo el backend).

El backend se conecta a este puente cuando en
Backend/CiudadInteligente.API/appsettings.json tiene:
    "Esp32": { "Transporte": "RaspberryPi",
               "RaspberryPi": { "Host": "<IP de la Raspberry>", "Puerto": 5090 } }

Uso (en la Raspberry Pi):
    pip install -r requirements.txt
    python3 puente_serial.py --serial /dev/ttyUSB0 --baud 115200 --puerto-tcp 5090

    Para ver que puerto tiene el ESP32:  ls /dev/ttyUSB* /dev/ttyACM*
    Si da "Permission denied":           sudo usermod -aG dialout $USER  (y reinicia sesion)
"""
import argparse
import socket
import sys
import threading
import time

import serial  # pyserial

LOCK_SERIAL = threading.Lock()
LOCK_CLIENTES = threading.Lock()
CLIENTES = []  # sockets conectados


def log(msg):
    print(f"[{time.strftime('%H:%M:%S')}] {msg}", flush=True)


def abrir_serial(puerto, baud):
    """Abre el puerto serial; reintenta hasta que el ESP32 aparezca."""
    while True:
        try:
            ser = serial.Serial()
            ser.port = puerto
            ser.baudrate = baud
            ser.timeout = 0.2
            # IMPORTANTE: en las placas ESP32 las lineas DTR/RTS del adaptador USB
            # estan cableadas a EN (reset) y GPIO0 (boot). pyserial las activa al
            # abrir y el ESP32 se queda trabado en reset mientras el puerto este
            # abierto: no corre el programa, no lee comandos y no manda nada.
            # Abrir con las dos apagadas deja al ESP32 corriendo normal.
            ser.dtr = False
            ser.rts = False
            ser.open()
            # por si el ESP32 se reinicio al conectar el USB, darle tiempo de arrancar
            time.sleep(2.0)
            ser.reset_input_buffer()
            log(f"serial abierto: {puerto} @ {baud} (DTR/RTS apagados)")
            return ser
        except serial.SerialException as exc:
            log(f"no se pudo abrir {puerto} ({exc}); reintento en 3 s")
            time.sleep(3)


def reenviar_a_clientes(linea_bytes):
    with LOCK_CLIENTES:
        caidos = []
        for cli in CLIENTES:
            try:
                cli.sendall(linea_bytes)
            except OSError:
                caidos.append(cli)
        for cli in caidos:
            CLIENTES.remove(cli)
            try:
                cli.close()
            except OSError:
                pass


def hilo_lectura_serial(estado):
    """Lee lineas del ESP32 y las manda a los clientes TCP."""
    pendiente = b""
    ultimo_dato = time.time()
    ya_aviso = False
    while True:
        # diagnostico: el ESP32 manda EST: cada pocos segundos en modo AUTO
        if not ya_aviso and time.time() - ultimo_dato > 10:
            log(f"AVISO: no recibo NADA del ESP32 en {estado['puerto']} desde hace 10 s. "
                "Revisa que sea el puerto correcto (ls -l /dev/serial/by-id/) y que el "
                "ESP32 tenga cargado semaforo_v2.ino.")
            ya_aviso = True
        ser = estado["serial"]
        try:
            datos = ser.read(ser.in_waiting or 1)  # sin esperar a juntar 256 bytes
        except serial.SerialException as exc:
            log(f"se perdio el serial ({exc}); reabriendo...")
            with LOCK_SERIAL:
                try:
                    ser.close()
                except Exception:
                    pass
                estado["serial"] = abrir_serial(estado["puerto"], estado["baud"])
            continue

        if not datos:
            continue
        ultimo_dato = time.time()
        ya_aviso = False
        pendiente += datos
        while b"\n" in pendiente:
            linea, pendiente = pendiente.split(b"\n", 1)
            linea = linea.rstrip(b"\r")
            if not linea:
                continue
            texto = linea.decode(errors="replace")
            # las respuestas a comandos siempre se ven; las EST: (muchas) solo con --verbose
            if estado["verbose"] or not texto.startswith("EST:"):
                log(f"ESP32 -> {texto}")
            reenviar_a_clientes(linea + b"\n")


def atender_cliente(cli, direccion, estado):
    """Lee comandos de un cliente TCP y los escribe al ESP32."""
    log(f"cliente conectado: {direccion[0]}:{direccion[1]}")
    pendiente = b""
    try:
        while True:
            datos = cli.recv(1024)
            if not datos:
                break
            pendiente += datos
            while b"\n" in pendiente:
                linea, pendiente = pendiente.split(b"\n", 1)
                linea = linea.strip()
                if not linea:
                    continue
                with LOCK_SERIAL:
                    try:
                        estado["serial"].write(linea + b"\n")
                    except serial.SerialException as exc:
                        log(f"no se pudo escribir al ESP32: {exc}")
                        cli.sendall(b"ERR:puente sin conexion serial con el ESP32\n")
                        continue
                log(f"PC -> ESP32: {linea.decode(errors='replace')}")
    except OSError:
        pass
    finally:
        with LOCK_CLIENTES:
            if cli in CLIENTES:
                CLIENTES.remove(cli)
        try:
            cli.close()
        except OSError:
            pass
        log(f"cliente desconectado: {direccion[0]}:{direccion[1]}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--serial", default="/dev/ttyUSB0", help="Puerto serial del ESP32 (default: /dev/ttyUSB0)")
    ap.add_argument("--baud", type=int, default=115200, help="Baudios (default: 115200, igual que el firmware)")
    ap.add_argument("--host", default="0.0.0.0", help="Interfaz donde escuchar (default: 0.0.0.0 = toda la red)")
    ap.add_argument("--puerto-tcp", type=int, default=5090, help="Puerto TCP para el backend (default: 5090)")
    ap.add_argument("--verbose", action="store_true", help="Imprime tambien cada linea que manda el ESP32")
    args = ap.parse_args()

    estado = {
        "puerto": args.serial,
        "baud": args.baud,
        "verbose": args.verbose,
        "serial": abrir_serial(args.serial, args.baud),
    }
    threading.Thread(target=hilo_lectura_serial, args=(estado,), daemon=True).start()

    servidor = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    servidor.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    servidor.bind((args.host, args.puerto_tcp))
    servidor.listen(4)
    log(f"puente escuchando en {args.host}:{args.puerto_tcp} (Ctrl+C para salir)")

    try:
        while True:
            cli, direccion = servidor.accept()
            cli.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            with LOCK_CLIENTES:
                CLIENTES.append(cli)
            threading.Thread(target=atender_cliente, args=(cli, direccion, estado), daemon=True).start()
    except KeyboardInterrupt:
        log("cerrando puente")
    finally:
        servidor.close()
        try:
            estado["serial"].close()
        except Exception:
            pass
        sys.exit(0)


if __name__ == "__main__":
    main()
