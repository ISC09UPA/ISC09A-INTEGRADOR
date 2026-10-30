"""
Agente Edge para Carril de Estacionamiento Inteligente de Aeropuerto
Hardware Objetivo: Raspberry Pi 5 con SSD NVMe industrial y UPS HAT
"""

import sys
import os
import json
import time
import logging
from typing import Optional

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] [%(name)s] %(message)s",
    handlers=[logging.StreamHandler(sys.stdout)]
)
logger = logging.getLogger("AirportEdgeAgent")

class EdgeLaneStateMachine:
    """Máquina de estados finita del carril (Reposo -> Captura -> Reconocimiento -> Decisión -> Apertura)"""
    
    def __init__(self, config_path: str = "config.example.json"):
        self.config = self._load_config(config_path)
        self.lane_id = self.config.get("laneId", "entry-01")
        self.state = "IDLE"
        logger.info(f"Inicializando Agente Edge en carril: {self.lane_id} - Estado: {self.state}")

    def _load_config(self, path: str) -> dict:
        if os.path.exists(path):
            with open(path, "r", encoding="utf-8") as f:
                return json.load(f)
        logger.warning(f"Archivo de configuración {path} no encontrado, usando defaults.")
        return {"laneId": "entry-01", "laneType": "ENTRY"}

    def on_presence_detected(self):
        """Disparado por el lazo inductivo al detectar masa metálica vehicular"""
        logger.info("Presencia vehicular detectada por sensor inductivo. Iniciando ráfaga de captura...")
        self.state = "CAPTURING"
        self._capture_and_process()

    def _capture_and_process(self):
        """Captura ráfaga, recorta placa con YOLO y ejecuta OCR"""
        self.state = "RECOGNIZING"
        logger.info("Ejecutando recorte con modelo YOLO ONNX y OCR normalizador...")
        # Simulación de lectura edge en < 500ms
        sample_plate = "ABC123D"
        confidence = 0.96
        
        self.state = "DECIDING"
        logger.info(f"Placa leída: {sample_plate} (Confianza: {confidence:.2f}). Evaluando acceso...")
        self._dispatch_decision(sample_plate, confidence)

    def _dispatch_decision(self, plate: str, confidence: float):
        """Comunica al Access Service vía HTTPS / mTLS"""
        logger.info(f"Decisión recibida: 'ALLOW'. Activando relé de pluma...")
        self.state = "OPENING"
        self._trigger_barrier_relay()

    def _trigger_barrier_relay(self):
        """Conmuta el relé de contacto seco hacia el controlador de la pluma"""
        logger.info("Pulso de contacto seco enviado (duración: 500 ms). Pluma abriendo.")
        time.sleep(0.5)
        self.state = "IDLE"
        logger.info("Ciclo de acceso completado. Carril listo en reposo (IDLE).")

if __name__ == "__main__":
    agent = EdgeLaneStateMachine()
    logger.info("Agente Edge iniciado en modo autónomo. Esperando eventos de hardware...")
    # Prueba de ciclo
    agent.on_presence_detected()
