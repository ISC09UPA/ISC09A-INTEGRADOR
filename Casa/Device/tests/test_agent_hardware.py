#!/usr/bin/env python3
"""Prueba de integración del agente con la placa real y la cámara real.

Requiere el controlador de E/S en /dev/ttyUSB0.
No usa Azure: el doble de Home valida los eventos contra contracts/.

Uso:
    Casa/.venv-device/bin/python Casa/Device/tests/test_agent_hardware.py
"""
from __future__ import annotations

import os
import sys
import tempfile
import time
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "device" / "agent"))
sys.path.insert(0, str(ROOT / "device" / "tests"))

from agent import DeviceAgent  # noqa: E402
from camera import Camera  # noqa: E402
from casa_agent import start_local_api  # noqa: E402
from config import Config  # noqa: E402
from home_double import HomeDouble, make_command, send_method  # noqa: E402
from serial_link import SerialLink  # noqa: E402
from store import Store  # noqa: E402
from transports import LocalTransport  # noqa: E402

FAILURES: list[str] = []


def check(condition: bool, label: str) -> None:
    print(("PASS  " if condition else "FAIL  ") + label)
    if not condition:
        FAILURES.append(label)


def wait_result(home: HomeDouble, command_id: str, timeout: float = 15.0):
    return home.wait_for(
        lambda event: event.get("type") == "CommandResult" and event["payload"]["commandId"] == command_id,
        timeout,
    )


def main() -> int:
    workdir = tempfile.mkdtemp(prefix="casa-hw-")
    home = HomeDouble()
    config = Config(
        serial_port=os.environ.get("CASA_SERIAL_PORT", "/dev/ttyUSB0"),
        device_id="casa-pi-01",
        light_id="sala",
        db_path=str(Path(workdir) / "state.db"),
        report_interval_s=0.5,
        transport="local",
        local_home_url=home.events_url,
        camera_backend="v4l2",
        camera_device="/dev/video0",
    )
    store = Store(config.db_path)
    serial_link = SerialLink(config.serial_port, config.serial_baud)
    agent = DeviceAgent(config, store, serial_link, LocalTransport(home.events_url), Camera("v4l2", "/dev/video0"))
    agent.start()
    server = start_local_api(agent, serial_link, "127.0.0.1", 0)
    base = f"http://127.0.0.1:{server.server_address[1]}"

    try:
        check(serial_link.available, "enlace serie con el controlador de E/S")
        initial = home.wait_for(
            lambda event: event.get("type") == "DeviceStateReported", timeout=10.0
        )
        check(initial is not None, "estado v2 real desde el controlador")
        check(initial is not None and initial["payload"]["lightId"] == "sala", "lightId correcto")

        light_on = make_command("SetLight", {"on": True}, sequence=0)
        check(send_method(base, "SetLight", light_on) == 202, "SetLight on aceptado")
        result = wait_result(home, light_on["commandId"])
        check(result is not None and result["payload"]["status"] == "executed", "placa confirmó luz encendida")

        light_off = make_command("SetLight", {"on": False}, sequence=1)
        check(send_method(base, "SetLight", light_off) == 202, "SetLight off aceptado")
        result = wait_result(home, light_off["commandId"])
        check(result is not None and result["payload"]["status"] == "executed", "placa confirmó luz apagada")

        armed = make_command("SetSecurityMode", {"mode": "armed"}, sequence=2)
        check(send_method(base, "SetSecurityMode", armed) == 202, "SetSecurityMode armed aceptado")
        result = wait_result(home, armed["commandId"])
        check(result is not None and result["payload"]["status"] == "executed", "placa confirmó modo armed")

        door = make_command("OpenDoor", {"doorId": "puerta", "durationMs": 300}, sequence=3)
        check(send_method(base, "OpenDoor", door) == 202, "OpenDoor aceptado")
        result = wait_result(home, door["commandId"])
        check(result is not None and result["payload"]["status"] == "executed", "placa confirmó la puerta")

        capture_id = str(uuid.uuid4())
        capture = make_command(
            "CaptureImage",
            {
                "captureId": capture_id,
                "uploadUrl": home.upload_url(capture_id),
                "uploadToken": "B" * 43,
                "uploadExpiresAtUtc": (datetime.now(timezone.utc) + timedelta(minutes=2))
                .isoformat(timespec="milliseconds")
                .replace("+00:00", "Z"),
            },
            sequence=4,
        )
        check(send_method(base, "CaptureImage", capture) == 202, "CaptureImage aceptado")
        uploaded = home.wait_kind("CaptureUploaded", timeout=40.0)
        check(uploaded is not None, "CaptureUploaded emitido")
        image = home.uploads.get(capture_id, b"")
        check(image.startswith(b"\xff\xd8") and len(image) > 1024, f"JPEG real subido ({len(image)} bytes)")
        result = wait_result(home, capture["commandId"], timeout=40.0)
        check(result is not None and result["payload"]["status"] == "executed", "captura ejecutada")

        if "--interactive" in sys.argv:
            print()
            print("… pasa la mano frente al PIR (20 s)")
            motion = home.wait_kind("MotionDetected", timeout=20.0)
            check(motion is not None, "MotionDetected real desde el PIR")
            check(motion is not None and motion["payload"]["securityModeApplied"] == "armed", "movimiento con modo armed")

            print("… coloca el dedo enrolado en el lector (30 s)")
            access = home.wait_kind("AccessRequested", timeout=30.0)
            check(access is not None, "AccessRequested real desde el R307")
            check(access is not None and access["payload"]["roomId"] == "sala", "huella con roomId correcto")

        time.sleep(1.0)
        check(not home.errors, f"todos los eventos validaron contra los esquemas ({len(home.events)} eventos)")
    finally:
        server.shutdown()
        agent.stop()
        home.close()

    print()
    if FAILURES:
        print(f"{len(FAILURES)} fallo(s): " + ", ".join(FAILURES))
        return 1
    print("Integración real completa: todo pasó.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
