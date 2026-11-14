#!/usr/bin/env python3
"""Presenta en la Mac los eventos que Azure recibe de la Raspberry."""
import argparse
from datetime import datetime
import json
import os
import signal
import subprocess
import sys


def render(event):
    payload = event.get("payload", {})
    if not isinstance(payload, dict):
        return
    kind = payload.get("type", "Evento")
    values = payload.get("payload", {})
    if not isinstance(values, dict):
        values = {}
    try:
        when = datetime.fromisoformat(payload.get("occurredAtUtc", "").replace("Z", "+00:00"))
        stamp = when.astimezone().strftime("%d/%m/%Y %H:%M:%S")
    except (ValueError, TypeError):
        stamp = "Hora no disponible"
    origin = event.get("origin", "desconocido")
    use_color = sys.stdout.isatty()
    cyan, green, yellow, reset = ("\033[96m", "\033[92m", "\033[93m", "\033[0m") if use_color else ("", "", "", "")
    print(f"\n{cyan}╭─ CASA DOMÓTICA · {stamp} ─╮{reset}")
    print(f"│ Raspberry: {origin}")
    if kind == "TelemetryReported":
        light = values.get("lightPercent")
        light_text = f"{light:g}%" if isinstance(light, (int, float)) and not isinstance(light, bool) else "sin dato"
        motion = {True: "Detectado", False: "Sin movimiento"}.get(values.get("motion"), "Sin dato")
        lamp = {True: "Encendida", False: "Apagada"}.get(values.get("lightOn"), "Sin dato")
        security = {"armed": "Armada", "disarmed": "Desarmada"}.get(values.get("securityModeApplied"), "Sin dato")
        print(f"│ ☀  Luz ambiental: {yellow}{light_text}{reset}")
        print(f"│ ◉  Movimiento:    {motion}")
        print(f"│ 💡 Luz de casa:   {green}{lamp}{reset}")
        print(f"│ 🔒 Seguridad:     {security}")
    elif kind == "MotionDetected":
        print(f"│ ⚠  Movimiento detectado en {values.get('roomId', 'habitación desconocida')}")
        print("│ Seguridad armada al detectar movimiento")
    else:
        print(f"│ Evento recibido: {kind}")
        if kind == "CommandResult":
            print(f"│ Resultado: {values.get('status', 'sin dato')}")
    print(f"{cyan}╰────────────────────────────────────────────╯{reset}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cantidad", type=int, default=None, help="Detener después de recibir esta cantidad de mensajes")
    args = parser.parse_args()
    if args.cantidad is not None and args.cantidad < 1:
        parser.error("la cantidad debe ser positiva")
    cmd = ["az", "iot", "hub", "monitor-events", "--hub-name", "i9n-karma-iothub",
           "--device-id", "raspberry-sim", "--transport", "amqp_ws", "--yes"]
    if args.cantidad:
        cmd += ["--message-count", str(args.cantidad), "--timeout", "60"]
    print("Conectando con Azure… Esperando datos de la Raspberry. Ctrl+C para salir.", flush=True)
    env = dict(os.environ, PYTHONUNBUFFERED="1")
    try:
        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, text=True, env=env, start_new_session=True)
    except FileNotFoundError:
        print("No se encontró Azure CLI (az) en esta Mac.", file=sys.stderr)
        return 1
    decoder = json.JSONDecoder()
    buffer = ""
    interrupted = False
    try:
        for line in proc.stdout:
            if not buffer and not line.lstrip().startswith("{"):
                continue
            buffer += line
            if len(buffer) > 1048576:
                buffer = ""
                continue
            try:
                parsed, end = decoder.raw_decode(buffer.lstrip())
            except json.JSONDecodeError:
                continue
            if isinstance(parsed, dict) and isinstance(parsed.get("event"), dict):
                render(parsed["event"])
            buffer = ""
    except KeyboardInterrupt:
        interrupted = True
        print("\nMonitor detenido.", flush=True)
    finally:
        if proc.poll() is None:
            os.killpg(proc.pid, signal.SIGINT)
            try:
                proc.wait(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(proc.pid, signal.SIGTERM)
                proc.wait(timeout=5)
        proc.stdout.close()
    return 0 if interrupted else proc.returncode


if __name__ == "__main__":
    raise SystemExit(main())
