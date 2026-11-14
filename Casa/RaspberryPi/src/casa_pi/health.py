from .hardware.simulated import atomic_json
from .models import timestamp


def write_health(directory, status, **fields):
    atomic_json(directory / "health.json", {"status": status, "updatedAtUtc": timestamp(), **fields})
