#!/usr/bin/env python3
"""
Genera un historial SIMULADO del invernadero para entrenar el modelo de Machine Learning.

Por qué existe este script: el modelo de ML necesita historial (humedad de suelo, temperatura,
luz, agua suministrada, fecha y hora) y mediciones de la planta (altura y número de hojas).
Hasta hoy la Raspberry todavía no envía datos a la nube, así que el historial se simula con la
MISMA lógica de riego y los MISMOS parámetros de `Invernadero/Embebidos/config.py`.
El contexto del proyecto permite esto de forma explícita: "si no alcanzan los datos reales, se
complementa con datos simulados y se documenta".

Los archivos se escriben con las mismas columnas que entrega la API, para que el día que haya
datos reales se reentrene sin tocar el código de entrenamiento:

  lecturas.csv    = columnas de GET /api/Lecturas/exportar.csv
  riegos.csv      = campos de RiegoDto (GET /api/Riegos)
  mediciones.csv  = medición manual diaria de la planta (altura y hojas); esto NO lo da la API,
                    lo anota Juan con una regla, y es la tarea diaria del cronograma.

No usa dependencias externas: corre con Python 3 limpio en cualquier máquina.

Uso:
    python3 generar_datos.py --salida datos/ --dias 60 --semilla 20261116
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import random
from datetime import datetime, timedelta, timezone
from pathlib import Path

# ---------------------------------------------------------------------------
# Parámetros copiados de Invernadero/Embebidos/config.py (si allá cambian, cambiar aquí)
# ---------------------------------------------------------------------------
HUMEDAD_MINIMA = 35.0        # % en que arranca el riego
HUMEDAD_OBJETIVO = 60.0      # % en que se detiene el riego
NIVEL_MINIMO = 10.0          # % de depósito para considerar "sin agua"
TIEMPO_MAX_BOMBA_S = 10      # la bomba no pasa de 10 s por pulso
ESPERA_ENTRE_RIEGOS_S = 300  # 5 min entre pulsos
TEMP_VENT_ON = 30.0
TEMP_VENT_OFF = 28.0

# ---------------------------------------------------------------------------
# Parámetros físicos de la maqueta (calibrados para que el historial sea creíble)
# ---------------------------------------------------------------------------
CAUDAL_BOMBA_LPM = 0.33          # L/min que entrega la mini bomba de 5 V
PUNTOS_HUMEDAD_POR_ML = 0.065    # cuánto sube la humedad del suelo por mL (maceta chica)
SECADO_BASE_PCT_MIN = 0.0085     # %/min de evapotranspiración a 25 °C y 800 lux
CAPACIDAD_DEPOSITO_ML = 4000.0   # depósito de 4 L
RECARGA_DEPOSITO_PCT = 95.0      # a cuánto queda el depósito al rellenarlo a mano

TEMP_MEDIA = 24.0
TEMP_AMPLITUD = 5.5              # diferencia día/noche
LUZ_MAX_LUX = 1400.0

ALTURA_INICIAL_CM = 6.0
ALTURA_MAXIMA_CM = 42.0          # albahaca en maceta
TASA_CRECIMIENTO = 0.085         # constante de la curva logística (por día)

# Catálogo de sensores: debe coincidir con CONTRATOS-integracion.md
SENSORES = [
    ("DHT22-01", "temperatura", "°C"),
    ("DHT22-01", "humedad_aire", "%"),
    ("SUELO-01", "humedad_suelo", "%"),
    ("BH1750-01", "luz", "lux"),
    ("NIVEL-01", "nivel_deposito", "%"),
    ("FLUJO-01", "caudal", "L/min"),
    ("FLOT-01", "flotador_vacio", "bool"),
]

INVERNADERO_ID = 1
ZONA = "Zona A"


def iso(momento: datetime) -> str:
    """Fecha en UTC ISO-8601 con Z, como lo exige el contrato."""
    return momento.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


class Clima:
    """Temperatura, luz y humedad del aire a lo largo del día, con variación entre días."""

    def __init__(self, azar: random.Random):
        self._azar = azar
        self._por_dia: dict[int, tuple[float, float]] = {}

    def _del_dia(self, dia: int) -> tuple[float, float]:
        """Desvío de temperatura y nubosidad de ese día (un frente de calor dura varios días)."""
        if dia not in self._por_dia:
            desvio = self._azar.gauss(0.0, 1.8)
            nubosidad = min(1.0, max(0.25, self._azar.gauss(0.82, 0.18)))
            self._por_dia[dia] = (desvio, nubosidad)
        return self._por_dia[dia]

    def temperatura(self, dia: int, hora: float) -> float:
        desvio, nubosidad = self._del_dia(dia)
        # El máximo cae alrededor de las 15:00
        ciclo = math.sin(2 * math.pi * (hora - 9.0) / 24.0)
        base = TEMP_MEDIA + desvio + TEMP_AMPLITUD * ciclo
        base -= (1.0 - nubosidad) * 1.5
        return base + self._azar.gauss(0.0, 0.25)

    def luz(self, dia: int, hora: float) -> float:
        _, nubosidad = self._del_dia(dia)
        if hora < 7.0 or hora > 19.0:
            return max(0.0, self._azar.gauss(1.5, 0.8))
        arco = math.sin(math.pi * (hora - 7.0) / 12.0)
        return max(0.0, LUZ_MAX_LUX * arco * nubosidad + self._azar.gauss(0.0, 25.0))

    def humedad_aire(self, temperatura: float) -> float:
        valor = 95.0 - 1.45 * temperatura + self._azar.gauss(0.0, 2.0)
        return min(95.0, max(20.0, valor))


class Planta:
    """Crecimiento con curva logística, frenado por estrés de agua, temperatura y luz."""

    def __init__(self, azar: random.Random):
        self._azar = azar
        self.altura_cm = ALTURA_INICIAL_CM
        self.hojas = 6

    def crecer_un_dia(self, humedad_ok: float, temp_media: float, luz_dia: float) -> None:
        # Potencial logístico del día
        potencial = TASA_CRECIMIENTO * self.altura_cm * (1.0 - self.altura_cm / ALTURA_MAXIMA_CM)

        # Factor de agua: fracción del día con la humedad del suelo en el rango bueno (35-70 %)
        factor_agua = 0.45 + 0.70 * humedad_ok

        # Factor de temperatura: óptimo entre 22 y 28 °C
        if 22.0 <= temp_media <= 28.0:
            factor_temp = 1.0
        else:
            distancia = min(abs(temp_media - 22.0), abs(temp_media - 28.0))
            factor_temp = max(0.35, 1.0 - 0.09 * distancia)

        # Factor de luz: luz_dia es el promedio de lux de las 24 h (día y noche)
        factor_luz = min(1.0, max(0.40, luz_dia / 450.0))

        crecimiento = potencial * factor_agua * factor_temp * factor_luz
        crecimiento = max(0.0, crecimiento + self._azar.gauss(0.0, 0.035))
        self.altura_cm = min(ALTURA_MAXIMA_CM, self.altura_cm + crecimiento)

        # Las hojas acompañan a la altura (la albahaca saca pares de hojas)
        objetivo = 4 + 1.15 * self.altura_cm
        if objetivo > self.hojas:
            self.hojas += int(round(min(objetivo - self.hojas, self._azar.uniform(0.0, 1.6))))


def simular(dias: int, fin: datetime, semilla: int) -> dict:
    """Simula minuto a minuto y devuelve lecturas (por hora), riegos (todos) y mediciones (diarias)."""
    azar = random.Random(semilla)
    clima = Clima(azar)
    planta = Planta(azar)

    inicio = (fin - timedelta(days=dias)).replace(minute=0, second=0, microsecond=0)
    total_minutos = dias * 24 * 60

    humedad_suelo = 58.0
    nivel_deposito = 92.0
    ventilador = False

    regando = False
    sesion_riego = False  # ver nota "SESION DE RIEGO" más abajo
    fecha_inicio_riego = inicio
    humedad_al_iniciar = 0.0
    volumen_riego_ml = 0.0
    minuto_fin_ultimo_riego: int | None = None
    riego_manual_pedido = False

    lecturas: list[dict] = []
    riegos: list[dict] = []
    mediciones: list[dict] = []
    recargas: list[str] = []

    # Acumuladores del día para el crecimiento
    minutos_humedad_ok = 0
    suma_temp = 0.0
    suma_luz = 0.0
    minutos_dia = 0

    id_lectura = 1
    id_riego = 1

    for i in range(total_minutos):
        momento = inicio + timedelta(minutes=i)
        dia = i // (24 * 60)
        hora = momento.hour + momento.minute / 60.0

        temperatura = clima.temperatura(dia, hora)
        luz = clima.luz(dia, hora)

        # Ventilador con histéresis (igual que ventilador.py): al encender baja un poco la temperatura
        if ventilador and temperatura <= TEMP_VENT_OFF:
            ventilador = False
        elif not ventilador and temperatura >= TEMP_VENT_ON:
            ventilador = True
        if ventilador:
            temperatura -= 1.4

        humedad_aire = clima.humedad_aire(temperatura)
        flotador_vacio = 1 if nivel_deposito < 3.0 else 0
        sin_agua = bool(flotador_vacio) or nivel_deposito < NIVEL_MINIMO

        # --- Evapotranspiración: más calor y más luz secan más rápido ---
        factor_temp = 1.0 + 0.055 * (temperatura - 25.0)
        factor_luz = 0.55 + 0.45 * min(1.0, luz / 800.0)
        secado = SECADO_BASE_PCT_MIN * max(0.25, factor_temp) * factor_luz
        humedad_suelo = max(3.0, humedad_suelo - secado)

        # --- Algún riego manual de vez en cuando (hoy con la tecla r, pronto por voz) ---
        if not regando and azar.random() < 1.0 / (60 * 72) and 8.0 <= hora <= 20.0:
            riego_manual_pedido = True

        # --- Lógica de riego: la misma de Embebidos/main.py ---
        caudal_actual = 0.0
        if regando:
            caudal_actual = CAUDAL_BOMBA_LPM
            # La bomba nunca pasa de TIEMPO_MAX_BOMBA_S, así que el pulso cabe dentro de este minuto
            segundos = min(TIEMPO_MAX_BOMBA_S, 60)
            entregado_ml = CAUDAL_BOMBA_LPM * 1000.0 * (segundos / 60.0)
            volumen_riego_ml += entregado_ml
            humedad_suelo = min(95.0, humedad_suelo + entregado_ml * PUNTOS_HUMEDAD_POR_ML)
            nivel_deposito = max(0.0, nivel_deposito - entregado_ml / CAPACIDAD_DEPOSITO_ML * 100.0)

            riegos.append({
                "id": id_riego,
                "invernaderoId": INVERNADERO_ID,
                "zona": ZONA,
                "fechaHoraInicio": iso(fecha_inicio_riego),
                "duracionSegundos": segundos,
                "volumenMl": round(volumen_riego_ml, 1),
                "origen": "manual" if riego_manual_pedido else "automatico",
                "humedadDetectada": "" if riego_manual_pedido else round(humedad_al_iniciar, 1),
                "umbral": "" if riego_manual_pedido else HUMEDAD_MINIMA,
                "lecturaSensorId": "",
            })
            id_riego += 1

            regando = False
            riego_manual_pedido = False
            minuto_fin_ultimo_riego = i
        else:
            espera_cumplida = (minuto_fin_ultimo_riego is None
                               or (i - minuto_fin_ultimo_riego) * 60 >= ESPERA_ENTRE_RIEGOS_S)

            # SESION DE RIEGO: un pulso de 10 s solo sube la humedad ~3.6 puntos, así que una
            # sola activación nunca llega a HUMEDAD_OBJETIVO. Aquí se simula la intención del
            # diseño (main.py: "listo = humedad >= HUMEDAD_OBJETIVO"): al bajar de
            # HUMEDAD_MINIMA se abre una sesión que da pulsos cada ESPERA_ENTRE_RIEGOS_S hasta
            # alcanzar el objetivo. OJO: main.py tal como está hoy NO reabre el riego mientras
            # la humedad esté por encima del mínimo, por lo que nunca llega a 60 %.
            # Está anotado como pendiente en Invernadero/ML/README.md.
            if humedad_suelo < HUMEDAD_MINIMA:
                sesion_riego = True
            elif humedad_suelo >= HUMEDAD_OBJETIVO:
                sesion_riego = False

            if not sin_agua and (riego_manual_pedido
                                 or (sesion_riego and espera_cumplida
                                     and humedad_suelo < HUMEDAD_OBJETIVO)):
                regando = True
                fecha_inicio_riego = momento
                humedad_al_iniciar = humedad_suelo
                volumen_riego_ml = 0.0
            elif sin_agua and riego_manual_pedido:
                riego_manual_pedido = False  # sin agua no se puede regar

        # --- Recarga manual del depósito cuando se vacía ---
        if nivel_deposito < 15.0:
            nivel_deposito = RECARGA_DEPOSITO_PCT
            recargas.append(iso(momento))

        # --- Acumuladores para el crecimiento del día ---
        if 35.0 <= humedad_suelo <= 70.0:
            minutos_humedad_ok += 1
        suma_temp += temperatura
        suma_luz += luz
        minutos_dia += 1

        # --- Exportar una lectura por hora (el equipo real manda cada minuto) ---
        if momento.minute == 0:
            valores = {
                "temperatura": round(temperatura, 1),
                "humedad_aire": round(humedad_aire, 1),
                "humedad_suelo": round(humedad_suelo, 1),
                "luz": round(luz, 0),
                "nivel_deposito": round(nivel_deposito, 1),
                "caudal": round(caudal_actual, 2),
                "flotador_vacio": flotador_vacio,
            }
            for sensor_id, tipo, unidad in SENSORES:
                lecturas.append({
                    "id": id_lectura,
                    "sensorId": sensor_id,
                    "tipo": tipo,
                    "valor": valores[tipo],
                    "unidad": unidad,
                    "invernaderoId": INVERNADERO_ID,
                    "fechaHora": iso(momento),
                })
                id_lectura += 1

        # --- Medición manual de la planta a las 08:00 (tarea diaria de Juan) ---
        if momento.hour == 8 and momento.minute == 0 and minutos_dia > 60:
            humedad_ok = minutos_humedad_ok / minutos_dia
            planta.crecer_un_dia(humedad_ok, suma_temp / minutos_dia, suma_luz / minutos_dia)
            mediciones.append({
                "fecha": momento.strftime("%Y-%m-%d"),
                # La regla de Juan mide al milímetro: se agrega error de medición
                "altura_cm": round(planta.altura_cm + azar.gauss(0.0, 0.25), 1),
                "hojas": max(1, planta.hojas + azar.choice([-1, 0, 0, 0, 1])),
                "observaciones": "",
            })
            minutos_humedad_ok = 0
            suma_temp = 0.0
            suma_luz = 0.0
            minutos_dia = 0

    return {
        "lecturas": lecturas,
        "riegos": riegos,
        "mediciones": mediciones,
        "metadatos": {
            "generado": iso(datetime.now(timezone.utc)),
            "origen": "SIMULADO con generar_datos.py (la Raspberry todavia no envia datos reales)",
            "semilla": semilla,
            "dias": dias,
            "inicio": iso(inicio),
            "fin": iso(fin),
            "paso_simulacion_min": 1,
            "paso_exportacion_lecturas": "1 h (el equipo real publica cada 60 s)",
            "parametros_config_py": {
                "HUMEDAD_MINIMA": HUMEDAD_MINIMA,
                "HUMEDAD_OBJETIVO": HUMEDAD_OBJETIVO,
                "NIVEL_MINIMO": NIVEL_MINIMO,
                "TIEMPO_MAX_BOMBA_S": TIEMPO_MAX_BOMBA_S,
                "ESPERA_ENTRE_RIEGOS_S": ESPERA_ENTRE_RIEGOS_S,
                "TEMP_VENT_ON": TEMP_VENT_ON,
                "TEMP_VENT_OFF": TEMP_VENT_OFF,
            },
            "parametros_maqueta": {
                "CAUDAL_BOMBA_LPM": CAUDAL_BOMBA_LPM,
                "PUNTOS_HUMEDAD_POR_ML": PUNTOS_HUMEDAD_POR_ML,
                "SECADO_BASE_PCT_MIN": SECADO_BASE_PCT_MIN,
                "CAPACIDAD_DEPOSITO_ML": CAPACIDAD_DEPOSITO_ML,
                "ALTURA_MAXIMA_CM": ALTURA_MAXIMA_CM,
            },
            "totales": {
                "lecturas": len(lecturas),
                "riegos": len(riegos),
                "mediciones": len(mediciones),
                "recargas_deposito": len(recargas),
            },
        },
    }


def escribir_csv(ruta: Path, filas: list[dict]) -> None:
    ruta.parent.mkdir(parents=True, exist_ok=True)
    with ruta.open("w", newline="", encoding="utf-8") as archivo:
        escritor = csv.DictWriter(archivo, fieldnames=list(filas[0].keys()))
        escritor.writeheader()
        escritor.writerows(filas)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--salida", default="datos", help="carpeta donde se escriben los CSV")
    parser.add_argument("--dias", type=int, default=60, help="días de historial a simular")
    parser.add_argument("--semilla", type=int, default=20261116, help="semilla (datos reproducibles)")
    parser.add_argument("--fin", default="2026-11-16T23:00:00Z",
                        help="último momento del historial, en UTC ISO-8601")
    args = parser.parse_args()

    fin = datetime.strptime(args.fin, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    datos = simular(args.dias, fin, args.semilla)

    salida = Path(args.salida)
    escribir_csv(salida / "lecturas.csv", datos["lecturas"])
    escribir_csv(salida / "riegos.csv", datos["riegos"])
    escribir_csv(salida / "mediciones.csv", datos["mediciones"])
    (salida / "metadatos.json").write_text(
        json.dumps(datos["metadatos"], indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    totales = datos["metadatos"]["totales"]
    print(f"Historial simulado en {salida}/")
    print(f"  lecturas.csv    {totales['lecturas']:>6} filas")
    print(f"  riegos.csv      {totales['riegos']:>6} filas")
    print(f"  mediciones.csv  {totales['mediciones']:>6} filas")
    print(f"  recargas del deposito: {totales['recargas_deposito']}")


if __name__ == "__main__":
    main()
