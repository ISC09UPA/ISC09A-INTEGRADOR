"""Agente del dispositivo.

Recibe métodos directos (SetLight, SetLightingMode, SetSecurityMode, OpenDoor,
CaptureImage), ejecuta contra la placa de E/S, persiste el resultado y publica
eventos conformes al contrato. No conoce detalles de Azure: usa un transporte.
"""
from __future__ import annotations

import hashlib
import json
import logging
import threading
import time
import urllib.request
import uuid
from datetime import datetime, timezone
from typing import Any

from camera import Camera, CameraError
from config import Config
from events import (
    access_requested,
    capture_uploaded,
    command_result,
    device_state,
    iso,
    light_snapshot,
    motion_detected,
    new_id,
    result_event,
    telemetry,
)
from store import Store, utc_now_iso

TERMINAL = {"executed", "rejected", "expired", "unknown", "failed"}


class MethodError(Exception):
    """Rechazo definitivo del dispositivo (se responde 400)."""

    def __init__(self, code: str, detail: str = ""):
        super().__init__(detail or code)
        self.code = code


def _parse_utc(value: str) -> datetime:
    text = value.strip()
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    return datetime.fromisoformat(text).astimezone(timezone.utc)


def _is_uuid(value: Any) -> bool:
    if not isinstance(value, str):
        return False
    try:
        uuid.UUID(value)
        return True
    except ValueError:
        return False


