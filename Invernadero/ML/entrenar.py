#!/usr/bin/env python3
"""
Entrena los modelos de Machine Learning del Invernadero Inteligente.

Son dos preguntas distintas, así que son dos modelos (regresión, scikit-learn):

  1. CRECIMIENTO  ¿cuánto va a crecer la planta en los próximos 7 días?
                  Objetivos: centímetros de altura y hojas nuevas.
                  Lo pide la rúbrica ("predice el crecimiento de la planta").

  2. RIEGO        ¿cuándo toca el próximo riego y cuánta agua se va a usar en 24 h?
                  Objetivos: horas hasta la próxima sesión de riego y mL en las próximas 24 h.
                  Sirve para pasar de un umbral fijo a riego adaptativo.

Entra el mismo formato que entrega la API, así que funciona igual con datos simulados o reales:

  lecturas.csv    GET /api/Lecturas/exportar.csv
  riegos.csv      GET /api/Riegos  (guardado como CSV)
  mediciones.csv  fecha,altura_cm,hojas  (medición manual diaria de la planta)

Decisiones de método, para que se puedan defender en la evaluación:
  * El objetivo del modelo de crecimiento es el INCREMENTO a 7 días, no la altura absoluta.
    Predecir la altura absoluta daría un R² altísimo y engañoso, porque la altura de hoy ya
    explica casi toda la altura de la semana que viene.
  * La separación entrenamiento/prueba es TEMPORAL (lo viejo entrena, lo nuevo prueba), nunca
    aleatoria: con una serie de tiempo, repartir al azar filtra información del futuro.
  * La validación cruzada es TimeSeriesSplit, por la misma razón.
  * Cada modelo se compara contra una referencia tonta (la media) y, en el caso del riego,
    contra una regla física (humedad actual ÷ velocidad de secado). Si el modelo no le gana a
    la regla, no vale la pena usarlo: eso se reporta en metricas.json.

Uso:
    python3 entrenar.py --datos datos/ --salida modelos/
"""

from __future__ import annotations

import argparse
import json
import platform
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pandas as pd
import sklearn
from sklearn.ensemble import GradientBoostingRegressor, RandomForestRegressor
from sklearn.linear_model import Ridge
from sklearn.metrics import mean_absolute_error, r2_score
from sklearn.model_selection import TimeSeriesSplit, cross_val_score
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler

try:  # sklearn >= 1.4
    from sklearn.metrics import root_mean_squared_error
except ImportError:  # pragma: no cover
    from sklearn.metrics import mean_squared_error

    def root_mean_squared_error(y_true, y_pred):  # type: ignore[misc]
        return float(np.sqrt(mean_squared_error(y_true, y_pred)))

import joblib

HORIZONTE_CRECIMIENTO_DIAS = 7
HUMEDAD_MINIMA = 35.0     # mismo umbral que Embebidos/config.py
HUECO_SESION_HORAS = 1.0  # pulsos separados por más de 1 h = sesiones de riego distintas
PROPORCION_PRUEBA = 0.25

# Altura a la que la planta deja de crecer (albahaca en maceta). Es un dato agronómico, no se
# estima de los datos. Se usa para el "potencial logístico": una planta de 40 cm ya casi no
# crece aunque el clima sea perfecto, y una de 6 cm tampoco crece rápido porque es pequeña.
# Sin esta variable el modelo no sabe frenar y sobreestima el crecimiento al final del ciclo.
ALTURA_MAXIMA_CM = 45.0


# ---------------------------------------------------------------------------
# Carga
# ---------------------------------------------------------------------------
def cargar_lecturas(ruta: Path) -> pd.DataFrame:
    """Pasa las lecturas de formato largo (una fila por sensor) a una columna por tipo."""
    datos = pd.read_csv(ruta)
    faltantes = {"tipo", "valor", "fechaHora"} - set(datos.columns)
    if faltantes:
        raise ValueError(f"{ruta} no trae las columnas {faltantes}")

    datos["fechaHora"] = pd.to_datetime(datos["fechaHora"], utc=True, format="mixed")
    ancho = datos.pivot_table(index="fechaHora", columns="tipo", values="valor", aggfunc="mean")
    ancho = ancho.sort_index()

    # Rejilla horaria regular: el equipo real publica cada minuto y puede traer huecos
    ancho = ancho.resample("1h").mean().interpolate(limit=6, limit_direction="both")
    return ancho


