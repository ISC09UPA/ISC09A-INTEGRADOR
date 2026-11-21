import json
import sqlite3

from ..models import is_uuid


class Store:
    def __init__(self, path, max_events=1000):
        self.max_events = max_events
        self.db = sqlite3.connect(str(path), timeout=10)
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("PRAGMA synchronous=FULL")
        self.db.executescript('''
            CREATE TABLE IF NOT EXISTS state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS commands (
                id TEXT PRIMARY KEY,
                body TEXT NOT NULL,
                status TEXT NOT NULL,
                result TEXT,
                event_id TEXT,
                correlation_id TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS events (
                seq INTEGER PRIMARY KEY AUTOINCREMENT,
                body TEXT NOT NULL,
                delivered INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS outbox (
                seq INTEGER PRIMARY KEY AUTOINCREMENT,
                command_id TEXT NOT NULL,
                body TEXT NOT NULL
            );
        ''')
        # Add columns/tables introduced after the first C11 delivery without
        # dropping existing data.
        self._migrate_commands_columns()
        # A crash may have occurred after the physical effect. Never retry it.
        self._recover_executing()

    def get(self, key, default=None):
        row = self.db.execute("SELECT value FROM state WHERE key=?", (key,)).fetchone()
        return json.loads(row[0]) if row else default

    def set(self, key, value):
        with self.db:
            self.db.execute("INSERT OR REPLACE INTO state VALUES (?,?)", (key, json.dumps(value)))

    # ------------------------------------------------------------------
    # State revision (monotonic per-light state change counter)
    # ------------------------------------------------------------------

    def state_revision(self):
        return self.get("light_state_revision", 0)

    # ------------------------------------------------------------------
    # Command sequence (per-light monotonic counter from Home)
    # ------------------------------------------------------------------

    def last_command_sequence(self):
        return self.get("last_command_sequence", -1)

    def record_command_sequence(self, sequence):
        """Persist sequence only if greater than the last accepted one."""
        if type(sequence) is not int:
            raise ValueError("commandSequence debe ser entero")
        with self.db:
            last = self.last_command_sequence()
            if sequence <= last:
                return False
            self.set("last_command_sequence", sequence)
            return True

    # ------------------------------------------------------------------
    # Command durable outbox
    # ------------------------------------------------------------------

    def command_record(self, command_id):
        row = self.db.execute(
            "SELECT status, result, body, event_id, correlation_id FROM commands WHERE id=?",
            (command_id,),
        ).fetchone()
        if not row:
            return None
        return {
            "status": row[0],
            "result": json.loads(row[1]) if row[1] else None,
            "command": json.loads(row[2]),
            "event_id": row[3],
            "correlation_id": row[4],
        }

    def _is_terminal_status(self, status):
        return status in ("executed", "rejected", "expired", "unknown", "failed")

    def atomic_outcome(self, command, result_payload, event, state_delta=None):
        """Persist state delta, final result and event atomically.

        state_delta is a dict of state keys to apply. The effective light
        state revision is bumped only when light_on or lighting_mode actually
        change with respect to the current DB value. The committed revision
        (or None for non-light commands) is returned so the caller can build
        the response/envelope with the same value.

        A terminal command row is never overwritten; this protects an executed
        result from being replaced by a later transient rejection.
        """
        command_id = result_payload["commandId"]
        correlation_id = result_payload.get("correlationId")
        event_id = result_payload.get("eventId")
        body_json = json.dumps(command, sort_keys=True)
        result_json = json.dumps(result_payload)
        event_json = json.dumps(event)

        committed_revision = None
        with self.db:
            existing = self.db.execute(
                "SELECT status FROM commands WHERE id=?", (command_id,)
            ).fetchone()
            if existing and self._is_terminal_status(existing[0]):
                # Do not overwrite a finished result; caller handles replay.
                raise RuntimeError("command already terminal")

            if state_delta:
                light_changed = False
                for key, value in state_delta.items():
                    if key == "light_on":
                        current = self.get("light_on", False)
                        if current != value:
                            light_changed = True
                    elif key == "lighting_mode":
                        current = self.get("lighting_mode", "auto")
                        if current != value:
                            light_changed = True
                    self.db.execute(
                        "INSERT OR REPLACE INTO state VALUES (?,?)",
                        (key, json.dumps(value)),
                    )
                if light_changed:
                    committed_revision = self.state_revision() + 1
                    self.db.execute(
                        "INSERT OR REPLACE INTO state VALUES (?,?)",
                        ("light_state_revision", json.dumps(committed_revision)),
                    )

            # Executed commands always report the effective light state revision.
            # If no light key changed, this is the current revision; otherwise it
            # is the newly bumped revision. Update the mutable result payload so
            # the stored event/outbox match the returned response.
            if result_payload.get("status") == "executed" and committed_revision is None:
                committed_revision = self.state_revision()
            if result_payload.get("status") == "executed":
                result_payload["stateRevision"] = committed_revision
                result_json = json.dumps(result_payload)
                event["payload"] = result_payload
                event_json = json.dumps(event)

            if existing:
                self.db.execute(
                    "UPDATE commands SET body=?, status=?, result=?, event_id=?, correlation_id=? WHERE id=?",
                    (body_json, result_payload["status"], result_json, event_id, correlation_id, command_id),
                )
            else:
                self.db.execute(
                    """INSERT INTO commands
                       (id, body, status, result, event_id, correlation_id)
                       VALUES (?,?,?,?,?,?)""",
                    (command_id, body_json, result_payload["status"], result_json,
                     event_id, correlation_id),
                )
            self.db.execute("INSERT INTO events(body) VALUES (?)", (event_json,))
            self.db.execute("INSERT INTO outbox(command_id, body) VALUES (?,?)", (command_id, event_json))
            self._trim_events()
        return committed_revision

    def claim(self, command, correlation_id=None):
        """Insert an 'executing' row. Returns False if the id already exists.

        The optional correlation_id is stored now so that a crash after the
        physical effect can emit an 'unknown' result tied to the original
        admission correlationId.
        """
        with self.db:
            existing = self.db.execute(
                "SELECT status FROM commands WHERE id=?", (command["commandId"],)
            ).fetchone()
            if existing:
                return False
            self.db.execute(
                "INSERT INTO commands(id,body,status,correlation_id) VALUES (?,?,?,?)",
                (command["commandId"], json.dumps(command, sort_keys=True), "executing",
                 correlation_id),
            )
            return True

    def finish(self, command_id, result):
        with self.db:
            self.db.execute(
                "UPDATE commands SET status=?,result=? WHERE id=?",
                (result["status"], json.dumps(result), command_id),
            )

    # ------------------------------------------------------------------
    # Event queue
    # ------------------------------------------------------------------

    def emit(self, body):
        with self.db:
            self.db.execute("INSERT INTO events(body) VALUES (?)", (json.dumps(body),))
            self._trim_events()

    def _trim_events(self):
        count = self.db.execute("SELECT COUNT(*) FROM events").fetchone()[0]
        excess = count - self.max_events
        if excess > 0:
            # Prefer dropping periodic summaries over security events.
            self.db.execute(
                """DELETE FROM events WHERE seq IN (
                    SELECT seq FROM events
                    ORDER BY delivered DESC,
                             CASE WHEN body LIKE '%TelemetryReported%' THEN 0 ELSE 1 END,
                             seq
                    LIMIT ?)""",
                (excess,),
            )

    def pending(self, limit=20):
        return [(seq, json.loads(body)) for seq, body in self.db.execute(
            "SELECT seq,body FROM events WHERE delivered=0 ORDER BY seq LIMIT ?", (limit,))]

    def delivered(self, seq):
        with self.db:
            self.db.execute("UPDATE events SET delivered=1 WHERE seq=?", (seq,))

    def apply_light_state(self, on):
        """Atomically update light_on and bump stateRevision when it changes."""
        current = self.get("light_on", False)
        if bool(on) == bool(current):
            return current
        with self.db:
            self.db.execute("INSERT OR REPLACE INTO state VALUES (?,?)",
                            ("light_on", json.dumps(bool(on))))
            revision = self.state_revision()
            self.db.execute("INSERT OR REPLACE INTO state VALUES (?,?)",
                            ("light_state_revision", json.dumps(revision + 1)))
        return bool(on)

    def ensure_event_persisted(self, command_id):
        """Re-insert a durable CommandResult envelope from the outbox archive if
        it is no longer present in the pending events queue (e.g. trimmed).

        Priority is: archived outbox, then a previously emitted event still in
        the events table, then a complete legacy result with a known applied
        timestamp. Incomplete/error results without a known original timestamp
        are never fabricated as historical events.
        """
        from ..models import is_complete_command_result

        pending = self.db.execute(
            "SELECT 1 FROM events WHERE delivered=0 AND json_extract(body, '$.payload.commandId')=? LIMIT 1",
            (command_id,),
        ).fetchone()
        if pending:
            return
        archived = self.db.execute(
            "SELECT body FROM outbox WHERE command_id=? ORDER BY seq DESC LIMIT 1",
            (command_id,),
        ).fetchone()
        if archived:
            with self.db:
                self.db.execute("INSERT INTO events(body) VALUES (?)", (archived[0],))
                self._trim_events()
            return

        # A previously delivered event still in the table is the most reliable
        # source after the outbox.
        delivered = self.db.execute(
            "SELECT body FROM events WHERE json_extract(body, '$.payload.commandId')=? LIMIT 1",
            (command_id,),
        ).fetchone()
        if delivered:
            with self.db:
                self.db.execute("INSERT INTO events(body) VALUES (?)", (delivered[0],))
                self._trim_events()
            return

        prior = self.command_record(command_id)
        if not prior or not is_complete_command_result(prior["result"]):
            return
        result = prior["result"]

        # Only executed results carry the appliedAtUtc timestamp that proves
        # when the effect happened. Error results without an original timestamp
        # are not backfilled as historical events.
        if result["status"] != "executed":
            return
        occurred_at = result["snapshot"].get("appliedAtUtc")
        if not isinstance(occurred_at, str):
            return

        envelope = {
            "schemaVersion": 1,
            "type": "CommandResult",
            "eventId": result["eventId"],
            "occurredAtUtc": occurred_at,
            "correlationId": result["correlationId"],
            "payload": result,
        }
        with self.db:
            self.db.execute("INSERT INTO events(body) VALUES (?)", (json.dumps(envelope),))
            self.db.execute("INSERT INTO outbox(command_id, body) VALUES (?,?)",
                            (command_id, json.dumps(envelope)))
            self._trim_events()

    # ------------------------------------------------------------------
    # Recovery
    # ------------------------------------------------------------------

    def _recover_executing(self):
        """After a crash, any 'executing' command becomes unknown.

        The durable command row, the unknown CommandResult event and the outbox
        archive are persisted atomically so the outcome is never lost. The
        correlationId is reused from the stored command row when it is a valid
        UUID; otherwise a single UUID is generated once and persisted
        consistently across the record, result payload and envelope.
        """
        from uuid import uuid4
        from ..models import command_result, event

        rows = self.db.execute(
            "SELECT id, body, correlation_id FROM commands WHERE status='executing'"
        ).fetchall()
        for command_id, body_json, stored_corr in rows:
            command = json.loads(body_json)
            body_corr = command.get("correlationId") if isinstance(command, dict) else None
            if is_uuid(stored_corr):
                correlation_id = stored_corr
            elif is_uuid(body_corr):
                correlation_id = body_corr
            else:
                correlation_id = str(uuid4())
            result_payload = command_result(
                command_id=command_id,
                status="unknown",
                command_type=command.get("type", "SetLight"),
                reason_code="unknown",
                reason="reinicio durante ejecucion",
                correlation_id=correlation_id,
            )
            envelope = event("CommandResult", result_payload, correlation_id=correlation_id)
            # Reuse the generated eventId for the result payload so they match.
            result_payload["eventId"] = envelope["eventId"]
            with self.db:
                self.db.execute(
                    "UPDATE commands SET status=?, result=?, event_id=?, correlation_id=? WHERE id=?",
                    ("unknown", json.dumps(result_payload), result_payload["eventId"],
                     correlation_id, command_id),
                )
                self.db.execute("INSERT INTO events(body) VALUES (?)", (json.dumps(envelope),))
                self.db.execute("INSERT INTO outbox(command_id, body) VALUES (?,?)",
                                (command_id, json.dumps(envelope)))
                self._trim_events()

    def _migrate_commands_columns(self):
        """Add columns introduced after the first C11 delivery without dropping
        existing data. Idempotent: safe to call on every open.
        """
        columns = {row[1] for row in self.db.execute("PRAGMA table_info(commands)")}
        with self.db:
            if "event_id" not in columns:
                self.db.execute("ALTER TABLE commands ADD COLUMN event_id TEXT")
            if "correlation_id" not in columns:
                self.db.execute("ALTER TABLE commands ADD COLUMN correlation_id TEXT")

    def close(self):
        self.db.close()
