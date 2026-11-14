import json
import math


def apply_configuration(store, patch, settings):
    """Twin configuration cannot open doors or change security mode."""
    version = patch.get("$version")
    previous = store.get("configuration", {"appliedVersion": 0, "observedVersion": 0,
                         "values": {"lightOnBelow": settings.light_on_below,
                                    "lightOffAbove": settings.light_off_above,
                                    "reportSeconds": settings.report_seconds}})
    if type(version) is not int or version <= previous["observedVersion"]:
        return previous
    result = dict(previous, observedVersion=version)
    try:
        values = patch.get("agentConfiguration")
        allowed = {"lightOnBelow", "lightOffAbove", "reportSeconds", "lightingMode"}
        if not isinstance(values, dict) or set(values) - allowed:
            raise ValueError("Propiedades de configuracion no admitidas")
        merged = dict(previous["values"], **values)
        for key in ("lightOnBelow", "lightOffAbove", "reportSeconds"):
            value = merged[key]
            if type(value) not in (int, float) or not math.isfinite(value):
                raise ValueError("Configuracion numerica invalida")
        if not 0 <= merged["lightOnBelow"] < merged["lightOffAbove"] <= 100:
            raise ValueError("Umbrales invalidos")
        if not settings.sample_seconds <= merged["reportSeconds"] <= 3600:
            raise ValueError("Intervalo invalido")
        if "lightingMode" in merged and merged["lightingMode"] not in ("auto", "manual"):
            raise ValueError("Modo de luz invalido")
        result.update(appliedVersion=version, values=merged, status="applied", reason=None)
        with store.db:
            store.db.execute("INSERT OR REPLACE INTO state VALUES (?,?)", ("configuration", json.dumps(result)))
            if "lightingMode" in values:
                store.db.execute("INSERT OR REPLACE INTO state VALUES (?,?)", ("lighting_mode", json.dumps(values["lightingMode"])))
    except ValueError as exc:
        result.update(status="rejected", reason=str(exc))
        store.set("configuration", result)
    return result
