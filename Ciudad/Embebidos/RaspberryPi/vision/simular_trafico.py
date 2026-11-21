#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Cruce Inteligente - simulador de trafico
=========================================

Hace lo mismo que detectar_trafico.py (manda cuantos vehiculos hay en
Norte, Sur, Este y Oeste al backend) pero sin camara: inventa conteos
realistas para probar las estadisticas mientras no esta la maqueta.
Cuando la camara este lista, solo dejas de correr este script.

Como se inventan los conteos:
    - Entre semana hay hora pico en la manana (~8:00), un poco a medio dia
      (~14:00) y otra hora pico en la tarde (~18:30); de madrugada casi nada.
    - Sabado y domingo hay menos trafico y el pico es a medio dia.
    - Norte y Sur (Av. Central) llevan mas coches que Este y Oeste (Calle 8).
    - Cada dia es un poco distinto (+-15 %) y cada conteo tiene ruido
      (distribucion de Poisson, como llegan los coches de verdad).
    - En vivo, de vez en cuando hay un "evento" (p. ej. un choque o un
      partido) que carga un acceso durante unos minutos.

Dos formas de usarlo (se pueden juntar):

    1) Llenar historial: crea conteos con fecha de los ultimos dias y los
       sube de golpe a POST /api/trafico/lote (no se mandan al ESP32):
           python simular_trafico.py --historial 14

    2) En vivo: manda un conteo cada pocos segundos a POST /api/trafico,
       igual que la camara (en modo adaptativo cambia los verdes del ESP32):
           python simular_trafico.py --en-vivo

       Los dos juntos: python simular_trafico.py --historial 14 --en-vivo

Llave: el backend pide el encabezado X-Api-Key con la llave de
Dispositivos:ApiKey (dotnet user-secrets). Pasala con --api-key o con la
variable de entorno CRUCE_API_KEY.

