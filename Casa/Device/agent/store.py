"""Persistencia durable del agente (SQLite).

Guarda el estado aplicado, las órdenes recibidas y la bandeja de salida de eventos.
Es la base de la idempotencia: un reinicio nunca repite un efecto ya confirmado.
"""
from __future__ import annotations

import json
import sqlite3
import threading
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any

SCHEMA = """
CREATE TABLE IF NOT EXISTS meta (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS commands (
  command_id TEXT PRIMARY KEY,
  type TEXT NOT NULL,
  request_hash TEXT NOT NULL,
  payload TEXT NOT NULL,
  status TEXT NOT NULL,
  result TEXT,
  correlation_id TEXT NOT NULL,
  sequence INTEGER NOT NULL,
  received_at TEXT NOT NULL,
  expires_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS outbox (
  event_id TEXT PRIMARY KEY,
  kind TEXT NOT NULL,
  body TEXT NOT NULL,
  created_at TEXT NOT NULL,
  delivered_at TEXT
);
CREATE INDEX IF NOT EXISTS outbox_pending ON outbox (delivered_at, created_at);
CREATE TABLE IF NOT EXISTS inbox (
  event_id TEXT PRIMARY KEY,
  received_at TEXT NOT NULL
);
"""

TERMINAL = {"executed", "rejected", "expired", "unknown", "failed"}


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


@dataclass
class CommandRecord:
    command_id: str
    type: str
    request_hash: str
    payload: dict[str, Any]
    status: str
    correlation_id: str
    sequence: int
    received_at: str
    expires_at: str


