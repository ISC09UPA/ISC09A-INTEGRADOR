"""Publica un evento de prueba en locker-events. No acciona hardware."""
import json, os, uuid
from datetime import datetime, timezone
from dotenv import load_dotenv
from azure.servicebus import ServiceBusClient, ServiceBusMessage
load_dotenv()
conn=os.getenv('SERVICE_BUS_CONNECTION_STRING','')
if not conn or 'REEMPLAZAR' in conn: raise SystemExit('Configura SERVICE_BUS_CONNECTION_STRING en .env')
code=os.getenv('LOCKER_CODE','L-01')
event={'eventId':str(uuid.uuid4()),'lockerCodigo':code,'tipo':'PresenciaDetectada',
       'timestampUtc':datetime.now(timezone.utc).isoformat().replace('+00:00','Z'),
       'presenciaDetectada':False,'puertaCerrada':True,'detalle':'Evento de diagnóstico; no representa una devolución'}
with ServiceBusClient.from_connection_string(conn) as client:
    with client.get_queue_sender(queue_name=os.getenv('EVENTS_QUEUE','locker-events')) as sender:
        sender.send_messages(ServiceBusMessage(json.dumps(event),content_type='application/json'))
print('Evento de diagnóstico publicado:', event['eventId'])
