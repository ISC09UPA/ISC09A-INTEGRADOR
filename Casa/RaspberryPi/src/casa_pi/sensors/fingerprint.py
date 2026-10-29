from uuid import uuid4
from datetime import timedelta
from ..models import event, utcnow


class Fingerprint:
    def __init__(self):
        self.previous = None

    def update(self, value):
        if value is not None and (not isinstance(value, str) or not 1 <= len(value) <= 128):
            raise ValueError("Identificador de huella invalido")
        changed = value is not None and value != self.previous
        self.previous = value
        if changed:
            return event("AccessRequested", {"attemptId": str(uuid4()), "method": "fingerprint",
                         "templateId": value, "expiresAtUtc": (utcnow() + timedelta(seconds=8)).isoformat()})
