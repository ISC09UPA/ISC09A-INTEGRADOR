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
    cmd.add_argument("--mode", default="armed")
    cmd.add_argument("--duration-ms", type=int, default=1500)
    cmd.add_argument("--command-id")
    a = p.parse_args()
    directory = Path(a.data_dir)
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
        now = utcnow()
        payload = {"SetLight": {"on": a.on == "true"}, "SetSecurityMode": {"mode": a.mode},
                   "SetLightingMode": {"mode": a.mode},
                   "OpenDoor": {"doorId": "principal", "durationMs": a.duration_ms}}[a.kind]
        body = {"commandId": a.command_id or str(uuid4()), "deviceId": a.device_id,
                "type": a.kind, "attemptId": str(uuid4()), "issuedAtUtc": now.isoformat(),
                "expiresAtUtc": (now + timedelta(seconds=8)).isoformat(), "payload": payload}
        # The filename is independent of caller-supplied IDs.
        atomic_json(directory / "commands" / (str(uuid4()) + ".json"), body)
        print(json.dumps(body))


if __name__ == "__main__":
    main()
