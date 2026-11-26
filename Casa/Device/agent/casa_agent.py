#!/usr/bin/env python3
"""Punto de entrada del agente.

Modos:
  local  → API HTTP local para el doble de Home (desarrollo/pruebas).
  iot    → Azure IoT Hub con métodos directos y mensajes D2C.

Uso:
  python3 Device/agent/casa_agent.py --transport local
  CASA_IOT_CONNECTION_STRING=... python3 Device/agent/casa_agent.py --transport iot
"""
from __future__ import annotations

import argparse
import dataclasses
import json
import logging
import signal
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from agent import DeviceAgent  # noqa: E402
from camera import Camera  # noqa: E402
from config import Config  # noqa: E402
from serial_link import SerialLink  # noqa: E402
from store import Store  # noqa: E402
from transports import IotTransport, LocalTransport  # noqa: E402


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Agente del dispositivo Casa Domótica")
    parser.add_argument("--transport", choices=("local", "iot"), default=None)
    parser.add_argument("--serial-port", default=None)
    parser.add_argument("--db", default=None)
    parser.add_argument("--port", type=int, default=None)
    parser.add_argument("--host", default=None)
    parser.add_argument("--report-interval", type=float, default=None)
    parser.add_argument("--log-level", default="INFO")
    return parser


class LocalApi(BaseHTTPRequestHandler):
    agent: DeviceAgent
    serial_link: SerialLink

    def _send_json(self, status: int, payload: dict) -> None:
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:  # noqa: N802 - API de http.server
        if self.path == "/health":
            self._send_json(
                200,
                {
                    "status": "ok",
                    "serial": self.serial_link.available,
                    "device": self.agent.state(),
                    "sensors": self.serial_link.status or {},
                },
            )
            return
        if self.path == "/state":
            self._send_json(200, self.agent.state())
            return
        self._send_json(404, {"error": "notFound"})

    def do_POST(self) -> None:  # noqa: N802 - API de http.server
        if not self.path.startswith("/local/method/"):
            self._send_json(404, {"error": "notFound"})
            return
        name = self.path.rsplit("/", 1)[-1]
        length = int(self.headers.get("Content-Length", "0") or 0)
        raw = self.rfile.read(length) if length else b""
        try:
            body = json.loads(raw) if raw else None
        except json.JSONDecodeError:
            body = None
        status = self.agent.handle_method(name, body)
        self.send_response(status)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def log_message(self, fmt: str, *args) -> None:  # noqa: A003
        logging.getLogger("http").debug(fmt, *args)


def start_local_api(agent: DeviceAgent, serial_link: SerialLink, host: str, port: int) -> ThreadingHTTPServer:
    handler = type("BoundLocalApi", (LocalApi,), {"agent": agent, "serial_link": serial_link})
    server = ThreadingHTTPServer((host, port), handler)
    server.daemon_threads = True
    threading.Thread(target=server.serve_forever, name="local-api", daemon=True).start()
    logging.getLogger("http").info("API local escuchando en http://%s:%s", host, port)
    return server


def connect_iot(agent: DeviceAgent, transport: IotTransport) -> None:
    from azure.iot.device import MethodResponse

    client = transport.connect()

    def on_method(request):
        try:
            body = json.loads(request.payload)
        except Exception:  # noqa: BLE001
            body = None
        status = agent.handle_method(request.name, body)
        client.send_method_response(MethodResponse.create_from_method_request(request, status))

    transport.on_method(on_method)
    logging.getLogger("iot").info("Métodos directos registrados")


def main() -> int:
    options = build_parser().parse_args()
    logging.basicConfig(
        level=getattr(logging, options.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    config = Config.from_env()
    overrides = {}
    if options.transport:
        overrides["transport"] = options.transport
    if options.serial_port:
        overrides["serial_port"] = options.serial_port
    if options.db:
        overrides["db_path"] = options.db
    if options.host:
        overrides["local_host"] = options.host
    if options.port:
        overrides["local_port"] = options.port
    if options.report_interval:
        overrides["report_interval_s"] = options.report_interval
    config = dataclasses.replace(config, **overrides)

    Path(config.db_path).parent.mkdir(parents=True, exist_ok=True)
    store = Store(config.db_path)
    serial_link = SerialLink(config.serial_port, config.serial_baud)
    camera = Camera(config.camera_backend, config.camera_device)
    transport = LocalTransport(config.local_home_url) if config.transport == "local" else IotTransport(config.iot_connection_string)

    agent = DeviceAgent(config, store, serial_link, transport, camera)
    agent.start()

    server = None
    if config.transport == "local":
        server = start_local_api(agent, serial_link, config.local_host, config.local_port)
    else:
        connect_iot(agent, transport)

    stop = threading.Event()

    def handle_signal(signum, frame):  # noqa: ANN001
        logging.getLogger("main").info("Señal %s; cerrando.", signum)
        stop.set()

    signal.signal(signal.SIGINT, handle_signal)
    signal.signal(signal.SIGTERM, handle_signal)
    stop.wait()

    if server is not None:
        server.shutdown()
    agent.stop()
    return 0


if __name__ == "__main__":
    sys.exit(main())
