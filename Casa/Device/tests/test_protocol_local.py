#!/usr/bin/env python3
"""Prueba de contrato local del controlador de E/S (sin Azure).

Verifica el protocolo serie completo contra la placa conectada:
ready, ping, luz, deduplicación exacta, payload cambiado, modo, puerta y estado.

Requisitos:
    python3 -m venv .venv && .venv/bin/pip install pyserial

Uso:
    .venv/bin/python Device/tests/test_protocol_local.py
    CASA_SERIAL_PORT=/dev/ttyUSB0 .venv/bin/python Device/tests/test_protocol_local.py
"""
import json
import os
import sys
import time

import serial

PORT = os.environ.get("CASA_SERIAL_PORT", "/dev/ttyUSB0")
BAUD = 115200
LIGHT_ID = "5f0c2a9e-0c1a-4a43-9d3f-1a2b3c4d5e6f"
OTHER_ID = "7a1b2c3d-4e5f-4a6b-8c9d-0e1f2a3b4c5d"

failures: list[str] = []


def main() -> int:
    link = serial.Serial()
    link.port = PORT
    link.baudrate = BAUD
    link.timeout = 0.2
    # No tocar DTR/RTS: en la DevKit V1 están cableados a EN/GPIO0.
    link.dtr = False
    link.rts = False
    link.open()
    time.sleep(0.3)

    buffer = b""

    def collect(seconds: float) -> list[dict]:
        nonlocal buffer
        found: list[dict] = []
        deadline = time.time() + seconds
        while time.time() < deadline:
            chunk = link.read(2048)
            if not chunk:
                continue
            buffer += chunk
            while b"\n" in buffer:
                raw, buffer = buffer.split(b"\n", 1)
                text = raw.decode("utf-8", "replace").strip()
                if not text:
                    continue
                try:
                    found.append(json.loads(text))
                except json.JSONDecodeError:
                    continue
        return found

    def send(payload: dict) -> None:
        link.write((json.dumps(payload, separators=(",", ":")) + "\n").encode())
        link.flush()

    def expect(condition: bool, label: str) -> None:
        print(("PASS  " if condition else "FAIL  ") + label)
        if not condition:
            failures.append(label)

    boot = collect(3.0)
    ready = next((m for m in boot if m.get("t") == "ready"), None)
    expect(ready is not None, "ready al arrancar")
    expect(ready is not None and bool(ready.get("board")), "controlador identificado")

    send({"t": "ping"})
    pong = next((m for m in collect(1.0) if m.get("t") == "pong"), None)
    expect(pong is not None, "ping -> pong")

    send({"t": "cmd", "id": LIGHT_ID, "a": "light", "v": True})
    ack = next((m for m in collect(1.0) if m.get("t") == "ack"), None)
    expect(ack is not None and ack.get("ok") is True and ack.get("light") == 1, "luz on confirmada")

    send({"t": "cmd", "id": LIGHT_ID, "a": "light", "v": True})
    ack = next((m for m in collect(1.0) if m.get("t") == "ack"), None)
    expect(ack is not None and ack.get("dup") == 1, "reintento exacto devuelve dup sin repetir")

    send({"t": "cmd", "id": LIGHT_ID, "a": "light", "v": False})
    err = next((m for m in collect(1.0) if m.get("t") == "err"), None)
    expect(err is not None and err.get("code") == "duplicateChanged", "mismo id con otro payload rechazado")

    send({"t": "cmd", "id": OTHER_ID, "a": "light", "v": False})
    ack = next((m for m in collect(1.0) if m.get("t") == "ack"), None)
    expect(ack is not None and ack.get("light") == 0, "luz off confirmada")

    send({"t": "cmd", "id": "mode-1", "a": "mode", "v": "armed"})
    ack = next((m for m in collect(1.0) if m.get("t") == "ack"), None)
    expect(ack is not None and ack.get("mode") == "armed", "modo armed confirmado")

    send({"t": "cmd", "id": "door-1", "a": "door", "ms": 700})
    ack = next((m for m in collect(1.0) if m.get("t") == "ack"), None)
    expect(ack is not None and ack.get("ms") == 700, "puerta 700 ms confirmada")

    status = next((m for m in collect(7.0) if m.get("t") == "st"), None)
    expect(status is not None, "estado periódico presente")
    expect(status is not None and status.get("mode") == "armed", "estado refleja el modo armed")
    expect(status is not None and status.get("light") == 0, "estado refleja la luz apagada")

    link.close()
    print()
    if failures:
        print(f"{len(failures)} fallo(s): " + ", ".join(failures))
        return 1
    print("Todo el protocolo local pasó.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
