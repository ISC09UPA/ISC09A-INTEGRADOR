"""Enlace serie con el controlador de E/S.

- Reconoce `ready`, `ack`, `err`, eventos y estado.
- Espera el ack real del actuador; si no llega, el efecto es ambiguo.
- Reconexión con reintento; abrir el puerto reinicia la placa.
"""
from __future__ import annotations

import json
import logging
import queue
import threading
import time

import serial


class SerialLink:
    def __init__(self, port: str, baud: int = 115200, logger: logging.Logger | None = None):
        self._port = port
        self._baud = baud
        self._log = logger or logging.getLogger("serial")
        self._serial: serial.Serial | None = None
        self._thread: threading.Thread | None = None
        self._stop = threading.Event()
        self._lock = threading.RLock()
        self._pending: dict[str, queue.Queue] = {}
        self._events: queue.Queue[dict] = queue.Queue()
        self._ready = threading.Event()
        self._first_message = threading.Event()
        self._status: dict | None = None
        self._last_message_at = 0.0
        self._last_attempt = 0.0
        self._available = False

    # ------------------------------------------------------------ ciclo de vida

    @property
    def available(self) -> bool:
        return self._available and self._serial is not None and self._serial.is_open

    @property
    def status(self) -> dict | None:
        return self._status

    def start(self, ready_timeout: float = 6.0) -> bool:
        self._last_attempt = time.monotonic()
        try:
            self._serial = serial.Serial(self._port, self._baud, timeout=0.2)
        except Exception as error:  # noqa: BLE001 - el puerto puede no existir todavía
            self._log.warning("No se pudo abrir %s: %s", self._port, error)
            self._available = False
            return False

        self._stop.clear()
        self._ready.clear()
        self._first_message.clear()
        self._thread = threading.Thread(target=self._read_loop, name="serial-reader", daemon=True)
        self._thread.start()

        # Abrir el puerto reinicia la placa: esperamos ready o cualquier mensaje.
        got_first = self._first_message.wait(ready_timeout)
        if not got_first:
            self._log.warning("La placa no respondió en %.0f s", ready_timeout)
        else:
            self._log.info("Placa lista (%s)", "ready" if self._ready.is_set() else "mensaje inicial")
        self._available = got_first
        return got_first

    def ensure(self) -> None:
        """Reintenta abrir el puerto si se perdió la conexión."""
        if self.available:
            return
        if time.monotonic() - self._last_attempt < 5.0:
            return
        self._log.info("Reintentando el enlace serie en %s", self._port)
        self.stop()
        self.start()

    def stop(self) -> None:
        self._stop.set()
        if self._thread is not None:
            self._thread.join(timeout=1.5)
            self._thread = None
        if self._serial is not None:
            try:
                self._serial.close()
            except Exception:  # noqa: BLE001
                pass
        self._serial = None
        self._available = False

    # ------------------------------------------------------------ lectura

    def _read_loop(self) -> None:
        buffer = b""
        while not self._stop.is_set():
            try:
                if self._serial is None:
                    break
                chunk = self._serial.read(512)
            except Exception as error:  # noqa: BLE001
                self._log.warning("Lectura serie interrumpida: %s", error)
                break
            if not chunk:
                continue
            buffer += chunk
            while b"\n" in buffer:
                raw, buffer = buffer.split(b"\n", 1)
                text = raw.decode("utf-8", "replace").strip()
                if not text:
                    continue
                try:
                    message = json.loads(text)
                except json.JSONDecodeError:
                    self._log.debug("Línea no JSON ignorada: %r", text[:120])
                    continue
                self._last_message_at = time.monotonic()
                self._first_message.set()
                self._dispatch(message)
        self._available = False

    def _dispatch(self, message: dict) -> None:
        kind = message.get("t")
        if kind == "ready":
            self._ready.set()
            self._log.info("Firmware %s en placa %s", message.get("fw"), message.get("board"))
            return
        if kind in ("ack", "err"):
            command_id = message.get("id")
            with self._lock:
                waiter = self._pending.get(command_id)
            if waiter is not None:
                waiter.put(message)
            return
        if kind == "st":
            self._status = message
            return
        if kind == "ev":
            self._events.put(message)
            return
        if kind == "diag":
            self._log.debug("Diagnóstico de placa: %s", message)

    # ------------------------------------------------------------ comandos

    def request(self, action: str, extra: dict, command_id: str, timeout: float = 1.5) -> dict | None:
        """Envía un comando y espera su ack. Reintenta una vez con el mismo id."""
        if not self.available:
            return None
        waiter: queue.Queue = queue.Queue()
        with self._lock:
            self._pending[command_id] = waiter
        try:
            payload = {"t": "cmd", "id": command_id, "a": action, **extra}
            for _ in range(2):
                if not self._write(payload):
                    return None
                try:
                    return waiter.get(timeout=timeout)
                except queue.Empty:
                    self._log.warning("Sin ack para %s; se reintenta con el mismo id", command_id)
            return None
        finally:
            with self._lock:
                self._pending.pop(command_id, None)

    def take_events(self) -> list[dict]:
        events: list[dict] = []
        while True:
            try:
                events.append(self._events.get_nowait())
            except queue.Empty:
                return events

    def _write(self, payload: dict) -> bool:
        line = json.dumps(payload, separators=(",", ":")) + "\n"
        try:
            assert self._serial is not None
            self._serial.write(line.encode())
            self._serial.flush()
            return True
        except Exception as error:  # noqa: BLE001
            self._log.warning("Escritura serie fallida: %s", error)
            self._available = False
            return False
