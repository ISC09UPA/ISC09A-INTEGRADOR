#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Cruce Inteligente - deteccion de densidad de trafico por acceso
==================================================================

Corre en la Raspberry Pi 4 (la misma que tiene el modulo de camara CSI
conectado). Lee video de la camara, cuenta cuantos vehiculos hay dentro
de 4 regiones de interes (una por acceso: Norte, Sur, Este, Oeste) y
manda ese conteo al backend en .NET (POST /api/trafico), que a
su vez se lo reenvia al ESP32 con el comando serial TRAFICO:a,b,c,d para
que reparta el tiempo de verde segun la densidad de cada acceso (ver
calcularDuracionVerde() en Embebidos/ESP32/semaforo_v2/semaforo_v2.ino).

Deteccion de vehiculos (modo por defecto, sin nube):
    Resta de fondo (cv2.createBackgroundSubtractorMOG2) + conteo de
    contornos cuya area supera UMBRAL_AREA_VEHICULO dentro de cada ROI.
    No requiere credenciales ni conexion a internet; es exactamente lo
    que se pudo simular en Proteus con los sensores ultrasonicos (ver
    Seccion VI del reporte de la UA2), pero usando la camara real y
    pudiendo distinguir "cuantos" vehiculos en vez de solo "hay o no hay".

Deteccion con Azure Custom Vision (opcional, --backend-vision azure):
    Si el equipo entrena y publica un modelo de Custom Vision, este
    script puede llamarlo en vez del conteo local: ver la clase
    DetectorAzureCustomVision mas abajo. Requiere las variables de
    entorno AZURE_CV_ENDPOINT y AZURE_CV_PREDICTION_KEY (el endpoint y la
    prediction key que da el portal de Custom Vision al publicar el
    proyecto). Usa urllib de la libreria estandar, no requiere paquetes extra.
    Se deja pluggable a proposito para no bloquear la entrega de hoy en
    tener ya desplegados los recursos de Azure.

Uso:
    export CRUCE_API_KEY='<la misma llave de Dispositivos:ApiKey del backend>'
    python3 detectar_trafico.py \
        --backend-url http://<IP-DE-TU-PC>:5240/api/trafico \
        --camara 0 \
        --intervalo 2.0

    El backend solo acepta conteos con el encabezado X-Api-Key (la llave
    se pasa con --api-key o con la variable CRUCE_API_KEY).

    (cambia --backend-url por la IP real de quien corre el backend .NET,
    la misma que pones en Frontend/mobile/src/constants/api.ts)
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

import cv2
import numpy as np

NOMBRES_ACCESO = ["Norte", "Sur", "Este", "Oeste"]
UMBRAL_AREA_VEHICULO = 1500  # px^2 minimos para contar un contorno como vehiculo


