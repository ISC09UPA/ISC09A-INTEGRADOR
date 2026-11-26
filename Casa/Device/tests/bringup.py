#!/usr/bin/env python3
"""Puesta en marcha física de la placa de E/S.

Verifica cableado y responde con PASS/WARN/FAIL por componente. También permite
esperar movimiento, enrolar una huella o calibrar el servo.

Uso:
    python3 Device/tests/bringup.py                 # autotest completo
    python3 Device/tests/bringup.py --pir           # espera 20 s movimiento
    python3 Device/tests/bringup.py --enroll        # enrola una huella
    python3 Device/tests/bringup.py --finger        # espera una huella 30 s
    python3 Device/tests/bringup.py --cal-open 1600 --cal-closed 450
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time

import serial

HINTS = {
    "dht": "DHT11: VCC a 3.3 V, DATA a GPIO26 con pull-up de 10 kΩ a 3.3 V, GND común.",
    "ldr": "LDR: un extremo a 3.3 V, el otro a GPIO34 y a GND con 10 kΩ.",
    "pir": "PIR: VCC a 5 V, OUT a GPIO27, GND común. Ajusta el potenciómetro de sensibilidad.",
    "fp": "R307: VCC a 5 V, TX → divisor 1 kΩ/1.8 kΩ → GPIO16, RX ← GPIO17, GND común.",
    "servo": "Servo: señal a GPIO13, alimentación 5 V externa, GND común con la placa.",
    "led": "LED: GPIO33 → resistencia 220–330 Ω → ánodo, cátodo a GND.",
}


class Probe:
    def __init__(self, port: str, baud: int = 115200):
        self.link = serial.Serial()
        self.link.port = port
        self.link.baudrate = baud
        self.link.timeout = 0.2
        # En la DevKit V1, DTR/RTS están cableados a GPIO0/EN: hay que soltarlos
        # antes de abrir o la placa entra en modo descarga.
        self.link.dtr = False
        self.link.rts = False
        self.link.open()
        self.buffer = b""

    def close(self) -> None:
        self.link.close()

    def send(self, payload: dict) -> None:
        self.link.write((json.dumps(payload, separators=(",", ":")) + "\n").encode())
        self.link.flush()

    def read(self, seconds: float):
        deadline = time.time() + seconds
        while time.time() < deadline:
            chunk = self.link.read(1024)
            if not chunk:
                continue
            self.buffer += chunk
            while b"\n" in self.buffer:
                raw, self.buffer = self.buffer.split(b"\n", 1)
                text = raw.decode("utf-8", "replace").strip()
                if not text:
                    continue
                try:
                    yield json.loads(text)
                except json.JSONDecodeError:
                    continue

    def wait(self, predicate, seconds: float):
        for message in self.read(seconds):
            if predicate(message):
                return message
        return None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Puesta en marcha de la placa de E/S")
    parser.add_argument("--port", default=os.environ.get("CASA_SERIAL_PORT", "/dev/ttyUSB0"))
    parser.add_argument("--pir", action="store_true", help="espera movimiento 20 s")
    parser.add_argument("--ldr", action="store_true", help="verifica variación del LDR 6 s")
    parser.add_argument("--only", default="", help="solo reporta estos pasos (separados por coma)")
    parser.add_argument("--enroll", action="store_true", help="enrola una huella")
    parser.add_argument("--finger", action="store_true", help="espera una huella 30 s")
    parser.add_argument("--cal-open", type=int, default=None)
    parser.add_argument("--cal-closed", type=int, default=None)
    parser.add_argument("--skip-selftest", action="store_true")
    return parser.parse_args()


def main() -> int:
    options = parse_args()
    failures = 0
    probe = Probe(options.port)
    try:
        ready = probe.wait(lambda m: m.get("t") == "ready", 8.0)
        if ready is None:
            print("FAIL  no llegó ready. Revisa el puerto y el firmware.")
            return 1
        print(f"PASS  ready  fw={ready.get('fw')}  fp={ready.get('fp')}  plantillas={ready.get('tpl')}")
        print(f"      servo: abierto={ready.get('servo_open')} us  cerrado={ready.get('servo_closed')} us")

        if not options.skip_selftest:
            selected = {step.strip() for step in options.only.split(",") if step.strip()} or None
            probe.send({"t": "selftest"})
            seen = set()
            for message in probe.read(10.0):
                if message.get("t") != "ev" or message.get("k") != "selftest":
                    continue
                step = message.get("step")
                seen.add(step)
                if step in ("start", "done") or (selected and step not in selected):
                    continue
                ok = bool(message.get("ok"))
                if step == "ldr":
                    raw = message.get("raw", -1)
                    print(f"INFO  ldr    raw={raw} ({message.get('v')} %). Tapa y destapa el LDR y usa --ldr.")
                elif step == "dht":
                    detail = f"temp={message.get('t_c')} °C  humedad={message.get('rh')} %"
                    print(("PASS  " if ok else "FAIL  ") + f"dht    {detail}")
                    if not ok:
                        failures += 1
                        print(f"      pista: {HINTS['dht']}")
                elif step == "pir":
                    level = "alto" if message.get("v") else "bajo"
                    print(f"INFO  pir    nivel={level} (usa --pir para verificar movimiento)")
                elif step == "fp":
                    print(("PASS  " if ok else "FAIL  ") + f"fp     plantillas={message.get('tpl')}")
                    if not ok:
                        failures += 1
                        print(f"      pista: {HINTS['fp']}")
                elif step == "servo":
                    print("ACCIONADO servo (debe girar y volver; verifícalo a simple vista)")
                elif step == "led":
                    print("ACCIONADO led (debe parpadear dos veces; verifícalo a simple vista)")
            required = selected or {"ldr", "dht", "pir", "fp", "servo", "led"}
            for step in required:
                if step not in seen:
                    print(f"FAIL  {step:6s} no reportó resultado")
                    failures += 1

        if options.ldr:
            print("… tapa y destapa el LDR durante 6 s")
            values = []
            deadline = time.time() + 6
            while time.time() < deadline:
                probe.send({"t": "status"})
                for message in probe.read(0.35):
                    if message.get("t") == "st" and isinstance(message.get("ldr"), int):
                        values.append(message["ldr"])
            if values and max(values) - min(values) >= 5:
                print(f"PASS  ldr    varió entre {min(values)} % y {max(values)} %")
            else:
                print(f"FAIL  ldr    sin variación (min={min(values) if values else 'n/a'} max={max(values) if values else 'n/a'})")
                print(f"      pista: {HINTS['ldr']}")
                failures += 1

        if options.pir:
            print("… esperando movimiento hasta 20 s (pasa la mano frente al PIR)")
            motion = probe.wait(
                lambda m: m.get("t") == "ev" and m.get("k") == "motion", 20.0
            )
            if motion is None:
                print("FAIL  pir    no se detectó movimiento")
                print(f"      pista: {HINTS['pir']}")
                failures += 1
            else:
                print(f"PASS  pir    movimiento detectado (modo {motion.get('mode')})")

        if options.enroll:
            print("… enrolamiento: coloca el dedo dos veces cuando se pida")
            probe.send({"t": "enroll"})
            enrolled = None
            for message in probe.read(40.0):
                if message.get("t") == "ev" and message.get("k") == "enroll":
                    if message.get("v") == 1:
                        enrolled = message
                        break
                    print(f"      esperando lectura... (plantilla {message.get('tpl')})")
            if enrolled is not None:
                print(f"PASS  huella enrolada en la plantilla {enrolled.get('tpl')}")
            else:
                print("FAIL  no se pudo enrolar: ventana sucia, dedo seco o mal colocado")
                print(f"      pista: {HINTS['fp']}")
                failures += 1

        if options.finger:
            print("… esperando una huella hasta 30 s")
            match = probe.wait(lambda m: m.get("t") == "ev" and m.get("k") == "finger", 30.0)
            if match is None:
                print("FAIL  huella no reconocida")
                print(f"      pista: {HINTS['fp']}")
                failures += 1
            else:
                print(f"PASS  huella reconocida: plantilla {match.get('tpl')} confianza {match.get('conf')}")

        if options.cal_open is not None or options.cal_closed is not None:
            payload = {"t": "cmd", "id": f"cal-{int(time.time())}", "a": "cal"}
            if options.cal_open is not None:
                payload["open"] = options.cal_open
            if options.cal_closed is not None:
                payload["closed"] = options.cal_closed
            probe.send(payload)
            ack = probe.wait(lambda m: m.get("t") in ("ack", "err"), 3.0)
            if ack is None or ack.get("t") == "err":
                print("FAIL  servo  calibración rechazada")
                failures += 1
            else:
                print(f"PASS  servo  calibrado: abierto={ack.get('open')} us  cerrado={ack.get('closed')} us")

        probe.send({"t": "status"})
        status = probe.wait(lambda m: m.get("t") == "st", 3.0)
        if status:
            print(
                "      estado: luz={} ldr={}% modo={} temp={} hum={} fp={} plantillas={}".format(
                    status.get("light"),
                    status.get("ldr"),
                    status.get("mode"),
                    status.get("t_c"),
                    status.get("rh"),
                    status.get("fp"),
                    status.get("tpl"),
                )
            )
    finally:
        probe.close()

    print()
    if failures:
        print(f"{failures} componente(s) con problema.")
        return 1
    print("Puesta en marcha correcta.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
