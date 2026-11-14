import subprocess
import time
from ..models import parse_time, utcnow


def clock_trusted():
    try:
        result = subprocess.run(["timedatectl", "show", "-p", "NTPSynchronized", "--value"],
                                capture_output=True, text=True, timeout=2)
        return result.returncode == 0 and result.stdout.strip() == "yes"
    except (OSError, subprocess.TimeoutExpired):
        return False


class Access:
    def __init__(self, hardware, settings, trusted=clock_trusted):
        self.hardware, self.settings, self.trusted = hardware, settings, trusted
        self.deadline = None
        self.hardware.door(False)

    def validate(self, command):
        payload = command.get("payload", {})
        if command.get("deviceId") != self.settings.device_id:
            raise ValueError("Destino incorrecto")
        if not isinstance(command.get("attemptId"), str) or not command["attemptId"]:
            raise ValueError("Intento requerido")
        if payload.get("doorId") != self.settings.door_id:
            raise ValueError("Puerta incorrecta")
        duration = payload.get("durationMs")
        if type(duration) is not int or not 100 <= duration <= self.settings.max_open_ms:
            raise ValueError("Duracion de apertura invalida")
        issued, expires = parse_time(command.get("issuedAtUtc")), parse_time(command.get("expiresAtUtc"))
        now = utcnow()
        if not issued <= now < expires or not 0 < (expires - issued).total_seconds() <= 30:
            raise ValueError("Orden vencida, futura o ventana demasiado larga")
        if self.deadline is not None:
            raise ValueError("Ya hay un pulso activo; no se prolonga")
        if not self.trusted():
            raise ValueError("Reloj sin sincronizacion confiable")

    def open(self, command):
        # Repeat the checks immediately before the physical effect.
        self.validate(command)
        self.deadline = time.monotonic() + command["payload"]["durationMs"] / 1000
        try:
            self.hardware.door(True)
        except Exception:
            self.hardware.door(False)
            self.deadline = None
            raise

    def tick(self):
        if self.deadline is not None and time.monotonic() >= self.deadline:
            self.hardware.door(False)
            self.deadline = None
