#!/usr/bin/env python3
"""Banco de pruebas local del agente (sin Azure).

Usa una placa simulada y un doble de Home que valida cada evento contra los
esquemas reales de contracts/. Cubre el flujo completo y los rechazos.

Uso:
    Casa/.venv-device/bin/python Casa/Device/tests/test_agent_local.py
"""
from __future__ import annotations

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
from casa_agent import start_local_api  # noqa: E402
from config import Config  # noqa: E402
from home_double import HomeDouble, make_command, send_method  # noqa: E402
from store import Store  # noqa: E402
from transports import LocalTransport  # noqa: E402

FAILURES: list[str] = []


def check(condition: bool, label: str) -> None:
    print(("PASS  " if condition else "FAIL  ") + label)
    if not condition:
        FAILURES.append(label)


class FakeSerial:
    def __init__(self) -> None:
        self.available = True
        self.status = {"t": "st", "ldr": 55, "light": 0, "motion": 0, "mode": "disarmed", "fp": 0, "tpl": 0}
        self._events: list[dict] = []
        self.fail_ack = False

    def start(self, ready_timeout: float = 6.0) -> bool:
        self.available = True
        return True

    def stop(self) -> None:
        self.available = False

    def ensure(self) -> None:
        pass

    def request(self, action: str, extra: dict, command_id: str, timeout: float = 1.5):
        if not self.available:
            return None
        if self.fail_ack:
            return None
        if action == "light":
            self.status["light"] = 1 if extra.get("v") else 0
        if action == "mode":
            self.status["mode"] = extra.get("v")
        return {"t": "ack", "id": command_id, "ok": True, **extra}

    def take_events(self) -> list[dict]:
        events, self._events = self._events, []
        return events

    def push(self, event: dict) -> None:
        self._events.append(event)


class FakeCamera:
    def capture_jpeg(self) -> bytes:
        return b"\xff\xd8" + b"\x11" * 4096


def wait_state(home: HomeDouble, predicate, timeout: float = 6.0):
    return home.wait_for(
        lambda event: event.get("type") == "DeviceStateReported" and predicate(event["payload"]),
        timeout,
    )


def wait_new(home: HomeDouble, start: int, predicate, timeout: float = 6.0):
    deadline = time.time() + timeout
    while time.time() < deadline:
        for event in home.events[start:]:
            if predicate(event):
                return event
        time.sleep(0.05)
    return None


