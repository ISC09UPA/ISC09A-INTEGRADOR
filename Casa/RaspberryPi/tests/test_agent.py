import json
from datetime import timedelta
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from casa_pi.settings import Settings, load
from casa_pi.models import utcnow
from casa_pi.persistence.sqlite import Store
from casa_pi.hardware.simulated import SimulatedHardware
from casa_pi.control.access import Access
from casa_pi.control.commands import Commands
from casa_pi.control.lighting import automatic
from casa_pi.sensors.light import reading
from casa_pi.sensors.motion import Motion
from casa_pi.sensors.fingerprint import Fingerprint


class AgentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.settings = Settings(data_dir=self.temp.name)
        self.hardware = SimulatedHardware(self.directory)
        self.store = Store(self.directory / "agent.db", 10)
        self.access = Access(self.hardware, self.settings, trusted=lambda: True)
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)

    def tearDown(self):
        self.store.close()
        self.temp.cleanup()

    def command(self, **changes):
        now = utcnow()
        c = {"commandId": "test-one", "deviceId": "raspberry-sim", "type": "OpenDoor",
             "attemptId": "attempt-one", "issuedAtUtc": now.isoformat(),
             "expiresAtUtc": (now + timedelta(seconds=8)).isoformat(),
             "payload": {"doorId": "principal", "durationMs": 1500}}
        c.update(changes)
        return c

    def test_hysteresis(self):
        self.assertTrue(automatic(False, 20, 30, 60))
        self.assertTrue(automatic(True, 45, 30, 60))
        self.assertFalse(automatic(False, 45, 30, 60))
        self.assertFalse(automatic(True, 80, 30, 60))

    def test_invalid_sensor(self):
        for value in (True, -1, 101, float("nan"), float("inf"), "50"):
            with self.assertRaises(ValueError):
                reading(value)

    def test_motion_edge(self):
        m = Motion()
        self.assertEqual([m.update(v) for v in (False, True, True, False, True)],
                         [False, True, False, False, True])

    def test_fingerprint_requests_never_open(self):
        f = Fingerprint()
        self.assertEqual(f.update("demo")["type"], "AccessRequested")
        self.assertIsNone(f.update("demo"))
        self.assertEqual(self.hardware.outputs["door_pulses"], 0)

    def test_unauthenticated_rejected(self):
        self.assertEqual(self.commands.apply(self.command())["status"], "rejected")
        self.assertEqual(self.hardware.outputs["door_pulses"], 0)

    def test_duplicate_does_not_open_twice(self):
        c = self.command()
        self.assertEqual(self.commands.apply(c, True)["status"], "executed")
        self.commands.apply(c, True)
        self.assertEqual(self.hardware.outputs["door_pulses"], 1)
        self.access.deadline = 0
        self.access.tick()
        self.assertFalse(self.hardware.outputs["door_pulse_active"])

    def test_reject_changed_duplicate(self):
        c = self.command()
        self.commands.apply(c, True)
        c["payload"]["durationMs"] = 2000
        self.assertEqual(self.commands.apply(c, True)["status"], "rejected")
        self.assertEqual(self.hardware.outputs["door_pulses"], 1)

    def test_expired_future_wrong_target_and_excess_duration(self):
        cases = [self.command(expiresAtUtc=(utcnow() - timedelta(seconds=1)).isoformat()),
                 self.command(issuedAtUtc=(utcnow() + timedelta(seconds=1)).isoformat()),
                 self.command(deviceId="another-device"),
                 self.command(payload={"doorId": "principal", "durationMs": 9999})]
        for c in cases:
            self.assertEqual(self.commands.apply(c, True)["status"], "rejected")
        self.assertEqual(self.hardware.outputs["door_pulses"], 0)

    def test_untrusted_clock(self):
        self.access.trusted = lambda: False
        self.assertEqual(self.commands.apply(self.command(), True)["status"], "rejected")

    def test_does_not_extend_active_opening(self):
        self.commands.apply(self.command(), True)
        deadline = self.access.deadline
        self.assertEqual(self.commands.apply(self.command(commandId="another"), True)["status"], "rejected")
        self.assertEqual(self.access.deadline, deadline)

    def test_restart_does_not_repeat_effect(self):
        c = self.command()
        self.store.claim(c)
        self.store.close()
        self.store = Store(self.directory / "agent.db")
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)
        self.assertEqual(self.commands.apply(c, True)["status"], "unknown")
        self.assertEqual(self.hardware.outputs["door_pulses"], 0)

    def test_manual_light_and_security_persist(self):
        self.commands.apply(self.command(type="SetLight", payload={"on": True}), True)
        self.commands.apply(self.command(commandId="arm", type="SetSecurityMode", payload={"mode": "armed"}), True)
        self.assertTrue(self.store.get("light_on"))
        self.assertEqual(self.store.get("lighting_mode"), "manual")
        self.assertTrue(self.store.get("armed"))

    def test_bounded_queue_prefers_incidents(self):
        self.store.emit({"type": "MotionDetected"})
        for n in range(20):
            self.store.emit({"type": "TelemetryReported", "n": n})
        events = self.store.pending()
        self.assertEqual(len(events), 10)
        self.assertEqual(events[0][1]["type"], "MotionDetected")

    def test_bad_configuration(self):
        with self.assertRaises(ValueError):
            Settings(light_on_below=80, light_off_above=20)
        with self.assertRaises(ValueError):
            Settings(max_open_ms=True)


if __name__ == "__main__":
    unittest.main()
