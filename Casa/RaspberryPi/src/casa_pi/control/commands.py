import json
from ..models import event, parse_time, utcnow


class Commands:
    def __init__(self, store, hardware, access, settings):
        self.store, self.hardware, self.access, self.settings = store, hardware, access, settings

    def apply(self, command, trusted=False):
        cid = command.get("commandId")
        if not isinstance(cid, str) or not 1 <= len(cid) <= 128:
            return {"status": "rejected", "reason": "commandId invalido"}
        prior = self.store.result(cid)
        if prior:
            if prior["command"] != command:
                return {"commandId": cid, "status": "rejected", "reason": "ID reutilizado con otro contenido"}
            return prior["result"] or {"commandId": cid, "status": prior["status"]}
        kind, payload = command.get("type"), command.get("payload", {})
        try:
            if not trusted:
                raise ValueError("Canal no autenticado")
            if not isinstance(payload, dict) or command.get("deviceId") != self.settings.device_id:
                raise ValueError("Destino o payload invalido")
            issued, expires = parse_time(command.get("issuedAtUtc")), parse_time(command.get("expiresAtUtc"))
            if not issued <= utcnow() < expires or not 0 < (expires-issued).total_seconds() <= 30:
                raise ValueError("Orden vencida o ventana invalida")
            if kind == "OpenDoor":
                self.access.validate(command)
            elif kind == "SetLight":
                if type(payload.get("on")) is not bool:
                    raise ValueError("on debe ser booleano")
            elif kind == "SetSecurityMode":
                if payload.get("mode") not in ("armed", "disarmed"):
                    raise ValueError("Modo de seguridad invalido")
            elif kind == "SetLightingMode":
                if payload.get("mode") not in ("auto", "manual"):
                    raise ValueError("Modo de luz invalido")
            else:
                raise ValueError("Orden no implementada: " + str(kind))
        except (ValueError, TypeError, KeyError) as exc:
            return {"commandId": cid, "status": "rejected", "reason": str(exc)}
        if not self.store.claim(command):
            return {"commandId": cid, "status": "unknown"}
        try:
            if kind == "OpenDoor":
                self.access.open(command)
            elif kind == "SetLight":
                self.store.set("lighting_mode", "manual")
                self.hardware.light(payload["on"])
                self.store.set("light_on", payload["on"])
            elif kind == "SetSecurityMode":
                self.store.set("armed", payload["mode"] == "armed")
            elif kind == "SetLightingMode":
                self.store.set("lighting_mode", payload["mode"])
            result = {"commandId": cid, "status": "executed", "type": kind}
        except Exception:
            # A partial actuator effect cannot safely be retried.
            result = {"commandId": cid, "status": "unknown", "reason": "Fallo durante ejecucion"}
        self.store.finish(cid, result)
        self.store.emit(event("CommandResult", result))
        return result
