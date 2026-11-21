import json

from ..models import (
    command_result, event, is_complete_command_result, is_uuid,
    light_snapshot, new_correlation_id, parse_time, timestamp, utcnow,
)


class Commands:
    def __init__(self, store, hardware, access, settings):
        self.store, self.hardware, self.access, self.settings = store, hardware, access, settings

    @staticmethod
    def _canonical(command):
        """Type-preserving canonical JSON for deterministic comparison.

        Key order is irrelevant, but `true`, `1` and `1.0` are distinct.
        """
        return json.dumps(command, sort_keys=True, allow_nan=False)

    @staticmethod
    def _commands_equal(stored, incoming):
        """Compare a stored command body with an incoming one.

        Legacy rows may contain an internal `correlationId` field that was
        injected before C11; it is not part of the wire schema and is ignored
        for equality. The incoming command must already be wire-shape valid.
        """
        if not isinstance(stored, dict) or not isinstance(incoming, dict):
            return False
        cleaned = {k: v for k, v in stored.items() if k != "correlationId"}
        return Commands._canonical(cleaned) == Commands._canonical(incoming)

    @staticmethod
    def _validate_wire_shape(command):
        """Validate the incoming wire shape without mutating the command.

        Returns a rejection reason or None if the command is well-formed.
        This is a first-flow guard: it runs before duplicate detection so that
        a malformed replay cannot be mistaken for the original command.
        """
        if not isinstance(command, dict):
            return "Orden debe ser objeto"
        allowed_top = {"commandId", "deviceId", "type", "commandSequence",
                       "attemptId", "issuedAtUtc", "expiresAtUtc", "payload"}
        extra = set(command.keys()) - allowed_top
        if extra:
            return f"Propiedades no permitidas: {sorted(extra)}"
        cid = command.get("commandId")
        if not isinstance(cid, str) or not 1 <= len(cid) <= 128:
            return "commandId invalido"
        device_id = command.get("deviceId")
        if not isinstance(device_id, str) or not device_id:
            return "deviceId invalido"
        ctype = command.get("type")
        if ctype not in ("SetLight", "SetLightingMode", "SetSecurityMode", "OpenDoor", "CaptureImage"):
            return f"Tipo no implementado: {ctype}"
        seq = command.get("commandSequence")
        if type(seq) is not int or seq < 0:
            return "commandSequence debe ser un entero >= 0"
        for key in ("issuedAtUtc", "expiresAtUtc"):
            if not isinstance(command.get(key), str):
                return f"{key} debe ser cadena ISO"
        attempt = command.get("attemptId")
        if attempt is not None and not is_uuid(attempt):
            return "attemptId debe ser UUID o null"
        payload = command.get("payload")
        if not isinstance(payload, dict):
            return "Payload invalido"
        if ctype == "SetLight":
            if type(payload.get("on")) is not bool:
                return "on debe ser booleano"
            if set(payload.keys()) != {"on"}:
                return "payload extra keys"
        elif ctype == "SetLightingMode":
            if payload.get("mode") not in ("auto", "manual"):
                return "Modo de luz invalido"
            if set(payload.keys()) != {"mode"}:
                return "payload extra keys"
        elif ctype == "SetSecurityMode":
            if payload.get("mode") not in ("armed", "disarmed"):
                return "Modo de seguridad invalido"
            if set(payload.keys()) != {"mode"}:
                return "payload extra keys"
        elif ctype == "OpenDoor":
            if not isinstance(command.get("attemptId"), str) or not is_uuid(command["attemptId"]):
                return "attemptId debe ser UUID"
            if not isinstance(payload.get("doorId"), str) or not 1 <= len(payload["doorId"]) <= 128:
                return "doorId invalido"
            duration = payload.get("durationMs")
            if type(duration) is not int or not 100 <= duration <= 3000:
                return "durationMs invalido"
            if set(payload.keys()) != {"doorId", "durationMs"}:
                return "payload extra keys"
        return None

    def _source(self):
        return "device" if self.settings.mode == "real" else "simulated"

    def _durable_correlation_id(self, command, admission_correlation_id):
        """Return a single correlationId to use for the durable outcome.

        Priority:
        1. correlation already stored for this commandId (column or legacy body),
        2. admission correlation passed by the network worker,
        3. a fresh UUID.
        The command body is never modified.
        """
        prior = self.store.command_record(command.get("commandId")) if isinstance(command.get("commandId"), str) else None
        if prior:
            if is_uuid(prior.get("correlation_id")):
                return prior["correlation_id"]
            stored_command = prior.get("command") or {}
            body_corr = stored_command.get("correlationId") if isinstance(stored_command, dict) else None
            if is_uuid(body_corr):
                return body_corr
        if is_uuid(admission_correlation_id):
            return admission_correlation_id
        return new_correlation_id()

    def _classify_expiry(self, command):
        """Return ('ok', None) or ('expired', reason) or ('rejected', reason)."""
        try:
            issued = parse_time(command.get("issuedAtUtc"))
            expires = parse_time(command.get("expiresAtUtc"))
        except (ValueError, TypeError):
            return "rejected", "Fechas invalidas"
        window = (expires - issued).total_seconds()
        if not 0 < window <= 30:
            return "rejected", "Ventana de vigencia invalida"
        now = utcnow()
        if issued > now:
            return "rejected", "Orden emitida en el futuro"
        if now >= expires:
            return "expired", "Orden expirada"
        return "ok", None

    def _reject_persistent(self, command, status, reason_code, reason, correlation_id, command_type="SetLight"):
        """Persist a terminal result and emit its CommandResult event."""
        command_id = command.get("commandId") if isinstance(command.get("commandId"), str) else "__invalid__"
        command_type = command.get("type") if isinstance(command.get("type"), str) else command_type
        result_payload = command_result(
            command_id=command_id,
            status=status,
            command_type=command_type,
            reason_code=reason_code,
            reason=reason,
            correlation_id=correlation_id,
        )
        envelope = event("CommandResult", result_payload, correlation_id=result_payload["correlationId"])
        result_payload["eventId"] = envelope["eventId"]
        self.store.atomic_outcome(command, result_payload, envelope)
        return result_payload

    def _reject_transient(self, command, reason_code, reason, correlation_id=None):
        """Return a rejected payload without overwriting any durable record."""
        command_id = command.get("commandId") if isinstance(command.get("commandId"), str) else "__invalid__"
        command_type = command.get("type") if isinstance(command.get("type"), str) else "SetLight"
        return command_result(
            command_id=command_id,
            status="rejected",
            command_type=command_type,
            reason_code=reason_code,
            reason=reason,
            correlation_id=correlation_id,
        )

    def reject_queue_full(self, command, admission_correlation_id=None):
        """Handle a command that the inbound queue rejected.

        Queue-full rejections are transient overload signals, not durable
        results. The main thread may look at durable state to replay an
        already-finished exact duplicate, but it never inserts a new terminal
        row for an overflow. Malformed or changed duplicates are rejected
        transiently.
        """
        cid = command.get("commandId") if isinstance(command, dict) else None
        if not isinstance(cid, str) or not 1 <= len(cid) <= 128:
            return self._reject_transient(command, "invalidPayload", "commandId invalido")
        correlation_id = self._durable_correlation_id(command, admission_correlation_id)
        shape_reason = self._validate_wire_shape(command)
        if shape_reason is not None:
            return self._reject_transient(command, "invalidPayload", shape_reason, correlation_id)
        prior = self.store.command_record(cid)
        if prior:
            if self._commands_equal(prior["command"], command):
                if prior["status"] == "executing":
                    return self._finalize_executing_as_unknown(command, prior)
                if is_complete_command_result(prior["result"]):
                    self.store.ensure_event_persisted(cid)
                    return prior["result"]
                return self._legacy_unknown_response(command, prior, admission_correlation_id)
            return self._reject_transient(command, "duplicateChanged", "ID reutilizado con otro contenido", correlation_id)
        return self._reject_transient(command, "deviceRejected", "Cola de ordenes llena", correlation_id)

    def _finalize_executing_as_unknown(self, command, prior):
        """Convert an 'executing' row into a durable 'unknown' outcome.

        This is used when a duplicate arrives after the original outcome failed
        to commit in-process, or after recovery. The physical effect is never
        retried. The durable identity and correlation of the original flow are
        preserved; delivery metadata of the duplicate is ignored.
        """
        cid = command["commandId"]
        # _durable_correlation_id prioritizes the stored/legacy correlation.
        corr = self._durable_correlation_id(command, None)
        command_type = command.get("type") if isinstance(command.get("type"), str) else "SetLight"
        result_payload = command_result(
            command_id=cid,
            status="unknown",
            command_type=command_type,
            reason_code="unknown",
            reason="Fallo al persistir resultado",
            correlation_id=corr,
        )
        envelope = event("CommandResult", result_payload, correlation_id=result_payload["correlationId"])
        result_payload["eventId"] = envelope["eventId"]
        try:
            self.store.atomic_outcome(command, result_payload, envelope)
        except Exception:
            # DB still not writable; caller gets an explicit unknown transient.
            return result_payload
        return result_payload

    def _legacy_unknown_response(self, command, prior, admission_correlation_id):
        """Return an explicit unknown transient for a legacy/incomplete row.

        The original history is preserved untouched; no v1 event is fabricated.
        """
        cid = command["commandId"]
        corr = self._durable_correlation_id(command, admission_correlation_id)
        command_type = command.get("type") if isinstance(command.get("type"), str) else "SetLight"
        return command_result(
            command_id=cid,
            status="unknown",
            command_type=command_type,
            reason_code="unknown",
            reason="Evidencia legacy insuficiente para confirmar resultado",
            correlation_id=corr,
        )

    def apply(self, command, trusted=False, admission_correlation_id=None):
        cid = command.get("commandId")
        if not isinstance(cid, str) or not 1 <= len(cid) <= 128:
            return {"status": "rejected", "reason": "commandId invalido", "reasonCode": "invalidPayload"}

        correlation_id = self._durable_correlation_id(command, admission_correlation_id)

        # First-flow wire-shape validation. A malformed command is rejected
        # transiently even if its commandId is already known, so it can never
        # replay or overwrite a valid original.
        shape_reason = self._validate_wire_shape(command)
        if shape_reason is not None:
            return self._reject_transient(command, "invalidPayload", shape_reason, correlation_id)

        command_type = command.get("type")

        # Duplicate detection before validation/expiry: exact replay returns the
        # same durable result (and therefore the same eventId/correlationId),
        # changed content is rejected without overwriting the original.
        prior = self.store.command_record(cid)
        if prior:
            if not self._commands_equal(prior["command"], command):
                return self._reject_transient(command, "duplicateChanged", "ID reutilizado con otro contenido", correlation_id)
            if prior["status"] == "executing":
                # The original outcome never committed; finalize it as unknown
                # without repeating the physical effect.
                return self._finalize_executing_as_unknown(command, prior)
            # Only replay a complete, schema-conformant result. Incomplete legacy
            # rows are preserved but never presented as a false v1 confirmation.
            if is_complete_command_result(prior["result"]):
                self.store.ensure_event_persisted(cid)
                return prior["result"]
            return self._legacy_unknown_response(command, prior, admission_correlation_id)

        # Expiry classification.
        expiry_status, expiry_reason = self._classify_expiry(command)
        if expiry_status == "expired":
            return self._reject_persistent(command, "expired", "expired", expiry_reason, correlation_id, command_type)
        if expiry_status == "rejected":
            return self._reject_persistent(command, "rejected", "invalidPayload", expiry_reason, correlation_id, command_type)

        # Validation: deviceId must match and payload must be valid.
        try:
            if not trusted:
                raise ValueError("Canal no autenticado")
            if command.get("deviceId") != self.settings.device_id:
                raise ValueError("Destino invalido")
            self._validate_payload(command_type, command.get("payload", {}), command)
        except (ValueError, TypeError, KeyError) as exc:
            return self._reject_persistent(command, "rejected", "invalidPayload", str(exc), correlation_id, command_type)

        # Re-check expiry immediately before claiming, after validation work.
        expiry_status, expiry_reason = self._classify_expiry(command)
        if expiry_status == "expired":
            return self._reject_persistent(command, "expired", "expired", expiry_reason, correlation_id, command_type)

        # commandSequence is required for lighting commands and must be a
        # non-negative integer. Booleans, floats and strings are rejected as
        # invalidPayload before any durable claim or physical effect.
        if command_type in ("SetLight", "SetLightingMode"):
            sequence = command.get("commandSequence")
            if type(sequence) is not int or sequence < 0:
                return self._reject_persistent(
                    command, "rejected", "invalidPayload",
                    "commandSequence debe ser un entero >= 0", correlation_id, command_type)
            if not self.store.record_command_sequence(sequence):
                return self._reject_persistent(
                    command, "rejected", "sequenceRejected",
                    "commandSequence no monotono", correlation_id, command_type)

        # Claim durable execution before physical effect, remembering the
        # admission correlationId so recovery can reuse it if the process
        # crashes before the outcome is committed.
        if not self.store.claim(command, correlation_id):
            prior = self.store.command_record(cid)
            if prior and self._commands_equal(prior["command"], command):
                if prior["status"] == "executing":
                    return self._finalize_executing_as_unknown(command, prior)
                if is_complete_command_result(prior["result"]):
                    return prior["result"]
                return self._legacy_unknown_response(command, prior, admission_correlation_id)
            return self._reject_transient(command, "duplicateChanged", "ID reutilizado con otro contenido", correlation_id)

        state_delta = {}
        try:
            state_delta = self._execute(command_type, command.get("payload", {}), command)
        except Exception as exc:
            # Any failure during/after the physical effect is unknown; the
            # actuator may be in an indeterminate state and must not be retried.
            result_payload = command_result(
                command_id=cid,
                status="unknown",
                command_type=command_type,
                reason_code="unknown",
                reason=f"Fallo durante ejecucion: {exc}",
                correlation_id=correlation_id,
            )
            snapshot = None
            committed_revision = None
        else:
            # Compute the snapshot from the intended new state. The effective
            # state revision is returned by atomic_outcome after comparing with
            # the current DB state.
            snapshot = light_snapshot(
                state_delta.get("light_on", self.store.get("light_on", False)),
                state_delta.get("lighting_mode", self.store.get("lighting_mode", "auto")),
                source=self._source(),
                applied_at_utc=timestamp(),
            )
            result_payload = command_result(
                command_id=cid,
                status="executed",
                command_type=command_type,
                snapshot=snapshot,
                state_revision=0,  # replaced by atomic_outcome below
                correlation_id=correlation_id,
            )
            committed_revision = None

        envelope = event("CommandResult", result_payload, correlation_id=result_payload["correlationId"])
        result_payload["eventId"] = envelope["eventId"]
        try:
            committed_revision = self.store.atomic_outcome(command, result_payload, envelope, state_delta)
            if result_payload["status"] == "executed" and committed_revision is not None:
                # atomic_outcome already mutated the payload and stored event;
                # this assignment just keeps the in-memory object consistent.
                result_payload["stateRevision"] = committed_revision
        except Exception as exc:
            # The physical effect may have happened but the durable outcome did
            # not commit. The command stays 'executing' and recovery will emit
            # 'unknown' without repeating the effect.
            return command_result(
                command_id=cid,
                status="unknown",
                command_type=command_type,
                reason_code="unknown",
                reason=f"Fallo al persistir resultado: {exc}",
                correlation_id=correlation_id,
            )
        return result_payload

    def _validate_payload(self, kind, payload, command):
        if kind == "SetLight":
            if type(payload.get("on")) is not bool:
                raise ValueError("on debe ser booleano")
        elif kind == "SetLightingMode":
            if payload.get("mode") not in ("auto", "manual"):
                raise ValueError("Modo de luz invalido")
        elif kind == "SetSecurityMode":
            if payload.get("mode") not in ("armed", "disarmed"):
                raise ValueError("Modo de seguridad invalido")
        elif kind == "OpenDoor":
            self.access.validate(command)
        elif kind == "CaptureImage":
            raise ValueError("CaptureImage no implementado")

    def _execute(self, kind, payload, command):
        """Execute the physical/simulated effect and return the state delta.

        This method must NOT commit any SQLite state; the caller applies the
        delta atomically together with the durable CommandResult. Raises on
        failure so the caller emits an 'unknown' result without retrying.
        """
        if kind == "OpenDoor":
            self.access.open(command)
            return {}
        if kind == "SetSecurityMode":
            # Security mode does not affect the per-light state revision.
            return {"armed": payload["mode"] == "armed"}

        # Lighting commands force manual authority and apply the real flag.
        delta = {"lighting_mode": "manual"}
        if kind == "SetLight":
            self.hardware.light(payload["on"])
            delta["light_on"] = payload["on"]
        elif kind == "SetLightingMode":
            # The command confirms the requested mode; the light value itself
            # is not changed by this command (authority now rests with commands).
            delta["lighting_mode"] = payload["mode"]
        return delta