def cargar_riegos(ruta: Path) -> pd.DataFrame:
    datos = pd.read_csv(ruta)
    datos["fechaHoraInicio"] = pd.to_datetime(datos["fechaHoraInicio"], utc=True, format="mixed")
    datos = datos.sort_values("fechaHoraInicio").reset_index(drop=True)
    if "volumenMl" not in datos.columns:
        datos["volumenMl"] = np.nan
    datos["volumenMl"] = pd.to_numeric(datos["volumenMl"], errors="coerce")

    # Una "sesión" es el primer pulso después de más de HUECO_SESION_HORAS sin regar
    diferencia = datos["fechaHoraInicio"].diff().dt.total_seconds() / 3600.0
    datos["inicia_sesion"] = diferencia.isna() | (diferencia > HUECO_SESION_HORAS)
    return datos


def cargar_mediciones(ruta: Path) -> pd.DataFrame:
    datos = pd.read_csv(ruta)
    datos["fecha"] = pd.to_datetime(datos["fecha"], utc=True, format="mixed").dt.normalize()
    datos = datos.sort_values("fecha").set_index("fecha")
    return datos[["altura_cm", "hojas"]].astype(float)


# ---------------------------------------------------------------------------
# Variables de entrada
# ---------------------------------------------------------------------------
def variables_riego(lecturas: pd.DataFrame, riegos: pd.DataFrame) -> pd.DataFrame:
    """Una fila por hora con el estado actual del invernadero y lo que pasó después."""
    tabla = pd.DataFrame(index=lecturas.index)

    for columna in ("humedad_suelo", "temperatura", "humedad_aire", "luz", "nivel_deposito"):
        tabla[columna] = lecturas.get(columna, np.nan)

    hora = tabla.index.hour + tabla.index.minute / 60.0
    tabla["hora_sin"] = np.sin(2 * np.pi * hora / 24.0)
    tabla["hora_cos"] = np.cos(2 * np.pi * hora / 24.0)

    # Velocidad de secado: cuántos puntos de humedad se perdieron por hora
    for ventana in (3, 6):
        caida = tabla["humedad_suelo"].shift(ventana) - tabla["humedad_suelo"]
        tabla[f"secado_{ventana}h"] = (caida / ventana).clip(lower=0.0)

    tabla["temp_media_6h"] = tabla["temperatura"].rolling(6, min_periods=3).mean()
    tabla["temp_max_24h"] = tabla["temperatura"].rolling(24, min_periods=12).max()
    tabla["luz_media_6h"] = tabla["luz"].rolling(6, min_periods=3).mean()
    tabla["luz_media_24h"] = tabla["luz"].rolling(24, min_periods=12).mean()

    # Agua y riegos de las últimas 24 h
    agua = riegos.set_index("fechaHoraInicio")["volumenMl"].resample("1h").sum()
    conteo = riegos.set_index("fechaHoraInicio")["volumenMl"].resample("1h").count()
    agua = agua.reindex(tabla.index, fill_value=0.0)
    conteo = conteo.reindex(tabla.index, fill_value=0)
    tabla["agua_24h_ml"] = agua.rolling(24, min_periods=1).sum()
    tabla["riegos_24h"] = conteo.rolling(24, min_periods=1).sum()

    # --- Objetivos ---
    sesiones = riegos.loc[riegos["inicia_sesion"], "fechaHoraInicio"].to_numpy()
    momentos = tabla.index.to_numpy()
    siguiente = np.searchsorted(sesiones, momentos, side="right")
    horas = np.full(len(momentos), np.nan)
    valido = siguiente < len(sesiones)
    horas[valido] = (sesiones[siguiente[valido]] - momentos[valido]) / np.timedelta64(1, "h")
    tabla["objetivo_horas_proximo_riego"] = horas

    futuro = agua[::-1].rolling(24, min_periods=1).sum()[::-1].shift(-1)
    tabla["objetivo_agua_24h_ml"] = futuro.reindex(tabla.index)

    # Las últimas 24 h no tienen futuro conocido: no sirven para entrenar
    tabla.loc[tabla.index > tabla.index.max() - pd.Timedelta(hours=24),
              "objetivo_agua_24h_ml"] = np.nan
    return tabla


