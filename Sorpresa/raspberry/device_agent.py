"""Agente LockerSmart: recibe comandos y publica confirmaciones en Azure Service Bus."""
import json, os, sqlite3, time, uuid, logging
from datetime import datetime, timezone
from hardware import LockerHardware, HardwareError
try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass
logging.basicConfig(level=os.getenv("LOG_LEVEL", "INFO"), format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("lockersmart-device")

class CommandStore:
    def __init__(self, path):
        self.db = sqlite3.connect(path)
        self.db.execute("CREATE TABLE IF NOT EXISTS commands (command_id TEXT PRIMARY KEY, event_json TEXT NOT NULL)")
        self.db.commit()
    def get(self, command_id):
        row = self.db.execute("SELECT event_json FROM commands WHERE command_id=?", (command_id,)).fetchone()
        return json.loads(row[0]) if row else None
    def put(self, command_id, event):
        self.db.execute("INSERT OR REPLACE INTO commands VALUES (?,?)", (command_id, json.dumps(event, ensure_ascii=False)))
        self.db.commit()

class DeviceAgent:
    def __init__(self):
        conn = os.getenv("SERVICE_BUS_CONNECTION_STRING", "")
        if not conn or "REEMPLAZAR" in conn:
            raise RuntimeError("Configura SERVICE_BUS_CONNECTION_STRING en .env")
        try:
            from azure.servicebus import ServiceBusClient
        except ImportError as exc:
            raise RuntimeError("Instala dependencias: pip install -r requirements.txt") from exc
        self.ServiceBusMessage = __import__("azure.servicebus", fromlist=["ServiceBusMessage"]).ServiceBusMessage
        try:
            from azure.servicebus import ServiceBusClient, ServiceBusMessage
        except ImportError as exc:
            raise RuntimeError("Instala dependencias: pip install -r requirements.txt") from exc
        self.ServiceBusMessage = ServiceBusMessage
        self.client = ServiceBusClient.from_connection_string(conn)
        self.receiver = self.client.get_queue_receiver(queue_name=os.getenv("COMMANDS_QUEUE", "locker-commands"), max_wait_time=5)
        self.sender = self.client.get_queue_sender(queue_name=os.getenv("EVENTS_QUEUE", "locker-events"))
        self.code = os.getenv("LOCKER_CODE", "L-01")
        self.hw = LockerHardware()
        self.store = CommandStore(os.getenv("DATABASE_PATH", "./device_state.sqlite3"))

    @staticmethod
    def now(): return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

    def make_event(self, command, success, detail):
        return {
            "eventId": str(uuid.uuid4()), "commandId": command["commandId"],
            "lockerCodigo": self.code,
            "tipo": "ComandoEjecutado" if success else "ComandoRechazado",
            "timestampUtc": self.now(), "puertaCerrada": self.hw.door_closed(),
            "presenciaDetectada": self.hw.presence_detected(),
            "detalle": detail
        }

    def publish(self, event):
        self.sender.send_messages(self.self.ServiceBusMessage(json.dumps(event, ensure_ascii=False), content_type="application/json"))

    def handle(self, message):
        try:
            command = json.loads(str(message))
            required = ("commandId", "accion", "lockerCodigo")
            if any(not command.get(k) for k in required):
                raise ValueError("Comando incompleto: requiere commandId, accion y lockerCodigo")
            if command["lockerCodigo"] != self.code:
                raise ValueError(f"El comando corresponde a {command['lockerCodigo']}, no a {self.code}")
            cid = command["commandId"]
            previous = self.store.get(cid)
            if previous:
                log.warning("Comando duplicado %s: se reenvía confirmación almacenada", cid)
                self.publish(previous)
                return
            if command["accion"].lower() != "abrir":
                event = self.make_event(command, False, f"Acción no soportada: {command['accion']}")
            else:
                try:
                    self.hw.open_locker()
                    event = self.make_event(command, True, "Apertura confirmada por el agente" if not self.hw.simulation else "SIMULACIÓN: no se accionó hardware físico")
                except HardwareError as exc:
                    event = self.make_event(command, False, str(exc))
            # Guardar antes de publicar evita repetir la activación si se redelivery el mensaje.
            self.store.put(cid, event)
            self.publish(event)
        except Exception:
            log.exception("No se pudo procesar el mensaje; se abandonará para reintento")
            raise

    def run(self):
        log.info("Agente activo. locker=%s simulation=%s", self.code, self.hw.simulation)
        try:
            while True:
                for msg in self.receiver.receive_messages(max_message_count=5, max_wait_time=5):
                    try:
                        self.handle(msg)
                        self.receiver.complete_message(msg)
                    except Exception:
                        self.receiver.abandon_message(msg)
                time.sleep(0.1)
        finally:
            self.hw.close(); self.receiver.close(); self.sender.close(); self.client.close()

if __name__ == "__main__":
    DeviceAgent().run()
