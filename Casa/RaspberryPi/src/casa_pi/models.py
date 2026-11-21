from datetime import datetime, timezone
from uuid import UUID, uuid4


# Clock is monkey-patchable for deterministic tests.
_utcnow = lambda: datetime.now(timezone.utc)


def utcnow():
    return _utcnow()


def set_utcnow(fn):
    global _utcnow
    _utcnow = fn


def is_uuid(value):
    """Return True if value is a canonical UUIDv4 (or any UUID) string."""
    if not isinstance(value, str):
        return False
    try:
        UUID(value, version=4)
        return True
    except ValueError:
        return False


def new_correlation_id():
    return str(uuid4())


def timestamp():
    return utcnow().isoformat().replace("+00:00", "Z")


def parse_time(value):
    if not isinstance(value, str):
        raise ValueError("Fecha requerida")
    dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if dt.tzinfo is None or dt.utcoffset().total_seconds() != 0:
        raise ValueError("La fecha debe tener zona UTC")
    return dt


def event(kind, payload, correlation_id=None):
    """Build a Telemetry envelope. correlationId ties a command to its result."""
    return {
        "schemaVersion": 1,
        "type": kind,
        "eventId": str(uuid4()),
        "occurredAtUtc": timestamp(),
        "correlationId": correlation_id if correlation_id is not None else str(uuid4()),
        "payload": payload,
    }


def is_valid_reason_code(value):
    return value in ("invalidPayload", "unauthorized", "forbidden", "duplicateChanged",
                     "sequenceRejected", "deviceRejected", "expired", "unknown")


def is_complete_command_result(result):
    """Return True if result is a complete, schema-conformant CommandResult.

    Incomplete legacy rows are rejected so they are never backfilled as a
    false v1 confirmation.
    """
    if not isinstance(result, dict):
        return False
    status = result.get("status")
    if status not in ("executed", "rejected", "expired", "unknown"):
        return False
    if not isinstance(result.get("commandId"), str):
        return False
    if not isinstance(result.get("type"), str):
        return False
    if not is_uuid(result.get("eventId")) or not is_uuid(result.get("correlationId")):
        return False
    if status == "executed":
        snap = result.get("snapshot")
        if not isinstance(snap, dict):
            return False
        if type(snap.get("lightOn")) is not bool:
            return False
        if snap.get("lightingMode") not in ("auto", "manual"):
            return False
        if not isinstance(snap.get("source"), str):
            return False
        if not isinstance(snap.get("appliedAtUtc"), str):
            return False
        if type(result.get("stateRevision")) is not int:
            return False
        if result.get("reasonCode") is not None:
            return False
    else:
        if result.get("snapshot") is not None or result.get("stateRevision") is not None:
            return False
        if not is_valid_reason_code(result.get("reasonCode")):
            return False
    return True


def light_snapshot(light_on, lighting_mode, source, applied_at_utc=None):
    """Capability snapshot required for executed light commands."""
    if source not in ("simulated", "device"):
        raise ValueError("source must be simulated or device")
    return {
        "lightOn": bool(light_on),
        "lightingMode": lighting_mode if lighting_mode in ("auto", "manual") else "auto",
        "source": source,
        "appliedAtUtc": applied_at_utc if applied_at_utc is not None else timestamp(),
    }


def command_result(
    command_id,
    status,
    command_type,
    reason_code=None,
    reason=None,
    snapshot=None,
    state_revision=None,
    event_id=None,
    correlation_id=None,
):
    if correlation_id is None:
        correlation_id = str(uuid4())
    """Build a CommandResult payload conforming to command-result.json.

    Terminal error states require a non-null reasonCode and null
    snapshot/stateRevision. Executed requires a complete snapshot and a
    non-null integer stateRevision.
    """
    if status not in ("pending", "executed", "rejected", "expired", "unknown", "failed"):
        raise ValueError(f"status invalido: {status}")

    if status == "executed":
        if snapshot is None or state_revision is None:
            raise ValueError("executed requiere snapshot y stateRevision")
        if reason_code is not None:
            raise ValueError("executed requiere reasonCode null")
    elif status == "pending":
        if snapshot is not None or state_revision is not None or reason_code is not None:
            raise ValueError("pending requiere snapshot/stateRevision/reasonCode null")
    else:
        if reason_code is None:
            raise ValueError(f"{status} requiere reasonCode no nulo")
        if snapshot is not None or state_revision is not None:
            raise ValueError(f"{status} requiere snapshot/stateRevision null")

    payload = {
        "commandId": command_id,
        "status": status,
        "type": command_type,
        "reasonCode": reason_code,
        "reason": reason,
        "snapshot": snapshot,
        "stateRevision": state_revision,
        "eventId": event_id if event_id is not None else str(uuid4()),
        "correlationId": correlation_id if correlation_id is not None else command_id,
    }
    return payload
