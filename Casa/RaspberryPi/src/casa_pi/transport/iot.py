"""Network I/O runs in a worker; hardware and SQLite stay in the main thread."""
import json
import logging
import os
from queue import Empty, Full, Queue
from threading import Event, Lock, Thread


class LocalTransport:
    def start(self): pass
    def flush(self, store): pass
    def report(self, state): pass
    def status(self): return {"connected": False, "mode": "local"}
    def close(self): pass


class AzureTransport:
    METHODS = {"SetLight", "SetSecurityMode", "SetLightingMode", "OpenDoor"}

    def __init__(self, inbound, desired, client=None, response_factory=None):
        if client is None:
            from azure.iot.device import IoTHubDeviceClient, MethodResponse
            connection = os.environ.get("IOTHUB_DEVICE_CONNECTION_STRING")
            if not connection:
                raise RuntimeError("Falta credencial del dispositivo IoT Hub")
            client = IoTHubDeviceClient.create_from_connection_string(connection)
            response_factory = MethodResponse.create_from_method_request
        self.client, self.response_factory = client, response_factory
        self.inbound, self.desired = inbound, desired
        self.outbound, self.acknowledged = Queue(maxsize=20), Queue()
        self.inflight = set()
        self.stop = Event()
        self.lock = Lock()
        self.reported = None
        self.connected = False
        self.worker = None

    def receive_method(self, request):
        body = request.payload
        valid = (request.name in self.METHODS and isinstance(body, dict)
                 and body.get("type") == request.name and len(json.dumps(body)) <= 16384)
        if valid:
            response = self.inbound(body)
            code = 202 if response.get("status") == "pending" else 429
        else:
            response, code = {"status": "rejected", "reason": "Metodo o payload invalido"}, 400
        self.client.send_method_response(self.response_factory(request, code, response))

    def receive_desired(self, patch):
        if isinstance(patch, dict) and "agentConfiguration" in patch:
            self.desired(patch)

    def start(self):
        self.worker = Thread(target=self._run, name="iot-worker", daemon=True)
        self.worker.start()

    def _run(self):
        current, last_reported = None, None
        backoff, needs_twin = 2, True
        handlers_ready = False
        try:
            while not self.stop.is_set():
                try:
                    # These SDK setters subscribe and can connect to the network.
                    # They must also run off the hardware thread and be retried.
                    if not handlers_ready:
                        self.client.on_method_request_received = self.receive_method
                        self.client.on_twin_desired_properties_patch_received = self.receive_desired
                        handlers_ready = True
                    if not self.client.connected:
                        self.client.connect()
                        needs_twin = True
                    if needs_twin:
                        self.receive_desired(self.client.get_twin().get("desired", {}))
                        needs_twin, last_reported = False, None
                    self.connected = True
                    with self.lock:
                        state = self.reported
                    if state is not None and state != last_reported:
                        self.client.patch_twin_reported_properties({"casaPi": state})
                        last_reported = state
                    if current is None:
                        try:
                            current = self.outbound.get(timeout=0.2)
                        except Empty:
                            continue
                    seq, body = current
                    from azure.iot.device import Message
                    message = Message(json.dumps(body))
                    message.content_type, message.content_encoding = "application/json", "utf-8"
                    self.client.send_message(message)
                    self.acknowledged.put(seq)
                    current, backoff = None, 2
                except Exception as exc:
                    self.connected = False
                    needs_twin = True
                    logging.warning("IoT no disponible (%s); reintento en %s s", type(exc).__name__, backoff)
                    if self.stop.wait(backoff): break
                    backoff = min(30, backoff * 2)
        finally:
            self.connected = False
            try: self.client.shutdown()
            except Exception: pass

    def flush(self, store):
        while True:
            try: seq = self.acknowledged.get_nowait()
            except Empty: break
            store.delivered(seq)
            self.inflight.discard(seq)
        for seq, body in store.pending():
            if seq in self.inflight: continue
            try: self.outbound.put_nowait((seq, body))
            except Full: break
            self.inflight.add(seq)

    def report(self, state):
        with self.lock:
            self.reported = state

    def status(self):
        return {"connected": self.connected, "mode": "azure", "queued": len(self.inflight)}

    def close(self):
        self.stop.set()
        if self.worker is not None: self.worker.join(timeout=2)
