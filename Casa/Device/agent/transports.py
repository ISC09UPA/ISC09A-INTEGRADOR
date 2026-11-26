"""Transportes de eventos del agente.

- `LocalTransport`: entrega por HTTP a un doble local de Home (desarrollo y pruebas).
- `IotTransport`: entrega por Azure IoT Hub (producción), importado de forma diferida.
"""
from __future__ import annotations

import json
import logging
import urllib.error
import urllib.request
from typing import Any


class LocalTransport:
    def __init__(self, url: str, logger: logging.Logger | None = None, timeout: float = 5.0):
        self._url = url
        self._timeout = timeout
        self._log = logger or logging.getLogger("transport")

    def deliver(self, event: dict[str, Any]) -> bool:
        if not self._url:
            self._log.debug("Sin URL local configurada; el evento queda en outbox.")
            return False
        data = json.dumps(event, separators=(",", ":")).encode()
        request = urllib.request.Request(
            self._url, data=data, headers={"Content-Type": "application/json"}, method="POST"
        )
        try:
            with urllib.request.urlopen(request, timeout=self._timeout) as response:
                return 200 <= response.status < 300
        except urllib.error.HTTPError as error:
            self._log.warning("El doble rechazó un evento con HTTP %s", error.code)
            return False
        except Exception as error:  # noqa: BLE001
            self._log.debug("Entrega local fallida: %s", error)
            return False


class IotTransport:
    def __init__(self, connection_string: str, logger: logging.Logger | None = None):
        self._connection_string = connection_string
        self._log = logger or logging.getLogger("iot")
        self._client = None

    def connect(self):
        if self._client is not None:
            return self._client
        from azure.iot.device import IoTHubDeviceClient  # import diferido

        if not self._connection_string:
            raise RuntimeError("CASA_IOT_CONNECTION_STRING no está configurada.")
        self._client = IoTHubDeviceClient.create_from_connection_string(
            self._connection_string, websockets=False
        )
        self._client.connect()
        self._log.info("Conectado a IoT Hub")
        return self._client

    def on_method(self, callback) -> None:
        client = self.connect()
        client.on_method_request_received = callback

    def deliver(self, event: dict[str, Any]) -> bool:
        from azure.iot.device import Message

        try:
            client = self.connect()
            message = Message(json.dumps(event, separators=(",", ":")))
            message.content_encoding = "utf-8"
            message.content_type = "application/json"
            client.send_message(message)
            return True
        except Exception as error:  # noqa: BLE001
            self._log.warning("Envío a IoT Hub fallido: %s", error)
            return False

    def disconnect(self) -> None:
        if self._client is not None:
            try:
                self._client.shutdown()
            except Exception:  # noqa: BLE001
                pass
            self._client = None
