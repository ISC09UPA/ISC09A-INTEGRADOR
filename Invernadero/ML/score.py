#!/usr/bin/env python3
"""
Script de scoring para el endpoint en línea de Azure Machine Learning (ml-invernadero).

Azure llama a `init()` una vez al arrancar el contenedor y a `run(datos)` en cada petición.
El contrato de entrada tiene dos formas; la API de Telemetría usa la primera:

1. "historial" — se le manda lo que ya está en la base de datos y el endpoint calcula las
   variables con las mismas funciones del entrenamiento:

   {
     "modo": "historial",
     "lecturas":   [{"tipo":"humedad_suelo","valor":41.2,"fechaHora":"2026-11-16T22:00:00Z"}, ...],
     "riegos":     [{"fechaHoraInicio":"2026-11-16T07:13:00Z","volumenMl":55}, ...],
     "mediciones": [{"fecha":"2026-11-16","altura_cm":39.5,"hojas":49}, ...]
   }

   Conviene mandar al menos 8 días de lecturas: las variables usan ventanas de 24 h y de 7 días.

2. "variables" — para pruebas rápidas: los valores ya calculados, uno por modelo.

   {"modo": "variables", "riego": {"humedad_suelo": 41.2, ...}, "crecimiento": {...}}

Respuesta: el mismo JSON que devuelve predecir.py (riego, crecimiento y riego_adaptativo).
"""

from __future__ import annotations

import json
import logging
import os
from pathlib import Path

import joblib
import numpy as np
import pandas as pd

_paquete: dict | None = None
_registro = logging.getLogger("invernadero.ml")


def _ruta_modelo() -> Path:
    """Encuentra el .joblib: en Azure viene bajo AZUREML_MODEL_DIR; en local, junto al script."""
    carpetas = [Path(os.environ["AZUREML_MODEL_DIR"])] if "AZUREML_MODEL_DIR" in os.environ else []
    carpetas += [Path(__file__).parent / "modelos", Path(__file__).parent]

    for carpeta in carpetas:
        if not carpeta.exists():
            continue
        encontrados = sorted(carpeta.rglob("modelo_invernadero.joblib"))
        if encontrados:
            return encontrados[0]

    raise FileNotFoundError(
        f"No se encontro modelo_invernadero.joblib en {[str(c) for c in carpetas]}")


def init() -> None:
    global _paquete
    ruta = _ruta_modelo()
    _paquete = joblib.load(ruta)
    _registro.info("Modelo %s cargado de %s", _paquete.get("version"), ruta)


def _predecir_fila(nombre: str, fila: pd.Series) -> float:
    assert _paquete is not None
    columnas = _paquete["variables"][nombre]
    faltantes = [c for c in columnas if c not in fila.index or pd.isna(fila[c])]
    if faltantes:
        raise ValueError(f"Al modelo '{nombre}' le faltan variables: {faltantes}")

    valor = float(_paquete["modelos"][nombre].predict(
        np.array([fila[columnas].to_numpy()], dtype=float))[0])

    normalizador = _paquete.get("normalizadores", {}).get(nombre)
    if normalizador:
        valor *= float(fila[normalizador])
    return valor


def _desde_historial(cuerpo: dict) -> tuple[pd.Series, pd.Series]:
    """Calcula las variables a partir del historial crudo, igual que en el entrenamiento."""
    # Se importa aquí para que un error de dependencias salga en run() y no tumbe el contenedor
    from entrenar import variables_crecimiento, variables_riego

    lecturas = pd.DataFrame(cuerpo.get("lecturas") or [])
    riegos = pd.DataFrame(cuerpo.get("riegos") or [])
    mediciones = pd.DataFrame(cuerpo.get("mediciones") or [])
    if lecturas.empty:
        raise ValueError("Se requiere el arreglo 'lecturas'.")

    lecturas["fechaHora"] = pd.to_datetime(lecturas["fechaHora"], utc=True, format="mixed")
    ancho = lecturas.pivot_table(index="fechaHora", columns="tipo", values="valor",
                                 aggfunc="mean").sort_index()
    ancho = ancho.resample("1h").mean().interpolate(limit=6, limit_direction="both")

    if riegos.empty:
        riegos = pd.DataFrame({"fechaHoraInicio": pd.Series(dtype="datetime64[ns, UTC]"),
                               "volumenMl": pd.Series(dtype=float)})
    else:
        riegos["fechaHoraInicio"] = pd.to_datetime(riegos["fechaHoraInicio"], utc=True,
                                                   format="mixed")
        riegos = riegos.sort_values("fechaHoraInicio").reset_index(drop=True)
    diferencia = riegos["fechaHoraInicio"].diff().dt.total_seconds() / 3600.0
    riegos["inicia_sesion"] = diferencia.isna() | (diferencia > 1.0)
    riegos["volumenMl"] = pd.to_numeric(riegos.get("volumenMl"), errors="coerce")

    tabla_riego = variables_riego(ancho, riegos)
    fila_riego = tabla_riego[_paquete["variables"]["riego_horas"]].dropna().iloc[-1]

    if mediciones.empty:
        raise ValueError("Se requiere el arreglo 'mediciones' (altura_cm y hojas) para el "
                         "modelo de crecimiento.")
    mediciones["fecha"] = pd.to_datetime(mediciones["fecha"], utc=True,
                                         format="mixed").dt.normalize()
    mediciones = mediciones.sort_values("fecha").set_index("fecha")[["altura_cm", "hojas"]]

    tabla_crecimiento = variables_crecimiento(ancho, riegos, mediciones.astype(float))
    columnas = _paquete["variables"]["crecimiento_altura"] + ["potencial_logistico"]
    fila_crecimiento = tabla_crecimiento[list(dict.fromkeys(columnas))].dropna().iloc[-1]
    return fila_riego, fila_crecimiento