class DetectorLocalOpenCV:
    """Conteo de vehiculos por resta de fondo, sin nube ni credenciales."""

    def __init__(self):
        self._subtractor = cv2.createBackgroundSubtractorMOG2(
            history=300, varThreshold=40, detectShadows=True
        )

    def contar(self, frame_bgr, rois):
        """rois: lista de 4 tuplas (x, y, w, h) en pixeles del frame."""
        mascara = self._subtractor.apply(frame_bgr)
        # quita sombras (valor 127 en MOG2) y ruido pequeno
        _, mascara = cv2.threshold(mascara, 200, 255, cv2.THRESH_BINARY)
        mascara = cv2.morphologyEx(mascara, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
        mascara = cv2.dilate(mascara, np.ones((5, 5), np.uint8), iterations=2)

        conteos = []
        for (x, y, w, h) in rois:
            recorte = mascara[y:y + h, x:x + w]
            contornos, _ = cv2.findContours(recorte, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
            vehiculos = sum(1 for c in contornos if cv2.contourArea(c) >= UMBRAL_AREA_VEHICULO)
            conteos.append(vehiculos)
        return conteos


class DetectorAzureCustomVision:
    """
    Conteo de vehiculos llamando a un proyecto de deteccion de objetos ya
    entrenado y publicado en Azure Custom Vision. Pluggable: mismo metodo
    contar(frame, rois) que el detector local, para no tocar el resto del
    script al cambiar de backend.
    """

    def __init__(self):
        self.endpoint = os.environ.get("AZURE_CV_ENDPOINT")
        self.prediction_key = os.environ.get("AZURE_CV_PREDICTION_KEY")
        if not self.endpoint or not self.prediction_key:
            raise RuntimeError(
                "Faltan las variables de entorno AZURE_CV_ENDPOINT y "
                "AZURE_CV_PREDICTION_KEY (se obtienen al publicar el "
                "proyecto de deteccion de objetos en Azure Custom Vision)."
            )

    def contar(self, frame_bgr, rois):
        ok, buf = cv2.imencode(".jpg", frame_bgr)
        if not ok:
            return [0, 0, 0, 0]

        req = urllib.request.Request(
            self.endpoint,
            data=buf.tobytes(),
            headers={
                "Content-Type": "application/octet-stream",
                "Prediction-Key": self.prediction_key,
            },
            method="POST",
        )
        try:
            with urllib.request.urlopen(req, timeout=5) as resp:
                prediccion = json.loads(resp.read().decode("utf-8"))
        except urllib.error.URLError as exc:
            print(f"[azure] error llamando a Custom Vision: {exc}", file=sys.stderr)
            return [0, 0, 0, 0]

        alto, ancho = frame_bgr.shape[:2]
        conteos = [0, 0, 0, 0]
        for obj in prediccion.get("predictions", []):
            if obj.get("probability", 0) < 0.5:
                continue
            bb = obj["boundingBox"]
            cx = (bb["left"] + bb["width"] / 2) * ancho
            cy = (bb["top"] + bb["height"] / 2) * alto
            for i, (x, y, w, h) in enumerate(rois):
                if x <= cx <= x + w and y <= cy <= y + h:
                    conteos[i] += 1
                    break
        return conteos


def calcular_rois(ancho, alto):
    """
    4 cuadrantes del frame, uno por acceso (Norte=arriba, Sur=abajo,
    Este=derecha, Oeste=izquierda). Ajustar aqui si la camara ve el
    cruce con otro encuadre.
    """
    mw, mh = ancho // 2, alto // 2
    return [
        (0, 0, ancho, mh),       # Norte: mitad superior
        (0, mh, ancho, alto - mh),  # Sur: mitad inferior
        (mw, 0, ancho - mw, alto),  # Este: mitad derecha
        (0, 0, mw, alto),        # Oeste: mitad izquierda
    ]


def enviar_conteo(backend_url, conteo, api_key):
    cuerpo = json.dumps({"vehiculos": conteo}).encode("utf-8")
    req = urllib.request.Request(
        backend_url,
        data=cuerpo,
        headers={"Content-Type": "application/json", "X-Api-Key": api_key},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=3) as resp:
            return resp.status
    except urllib.error.HTTPError as exc:
        if exc.code in (401, 403):
            print("[backend] llave rechazada (401/403): revisa --api-key / CRUCE_API_KEY "
                  "contra Dispositivos:ApiKey del backend", file=sys.stderr)
        else:
            print(f"[backend] el backend respondio {exc.code}", file=sys.stderr)
        return exc.code
    except urllib.error.URLError as exc:
        print(f"[backend] no se pudo enviar el conteo: {exc}", file=sys.stderr)
        return None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--backend-url", required=True,
                     help="URL completa del endpoint, p. ej. http://<IP-DE-TU-PC>:5240/api/trafico")
    ap.add_argument("--camara", default="0",
                     help="Indice de la camara para cv2.VideoCapture (default: 0)")
    ap.add_argument("--intervalo", type=float, default=2.0,
                     help="Segundos entre cada conteo enviado al backend (default: 2.0)")
    ap.add_argument("--backend-vision", choices=["local", "azure"], default="local",
                     help="local = resta de fondo con OpenCV (default, sin nube); "
                          "azure = Azure Custom Vision (requiere AZURE_CV_ENDPOINT y AZURE_CV_PREDICTION_KEY)")
    ap.add_argument("--api-key", default=os.environ.get("CRUCE_API_KEY", ""),
                     help="Llave de dispositivo (Dispositivos:ApiKey del backend); default: variable CRUCE_API_KEY")
    ap.add_argument("--mostrar-ventana", action="store_true",
                     help="Muestra una ventana con las 4 regiones y el conteo (solo con monitor conectado)")
    args = ap.parse_args()
    if not args.api_key:
        ap.error("falta la llave: usa --api-key o exporta CRUCE_API_KEY")

    camara_idx = int(args.camara) if args.camara.isdigit() else args.camara
    cap = cv2.VideoCapture(camara_idx)
    if not cap.isOpened():
        print(f"No se pudo abrir la camara '{args.camara}'.", file=sys.stderr)
        sys.exit(1)

    detector = DetectorAzureCustomVision() if args.backend_vision == "azure" else DetectorLocalOpenCV()
    print(f"[vision] backend de deteccion: {args.backend_vision}")
    print(f"[vision] enviando conteo cada {args.intervalo}s a {args.backend_url}")

    ultimo_envio = 0.0
    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                print("[vision] no se pudo leer un frame de la camara, reintentando...", file=sys.stderr)
                time.sleep(0.5)
                continue

            alto, ancho = frame.shape[:2]
            rois = calcular_rois(ancho, alto)

            ahora = time.time()
            if ahora - ultimo_envio >= args.intervalo:
                conteo = detector.contar(frame, rois)
                status = enviar_conteo(args.backend_url, conteo, args.api_key)
                resumen = ", ".join(f"{n}={c}" for n, c in zip(NOMBRES_ACCESO, conteo))
                print(f"[vision] conteo: {resumen}  ->  backend status={status}")
                ultimo_envio = ahora

            if args.mostrar_ventana:
                vista = frame.copy()
                for (x, y, w, h), nombre in zip(rois, NOMBRES_ACCESO):
                    cv2.rectangle(vista, (x, y), (x + w, y + h), (0, 255, 0), 2)
                    cv2.putText(vista, nombre, (x + 5, y + 20), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
                cv2.imshow("Cruce Inteligente - deteccion de trafico", vista)
                if cv2.waitKey(1) & 0xFF == ord("q"):
                    break
    except KeyboardInterrupt:
        pass
    finally:
        cap.release()
        if args.mostrar_ventana:
            cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
