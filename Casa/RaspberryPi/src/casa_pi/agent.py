import fcntl
import json
import logging
import os
from pathlib import Path
from queue import Queue, Empty, Full
import signal
from threading import Lock
import time

from .control.access import Access
from .control.commands import Commands
from .control.lighting import automatic
from .control.security import alarm
from .control.configuration import apply_configuration
from .hardware.simulated import SimulatedHardware
from .health import write_health
from .models import event, new_correlation_id
from .persistence.sqlite import Store
from .sensors.light import reading
from .sensors.motion import Motion
from .sensors.fingerprint import Fingerprint
from .transport.iot import LocalTransport, AzureTransport


class AdmissionQueue:
    """Inbound command queue used by the network worker thread.

    The command body is never mutated. Delivery metadata (correlationId) is
    kept in a separate admission dict so the wire payload stays schema-valid.

    A bounded in-flight map tracks commandIds already admitted to the inbound
    queue but not yet completed by the main thread. This prevents a duplicate
    delivery from being written to the rejected queue before the original
    command has had a chance to execute.
    """

    def __init__(self, inbound_max=100, rejected_max=20):
        self.inbound = Queue(maxsize=inbound_max)
        self.rejected = Queue(maxsize=rejected_max)
        self.inflight = {}
        self.lock = Lock()

    def put(self, command):
        """Enqueue a command from the worker thread. Never touches SQLite.

        The duplicate check, inbound enqueue and in-flight map update are
        performed under the same lock so a concurrent complete() cannot remove
        the id before it has been recorded.
        """
        cid = command.get("commandId") if isinstance(command, dict) else None
        canonical = json.dumps(command, sort_keys=True) if isinstance(command, dict) else None
        request_correlation_id = new_correlation_id()
        admission = {"correlationId": request_correlation_id}
        with self.lock:
            if isinstance(cid, str) and cid in self.inflight:
                if self.inflight[cid] == canonical:
                    return {"commandId": cid, "status": "pending"}
                return {
                    "commandId": cid,
                    "status": "rejected",
                    "reason": "ID reutilizado con otro contenido",
                    "reasonCode": "duplicateChanged",
                }
            try:
                self.inbound.put_nowait((command, admission))
                if isinstance(cid, str):
                    self.inflight[cid] = canonical
                return {"commandId": cid, "status": "pending"}
            except Full:
                # Hand off to the main thread for transient overflow handling.
                # If that queue is also full, return an immediate rejection.
                try:
                    self.rejected.put_nowait((command, admission))
                    return {
                        "commandId": cid,
                        "status": "rejected",
                        "reason": "Cola de ordenes llena",
                        "reasonCode": "deviceRejected",
                        "correlationId": request_correlation_id,
                    }
                except Full:
                    return {
                        "commandId": cid,
                        "status": "rejected",
                        "reason": "Sobrecarga del agente; reintentar",
                        "reasonCode": "deviceRejected",
                        "correlationId": request_correlation_id,
                    }

    def complete(self, command_id):
        """Remove a commandId from the in-flight set once the main thread has
        finished processing it."""
        with self.lock:
            self.inflight.pop(command_id, None)


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
    admission = AdmissionQueue()
    configurations = Queue(maxsize=20)

    def enqueue_configuration(patch):
        try:
            configurations.put_nowait(patch)
        except Full:
            logging.warning("Cola de configuracion llena; se recuperara al reconectar")

    transport = AzureTransport(admission.put, enqueue_configuration) if settings.transport == "azure" else LocalTransport()
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
                    cmd, meta = admission.rejected.get_nowait()
                    result = commands.reject_queue_full(cmd, admission_correlation_id=meta.get("correlationId"))
                    store.set("last_command", result)
                except Empty:
                    break
            for _ in range(10):
                try:
                    cmd, meta = admission.inbound.get_nowait()
                    try:
                        result = commands.apply(cmd, trusted=True, admission_correlation_id=meta.get("correlationId"))
                    finally:
                        admission.complete(cmd.get("commandId"))
                    store.set("last_command", result)
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
                        previous_on = store.get("light_on", False)
                        on = automatic(previous_on, light,
                                       configured.get("lightOnBelow", settings.light_on_below),
                                       configured.get("lightOffAbove", settings.light_off_above))
                        if store.apply_light_state(on) != previous_on:
                            hardware.light(on)
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