def run(datos: str | dict) -> dict:
    if _paquete is None:
        init()

    try:
        cuerpo = json.loads(datos) if isinstance(datos, str) else datos
        modo = cuerpo.get("modo", "historial")

        if modo == "variables":
            fila_riego = pd.Series(cuerpo["riego"], name=pd.Timestamp.now(tz="UTC"))
            fila_crecimiento = pd.Series(cuerpo["crecimiento"])
            if "potencial_logistico" not in fila_crecimiento:
                altura = float(fila_crecimiento["altura_cm"])
                maxima = float(_paquete.get("altura_maxima_cm", 45.0))
                fila_crecimiento["potencial_logistico"] = altura * (1.0 - altura / maxima)
        elif modo == "historial":
            fila_riego, fila_crecimiento = _desde_historial(cuerpo)
        else:
            return {"error": f"modo desconocido: '{modo}'. Use 'historial' o 'variables'."}

        horas = max(0.0, _predecir_fila("riego_horas", fila_riego))
        agua = max(0.0, _predecir_fila("riego_agua_24h", fila_riego))
        crecimiento = max(0.0, _predecir_fila("crecimiento_altura", fila_crecimiento))
        hojas = max(0.0, _predecir_fila("crecimiento_hojas", fila_crecimiento))
        altura = float(fila_crecimiento["altura_cm"])

        momento = pd.Timestamp(fila_riego.name) + pd.Timedelta(hours=horas)

        return {
            "modeloVersion": _paquete.get("version"),
            "modeloEntrenado": _paquete.get("entrenado"),
            "momentoDatos": pd.Timestamp(fila_riego.name).strftime("%Y-%m-%dT%H:%M:%SZ"),
            "horasHastaProximoRiego": round(horas, 1),
            "momentoProximoRiego": momento.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "aguaEstimada24hMl": round(agua, 0),
            "horizonteDias": _paquete.get("horizonte_crecimiento_dias", 7),
            "crecimientoEstimadoCm": round(crecimiento, 2),
            "alturaEstimadaCm": round(altura + crecimiento, 1),
            "hojasNuevasEstimadas": round(hojas, 1),
            "umbralSugeridoPct": _umbral(fila_crecimiento),
        }
    except Exception as error:  # el endpoint nunca debe tumbarse por una petición mala
        _registro.exception("Fallo al predecir")
        return {"error": f"{type(error).__name__}: {error}"}


def _umbral(fila_crecimiento: pd.Series) -> float | None:
    """Umbral de humedad con el que el modelo predice más crecimiento (riego adaptativo)."""
    try:
        from predecir import umbral_sugerido
        assert _paquete is not None
        return umbral_sugerido(_paquete, fila_crecimiento)["umbral_sugerido_pct"]
    except Exception:  # si falla, el resto de la predicción sigue sirviendo
        _registro.warning("No se pudo calcular el umbral sugerido", exc_info=True)
        return None


if __name__ == "__main__":
    # Prueba local del contrato del endpoint, sin Azure:
    #   python3 score.py
    import csv
    from collections import defaultdict

    init()
    carpeta = Path(__file__).parent / "datos"

    lecturas = []
    with (carpeta / "lecturas.csv").open(encoding="utf-8") as archivo:
        for fila in csv.DictReader(archivo):
            lecturas.append({"tipo": fila["tipo"], "valor": float(fila["valor"]),
                             "fechaHora": fila["fechaHora"]})
    por_tipo: dict[str, int] = defaultdict(int)
    for lectura in lecturas:
        por_tipo[lectura["tipo"]] += 1
    lecturas = lecturas[-7 * 24 * 10:]  # últimos ~10 días

    with (carpeta / "riegos.csv").open(encoding="utf-8") as archivo:
        riegos = [{"fechaHoraInicio": f["fechaHoraInicio"], "volumenMl": float(f["volumenMl"])}
                  for f in csv.DictReader(archivo)]
    with (carpeta / "mediciones.csv").open(encoding="utf-8") as archivo:
        mediciones = [{"fecha": f["fecha"], "altura_cm": float(f["altura_cm"]),
                       "hojas": float(f["hojas"])} for f in csv.DictReader(archivo)]

    peticion = {"modo": "historial", "lecturas": lecturas, "riegos": riegos,
                "mediciones": mediciones}
    print(json.dumps(run(json.dumps(peticion)), indent=2, ensure_ascii=False))