class Store:
    def __init__(self, path: str):
        self._lock = threading.RLock()
        self._db = sqlite3.connect(path, check_same_thread=False)
        self._db.row_factory = sqlite3.Row
        with self._lock:
            self._db.execute("PRAGMA journal_mode=WAL")
            self._db.execute("PRAGMA synchronous=FULL")
            self._db.executescript(SCHEMA)
            self._db.commit()

    # ------------------------------------------------------------ estado

    def ensure_initialized(self, lighting_mode: str, security_mode: str) -> None:
        defaults = {
            "light_on": "0",
            "lighting_mode": lighting_mode,
            "security_mode": security_mode,
            "revision": "0",
            "applied_at": utc_now_iso(),
            "last_sequence": "-1",
        }
        with self._lock:
            for key, value in defaults.items():
                self._db.execute("INSERT OR IGNORE INTO meta (key, value) VALUES (?, ?)", (key, value))
            self._db.commit()

    def _get(self, key: str) -> str:
        row = self._db.execute("SELECT value FROM meta WHERE key = ?", (key,)).fetchone()
        return row["value"] if row else ""

    def _set(self, key: str, value: str) -> None:
        self._db.execute(
            "INSERT INTO meta (key, value) VALUES (?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value",
            (key, value),
        )

    def state(self) -> dict[str, Any]:
        with self._lock:
            return {
                "light_on": self._get("light_on") == "1",
                "lighting_mode": self._get("lighting_mode") or "manual",
                "security_mode": self._get("security_mode") or "disarmed",
                "revision": int(self._get("revision") or 0),
                "applied_at": self._get("applied_at") or utc_now_iso(),
                "last_sequence": int(self._get("last_sequence") or -1),
            }

    def apply_light(self, light_on: bool) -> bool:
        """Aplica el estado de la luz. Devuelve True si la revisión avanzó."""
        with self._lock:
            current = self.state()
            if current["light_on"] == light_on:
                return False
            self._set("light_on", "1" if light_on else "0")
            self._set("revision", str(current["revision"] + 1))
            self._set("applied_at", utc_now_iso())
            self._db.commit()
            return True

    def apply_lighting_mode(self, mode: str) -> bool:
        with self._lock:
            current = self.state()
            if current["lighting_mode"] == mode:
                return False
            self._set("lighting_mode", mode)
            self._set("revision", str(current["revision"] + 1))
            self._set("applied_at", utc_now_iso())
            self._db.commit()
            return True

    def apply_security_mode(self, mode: str) -> bool:
        with self._lock:
            current = self.state()
            if current["security_mode"] == mode:
                return False
            self._set("security_mode", mode)
            self._db.commit()
            return True

    def bump_sequence(self, sequence: int) -> None:
        with self._lock:
            self._set("last_sequence", str(sequence))
            self._db.commit()

    def touch_applied(self) -> None:
        with self._lock:
            self._set("applied_at", utc_now_iso())
            self._db.commit()

    # ------------------------------------------------------------ órdenes

    def command(self, command_id: str) -> CommandRecord | None:
        with self._lock:
            row = self._db.execute("SELECT * FROM commands WHERE command_id = ?", (command_id,)).fetchone()
        if row is None:
            return None
        return CommandRecord(
            command_id=row["command_id"],
            type=row["type"],
            request_hash=row["request_hash"],
            payload=json.loads(row["payload"]),
            status=row["status"],
            correlation_id=row["correlation_id"],
            sequence=row["sequence"],
            received_at=row["received_at"],
            expires_at=row["expires_at"],
        )

    def begin_command(
        self, command_id: str, type_: str, request_hash: str, payload: dict, correlation_id: str, sequence: int, expires_at: str
    ) -> None:
        with self._lock:
            self._db.execute(
                "INSERT INTO commands (command_id, type, request_hash, payload, status, result, correlation_id, sequence, received_at, expires_at)"
                " VALUES (?, ?, ?, ?, 'executing', NULL, ?, ?, ?, ?)",
                (command_id, type_, request_hash, json.dumps(payload), correlation_id, sequence, utc_now_iso(), expires_at),
            )
            self._db.commit()

    def finish_command(self, command_id: str, status: str, result: dict[str, Any]) -> None:
        with self._lock:
            self._db.execute(
                "UPDATE commands SET status = ?, result = ? WHERE command_id = ?",
                (status, json.dumps(result), command_id),
            )
            self._db.commit()

    def result(self, command_id: str) -> dict[str, Any] | None:
        with self._lock:
            row = self._db.execute("SELECT result FROM commands WHERE command_id = ?", (command_id,)).fetchone()
        if row is None or row["result"] is None:
            return None
        return json.loads(row["result"])

    def interrupted_commands(self) -> list[CommandRecord]:
        with self._lock:
            rows = self._db.execute("SELECT command_id FROM commands WHERE status = 'executing'").fetchall()
        return [record for row in rows if (record := self.command(row["command_id"])) is not None]

    # ------------------------------------------------------------ outbox

    def enqueue(self, event: dict[str, Any]) -> None:
        with self._lock:
            self._db.execute(
                "INSERT OR IGNORE INTO outbox (event_id, kind, body, created_at) VALUES (?, ?, ?, ?)",
                (event["eventId"], event["type"], json.dumps(event), utc_now_iso()),
            )
            self._db.commit()

    def pending_outbox(self, limit: int = 25) -> list[dict[str, Any]]:
        with self._lock:
            rows = self._db.execute(
                "SELECT body FROM outbox WHERE delivered_at IS NULL ORDER BY created_at LIMIT ?", (limit,)
            ).fetchall()
        return [json.loads(row["body"]) for row in rows]

    def mark_delivered(self, event_id: str) -> None:
        with self._lock:
            self._db.execute("UPDATE outbox SET delivered_at = ? WHERE event_id = ?", (utc_now_iso(), event_id))
            self._db.commit()

    def has_event(self, event_id: str) -> bool:
        with self._lock:
            row = self._db.execute("SELECT 1 FROM inbox WHERE event_id = ?", (event_id,)).fetchone()
        return row is not None

    def remember_event(self, event_id: str) -> None:
        with self._lock:
            self._db.execute("INSERT OR IGNORE INTO inbox (event_id, received_at) VALUES (?, ?)", (event_id, utc_now_iso()))
            self._db.commit()