def variables_crecimiento(lecturas: pd.DataFrame, riegos: pd.DataFrame,
                          mediciones: pd.DataFrame) -> pd.DataFrame:
    """Una fila por día con el clima y el agua de la última semana más la medición de la planta."""
    diario = pd.DataFrame(index=lecturas.resample("1D").mean().index)
    diario["temp_media"] = lecturas["temperatura"].resample("1D").mean()
    diario["temp_max"] = lecturas["temperatura"].resample("1D").max()
    diario["luz_media"] = lecturas["luz"].resample("1D").mean()
    diario["humedad_suelo_media"] = lecturas["humedad_suelo"].resample("1D").mean()
    diario["horas_estres"] = (lecturas["humedad_suelo"] < HUMEDAD_MINIMA).resample("1D").sum()

    agua_dia = riegos.set_index("fechaHoraInicio")["volumenMl"].resample("1D").sum()
    diario["agua_dia_ml"] = agua_dia.reindex(diario.index, fill_value=0.0)

    tabla = pd.DataFrame(index=diario.index)
    tabla["dias_desde_inicio"] = np.arange(len(diario), dtype=float)
    tabla["temp_media_7d"] = diario["temp_media"].rolling(7, min_periods=3).mean()
    tabla["temp_max_7d"] = diario["temp_max"].rolling(7, min_periods=3).max()
    tabla["luz_media_7d"] = diario["luz_media"].rolling(7, min_periods=3).mean()
    tabla["humedad_suelo_media_7d"] = diario["humedad_suelo_media"].rolling(7, min_periods=3).mean()
    tabla["horas_estres_7d"] = diario["horas_estres"].rolling(7, min_periods=3).sum()
    tabla["agua_7d_ml"] = diario["agua_dia_ml"].rolling(7, min_periods=3).sum()
    tabla["agua_acum_ml"] = diario["agua_dia_ml"].cumsum()

    tabla = tabla.join(mediciones, how="inner")

    # Potencial logístico: máximo cuando la planta va a media altura, casi cero al principio
    # (es pequeña) y al final (ya se saturó).
    tabla["potencial_logistico"] = (tabla["altura_cm"]
                                    * (1.0 - tabla["altura_cm"] / ALTURA_MAXIMA_CM))

    # Objetivo: cuánto crece de aquí a 7 días (incremento, no altura absoluta)
    futuro = tabla[["altura_cm", "hojas"]].shift(-HORIZONTE_CRECIMIENTO_DIAS)
    tabla["objetivo_crecimiento_cm"] = futuro["altura_cm"] - tabla["altura_cm"]
    tabla["objetivo_hojas_nuevas"] = futuro["hojas"] - tabla["hojas"]
    return tabla


# ---------------------------------------------------------------------------
# Entrenamiento
# ---------------------------------------------------------------------------
def candidatos(n_filas: int, familia: str = "todos") -> dict:
    """
    Modelos a probar.

    familia="lineales" se usa para el crecimiento, por una razón estructural y no por las
    métricas: un bosque o un gradient boosting nunca predicen fuera del rango que vieron al
    entrenar, y la planta siempre está creciendo hacia tamaños que el modelo no ha visto. Un
    modelo lineal sobre el potencial logístico sí extrapola esa curva. Para los modelos de
    riego, que se mueven dentro de un rango de humedad ya conocido, se prueban los tres.
    """
    if familia == "lineales":
        return {f"ridge_alpha_{alpha}": make_pipeline(StandardScaler(), Ridge(alpha=alpha))
                for alpha in (0.3, 1.0, 3.0, 10.0)}

    hoja_minima = 2 if n_filas > 200 else 3
    return {
        "ridge": make_pipeline(StandardScaler(), Ridge(alpha=1.0)),
        "bosque_aleatorio": RandomForestRegressor(
            n_estimators=300, min_samples_leaf=hoja_minima, random_state=42, n_jobs=-1),
        "gradient_boosting": GradientBoostingRegressor(
            n_estimators=200, max_depth=3, learning_rate=0.05, random_state=42),
    }


