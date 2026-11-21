#!/usr/bin/env python3
"""Control local para pruebas del agente en modo simulado."""
import argparse
from datetime import timedelta
import fcntl
import json
from pathlib import Path
from uuid import uuid4
from casa_pi.hardware.simulated import atomic_json
from casa_pi.models import utcnow


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--data-dir", default="/var/lib/casa-pi")
    p.add_argument("--device-id", default="raspberry-sim")
    sub = p.add_subparsers(dest="action", required=True)
    sub.add_parser("status")
    sensor = sub.add_parser("sensor")
    sensor.add_argument("--light", type=float)
    sensor.add_argument("--motion", choices=("on", "off"))
    sensor.add_argument("--fingerprint", help="ID de prueba, o none para soltarlo")
    cmd = sub.add_parser("command")
    cmd.add_argument("kind", choices=("SetLight", "SetSecurityMode", "SetLightingMode", "OpenDoor"))
    cmd.add_argument("--on", choices=("true", "false"), default="true")
    cmd.add_argument("--mode", help="Modo: armed/disarmed para seguridad, auto/manual para luz")
    cmd.add_argument("--duration-ms", type=int, default=1500)
    cmd.add_argument("--command-id")
    cmd.add_argument("--sequence", type=int, help="commandSequence monotonico por luz (requerido para SetLight/SetLightingMode)")
    a = p.parse_args()
    directory = Path(a.data_dir)
    directory.mkdir(parents=True, exist_ok=True)
    if a.action == "status":
        for name in ("health.json", "outputs.json", "simulator.json"):
            path = directory / name
            print(name + ": " + (path.read_text() if path.exists() else "no disponible"))
    elif a.action == "sensor":
        with (directory / "inputs.lock").open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX)
            path = directory / "simulator.json"
            values = json.loads(path.read_text())
            if a.light is not None:
                from casa_pi.sensors.light import reading
                values["light_percent"] = reading(a.light)
            if a.motion is not None:
                values["motion"] = a.motion == "on"
            if a.fingerprint is not None:
                values["fingerprint"] = None if a.fingerprint == "none" else a.fingerprint
            atomic_json(path, values)
            print(json.dumps(values))
    else:
        # device-command.json requires commandSequence for every v1 command.
        if a.sequence is None:
            p.error("--sequence es obligatorio para comandos v1")
        if a.sequence < 0:
            p.error("--sequence debe ser un entero >= 0")
        if a.kind == "SetLightingMode":
            if a.mode not in ("auto", "manual"):
                p.error("--mode debe ser auto o manual para SetLightingMode")
        elif a.kind == "SetSecurityMode":
            if a.mode is None:
                a.mode = "armed"
            elif a.mode not in ("armed", "disarmed"):
                p.error("--mode debe ser armed o disarmed para SetSecurityMode")
        now = utcnow()
        payload = {"SetLight": {"on": a.on == "true"}, "SetSecurityMode": {"mode": a.mode},
                   "SetLightingMode": {"mode": a.mode},
                   "OpenDoor": {"doorId": "principal", "durationMs": a.duration_ms}}[a.kind]
        body = {"commandId": a.command_id or str(uuid4()), "deviceId": a.device_id,
                "type": a.kind, "attemptId": str(uuid4()), "issuedAtUtc": now.isoformat(),
                "expiresAtUtc": (now + timedelta(seconds=8)).isoformat(), "payload": payload}
        if a.sequence is not None:
            body["commandSequence"] = a.sequence
        # The filename is independent of caller-supplied IDs.
        (directory / "commands").mkdir(exist_ok=True)
        atomic_json(directory / "commands" / (str(uuid4()) + ".json"), body)
        print(json.dumps(body))


if __name__ == "__main__":
    main()
