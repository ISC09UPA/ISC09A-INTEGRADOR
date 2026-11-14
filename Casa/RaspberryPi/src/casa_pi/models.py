from datetime import datetime, timezone
from uuid import uuid4


def utcnow():
    return datetime.now(timezone.utc)


def timestamp():
    return utcnow().isoformat().replace("+00:00", "Z")


def parse_time(value):
    if not isinstance(value, str):
        raise ValueError("Fecha requerida")
    dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if dt.tzinfo is None or dt.utcoffset().total_seconds() != 0:
        raise ValueError("La fecha debe tener zona UTC")
    return dt


def event(kind, payload):
    return {"schemaVersion": 1, "type": kind, "eventId": str(uuid4()),
            "occurredAtUtc": timestamp(), "payload": payload}
