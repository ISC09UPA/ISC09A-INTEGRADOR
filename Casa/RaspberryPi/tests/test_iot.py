import tempfile
from pathlib import Path
from queue import Full
from types import SimpleNamespace
import unittest
from unittest.mock import Mock
from unittest.mock import patch
from threading import Event
import time

from casa_pi.control.configuration import apply_configuration
from casa_pi.persistence.sqlite import Store
from casa_pi.settings import Settings
from casa_pi.transport.iot import AzureTransport


class IoTTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.store = Store(Path(self.temp.name) / "agent.db")
        self.settings = Settings()
        self.client = Mock()
        self.transport = AzureTransport(lambda body: {"status": "pending"}, Mock(),
            client=self.client, response_factory=lambda req, code, body: (code, body))

    def tearDown(self):
        self.store.close()
        self.temp.cleanup()

    def test_flush_never_calls_network(self):
        self.store.emit({"type": "TelemetryReported"})
        self.transport.flush(self.store)
        self.client.send_message.assert_not_called()
        self.assertEqual(len(self.transport.inflight), 1)
        self.assertEqual(len(self.store.pending()), 1)

    def test_only_acknowledged_events_mark_delivered(self):
        self.store.emit({"type": "MotionDetected"})
        self.transport.flush(self.store)
        seq, body = self.transport.outbound.get_nowait()
        self.transport.acknowledged.put(seq)
        self.transport.flush(self.store)
        self.assertEqual(self.store.pending(), [])
        self.assertEqual(self.transport.inflight, set())

    def test_no_duplicate_inflight_enqueue(self):
        self.store.emit({"type": "TelemetryReported"})
        self.transport.flush(self.store)
        self.transport.flush(self.store)
        self.assertEqual(self.transport.outbound.qsize(), 1)

    def test_method_acceptance_is_not_execution(self):
        request = SimpleNamespace(name="SetLight", payload={"type": "SetLight"})
        self.transport.receive_method(request)
        self.assertEqual(self.client.send_method_response.call_args.args[0], (202, {"status": "pending"}))

    def test_bad_method_and_full_queue_rejected(self):
        request = SimpleNamespace(name="OpenDoor", payload={"type": "SetLight"})
        self.transport.receive_method(request)
        self.assertEqual(self.client.send_method_response.call_args.args[0][0], 400)
        self.transport.inbound = lambda body: {"status": "rejected"}
        self.transport.receive_method(SimpleNamespace(name="SetLight", payload={"type": "SetLight"}))
        self.assertEqual(self.client.send_method_response.call_args.args[0][0], 429)

    def test_desired_valid_partial_and_stale(self):
        result = apply_configuration(self.store, {"$version": 3, "agentConfiguration": {"lightOnBelow": 25}}, self.settings)
        self.assertEqual(result["appliedVersion"], 3)
        result = apply_configuration(self.store, {"$version": 2, "agentConfiguration": {"lightOnBelow": 20}}, self.settings)
        self.assertEqual(result["values"]["lightOnBelow"], 25)
        # Legacy lightingMode from twin must be ignored; lighting_mode only
        # changes through SetLightingMode commands.
        result = apply_configuration(self.store, {"$version": 4, "agentConfiguration": {"lightingMode": "manual"}}, self.settings)
        self.assertIsNone(self.store.get("lighting_mode"))
        self.assertEqual(result["values"]["lightOnBelow"], 25)

    def test_bad_desired_keeps_applied_configuration(self):
        apply_configuration(self.store, {"$version": 1, "agentConfiguration": {"lightOnBelow": 25}}, self.settings)
        result = apply_configuration(self.store, {"$version": 2, "agentConfiguration": {"lightOffAbove": 10}}, self.settings)
        self.assertEqual(result["appliedVersion"], 1)
        self.assertEqual(result["observedVersion"], 2)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["values"]["lightOffAbove"], 60)

    def test_desired_cannot_open_door(self):
        result = apply_configuration(self.store, {"$version": 1, "agentConfiguration": {"OpenDoor": True}}, self.settings)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["appliedVersion"], 0)

    def test_blocked_network_does_not_block_main_thread(self):
        entered, release = Event(), Event()
        self.client.connected = True
        self.client.get_twin.return_value = {"desired": {}}

        def slow_send(message):
            entered.set()
            release.wait(3)

        self.client.send_message.side_effect = slow_send
        self.store.emit({"type": "TelemetryReported"})
        fake_sdk = SimpleNamespace(Message=lambda value: SimpleNamespace(body=value))
        with patch.dict("sys.modules", {"azure.iot.device": fake_sdk}):
            try:
                self.transport.start()
                self.transport.flush(self.store)
                self.assertTrue(entered.wait(2))
                started = time.monotonic()
                self.transport.flush(self.store)
                self.transport.report({"lightOn": True})
                self.assertLess(time.monotonic() - started, 0.1)
                self.assertEqual(len(self.store.pending()), 1)
            finally:
                release.set()
                self.transport.close()

    def test_sdk_handler_subscription_cannot_block_startup(self):
        entered, release = Event(), Event()

        class SubscriptionClient:
            connected = True

            @property
            def on_method_request_received(self):
                return None

            @on_method_request_received.setter
            def on_method_request_received(self, value):
                entered.set()
                release.wait(3)

            def get_twin(self):
                return {"desired": {}}

            def shutdown(self):
                pass

        transport = AzureTransport(Mock(), Mock(), client=SubscriptionClient())
        try:
            started = time.monotonic()
            transport.start()
            self.assertLess(time.monotonic() - started, 0.1)
            self.assertTrue(entered.wait(2))
            transport.flush(self.store)
        finally:
            release.set()
            transport.close()

    def test_receive_method_does_not_mutate_payload(self):
        """The SDK worker must forward the exact command body to the agent's
        inbound queue; it must not inject delivery metadata into the wire JSON."""
        received = []

        def capture_inbound(body):
            received.append(body)
            return {"status": "pending"}

        transport = AzureTransport(capture_inbound, Mock(), client=self.client,
                                   response_factory=lambda req, code, body: (code, body))
        original = {"type": "SetLight", "commandId": "cmd-callback", "payload": {"on": True}}
        request = SimpleNamespace(name="SetLight", payload=original)
        transport.receive_method(request)

        self.assertEqual(len(received), 1)
        self.assertEqual(received[0], original)
        self.assertNotIn("correlationId", received[0])
        code, response = self.client.send_method_response.call_args.args[0]
        self.assertEqual(code, 202)
        self.assertEqual(response["status"], "pending")
