import json
import os
from pathlib import Path


def atomic_json(path, value):
    path = Path(path)
    tmp = path.with_name(path.name + ".tmp-" + str(os.getpid()))
    try:
        with tmp.open("w") as f:
            json.dump(value, f, indent=2)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp, path)
    finally:
        if tmp.exists():
            tmp.unlink()


class SimulatedHardware:
    def __init__(self, directory):
        self.directory = Path(directory)
        self.inputs = self.directory / "simulator.json"
        if not self.inputs.exists():
            atomic_json(self.inputs, {"light_percent": 50, "motion": False, "fingerprint": None})
        self.outputs = {"light_on": False, "door_pulse_active": False, "alarm_on": False,
                        "door_pulses": 0, "simulated": True}
        self.save()

    def save(self):
        atomic_json(self.directory / "outputs.json", self.outputs)

    def read(self):
        if self.inputs.stat().st_size > 16384:
            raise ValueError("Entrada simulada demasiado grande")
        return json.loads(self.inputs.read_text())

    def light(self, on):
        self.outputs["light_on"] = bool(on)
        self.save()

    def door(self, opened):
        if opened and not self.outputs["door_pulse_active"]:
            self.outputs["door_pulses"] += 1
        self.outputs["door_pulse_active"] = bool(opened)
        self.save()

    def alarm(self, on):
        self.outputs["alarm_on"] = bool(on)
        self.save()

    def close(self):
        self.door(False)
        self.alarm(False)
