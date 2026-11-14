import fcntl
import json
import logging
import os
from pathlib import Path
from queue import Queue, Empty, Full
import signal
import time

from .control.access import Access
from .control.commands import Commands
from .control.lighting import automatic
from .control.security import alarm
from .control.configuration import apply_configuration
from .hardware.simulated import SimulatedHardware
from .health import write_health
from .models import event
from .persistence.sqlite import Store
from .sensors.light import reading
from .sensors.motion import Motion
from .sensors.fingerprint import Fingerprint
from .transport.iot import LocalTransport, AzureTransport


def run(settings):
    directory = Path(settings.data_dir)
    directory.mkdir(parents=True, exist_ok=True)
    os.umask(0o077)
    lock = (directory / "agent.lock").open("a")
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    if settings.mode == "real":
        from .hardware.real import create_hardware
        hardware = create_hardware(settings)
    else:
        hardware = SimulatedHardware(directory)
    store = Store(directory / "agent.db", settings.max_events)
    hardware.light(store.get("light_on", False))
    access = Access(hardware, settings)
    commands = Commands(store, hardware, access, settings)
    inbound = Queue(maxsize=100)
    configurations = Queue(maxsize=20)

    def enqueue(command):
        try:
            inbound.put_nowait(command)
            return {"commandId": command.get("commandId"), "status": "pending"}
        except Full:
            return {"status": "rejected", "reason": "Cola de ordenes llena"}

    def enqueue_configuration(patch):
        try:
            configurations.put_nowait(patch)
        except Full:
            logging.warning("Cola de configuracion llena; se recuperara al reconectar")

    transport = AzureTransport(enqueue, enqueue_configuration) if settings.transport == "azure" else LocalTransport()
    stopping = False

    def stop(*args):
        nonlocal stopping
        stopping = True

    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    motion, finger = Motion(), Fingerprint()
    next_sample = next_report = 0
    command_dir = directory / "commands"
    command_dir.mkdir(exist_ok=True)
    logging.info("casa-pi iniciado: modo=%s transporte=%s", settings.mode, settings.transport)
    try:
        transport.start()
        while not stopping:
            now = time.monotonic()
            access.tick()
            for _ in range(20):
                try:
                    apply_configuration(store, configurations.get_nowait(), settings)
                except Empty:
                    break
            # Local injection is permitted only for the simulated device.
            if settings.mode == "simulado":
                for path in sorted(command_dir.glob("*.json"))[:10]:
                    try:
                        if path.stat().st_size > 16384:
                            raise ValueError("Orden demasiado grande")
                        body = json.loads(path.read_text())
                        if not isinstance(body, dict):
                            raise ValueError("Orden debe ser objeto")
                        result = commands.apply(body, trusted=True)
                        store.set("last_command", result)
                    except (ValueError, OSError):
                        logging.warning("Archivo de orden local invalido")
                    finally:
                        path.unlink(missing_ok=True)
            for _ in range(10):
                try:
                    store.set("last_command", commands.apply(inbound.get_nowait(), trusted=True))
                except Empty:
                    break
            if now >= next_sample:
                next_sample = now + settings.sample_seconds
                try:
                    values = hardware.read()
                    light = reading(values.get("light_percent"))
                    rising = motion.update(values.get("motion"))
                    attempt = finger.update(values.get("fingerprint"))
                    armed = store.get("armed", False)
                    hardware.alarm(alarm(armed, values["motion"]))
                    if store.get("lighting_mode", "auto") == "auto":
                        configured = store.get("configuration", {}).get("values", {})
                        on = automatic(store.get("light_on", False), light,
                                       configured.get("lightOnBelow", settings.light_on_below),
                                       configured.get("lightOffAbove", settings.light_off_above))
                        hardware.light(on)
                        store.set("light_on", on)
                    if rising and armed:
                        store.emit(event("MotionDetected", {"roomId": settings.room_id,
                                   "securityModeApplied": "armed"}))
                    if attempt:
                        store.emit(attempt)
                    if now >= next_report:
                        next_report = now + store.get("configuration", {}).get("values", {}).get("reportSeconds", settings.report_seconds)
                        store.emit(event("TelemetryReported", {"lightPercent": light,
                                   "motion": values["motion"], "lightOn": store.get("light_on", False),
                                   "securityModeApplied": "armed" if armed else "disarmed"}))
                    state = {"deviceId": settings.device_id, "mode": settings.mode,
                             "lightOn": store.get("light_on", False),
                             "lightingMode": store.get("lighting_mode", "auto"),
                             "securityModeApplied": "armed" if armed else "disarmed",
                             "doorPulseActive": access.deadline is not None,
                             "configuration": store.get("configuration", {"appliedVersion": 0}),
                             "lastCommand": store.get("last_command")}
                    transport.report(state)
                    write_health(directory, "running", mode=settings.mode, transport=settings.transport,
                                 cloud=transport.status(), lightPercent=light, armed=armed,
                                 lastCommand=store.get("last_command"))
                except (ValueError, OSError, TypeError, AttributeError):
                    write_health(directory, "degraded", reason="Entrada de sensor invalida")
                    logging.warning("Lectura invalida; se conserva la luz aplicada")
            # Only memory queues are exchanged here; network I/O is on the worker.
            if now >= getattr(transport, "next_flush", 0):
                transport.next_flush = now + 1
                try:
                    transport.flush(store)
                except Exception:
                    logging.warning("Transporte no disponible; eventos conservados localmente")
            time.sleep(0.05)
    finally:
        hardware.close()
        write_health(directory, "stopped")
        store.close()
        transport.close()
        lock.close()
