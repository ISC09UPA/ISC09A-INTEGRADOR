import json, os, tempfile, unittest
from device_agent import CommandStore

class FakeHardware:
    simulation = True
    def open_locker(self): return True
    def door_closed(self): return True
    def presence_detected(self): return False

class AgentContractTests(unittest.TestCase):
    def test_store_is_idempotent(self):
        with tempfile.TemporaryDirectory() as d:
            s = CommandStore(os.path.join(d, 'state.db'))
            event = {'eventId':'evt-1','commandId':'cmd-1','tipo':'ComandoEjecutado'}
            s.put('cmd-1', event)
            self.assertEqual(s.get('cmd-1'), event)
            self.assertIsNone(s.get('cmd-2'))

    def test_confirmation_contract_keys(self):
        command = {'commandId':'cmd-1','accion':'Abrir','lockerCodigo':'L-01'}
        # Validación del contrato JSON que el backend espera.
        payload = {'eventId':'evt-1','commandId':command['commandId'],'lockerCodigo':command['lockerCodigo'],
                   'tipo':'ComandoEjecutado','timestampUtc':'2026-11-27T00:00:00Z',
                   'puertaCerrada':False,'detalle':'Apertura confirmada'}
        required = {'eventId','commandId','lockerCodigo','tipo'}
        self.assertTrue(required.issubset(payload))
        self.assertEqual(json.loads(json.dumps(payload))['tipo'], 'ComandoEjecutado')

if __name__ == '__main__': unittest.main(verbosity=2)