Solo usa la libreria estandar de Python (corre en la PC o en la Raspberry).
Para empezar de cero: deten el backend, borra ciudad_inteligente.db (carpeta
Backend/CiudadInteligente.API) y vuelve a arrancarlo.
"""

import argparse
import json
import math
import os
import random
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta

NOMBRES_ACCESO = ["Norte", "Sur", "Este", "Oeste"]

# Que tanto trafico lleva cada acceso comparado con el Norte
PESO_ACCESO = [1.00, 0.85, 0.55, 0.45]

# Vehiculos en el acceso Norte en la hora pico de un dia normal
PICO_NORTE = 16.0

LOTE_MAXIMO = 2000  # conteos por peticion a /trafico/lote


def campana(x, centro, ancho):
    return math.exp(-((x - centro) ** 2) / (2 * ancho ** 2))


def intensidad(momento):
    """Que tan cargado esta el cruce a esa hora (0 = vacio, ~1 = hora pico)."""
    h = momento.hour + momento.minute / 60.0
    if momento.weekday() >= 5:  # sabado y domingo
        return (0.06
                + 0.55 * campana(h, 13.0, 2.5)
                + 0.35 * campana(h, 19.5, 2.0))
    return (0.06
            + 1.00 * campana(h, 8.0, 1.0)
            + 0.50 * campana(h, 14.0, 1.5)
            + 0.95 * campana(h, 18.5, 1.3)
            + 0.20 * campana(h, 12.0, 4.5))


def poisson(media, rng):
    """Numero de llegadas con esa media (algoritmo de Knuth; medias chicas)."""
    if media <= 0:
        return 0
    if media > 30:  # para medias grandes la normal es buena aproximacion
        return max(0, int(round(rng.gauss(media, math.sqrt(media)))))
    limite, k, p = math.exp(-media), 0, 1.0
    while True:
        p *= rng.random()
        if p <= limite:
            return k
        k += 1


class Simulador:
    def __init__(self, semilla):
        self.rng = random.Random(semilla)
        self.factor_por_dia = {}
        self.evento = None  # (acceso, extra, termina_en)

    def factor_dia(self, fecha):
        """Cada dia trae un poco mas o un poco menos trafico, siempre igual para esa fecha."""
        if fecha not in self.factor_por_dia:
            self.factor_por_dia[fecha] = random.Random(fecha.toordinal()).uniform(0.85, 1.15)
        return self.factor_por_dia[fecha]

    def conteo(self, momento, con_eventos=False):
        base = PICO_NORTE * intensidad(momento) * self.factor_dia(momento.date())
        medias = [base * peso for peso in PESO_ACCESO]

        if con_eventos:
            if self.evento and momento >= self.evento[2]:
                print(f"[simulador] termino el evento en {NOMBRES_ACCESO[self.evento[0]]}")
                self.evento = None
            if self.evento is None and self.rng.random() < 0.004:
                acceso = self.rng.randrange(4)
                minutos = self.rng.randint(3, 10)
                self.evento = (acceso, self.rng.uniform(6, 12), momento + timedelta(minutes=minutos))
                print(f"[simulador] EVENTO: se carga {NOMBRES_ACCESO[acceso]} durante {minutos} min")
            if self.evento:
                medias[self.evento[0]] += self.evento[1]

        return [poisson(m, self.rng) for m in medias]


def peticion(url, cuerpo, api_key, timeout=15):
    datos = json.dumps(cuerpo).encode("utf-8")
    req = urllib.request.Request(
        url, data=datos, method="POST",
        headers={"Content-Type": "application/json", "X-Api-Key": api_key})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            texto = resp.read().decode("utf-8", errors="replace")
            return resp.status, texto
    except urllib.error.HTTPError as exc:
        return exc.code, exc.read().decode("utf-8", errors="replace")


def explicar_error(status, texto):
    if status in (401, 403):
        return ("llave rechazada: revisa --api-key / CRUCE_API_KEY contra Dispositivos:ApiKey "
                "del backend (dotnet user-secrets list)")
    try:
        detalle = json.loads(texto).get("detail")
    except (ValueError, AttributeError):
        detalle = None
    return f"el backend respondio {status}: {detalle or texto[:200]}"


def llenar_historial(sim, base_url, api_key, dias, cada_minutos):
    ahora = datetime.now().astimezone()
    momento = ahora - timedelta(days=dias)
    conteos = []
    while momento < ahora - timedelta(minutes=1):
        conteos.append({"registradoEn": momento.isoformat(timespec="seconds"),
                        "vehiculos": sim.conteo(momento)})
        momento += timedelta(minutes=cada_minutos)

    print(f"[simulador] subiendo {len(conteos)} conteos de los ultimos {dias} dias "
          f"(uno cada {cada_minutos} min)...")
    subidos = 0
    for inicio in range(0, len(conteos), LOTE_MAXIMO):
        lote = conteos[inicio:inicio + LOTE_MAXIMO]
        status, texto = peticion(f"{base_url}/trafico/lote", {"conteos": lote}, api_key, timeout=60)
        if status != 200:
            print(f"[simulador] ERROR: {explicar_error(status, texto)}", file=sys.stderr)
            return False
        subidos += len(lote)
        print(f"[simulador]   {subidos}/{len(conteos)}")
    print("[simulador] historial listo: abre Estadisticas en la app")
    return True


def en_vivo(sim, base_url, api_key, cada_segundos):
    print(f"[simulador] mandando un conteo cada {cada_segundos} s a {base_url}/trafico (Ctrl+C para salir)")
    fallos = 0
    while True:
        ahora = datetime.now().astimezone()
        conteo = sim.conteo(ahora, con_eventos=True)
        try:
            status, texto = peticion(f"{base_url}/trafico", {"vehiculos": conteo}, api_key, timeout=10)
        except urllib.error.URLError as exc:
            status, texto = None, str(exc.reason)

        resumen = "  ".join(f"{n}={c:2d}" for n, c in zip(NOMBRES_ACCESO, conteo))
        if status in (200, 204):
            fallos = 0
            print(f"[{ahora:%H:%M:%S}] {resumen}  total={sum(conteo)}")
        else:
            fallos += 1
            motivo = explicar_error(status, texto) if status else f"sin conexion con el backend ({texto})"
            print(f"[{ahora:%H:%M:%S}] {resumen}  -> {motivo}", file=sys.stderr)
            if status in (401, 403):
                return
        time.sleep(cada_segundos if fallos < 3 else min(60, cada_segundos * 3))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--backend-url", default="http://localhost:5240/api",
                    help="Base de la API (default: http://localhost:5240/api)")
    ap.add_argument("--api-key", default=os.environ.get("CRUCE_API_KEY", ""),
                    help="Llave de dispositivo (Dispositivos:ApiKey del backend); default: variable CRUCE_API_KEY")
    ap.add_argument("--historial", type=int, metavar="DIAS", default=0,
                    help="Crea y sube conteos de los ultimos DIAS dias (1-30)")
    ap.add_argument("--cada-minutos", type=int, default=5,
                    help="Para --historial: un conteo cada cuantos minutos (default: 5)")
    ap.add_argument("--en-vivo", action="store_true",
                    help="Manda un conteo cada pocos segundos, como la camara")
    ap.add_argument("--cada", type=float, default=10.0,
                    help="Para --en-vivo: segundos entre conteos (default: 10)")
    ap.add_argument("--semilla", type=int, default=None,
                    help="Semilla para que salgan siempre los mismos numeros")
    args = ap.parse_args()

    if not args.api_key:
        ap.error("falta la llave: usa --api-key o exporta CRUCE_API_KEY")
    if not args.historial and not args.en_vivo:
        ap.error("elige --historial DIAS, --en-vivo o los dos")
    if not 0 <= args.historial <= 30:
        ap.error("--historial va de 1 a 30 dias")

    base_url = args.backend_url.rstrip("/")
    sim = Simulador(args.semilla)

    try:
        if args.historial and not llenar_historial(sim, base_url, args.api_key, args.historial, args.cada_minutos):
            sys.exit(1)
        if args.en_vivo:
            en_vivo(sim, base_url, args.api_key, args.cada)
    except urllib.error.URLError as exc:
        print(f"[simulador] no se pudo conectar con {base_url}: {exc.reason}. "
              "Revisa que el backend este corriendo (dotnet run).", file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        print("\n[simulador] detenido")


if __name__ == "__main__":
    main()