class DeviceAgent:
    def __init__(
        self,
        config: Config,
        store: Store,
        serial_link,
        transport,
        camera: Camera | None = None,
        logger: logging.Logger | None = None,
    ):
        self._cfg = config
        self._store = store
        self._serial = serial_link
        self._transport = transport
        self._camera = camera or Camera(config.camera_backend, config.camera_device)
        self._log = logger or logging.getLogger("agent")
        self._lock = threading.RLock()
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None
        self._last_report = 0.0
        self._motion = False

    # ------------------------------------------------------------ ciclo de vida

    def start(self) -> None:
        self._store.ensure_initialized(self._cfg.lighting_mode, self._cfg.security_mode)
        self._recover_interrupted()
        self._serial.start()
        if self._serial.available:
            self._sync_board_mode()
        self._report()
        self._thread = threading.Thread(target=self._loop, name="agent-loop", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        if self._thread is not None:
            self._thread.join(timeout=2.0)
        self._serial.stop()
        disconnect = getattr(self._transport, "disconnect", None)
        if callable(disconnect):
            disconnect()

    def _loop(self) -> None:
        while not self._stop.is_set():
            try:
                self._serial.ensure()
                for event in self._serial.take_events():
                    self._handle_board_event(event)
                now = time.monotonic()
                if now - self._last_report >= self._cfg.report_interval_s:
                    self._report()
                self._deliver_outbox()
            except Exception as error:  # noqa: BLE001
                self._log.exception("Error en el bucle del agente: %s", error)
            self._stop.wait(1.0)

    def state(self) -> dict[str, Any]:
        return self._store.state()

    # ------------------------------------------------------------ métodos directos

    def handle_method(self, name: str, body: Any) -> int:
        try:
            with self._lock:
                return self._handle_method(name, body)
        except MethodError as error:
            self._log.warning("Método %s rechazado: %s", name, error.code)
            return 400
        except Exception as error:  # noqa: BLE001
            self._log.exception("Fallo inesperado en %s: %s", name, error)
            return 500

    def _handle_method(self, name: str, body: Any) -> int:
        if not isinstance(body, dict):
            raise MethodError("invalidPayload", "El cuerpo no es un objeto.")
        for field in ("commandId", "deviceId", "type", "commandSequence", "payload", "expiresAtUtc"):
            if field not in body:
                raise MethodError("invalidPayload", f"Falta {field}.")
        if body["type"] != name:
            raise MethodError("invalidPayload", "El tipo no coincide con el método.")
        if body["deviceId"] != self._cfg.device_id:
            raise MethodError("unauthorized", "La orden es para otro dispositivo.")
        sequence = body["commandSequence"]
        if isinstance(sequence, bool) or not isinstance(sequence, int) or sequence < 0:
            raise MethodError("invalidPayload", "commandSequence inválido.")
        payload = body["payload"]
        if not isinstance(payload, dict):
            raise MethodError("invalidPayload", "payload inválido.")

        request_hash = self._request_hash(name, payload)
        command_id = str(body["commandId"])
        existing = self._store.command(command_id)
        if existing is not None:
            if existing.request_hash != request_hash:
                raise MethodError("duplicateChanged", "El id ya existe con otro contenido.")
            if existing.status in TERMINAL:
                result = self._store.result(command_id)
                if result is not None:
                    self._store.enqueue(result_event(result, correlation_id=existing.correlation_id))
            return 202

        expires_at = self._validate_payload(name, payload, body)
        now = datetime.now(timezone.utc)

        correlation_id = new_id()
        self._store.begin_command(
            command_id, name, request_hash, payload, correlation_id, sequence, iso(expires_at)
        )

        if expires_at <= now:
            self._finish_expired(command_id, name, correlation_id)
            return 202

        state = self._store.state()
        if sequence <= state["last_sequence"]:
            self._finish_rejected(command_id, "sequenceRejected", "Secuencia no monotónica.")
            raise MethodError("sequenceRejected", "Secuencia antigua.")
        self._store.bump_sequence(sequence)

        if name in ("SetLight", "SetSecurityMode", "OpenDoor") and not self._serial.available:
            self._finish_rejected(command_id, "deviceRejected", "La placa de E/S no responde.")
            raise MethodError("deviceRejected", "Placa no disponible.")

        if name == "SetLight":
            return self._execute_light(command_id, name, payload, correlation_id)
        if name == "SetLightingMode":
            return self._execute_lighting_mode(command_id, name, payload, correlation_id)
        if name == "SetSecurityMode":
            return self._execute_security_mode(command_id, name, payload, correlation_id)
        if name == "OpenDoor":
            return self._execute_door(command_id, name, payload, body, correlation_id)
        if name == "CaptureImage":
            threading.Thread(
                target=self._execute_capture,
                args=(command_id, name, payload, correlation_id),
                name="capture",
                daemon=True,
            ).start()
            return 202
        raise MethodError("invalidPayload", "Método no soportado.")

    # ------------------------------------------------------------ validación

    def _validate_payload(self, name: str, payload: dict, body: dict) -> datetime:
        if name == "SetLight":
            if set(payload) != {"on"} or not isinstance(payload["on"], bool):
                raise MethodError("invalidPayload", "SetLight requiere {on: bool}.")
        elif name == "SetLightingMode":
            if set(payload) != {"mode"} or payload["mode"] not in ("auto", "manual"):
                raise MethodError("invalidPayload", "SetLightingMode requiere un modo válido.")
        elif name == "SetSecurityMode":
            if set(payload) != {"mode"} or payload["mode"] not in ("armed", "disarmed"):
                raise MethodError("invalidPayload", "SetSecurityMode requiere un modo válido.")
        elif name == "OpenDoor":
            if set(payload) != {"doorId", "durationMs"}:
                raise MethodError("invalidPayload", "OpenDoor requiere doorId y durationMs.")
            duration = payload["durationMs"]
            if isinstance(duration, bool) or not isinstance(duration, int) or not 100 <= duration <= 3000:
                raise MethodError("invalidPayload", "durationMs fuera de rango.")
            attempt = body.get("attemptId")
            if not _is_uuid(attempt) or attempt != body["commandId"]:
                raise MethodError("invalidPayload", "attemptId inválido para OpenDoor.")
        elif name == "CaptureImage":
            if set(payload) != {"captureId", "uploadUrl", "uploadToken", "uploadExpiresAtUtc"}:
                raise MethodError("invalidPayload", "CaptureImage requiere el ticket de subida.")
            if not _is_uuid(payload["captureId"]):
                raise MethodError("invalidPayload", "captureId inválido.")
            url = payload["uploadUrl"]
            schemes = ("http://",) if self._cfg.transport == "local" else ("https://",)
            if not isinstance(url, str) or not url.startswith(schemes):
                raise MethodError("invalidPayload", "uploadUrl inválida.")
            if not isinstance(payload["uploadToken"], str) or len(payload["uploadToken"]) != 43:
                raise MethodError("invalidPayload", "uploadToken inválido.")
        else:
            raise MethodError("invalidPayload", "Método no soportado.")
        try:
            return _parse_utc(str(body["expiresAtUtc"]))
        except (ValueError, TypeError) as error:
            raise MethodError("invalidPayload", "expiresAtUtc inválido.") from error

    @staticmethod
    def _request_hash(name: str, payload: dict) -> str:
        canonical = json.dumps({"type": name, "payload": payload}, sort_keys=True, separators=(",", ":"))
        return hashlib.sha256(canonical.encode()).hexdigest()

    # ------------------------------------------------------------ ejecución

    def _execute_light(self, command_id: str, name: str, payload: dict, correlation_id: str) -> int:
        ack = self._serial.request("light", {"v": payload["on"]}, command_id)
        if ack is None:
            self._finish_unknown(command_id, name, correlation_id, "Sin ack de la placa.")
            return 202
        if ack.get("t") == "err":
            self._finish_rejected(command_id, self._map_error(ack.get("code")), "La placa rechazó la orden.")
            raise MethodError(self._map_error(ack.get("code")), "Placa rechazó.")
        applied = bool(ack.get("light", payload["on"]))
        self._store.apply_light(applied)
        self._finish_executed(command_id, name, correlation_id)
        return 202

    def _execute_lighting_mode(self, command_id: str, name: str, payload: dict, correlation_id: str) -> int:
        self._store.apply_lighting_mode(payload["mode"])
        self._finish_executed(command_id, name, correlation_id)
        return 202

    def _execute_security_mode(self, command_id: str, name: str, payload: dict, correlation_id: str) -> int:
        ack = self._serial.request("mode", {"v": payload["mode"]}, command_id)
        if ack is None:
            self._finish_unknown(command_id, name, correlation_id, "Sin ack de la placa.")
            return 202
        if ack.get("t") == "err":
            code = self._map_error(ack.get("code"))
            self._finish_rejected(command_id, code, "La placa rechazó la orden.")
            raise MethodError(code, "Placa rechazó.")
        self._store.apply_security_mode(payload["mode"])
        self._finish_executed(command_id, name, correlation_id)
        return 202

    def _execute_door(self, command_id: str, name: str, payload: dict, body: dict, correlation_id: str) -> int:
        ack = self._serial.request("door", {"ms": payload["durationMs"]}, command_id)
        if ack is None:
            self._finish_unknown(command_id, name, correlation_id, "Sin ack de la placa.")
            return 202
        if ack.get("t") == "err":
            code = self._map_error(ack.get("code"))
            self._finish_rejected(command_id, code, "La placa rechazó la orden.")
            raise MethodError(code, "Placa rechazó.")
        self._finish_executed(command_id, name, correlation_id)
        return 202

    def _execute_capture(self, command_id: str, name: str, payload: dict, correlation_id: str) -> None:
        try:
            image = self._camera.capture_jpeg()
            blob_name = self._upload(payload["uploadUrl"], payload["uploadToken"], image)
            self._store.enqueue(capture_uploaded(payload["captureId"], blob_name, correlation_id=correlation_id))
            self._finish_executed(command_id, name, correlation_id)
        except (CameraError, OSError, ValueError) as error:
            self._log.warning("Captura fallida: %s", error)
            self._finish_failed(command_id, name, correlation_id, str(error))
        except Exception as error:  # noqa: BLE001
            self._log.exception("Captura inesperada: %s", error)
            self._finish_failed(command_id, name, correlation_id, "Fallo inesperado en la captura.")

    def _upload(self, url: str, token: str, image: bytes) -> str:
        request = urllib.request.Request(
            url,
            data=image,
            method="POST",
            headers={
                "Content-Type": "image/jpeg",
                "Content-Length": str(len(image)),
                "X-Capture-Token": token,
            },
        )
        with urllib.request.urlopen(request, timeout=30) as response:
            if not 200 <= response.status < 300:
                raise ValueError(f"Subida rechazada con HTTP {response.status}.")
            body = json.loads(response.read().decode() or "{}")
        blob_name = body.get("blobName")
        if not isinstance(blob_name, str) or not blob_name:
            raise ValueError("La subida no devolvió blobName.")
        return blob_name

    # ------------------------------------------------------------ resultados

    def _finish_executed(self, command_id: str, name: str, correlation_id: str) -> None:
        state = self._store.state()
        snapshot = light_snapshot(state["light_on"], state["lighting_mode"], state["applied_at"])
        payload = command_result(
            command_id,
            name,
            "executed",
            snapshot=snapshot,
            revision=state["revision"],
            correlation_id=correlation_id,
        )
        self._finish(command_id, "executed", payload)

    def _finish_unknown(self, command_id: str, name: str, correlation_id: str, reason: str) -> None:
        payload = command_result(
            command_id, name, "unknown", reason_code="unknown", reason=reason, correlation_id=correlation_id
        )
        self._finish(command_id, "unknown", payload)

    def _finish_expired(self, command_id: str, name: str, correlation_id: str) -> None:
        payload = command_result(
            command_id, name, "expired", reason_code="expired", reason="La orden llegó vencida.",
            correlation_id=correlation_id,
        )
        self._finish(command_id, "expired", payload)

    def _finish_rejected(self, command_id: str, code: str, reason: str) -> None:
        # Rechazo definitivo sin evento: Home marca la orden como rechazada por el 400.
        self._store.finish_command(command_id, "rejected", {"reasonCode": code, "reason": reason})

    def _finish_failed(self, command_id: str, name: str, correlation_id: str, reason: str) -> None:
        payload = command_result(
            command_id, name, "failed", reason_code="failed", reason=reason, correlation_id=correlation_id
        )
        self._finish(command_id, "failed", payload)

    def _finish(self, command_id: str, status: str, payload: dict) -> None:
        self._store.finish_command(command_id, status, payload)
        self._store.enqueue(result_event(payload, correlation_id=payload["correlationId"]))

    @staticmethod
    def _map_error(code: Any) -> str:
        if code in ("duplicateChanged", "invalidPayload", "sequenceRejected", "deviceRejected", "unauthorized", "forbidden"):
            return str(code)
        return "deviceRejected"

    # ------------------------------------------------------------ eventos de placa

    def _handle_board_event(self, event: dict) -> None:
        kind = event.get("k")
        state = self._store.state()
        if kind == "motion":
            self._motion = True
            self._store.enqueue(motion_detected(self._cfg.light_id, state["security_mode"]))
            return
        if kind == "finger":
            if event.get("ok"):
                self._store.enqueue(
                    access_requested(self._cfg.light_id, "fingerprint", event.get("tpl"))
                )
            return
        if kind == "link":
            self._log.warning("La placa reportó pérdida de enlace con el cerebro.")
            return
        if kind == "enroll":
            self._log.info("Enrolamiento de huella: %s (plantilla %s)", event.get("v"), event.get("tpl"))
            return

    def _sync_board_mode(self) -> None:
        state = self._store.state()
        ack = self._serial.request("mode", {"v": state["security_mode"]}, f"sync-{new_id()}")
        if ack is None:
            self._log.warning("No se pudo sincronizar el modo de seguridad con la placa.")

    # ------------------------------------------------------------ reporte

    def _report(self) -> None:
        state = self._store.state()
        status = self._serial.status or {}
        light_percent = float(status.get("ldr", 0) or 0) if self._cfg.ldr_enabled else 0.0
        # The board event is a pulse; the status frame carries the current PIR level.
        # Keep the event until the next report, then follow the board instead of
        # leaving motion stuck at true forever.
        detected = self._motion or bool(status.get("motion", 0))
        snapshot = light_snapshot(state["light_on"], state["lighting_mode"], state["applied_at"])
        self._store.enqueue(
            device_state(self._cfg.light_id, state["revision"], state["security_mode"], snapshot)
        )
        self._store.enqueue(
            telemetry(light_percent, detected, state["light_on"], state["security_mode"])
        )
        self._motion = bool(status.get("motion", 0))
        self._last_report = time.monotonic()

    def _deliver_outbox(self) -> None:
        for event in self._store.pending_outbox():
            if not self._transport.deliver(event):
                break
            self._store.mark_delivered(event["eventId"])

    # ------------------------------------------------------------ recuperación

    def _recover_interrupted(self) -> None:
        for command in self._store.interrupted_commands():
            self._log.warning("Orden %s quedó en ejecución; se marca unknown.", command.command_id)
            payload = command_result(
                command.command_id,
                command.type,
                "unknown",
                reason_code="unknown",
                reason="El agente se reinició durante la ejecución.",
                correlation_id=command.correlation_id,
            )
            self._store.finish_command(command.command_id, "unknown", payload)
            self._store.enqueue(result_event(payload, correlation_id=command.correlation_id))
