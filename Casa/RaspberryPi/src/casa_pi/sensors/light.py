import math


def reading(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or not 0 <= value <= 100:
        raise ValueError("Luz invalida: porcentaje entre 0 y 100")
    return float(value)
