"""Capa de hardware. No asigna pines por defecto: requiere configuración confirmada."""
import os
import time
try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass

class HardwareError(RuntimeError):
    pass

class LockerHardware:
    def __init__(self):
        self.simulation = os.getenv("SIMULATION_MODE", "true").lower() == "true"
        self.relay = self.door = self.presence = None
        if self.simulation:
            return
        required = ["RELAY_GPIO", "DOOR_SENSOR_GPIO", "PRESENCE_SENSOR_GPIO"]
        missing = [key for key in required if not os.getenv(key, "").strip()]
        if missing:
            raise HardwareError("Faltan pines confirmados: " + ", ".join(missing))
        try:
            from gpiozero import DigitalInputDevice, DigitalOutputDevice
            self.relay = DigitalOutputDevice(
                int(os.environ["RELAY_GPIO"]),
                active_high=os.getenv("RELAY_ACTIVE_HIGH", "true").lower() == "true",
                initial_value=False)
            self.door = DigitalInputDevice(
                int(os.environ["DOOR_SENSOR_GPIO"]), pull_up=None,
                active_state=os.getenv("DOOR_CLOSED_ACTIVE_HIGH", "true").lower() == "true")
            self.presence = DigitalInputDevice(
                int(os.environ["PRESENCE_SENSOR_GPIO"]), pull_up=None,
                active_state=os.getenv("PRESENCE_ACTIVE_HIGH", "true").lower() == "true")
        except Exception as exc:
            raise HardwareError(f"No se pudo inicializar GPIO: {exc}") from exc

    def door_closed(self):
        return True if self.simulation else bool(self.door.is_active)

    def presence_detected(self):
        return False if self.simulation else bool(self.presence.is_active)

    def weight_kg(self):
        # Se integra cuando se conozcan modelo, interfaz y calibración de la báscula.
        return None

    def open_locker(self):
        if self.simulation:
            print("[SIMULACIÓN] Activación lógica de apertura")
            return True
        if not self.door_closed():
            raise HardwareError("El sensor indica que la puerta ya está abierta")
        pulse = float(os.getenv("RELAY_PULSE_SECONDS", "1.0"))
        if not 0.1 <= pulse <= 3.0:
            raise HardwareError("RELAY_PULSE_SECONDS debe estar entre 0.1 y 3 segundos")
        self.relay.on()
        try:
            time.sleep(pulse)
        finally:
            self.relay.off()
        deadline = time.monotonic() + float(os.getenv("DOOR_CONFIRM_TIMEOUT_SECONDS", "5"))
        while time.monotonic() < deadline:
            if not self.door_closed():
                return True
            time.sleep(0.1)
        raise HardwareError("No se confirmó la apertura mediante el sensor de puerta")

    def close(self):
        if self.relay:
            self.relay.off()
        for device in (self.relay, self.door, self.presence):
            if device:
                device.close()
