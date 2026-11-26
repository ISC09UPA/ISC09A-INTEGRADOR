"""Doble local de Home para desarrollo y pruebas.

- Recibe eventos del agente en POST /events y los valida contra contracts/v1 y v2.
- Sirve un endpoint de subida de capturas en POST /upload/{captureId}.
- Permite invocar métodos directos del agente y esperar eventos.

No sustituye a Azure: es el banco de pruebas del contrato.
"""
from __future__ import annotations

import json
import threading
import time
import urllib.error
import urllib.request
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Callable

from jsonschema import Draft202012Validator
from referencing import Registry, Resource

ROOT = Path(__file__).resolve().parents[2]
CONTRACTS = ROOT / "contracts"


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


class ContractValidator:
    def __init__(self) -> None:
        registry = Registry()
        for folder in (CONTRACTS / "v1" / "schemas", CONTRACTS / "v2" / "schemas"):
            for path in folder.glob("*.json"):
                document = _load(path)
                resource = Resource.from_contents(document)
                if "$id" in document:
                    registry = registry.with_resource(document["$id"], resource)
                registry = registry.with_resource(path.as_uri(), resource)
        self._v1 = Draft202012Validator(
            _load(CONTRACTS / "v1" / "schemas" / "telemetry-envelope.json"),
            registry=registry,
            format_checker=Draft202012Validator.FORMAT_CHECKER,
        )
        self._v2 = Draft202012Validator(
            _load(CONTRACTS / "v2" / "schemas" / "device-state-reported.json"),
            registry=registry,
            format_checker=Draft202012Validator.FORMAT_CHECKER,
        )

    def validate(self, event: dict) -> None:
        validator = self._v2 if event.get("schemaVersion") == 2 else self._v1
        errors = sorted(validator.iter_errors(event), key=lambda error: list(error.path))
        if errors:
            detail = "; ".join(f"{list(error.path)}: {error.message}" for error in errors[:5])
            raise ValueError(detail)


class HomeDouble:
    def __init__(self, host: str = "127.0.0.1", port: int = 0, validate: bool = True):
        self.events: list[dict] = []
        self.uploads: dict[str, bytes] = {}
        self.errors: list[str] = []
        self._condition = threading.Condition()
        self._validator = ContractValidator() if validate else None
        double = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, fmt: str, *args) -> None:  # noqa: A003
                pass

            def _json(self, status: int, payload: dict) -> None:
                body = json.dumps(payload).encode()
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

            def do_POST(self) -> None:  # noqa: N802
                length = int(self.headers.get("Content-Length", "0") or 0)
                raw = self.rfile.read(length) if length else b""
                if self.path == "/events":
                    try:
                        event = json.loads(raw)
                        if double._validator is not None:
                            double._validator.validate(event)
                    except Exception as error:  # noqa: BLE001
                        double.errors.append(str(error))
                        self._json(400, {"error": str(error)})
                        return
                    with double._condition:
                        double.events.append(event)
                        double._condition.notify_all()
                    self._json(200, {"accepted": True})
                    return
                if self.path.startswith("/upload/"):
                    capture_id = self.path.rsplit("/", 1)[-1]
                    token = self.headers.get("X-Capture-Token", "")
                    if len(token) != 43:
                        self._json(400, {"error": "token"})
                        return
                    double.uploads[capture_id] = raw
                    self._json(
                        200,
                        {
                            "captureId": capture_id,
                            "blobName": f"00000000-0000-0000-0000-000000000000/casa-pi-01/{capture_id}.jpg",
                        },
                    )
                    return
                self._json(404, {"error": "notFound"})

        self._server = ThreadingHTTPServer((host, port), Handler)
        self._server.daemon_threads = True
        self.host, self.port = self._server.server_address
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()

    # ------------------------------------------------------------ URLs

    @property
    def events_url(self) -> str:
        return f"http://{self.host}:{self.port}/events"

    def upload_url(self, capture_id: str) -> str:
        return f"http://{self.host}:{self.port}/upload/{capture_id}"

    # ------------------------------------------------------------ utilidades

    def close(self) -> None:
        self._server.shutdown()
        self._server.server_close()

    def wait_for(self, predicate: Callable[[dict], bool], timeout: float = 5.0) -> dict | None:
        deadline = time.time() + timeout
        with self._condition:
            while time.time() < deadline:
                for event in self.events:
                    if predicate(event):
                        return event
                self._condition.wait(timeout=min(0.2, max(0.0, deadline - time.time())))
        return None

    def wait_kind(self, kind: str, timeout: float = 5.0) -> dict | None:
        return self.wait_for(lambda event: event.get("type") == kind, timeout)

    def results_for(self, command_id: str) -> list[dict]:
        with self._condition:
            return [
                event
                for event in self.events
                if event.get("type") == "CommandResult"
                and event.get("payload", {}).get("commandId") == command_id
            ]


def send_method(agent_url: str, name: str, body: dict, timeout: float = 20.0) -> int:
    request = urllib.request.Request(
        f"{agent_url}/local/method/{name}",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return response.status
    except urllib.error.HTTPError as error:
        return error.code


def make_command(
    name: str,
    payload: dict,
    *,
    command_id: str | None = None,
    sequence: int = 0,
    device_id: str = "casa-pi-01",
    expires_in: float = 30.0,
    attempt_id: str | None = None,
) -> dict:
    from datetime import datetime, timedelta, timezone

    now = datetime.now(timezone.utc)
    command_id = command_id or str(uuid.uuid4())
    body = {
        "commandId": command_id,
        "deviceId": device_id,
        "type": name,
        "commandSequence": sequence,
        "attemptId": attempt_id if attempt_id is not None else (command_id if name == "OpenDoor" else None),
        "issuedAtUtc": now.isoformat(timespec="milliseconds").replace("+00:00", "Z"),
        "expiresAtUtc": (now + timedelta(seconds=expires_in)).isoformat(timespec="milliseconds").replace("+00:00", "Z"),
        "payload": payload,
    }
    return body
