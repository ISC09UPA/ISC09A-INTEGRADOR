"""Envío opcional de telemetría; no contiene credenciales."""
import json
import os
import queue
import threading
from pathlib import Path


class TelemetriaAzure:
    def __init__(self, cadena=None):
        self.cadena = cadena or os.environ.get('IOTHUB_DEVICE_CONNECTION_STRING')
        if not self.cadena:
            archivo = Path(__file__).resolve().parent / '.iot_device_connection'
            if archivo.is_file():
                self.cadena = archivo.read_text(encoding='utf-8').strip()
        if not self.cadena:
            raise ValueError('Falta IOTHUB_DEVICE_CONNECTION_STRING del dispositivo IoT Hub.')
        try:
            from azure.iot.device import IoTHubDeviceClient, Message
        except ImportError as exc:
            raise RuntimeError('Instala azure-iot-device en el entorno de Python.') from exc
        self.crear_cliente = IoTHubDeviceClient.create_from_connection_string
        self.message = Message
        self.cola = queue.Queue(maxsize=50)
        self.hilo = threading.Thread(target=self._enviar, daemon=True)
        self.hilo.start()

    def publicar(self, datos):
        try:
            self.cola.put_nowait(datos)
        except queue.Full:
            print('\nAzure: cola llena; se descartó una medición.', flush=True)

    def _enviar(self):
        cliente = None
        while True:
            datos = self.cola.get()
            if datos is None:
                break
            try:
                if cliente is None:
                    cliente = self.crear_cliente(self.cadena)
                mensaje = self.message(json.dumps(datos, ensure_ascii=False))
                mensaje.content_encoding = 'utf-8'
                mensaje.content_type = 'application/json'
                cliente.send_message(mensaje)
                print('\nAzure: telemetría enviada.', flush=True)
            except Exception:
                # No mostrar la excepción: puede contener datos de conexión.
                print('\nAzure: no se pudo enviar; se descartó la medición. Se reintentará con la siguiente.', flush=True)
                if cliente is not None:
                    try:
                        cliente.shutdown()
                    except Exception:
                        pass
                cliente = None
        if cliente is not None:
            try:
                cliente.shutdown()
            except Exception:
                pass

    def cerrar(self):
        # No bloquear la salida ni el control local si la red no responde.
        try:
            self.cola.put_nowait(None)
        except queue.Full:
            pass
