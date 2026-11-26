#!/usr/bin/env python3
"""Monitor en vivo de la placa de E/S, con consola de comandos.

Mantiene la sesión serie abierta y muestra los eventos en tiempo real.
Si se ejecuta en una terminal interactiva, acepta comandos:

    on | off            enciende o apaga la luz
    door [ms]           mueve el servo (por defecto 700 ms)
    armed | disarmed    cambia el modo de seguridad
    status              pide el estado ahora
    selftest            ejecuta el autotest de cableado
    enroll              inicia el enrolamiento de huella
    q                   sale

Uso:
    python3 Device/tests/monitor.py
    python3 Device/tests/monitor.py --no-status --bell
"""
from __future__ import annotations

import argparse
import json
import os
import select
import sys
import time

import serial

HELP = "comandos: on | off | door [ms] | armed | disarmed | status | dht | selftest | enroll | q"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Monitor en vivo de la placa")
    parser.add_argument("--port", default=os.environ.get("CASA_SERIAL_PORT", "/dev/ttyUSB0"))
    parser.add_argument("--no-status", action="store_true", help="oculta el estado periódico")
    parser.add_argument("--bell", action="store_true", help="pita al detectar movimiento")
    parser.add_argument("--raw", action="store_true", help="muestra líneas no JSON")
    parser.add_argument("--dht", action="store_true", help="sesión dedicada al DHT11 (solo lecturas)")
    return parser.parse_args()


def stamp() -> str:
    return time.strftime("%H:%M:%S")


def send(link: serial.Serial, payload: dict) -> None:
    link.write((json.dumps(payload, separators=(",", ":")) + "\n").encode())
    link.flush()


def main() -> int:
    options = parse_args()
    link = serial.Serial()
    link.port = options.port
    link.baudrate = 115200
    link.timeout = 0.15
    link.dtr = False
    link.rts = False
    link.open()

    interactive = sys.stdin.isatty()
    print(f"Monitor abierto en {options.port}. Ctrl+C para salir.")
    if interactive:
        print(HELP)

    buffer = b""
    pending_id = 0
    temps: list[float] = []
    hums: list[float] = []
    dht_failures = 0
    last_dht = 0.0
    try:
        while True:
            chunk = link.read(512)
            if chunk:
                buffer += chunk
                while b"\n" in buffer:
                    raw, buffer = buffer.split(b"\n", 1)
                    text = raw.decode("utf-8", "replace").strip()
                    if not text:
                        continue
                    try:
                        message = json.loads(text)
                    except json.JSONDecodeError:
                        if options.raw:
                            print(f"[{stamp()}] raw  {text[:120]}")
                        continue
                    kind = message.get("t")
                    if kind == "ready":
                        print(
                            f"[{stamp()}] READY   fw={message.get('fw')} "
                            f"fp={message.get('fp')} plantillas={message.get('tpl')} "
                            f"servo={message.get('servo_open')}/{message.get('servo_closed')}"
                        )
                        send(link, {"t": "dht"} if options.dht else {"t": "status"})
                    elif kind == "st":
                        if options.no_status or options.dht:
                            continue
                        print(
                            f"[{stamp()}] STATUS  luz={message.get('light')} "
                            f"ldr={message.get('ldr')}% pir={message.get('pir')} "
                            f"modo={message.get('mode')} puerta={message.get('door')} "
                            f"temp={message.get('t_c')} hum={message.get('rh')}"
                        )
                    elif kind == "ev":
                        name = message.get("k")
                        if name == "motion":
                            if options.bell:
                                sys.stdout.write("\a")
                                sys.stdout.flush()
                            print(f"[{stamp()}] >>> MOVIMIENTO   modo={message.get('mode')}")
                        elif name == "finger":
                            print(f"[{stamp()}] >>> HUELLA       plantilla={message.get('tpl')} confianza={message.get('conf')}")
                        elif name == "enroll":
                            state = "ok" if message.get("v") else "inicio/fallo"
                            print(f"[{stamp()}] >>> ENROLAMIENTO {state} plantilla={message.get('tpl')}")
                        elif name == "dht":
                            if message.get("ok"):
                                t_c = message.get("t_c")
                                rh = message.get("rh")
                                if isinstance(t_c, (int, float)):
                                    temps.append(float(t_c))
                                if isinstance(rh, (int, float)):
                                    hums.append(float(rh))
                                print(f"[{stamp()}] DHT     temp={t_c} °C  humedad={rh} %")
                            else:
                                dht_failures += 1
                                print(f"[{stamp()}] DHT     sin lectura (VCC a 3V3, DATA en D26, pull-up 10 kΩ)")
                        elif name == "link":
                            print(f"[{stamp()}] !!! enlace con el cerebro perdido")
                        elif name == "selftest":
                            step = message.get("step")
                            if step not in ("start", "done"):
                                print(
                                    f"[{stamp()}] TEST    {step:6s} ok={message.get('ok')} "
                                    f"raw={message.get('raw', '')} temp={message.get('t_c', '')} "
                                    f"hum={message.get('rh', '')} plantillas={message.get('tpl', '')}"
                                )
                    elif kind == "ack":
                        print(f"[{stamp()}] ack     {message}")
                    elif kind == "err":
                        print(f"[{stamp()}] error   {message.get('code')} (id={message.get('id')})")

            if options.dht and time.time() - last_dht >= 2.0:
                last_dht = time.time()
                send(link, {"t": "dht"})

            if interactive:
                ready, _, _ = select.select([sys.stdin], [], [], 0)
                if ready:
                    command = sys.stdin.readline().strip().lower()
                    if command in ("q", "quit", "exit"):
                        break
                    if command == "on":
                        send(link, {"t": "cmd", "id": f"mon-{pending_id}", "a": "light", "v": True})
                        pending_id += 1
                    elif command == "off":
                        send(link, {"t": "cmd", "id": f"mon-{pending_id}", "a": "light", "v": False})
                        pending_id += 1
                    elif command.startswith("door"):
                        parts = command.split()
                        ms = int(parts[1]) if len(parts) > 1 and parts[1].isdigit() else 700
                        send(link, {"t": "cmd", "id": f"mon-{pending_id}", "a": "door", "ms": ms})
                        pending_id += 1
                    elif command in ("armed", "disarmed"):
                        send(link, {"t": "cmd", "id": f"mon-{pending_id}", "a": "mode", "v": command})
                        pending_id += 1
                    elif command == "status":
                        send(link, {"t": "dht"} if options.dht else {"t": "status"})
                    elif command == "selftest":
                        send(link, {"t": "selftest"})
                    elif command == "enroll":
                        send(link, {"t": "enroll"})
                    elif command:
                        print(HELP)
    except KeyboardInterrupt:
        pass
    finally:
        link.close()
        if options.dht:
            if temps:
                print(
                    f"\nDHT: {len(temps)} lecturas  temp min/max/prom = "
                    f"{min(temps)}/{max(temps)}/{sum(temps) / len(temps):.1f} °C"
                )
            if hums:
                print(
                    f"     humedad min/max/prom = "
                    f"{min(hums)}/{max(hums)}/{sum(hums) / len(hums):.0f} %"
                )
            if dht_failures:
                print(f"     {dht_failures} lectura(s) fallida(s)")
        print("\nMonitor cerrado.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