def medir(y_real: np.ndarray, y_estimado: np.ndarray) -> dict:
    return {
        "mae": round(float(mean_absolute_error(y_real, y_estimado)), 4),
        "rmse": round(float(root_mean_squared_error(y_real, y_estimado)), 4),
        "r2": round(float(r2_score(y_real, y_estimado)), 4),
    }


def entrenar_objetivo(tabla: pd.DataFrame, columnas: list[str], objetivo: str,
                      unidad: str, referencias_extra: dict | None = None,
                      normalizador: str | None = None, familia: str = "todos") -> dict:
    """
    Prueba los modelos candidatos y elige el mejor por validación cruzada temporal.

    `normalizador` es el nombre de una columna por la que se divide el objetivo antes de
    entrenar (y por la que se multiplica la predicción al usarla). Para el crecimiento se usa
    `potencial_logistico`: así el modelo aprende la TASA de crecimiento, que sí se puede
    extrapolar, en lugar de los centímetros, que dependen del tamaño que ya tiene la planta.
    Todas las métricas se miden siempre en la unidad original, para que sean comparables.
    """
    necesarias = columnas + [objetivo] + ([normalizador] if normalizador else [])
    datos = tabla[list(dict.fromkeys(necesarias))].dropna()
    if normalizador:
        datos = datos[datos[normalizador].abs() > 1e-6]
    if len(datos) < 20:
        raise ValueError(f"Muy pocas filas para entrenar '{objetivo}': {len(datos)}")

    escala = datos[normalizador] if normalizador else pd.Series(1.0, index=datos.index)
    y_ajuste = datos[objetivo] / escala  # lo que ve el modelo

    corte = int(len(datos) * (1 - PROPORCION_PRUEBA))
    entrena, prueba = datos.iloc[:corte], datos.iloc[corte:]
    x_entrena, x_prueba = entrena[columnas].to_numpy(), prueba[columnas].to_numpy()
    y_entrena_ajuste = y_ajuste.iloc[:corte].to_numpy()
    y_entrena = entrena[objetivo].to_numpy()
    y_prueba = prueba[objetivo].to_numpy()
    escala_prueba = escala.iloc[corte:].to_numpy()

    particiones = TimeSeriesSplit(n_splits=min(5, max(2, len(entrena) // 12)))
    resultados: dict[str, dict] = {}
    mejor_nombre, mejor_mae_cv = None, float("inf")

    for nombre, modelo in candidatos(len(entrena), familia).items():
        mae_cv = -cross_val_score(modelo, x_entrena, y_entrena_ajuste, cv=particiones,
                                  scoring="neg_mean_absolute_error").mean()
        modelo.fit(x_entrena, y_entrena_ajuste)
        resultados[nombre] = {
            "mae_validacion_cruzada_escala_interna": round(float(mae_cv), 4),
            "prueba": medir(y_prueba, modelo.predict(x_prueba) * escala_prueba),
        }
        if mae_cv < mejor_mae_cv:
            mejor_nombre, mejor_mae_cv = nombre, mae_cv

    # Referencia tonta: predecir siempre la media de lo que se vio al entrenar
    referencias = {"media_del_entrenamiento": medir(
        y_prueba, np.full(len(y_prueba), y_entrena.mean()))}
    if referencias_extra:
        for nombre, estimado in referencias_extra.items():
            alineado = estimado.reindex(prueba.index).to_numpy()
            if not np.isnan(alineado).all():
                mascara = ~np.isnan(alineado)
                referencias[nombre] = medir(y_prueba[mascara], alineado[mascara])

    modelo_final = candidatos(len(entrena), familia)[mejor_nombre]
    modelo_final.fit(datos[columnas].to_numpy(), y_ajuste.to_numpy())

    importancias = None
    interno = getattr(modelo_final, "steps", [(None, modelo_final)])[-1][1]
    if hasattr(interno, "feature_importances_"):
        importancias = {c: round(float(v), 4) for c, v in
                        sorted(zip(columnas, interno.feature_importances_),
                               key=lambda par: -par[1])}

    return {
        "modelo": modelo_final,
        "normalizador": normalizador,
        "informe": {
            "objetivo": objetivo,
            "unidad": unidad,
            "variables_entrada": columnas,
            "normalizador": normalizador,
            "filas_totales": int(len(datos)),
            "filas_entrenamiento": int(len(entrena)),
            "filas_prueba": int(len(prueba)),
            "rango_entrenamiento": [str(entrena.index.min()), str(entrena.index.max())],
            "rango_prueba": [str(prueba.index.min()), str(prueba.index.max())],
            "familia_candidatos": familia,
            "modelo_elegido": mejor_nombre,
            "candidatos": resultados,
            "referencias": referencias,
            "metricas_modelo_elegido": resultados[mejor_nombre]["prueba"],
            "importancia_variables": importancias,
            "objetivo_media": round(float(datos[objetivo].mean()), 3),
            "objetivo_desviacion": round(float(datos[objetivo].std()), 3),
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--datos", default="datos", help="carpeta con los CSV de entrada")
    parser.add_argument("--salida", default="modelos", help="carpeta donde se guardan los modelos")
    args = parser.parse_args()

    carpeta = Path(args.datos)
    salida = Path(args.salida)
    salida.mkdir(parents=True, exist_ok=True)

    lecturas = cargar_lecturas(carpeta / "lecturas.csv")
    riegos = cargar_riegos(carpeta / "riegos.csv")
    mediciones = cargar_mediciones(carpeta / "mediciones.csv")

    print(f"Lecturas: {len(lecturas)} horas de {lecturas.index.min()} a {lecturas.index.max()}")
    print(f"Riegos: {len(riegos)} pulsos, {int(riegos['inicia_sesion'].sum())} sesiones")
    print(f"Mediciones de la planta: {len(mediciones)} días\n")

    # ---------------- Modelo de riego ----------------
    tabla_riego = variables_riego(lecturas, riegos)
    entradas_riego = ["humedad_suelo", "temperatura", "humedad_aire", "luz", "nivel_deposito",
                      "hora_sin", "hora_cos", "secado_3h", "secado_6h", "temp_media_6h",
                      "temp_max_24h", "luz_media_6h", "luz_media_24h", "agua_24h_ml", "riegos_24h"]

    # Regla física: horas = (humedad actual − umbral) ÷ velocidad de secado
    regla = ((tabla_riego["humedad_suelo"] - HUMEDAD_MINIMA)
             / tabla_riego["secado_6h"].clip(lower=0.05)).clip(lower=0.0, upper=240.0)

    riego_horas = entrenar_objetivo(tabla_riego, entradas_riego,
                                    "objetivo_horas_proximo_riego", "horas",
                                    {"regla_fisica_secado": regla})
    riego_agua = entrenar_objetivo(tabla_riego, entradas_riego,
                                   "objetivo_agua_24h_ml", "mL")

    # ---------------- Modelo de crecimiento ----------------
    tabla_crecimiento = variables_crecimiento(lecturas, riegos, mediciones)
    entradas_crecimiento = ["potencial_logistico", "altura_cm", "hojas", "temp_media_7d",
                            "temp_max_7d", "luz_media_7d", "humedad_suelo_media_7d",
                            "horas_estres_7d", "agua_7d_ml"]

    crecimiento_altura = entrenar_objetivo(tabla_crecimiento, entradas_crecimiento,
                                           "objetivo_crecimiento_cm", "cm en 7 dias",
                                           normalizador="potencial_logistico",
                                           familia="lineales")
    crecimiento_hojas = entrenar_objetivo(tabla_crecimiento, entradas_crecimiento,
                                          "objetivo_hojas_nuevas", "hojas en 7 dias",
                                          normalizador="potencial_logistico",
                                          familia="lineales")

    modelos = {
        "riego_horas": riego_horas,
        "riego_agua_24h": riego_agua,
        "crecimiento_altura": crecimiento_altura,
        "crecimiento_hojas": crecimiento_hojas,
    }

    paquete = {
        "version": "1.0.0",
        "entrenado": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "horizonte_crecimiento_dias": HORIZONTE_CRECIMIENTO_DIAS,
        "altura_maxima_cm": ALTURA_MAXIMA_CM,
        "humedad_minima": HUMEDAD_MINIMA,
        "modelos": {nombre: datos["modelo"] for nombre, datos in modelos.items()},
        "variables": {nombre: datos["informe"]["variables_entrada"]
                      for nombre, datos in modelos.items()},
        "normalizadores": {nombre: datos["normalizador"] for nombre, datos in modelos.items()},
    }
    # compress=3: el bosque aleatorio pesa ~3 MB sin comprimir y ~0.7 MB comprimido
    joblib.dump(paquete, salida / "modelo_invernadero.joblib", compress=3)

    metadatos_datos = {}
    ruta_metadatos = carpeta / "metadatos.json"
    if ruta_metadatos.exists():
        metadatos_datos = json.loads(ruta_metadatos.read_text(encoding="utf-8"))

    metricas = {
        "version": paquete["version"],
        "entrenado": paquete["entrenado"],
        "entorno": {
            "python": platform.python_version(),
            "scikit_learn": sklearn.__version__,
            "pandas": pd.__version__,
            "numpy": np.__version__,
        },
        "datos": {
            "origen": metadatos_datos.get("origen", "desconocido"),
            "lecturas_horas": int(len(lecturas)),
            "riegos_pulsos": int(len(riegos)),
            "riegos_sesiones": int(riegos["inicia_sesion"].sum()),
            "mediciones_dias": int(len(mediciones)),
            "desde": str(lecturas.index.min()),
            "hasta": str(lecturas.index.max()),
        },
        "metodo": {
            "separacion": f"temporal, ultimo {int(PROPORCION_PRUEBA * 100)} % para prueba",
            "validacion_cruzada": "TimeSeriesSplit sobre el tramo de entrenamiento",
            "objetivo_crecimiento": "incremento a 7 dias, no altura absoluta",
        },
        "modelos": {nombre: datos["informe"] for nombre, datos in modelos.items()},
    }
    (salida / "metricas.json").write_text(
        json.dumps(metricas, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    # ---------------- Resumen en pantalla ----------------
    print(f"{'modelo':<22}{'elegido':<20}{'MAE':>9}{'RMSE':>9}{'R2':>8}   referencia (MAE)")
    for nombre, datos in modelos.items():
        informe = datos["informe"]
        metricas_modelo = informe["metricas_modelo_elegido"]
        referencia = informe["referencias"]["media_del_entrenamiento"]["mae"]
        print(f"{nombre:<22}{informe['modelo_elegido']:<20}"
              f"{metricas_modelo['mae']:>9.3f}{metricas_modelo['rmse']:>9.3f}"
              f"{metricas_modelo['r2']:>8.3f}   {referencia:.3f}")

    regla_mae = riego_horas["informe"]["referencias"].get("regla_fisica_secado", {}).get("mae")
    if regla_mae is not None:
        print(f"\nRegla fisica de secado (sin ML), MAE: {regla_mae:.3f} horas")
    print(f"\nGuardado: {salida/'modelo_invernadero.joblib'} y {salida/'metricas.json'}")


if __name__ == "__main__":
    main()