def main() -> int:
    workdir = tempfile.mkdtemp(prefix="casa-agent-")
    home = HomeDouble()
    config = Config(
        device_id="casa-pi-01",
        light_id="sala",
        db_path=str(Path(workdir) / "state.db"),
        report_interval_s=0.3,
        transport="local",
        local_home_url=home.events_url,
        camera_backend="none",
    )
    store = Store(config.db_path)
    fake = FakeSerial()
    agent = DeviceAgent(config, store, fake, LocalTransport(home.events_url), FakeCamera())
    agent.start()
    server = start_local_api(agent, fake, "127.0.0.1", 0)
    base = f"http://127.0.0.1:{server.server_address[1]}"

    try:
        # ---------------------------------------------------------- reporte inicial
        initial = wait_state(home, lambda payload: payload["stateRevision"] == 0)
        check(initial is not None, "DeviceStateReported v2 inicial")
        check(initial is not None and initial["payload"]["lightId"] == "sala", "lightId correcto")
        check(
            initial is not None
            and initial["payload"]["snapshot"]["source"] == "device"
            and initial["payload"]["snapshot"]["lightingMode"] == "manual",
            "snapshot con source=device y modo manual",
        )
        telemetry = home.wait_kind("TelemetryReported", timeout=6.0)
        check(telemetry is not None, "TelemetryReported inicial")
        check(telemetry is not None and telemetry["payload"]["lightPercent"] == 55.0, "lightPercent real de la placa")

        # ---------------------------------------------------------- luz
        body = make_command("SetLight", {"on": True}, sequence=0)
        command_id = body["commandId"]
        status = send_method(base, "SetLight", body)
        check(status == 202, "SetLight aceptado con 202")
        result = home.wait_for(
            lambda event: event.get("type") == "CommandResult"
            and event["payload"]["commandId"] == command_id,
            timeout=6.0,
        )
        check(result is not None, "CommandResult de SetLight")
        check(result is not None and result["payload"]["status"] == "executed", "resultado executed")
        check(result is not None and result["payload"]["snapshot"]["lightOn"] is True, "snapshot refleja luz encendida")
        check(result is not None and result["payload"]["stateRevision"] == 1, "revisión 1")

        # ---------------------------------------------------------- idempotencia
        status = send_method(base, "SetLight", body)
        check(status == 202, "duplicado exacto aceptado con 202")
        time.sleep(1.2)
        check(len(home.results_for(command_id)) == 1, "el duplicado no duplica el resultado")
        check(agent.state()["revision"] == 1, "el duplicado no avanza la revisión")

        changed = dict(body, payload={"on": False})
        status = send_method(base, "SetLight", changed)
        check(status == 400, "mismo id con otro payload rechazado con 400")

        # ---------------------------------------------------------- más luz y modos
        body_b = make_command("SetLight", {"on": False}, sequence=1)
        check(send_method(base, "SetLight", body_b) == 202, "SetLight off aceptado")
        home.wait_for(
            lambda event: event.get("type") == "CommandResult"
            and event["payload"]["commandId"] == body_b["commandId"],
            timeout=6.0,
        )
        check(agent.state()["revision"] == 2, "revisión 2 tras apagar")

        body_c = make_command("SetLightingMode", {"mode": "manual"}, sequence=2)
        check(send_method(base, "SetLightingMode", body_c) == 202, "SetLightingMode manual aceptado")
        time.sleep(1.0)
        check(agent.state()["revision"] == 2, "modo sin cambio no avanza la revisión")

        body_d = make_command("SetLightingMode", {"mode": "auto"}, sequence=3)
        check(send_method(base, "SetLightingMode", body_d) == 202, "SetLightingMode auto aceptado")
        home.wait_for(
            lambda event: event.get("type") == "CommandResult"
            and event["payload"]["commandId"] == body_d["commandId"],
            timeout=6.0,
        )
        check(agent.state()["revision"] == 3, "revisión 3 tras cambiar a auto")

        body_e = make_command("SetSecurityMode", {"mode": "armed"}, sequence=4)
        check(send_method(base, "SetSecurityMode", body_e) == 202, "SetSecurityMode armed aceptado")
        armed = wait_state(home, lambda payload: payload["securityModeApplied"] == "armed", timeout=6.0)
        check(armed is not None, "el estado reporta armed")

        # ---------------------------------------------------------- eventos de placa
        fake.push({"t": "ev", "k": "motion", "v": 1, "mode": "armed"})
        motion = home.wait_kind("MotionDetected", timeout=6.0)
        check(motion is not None, "MotionDetected emitido")
        check(motion is not None and motion["payload"]["roomId"] == "sala", "motion con roomId de la luz")
        check(motion is not None and motion["payload"]["securityModeApplied"] == "armed", "motion con modo armed")

        # El evento de movimiento es un pulso; la telemetría debe reflejarlo y
        # volver al estado actual de la placa, no quedarse en true para siempre.
        start = len(home.events)
        motion_telemetry = wait_new(
            home, start,
            lambda event: event.get("type") == "TelemetryReported" and event["payload"]["motion"] is True,
        )
        check(motion_telemetry is not None, "telemetría con movimiento detectado")

        fake.status["motion"] = 0
        start = len(home.events)
        clear_telemetry = wait_new(
            home, start,
            lambda event: event.get("type") == "TelemetryReported" and event["payload"]["motion"] is False,
            timeout=2.0,
        )
        check(clear_telemetry is not None, "telemetría vuelve a sin movimiento")

        fake.push({"t": "ev", "k": "finger", "ok": 1, "tpl": 3, "conf": 88})
        access = home.wait_kind("AccessRequested", timeout=6.0)
        check(access is not None, "AccessRequested emitido")
        check(access is not None and access["payload"]["roomId"] == "sala", "huella con roomId de la luz")

        # ---------------------------------------------------------- puerta
        door = make_command("OpenDoor", {"doorId": "puerta", "durationMs": 500}, sequence=5)
        check(send_method(base, "OpenDoor", door) == 202, "OpenDoor aceptado")
        door_result = home.wait_for(
            lambda event: event.get("type") == "CommandResult"
            and event["payload"]["commandId"] == door["commandId"],
            timeout=6.0,
        )
        check(door_result is not None and door_result["payload"]["status"] == "executed", "puerta ejecutada")

        # ---------------------------------------------------------- captura
        capture_id = str(uuid.uuid4())
        capture_body = make_command(
            "CaptureImage",
            {
                "captureId": capture_id,
                "uploadUrl": home.upload_url(capture_id),
                "uploadToken": "A" * 43,
                "uploadExpiresAtUtc": (datetime.now(timezone.utc) + timedelta(minutes=2))
                .isoformat(timespec="milliseconds")
                .replace("+00:00", "Z"),
            },
            sequence=6,
        )
        check(send_method(base, "CaptureImage", capture_body) == 202, "CaptureImage aceptado")
        uploaded = home.wait_kind("CaptureUploaded", timeout=10.0)
        check(uploaded is not None, "CaptureUploaded emitido")
        check(capture_id in home.uploads, "la imagen llegó al endpoint de subida")
        capture_result = home.wait_for(
            lambda event: event.get("type") == "CommandResult"
            and event["payload"]["commandId"] == capture_body["commandId"],
            timeout=10.0,
        )
        check(capture_result is not None and capture_result["payload"]["status"] == "executed", "captura ejecutada")

        # ---------------------------------------------------------- rechazos
        expired = make_command("SetLight", {"on": True}, sequence=7, expires_in=-5)
        check(send_method(base, "SetLight", expired) == 202, "orden vencida aceptada para responder expired")
        expired_result = home.wait_for(
            lambda event: event.get("type") == "CommandResult"
            and event["payload"]["commandId"] == expired["commandId"],
            timeout=6.0,
        )
        check(expired_result is not None and expired_result["payload"]["status"] == "expired", "resultado expired")

        foreign = make_command("SetLight", {"on": True}, sequence=8, device_id="otro-dispositivo")
        check(send_method(base, "SetLight", foreign) == 400, "otro dispositivo rechazado con 400")

        stale = make_command("SetLight", {"on": True}, sequence=1)
        check(send_method(base, "SetLight", stale) == 400, "secuencia antigua rechazada con 400")

        fake.available = False
        down = make_command("SetLight", {"on": True}, sequence=9)
        check(send_method(base, "SetLight", down) == 400, "placa caída rechazada con 400")
        fake.available = True

        time.sleep(1.0)
        check(not home.errors, f"todos los eventos validaron contra los esquemas ({len(home.events)} eventos)")
        if home.errors:
            for error in home.errors:
                print("   esquema:", error)
    finally:
        server.shutdown()
        agent.stop()
        home.close()

    print()
    if FAILURES:
        print(f"{len(FAILURES)} fallo(s): " + ", ".join(FAILURES))
        return 1
    print("Banco local completo: todo pasó.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
