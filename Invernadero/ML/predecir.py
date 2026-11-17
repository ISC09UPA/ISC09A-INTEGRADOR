#!/usr/bin/env python3
"""
Usa los modelos entrenados para responder las tres preguntas del proyecto:

  1. ¿Cuánto va a crecer la planta en los próximos 7 días?
  2. ¿Cuándo toca el próximo riego y cuánta agua se va a usar en 24 h?
  3. ¿Qué umbral de humedad conviene cargarle a la Raspberry? (riego adaptativo)

Las variables de entrada se calculan con las MISMAS funciones del entrenamiento
(`entrenar.variables_riego` y `entrenar.variables_crecimiento`), para que no haya forma de que
el entrenamiento y la predicción se desincronicen.

Uso:
    python3 predecir.py --modelo modelos/modelo_invernadero.joblib --datos datos/
    python3 predecir.py --datos datos/ --json        # solo el JSON, para consumirlo desde la API
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import joblib
import numpy as np
import pandas as pd

from entrenar import (HUMEDAD_MINIMA, cargar_lecturas, cargar_mediciones, cargar_riegos,
                      variables_crecimiento, variables_riego)

# Límites del umbral que se le puede sugerir a la Raspberry. El mínimo evita dejar la planta
# seca y el máximo deja margen contra HUMEDAD_OBJETIVO (60 %) para que la bomba no se quede
# encendida de más.
UMBRAL_MINIMO = 30.0
UMBRAL_MAXIMO = 50.0


def _ultima_fila(tabla: pd.DataFrame, columnas: list[str]) -> pd.Series:
    """Última fila que tiene todas las variables que el modelo necesita."""
    completas = tabla[columnas].dropna()
    if completas.empty:
        raise ValueError("No hay ninguna fila con todas las variables; faltan datos de sensores.")
    return completas.iloc[-1]


def _predecir(paquete: dict, nombre: str, fila: pd.Series) -> float:
    """Aplica un modelo y deshace la normalización si el objetivo estaba normalizado."""
    modelo = paquete["modelos"][nombre]
    columnas = paquete["variables"][nombre]
    valor = float(modelo.predict(np.array([fila[columnas].to_numpy()], dtype=float))[0])

    normalizador = paquete.get("normalizadores", {}).get(nombre)
    if normalizador:
        valor *= float(fila[normalizador])
    return valor


def umbral_sugerido(paquete: dict, fila_crecimiento: pd.Series) -> dict:
    """
    Riego adaptativo: busca el umbral de humedad con el que el modelo predice más crecimiento.

    Para cada umbral candidato se estima cómo quedaría la última semana si la Raspberry hubiera
    usado ese umbral: la humedad media del suelo oscilaría entre el umbral y HUMEDAD_OBJETIVO
    (60 %), y no habría horas de estrés porque el riego entraría antes de cruzarlo. Con esas dos
    variables modificadas se le pregunta al modelo de crecimiento cuánto crecería la planta.

    Es una búsqueda sobre el modelo, no una regla fija: cuando se reentrene con datos reales, el
    umbral sugerido cambia solo. Si el crecimiento previsto apenas se mueve entre candidatos
    (menos de 0.1 cm), se deja el umbral actual para no mover la maqueta sin motivo.
    """
    candidatos = np.arange(UMBRAL_MINIMO, UMBRAL_MAXIMO + 0.1, 1.0)
    objetivo_humedad = 60.0  # HUMEDAD_OBJETIVO de config.py
    evaluados = []

    for umbral in candidatos:
        hipotetica = fila_crecimiento.copy()
        hipotetica["humedad_suelo_media_7d"] = (umbral + objetivo_humedad) / 2.0
        hipotetica["horas_estres_7d"] = 0.0
        evaluados.append((float(umbral), _predecir(paquete, "crecimiento_altura", hipotetica)))

    mejor_umbral, mejor_crecimiento = max(evaluados, key=lambda par: par[1])
    peor_crecimiento = min(valor for _, valor in evaluados)

    if mejor_crecimiento - peor_crecimiento < 0.1:
        return {
            "umbral_sugerido_pct": HUMEDAD_MINIMA,
            "motivo": "el crecimiento previsto no cambia con el umbral; se deja el actual",
            "crecimiento_previsto_cm": round(mejor_crecimiento, 2),
            "explorados": evaluados,
        }

    return {
        "umbral_sugerido_pct": round(mejor_umbral, 1),
        "umbral_actual_pct": HUMEDAD_MINIMA,
        "motivo": "umbral con el que el modelo predice mayor crecimiento a 7 dias",
        "crecimiento_previsto_cm": round(mejor_crecimiento, 2),
        "ganancia_vs_peor_umbral_cm": round(mejor_crecimiento - peor_crecimiento, 2),
        "explorados": evaluados,
    }


def predecir_todo(paquete: dict, carpeta: Path) -> dict:
    lecturas = cargar_lecturas(carpeta / "lecturas.csv")
    riegos = cargar_riegos(carpeta / "riegos.csv")
    mediciones = cargar_mediciones(carpeta / "mediciones.csv")

    tabla_riego = variables_riego(lecturas, riegos)
    tabla_crecimiento = variables_crecimiento(lecturas, riegos, mediciones)

    fila_riego = _ultima_fila(tabla_riego, paquete["variables"]["riego_horas"])
    fila_crecimiento = _ultima_fila(tabla_crecimiento, paquete["variables"]["crecimiento_altura"])

    horas = max(0.0, _predecir(paquete, "riego_horas", fila_riego))
    agua = max(0.0, _predecir(paquete, "riego_agua_24h", fila_riego))
    crecimiento = max(0.0, _predecir(paquete, "crecimiento_altura", fila_crecimiento))
    hojas = max(0.0, _predecir(paquete, "crecimiento_hojas", fila_crecimiento))

    momento_riego = fila_riego.name + pd.Timedelta(hours=horas)

    return {
        "modelo_version": paquete.get("version"),
        "modelo_entrenado": paquete.get("entrenado"),
        "momento_datos": str(fila_riego.name),
        "estado_actual": {
            "humedad_suelo_pct": round(float(fila_riego["humedad_suelo"]), 1),
            "temperatura_c": round(float(fila_riego["temperatura"]), 1),
            "luz_lux": round(float(fila_riego["luz"]), 0),
            "nivel_deposito_pct": round(float(fila_riego["nivel_deposito"]), 1),
            "altura_cm": round(float(fila_crecimiento["altura_cm"]), 1),
            "hojas": int(fila_crecimiento["hojas"]),
        },
        "riego": {
            "horas_hasta_proximo_riego": round(horas, 1),
            "momento_estimado": momento_riego.strftime("%Y-%m-%dT%H:%M:%SZ"),
            "agua_estimada_24h_ml": round(agua, 0),
        },
        "crecimiento": {
            "horizonte_dias": paquete.get("horizonte_crecimiento_dias", 7),
            "crecimiento_estimado_cm": round(crecimiento, 2),
            "altura_estimada_cm": round(float(fila_crecimiento["altura_cm"]) + crecimiento, 1),
            "hojas_nuevas_estimadas": round(hojas, 1),
        },
        "riego_adaptativo": umbral_sugerido(paquete, fila_crecimiento),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--modelo", default="modelos/modelo_invernadero.joblib")
    parser.add_argument("--datos", default="datos", help="carpeta con lecturas/riegos/mediciones")
    parser.add_argument("--json", action="store_true", help="imprime solo el JSON")
    args = parser.parse_args()

    paquete = joblib.load(args.modelo)
    resultado = predecir_todo(paquete, Path(args.datos))

    if args.json:
        print(json.dumps(resultado, indent=2, ensure_ascii=False))
        return

    estado = resultado["estado_actual"]
    riego = resultado["riego"]
    crecimiento = resultado["crecimiento"]
    adaptativo = resultado["riego_adaptativo"]

    print(f"Datos al {resultado['momento_datos']}  (modelo {resultado['modelo_version']})\n")
    print(f"  Humedad de suelo   {estado['humedad_suelo_pct']} %")
    print(f"  Temperatura        {estado['temperatura_c']} °C")
    print(f"  Deposito           {estado['nivel_deposito_pct']} %")
    print(f"  Planta             {estado['altura_cm']} cm, {estado['hojas']} hojas\n")
    print(f"  Proximo riego en   {riego['horas_hasta_proximo_riego']} h "
          f"({riego['momento_estimado']})")
    print(f"  Agua proximas 24 h {riego['agua_estimada_24h_ml']:.0f} mL")
    print(f"  Crecera            {crecimiento['crecimiento_estimado_cm']} cm en "
          f"{crecimiento['horizonte_dias']} dias "
          f"(hasta {crecimiento['altura_estimada_cm']} cm)")
    print(f"  Hojas nuevas       {crecimiento['hojas_nuevas_estimadas']}")
    print(f"  Umbral sugerido    {adaptativo['umbral_sugerido_pct']} % "
          f"({adaptativo['motivo']})")


if __name__ == "__main__":
    main()
