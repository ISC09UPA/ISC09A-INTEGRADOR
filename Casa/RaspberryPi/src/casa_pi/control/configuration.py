import json
import logging
import math


def apply_configuration(store, patch, settings):
    """Twin configuration cannot open doors or change security mode.

    The legacy lightingMode field must be ignored with a diagnostic and must
    not overwrite the locally stored lighting_mode (which is only changed by
    SetLightingMode commands).
    """
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

        # Legacy lightingMode must be ignored, not applied.
        if "lightingMode" in values:
            logging.warning("Ignorando lightingMode heredado del twin; "
                            "el modo de luz solo cambia por comando SetLightingMode")

        # Build merged values excluding the legacy key.
        merged = dict(previous["values"])
        for key in ("lightOnBelow", "lightOffAbove", "reportSeconds"):
            if key in values:
                merged[key] = values[key]

        for key in ("lightOnBelow", "lightOffAbove", "reportSeconds"):
            value = merged[key]
            if type(value) not in (int, float) or not math.isfinite(value):
                raise ValueError("Configuracion numerica invalida")
        if not 0 <= merged["lightOnBelow"] < merged["lightOffAbove"] <= 100:
            raise ValueError("Umbrales invalidos")
        if not settings.sample_seconds <= merged["reportSeconds"] <= 3600:
            raise ValueError("Intervalo invalido")

        result.update(appliedVersion=version, values=merged, status="applied", reason=None)
        with store.db:
            store.db.execute("INSERT OR REPLACE INTO state VALUES (?,?)", ("configuration", json.dumps(result)))
    except ValueError as exc:
        result.update(status="rejected", reason=str(exc))
        store.set("configuration", result)
    return result
