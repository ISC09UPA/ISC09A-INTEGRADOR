import json
import os
import sqlite3
import tempfile
import threading
import time
import unittest
from datetime import timedelta
from pathlib import Path
from queue import Queue, Full
from unittest.mock import patch

from casa_pi.settings import Settings
from casa_pi.models import utcnow, set_utcnow
from casa_pi.persistence.sqlite import Store
from casa_pi.hardware.simulated import SimulatedHardware
from casa_pi.control.access import Access
from casa_pi.control.commands import Commands

from schema_helper import validate


class LightCommandTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.settings = Settings(data_dir=self.temp.name)
        self.hardware = SimulatedHardware(self.directory)
        self.store = Store(self.directory / "agent.db", 100)
        self.access = Access(self.hardware, self.settings, trusted=lambda: True)
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)
        self._reset_clock()

    def tearDown(self):
        self.store.close()
        self.temp.cleanup()

    def _reset_clock(self):
        self.now = utcnow()
        set_utcnow(lambda: self.now)

    def _advance(self, seconds):
        self.now += timedelta(seconds=seconds)

    def command(self, **changes):
        now = utcnow()
        c = {
            "commandId": "cmd-SetLight-on-001",
            "deviceId": "raspberry-sim",
            "type": "SetLight",
            "commandSequence": 1,
            "issuedAtUtc": now.isoformat(),
            "expiresAtUtc": (now + timedelta(seconds=8)).isoformat(),
            "payload": {"on": True},
        }
        c.update(changes)
        return c

    def test_set_light_applied_with_snapshot(self):
        c = self.command()
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "executed")
        self.assertEqual(result["type"], "SetLight")
        self.assertEqual(result["commandId"], c["commandId"])
        self.assertEqual(result["reasonCode"], None)
        self.assertIn("snapshot", result)
        snap = result["snapshot"]
        self.assertEqual(snap["lightOn"], True)
        self.assertEqual(snap["lightingMode"], "manual")
        self.assertEqual(snap["source"], "simulated")
        self.assertIn("appliedAtUtc", snap)
        self.assertIsInstance(result["stateRevision"], int)
        self.assertTrue(self.store.get("light_on"))
        self.assertEqual(self.store.get("lighting_mode"), "manual")
        self.assertEqual(self.hardware.outputs["light_on"], True)

    def test_command_result_validates_against_schema(self):
        c = self.command()
        result = self.commands.apply(c, trusted=True)
        # The emitted event (durable) is the authoritative shape.
        events = self.store.pending()
        self.assertEqual(len(events), 1)
        envelope = events[0][1]
        self.assertEqual(envelope["type"], "CommandResult")
        # correlationId is a UUID that ties command -> result -> event.
        self.assertEqual(envelope["correlationId"], result["correlationId"])
        validate(envelope, "telemetry-envelope.json")
        validate(envelope["payload"], "command-result.json")

    def test_duplicate_exact_replay_returns_same_result_and_event_id(self):
        c = self.command()
        r1 = self.commands.apply(c, trusted=True)
        r2 = self.commands.apply(c, trusted=True)
        self.assertEqual(r1["eventId"], r2["eventId"])
        self.assertEqual(r1["stateRevision"], r2["stateRevision"])
        self.assertEqual(self.hardware.outputs["light_on"], True)

    def test_duplicate_changed_rejected_without_overwrite(self):
        c = self.command()
        r1 = self.commands.apply(c, trusted=True)
        c["payload"]["on"] = False
        r2 = self.commands.apply(c, trusted=True)
        self.assertEqual(r2["status"], "rejected")
        self.assertEqual(r2["reasonCode"], "duplicateChanged")
        # Original result and physical state remain.
        self.assertEqual(self.store.command_record(c["commandId"])["result"]["status"], "executed")
        self.assertEqual(self.hardware.outputs["light_on"], True)

    def test_old_sequence_rejected_no_revert(self):
        high = self.command(commandId="cmd-high", commandSequence=10, payload={"on": True})
        self.commands.apply(high, trusted=True)
        low = self.command(commandId="cmd-low", commandSequence=5, payload={"on": False})
        result = self.commands.apply(low, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "sequenceRejected")
        # State from the higher-sequence command is preserved.
        self.assertTrue(self.store.get("light_on"))

    def test_expired_command_rejected_with_reason_code(self):
        c = self.command(
            issuedAtUtc=(utcnow() - timedelta(seconds=10)).isoformat(),
            expiresAtUtc=(utcnow() - timedelta(seconds=1)).isoformat(),
        )
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "expired")
        self.assertEqual(result["reasonCode"], "expired")
        self.assertIsNone(result["snapshot"])
        events = self.store.pending()
        self.assertTrue(any(e["type"] == "CommandResult" and e["payload"]["status"] == "expired" for _, e in events))

    def test_invalid_payload_rejected_with_correlation(self):
        # A payload with the wrong type is a wire-shape violation; it is
        # rejected transiently without a durable event or physical effect.
        c = self.command(payload={"on": "yes"})
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "invalidPayload")
        self.assertIn("correlationId", result)
        self.assertEqual(self.hardware.outputs["light_on"], False)
        self.assertEqual(self.store.pending(), [])

    def test_queue_full_rejection_is_transient(self):
        # Queue-full rejections are transient overload signals; they must not
        # leave a durable CommandResult event.
        c = self.command()
        result = self.commands.reject_queue_full(c)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "deviceRejected")
        self.assertEqual(result["commandId"], c["commandId"])
        self.assertEqual(self.store.command_record(c["commandId"]), None)
        self.assertEqual(self.store.pending(), [])

    def test_crash_before_effect_unknown_recovery(self):
        c = self.command()
        # Claim but do not finish -> simulates crash between claim and effect.
        self.store.claim(c)
        self.store.close()

        self.store = Store(self.directory / "agent.db")
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "unknown")
        self.assertEqual(result["reasonCode"], "unknown")
        self.assertEqual(result["commandId"], c["commandId"])
        self.assertEqual(result["type"], "SetLight")
        self.assertIsNone(result["snapshot"])
        # No physical effect should have happened on recovery replay.
        self.assertEqual(self.hardware.outputs["light_on"], False)
        events = self.store.pending()
        self.assertTrue(any(
            e["type"] == "CommandResult" and e["payload"]["status"] == "unknown"
            for _, e in events))

    def test_crash_after_effect_still_executed_on_replay(self):
        c = self.command()
        self.commands.apply(c, trusted=True)
        # Simulate crash after atomic outcome: replay returns same executed result.
        self.store.close()
        self.store = Store(self.directory / "agent.db")
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "executed")
        self.assertEqual(self.hardware.outputs["light_on"], True)

    def test_set_lighting_mode_manual_auto(self):
        manual = self.command(commandId="cmd-mode-manual", type="SetLightingMode", payload={"mode": "manual"})
        r1 = self.commands.apply(manual, trusted=True)
        self.assertEqual(r1["status"], "executed")
        self.assertEqual(self.store.get("lighting_mode"), "manual")
        auto = self.command(commandId="cmd-mode-auto", type="SetLightingMode", payload={"mode": "auto"}, commandSequence=2)
        r2 = self.commands.apply(auto, trusted=True)
        self.assertEqual(r2["status"], "executed")
        self.assertEqual(self.store.get("lighting_mode"), "auto")

    def test_state_revision_monotonic_and_persisted(self):
        c1 = self.command(commandId="cmd-1", commandSequence=1)
        r1 = self.commands.apply(c1, trusted=True)
        rev1 = r1["stateRevision"]
        c2 = self.command(commandId="cmd-2", commandSequence=2, payload={"on": False})
        r2 = self.commands.apply(c2, trusted=True)
        self.assertGreater(r2["stateRevision"], rev1)
        self.store.close()
        self.store = Store(self.directory / "agent.db")
        self.assertEqual(self.store.state_revision(), r2["stateRevision"])

    def test_auto_light_bumps_state_revision(self):
        self.store.set("lighting_mode", "auto")
        self.store.set("configuration", {"values": {"lightOnBelow": 30, "lightOffAbove": 60}})
        from casa_pi.control.lighting import automatic
        from casa_pi.sensors.light import reading
        # Low light should turn light on.
        self.hardware._write_inputs({"light_percent": 10, "motion": False, "fingerprint": None})
        on = automatic(False, reading(10), 30, 60)
        self.assertTrue(on)
        self.assertEqual(self.store.apply_light_state(on), True)
        self.assertEqual(self.store.state_revision(), 1)

    def test_legacy_twin_lighting_mode_ignored(self):
        from casa_pi.control.configuration import apply_configuration
        result = apply_configuration(
            self.store,
            {"$version": 1, "agentConfiguration": {"lightOnBelow": 25, "lightingMode": "manual"}},
            self.settings,
        )
        self.assertEqual(result["appliedVersion"], 1)
        self.assertEqual(result["values"]["lightOnBelow"], 25)
        self.assertIsNone(self.store.get("lighting_mode"))

    def test_invalid_command_id_does_not_invent_id(self):
        c = self.command(commandId="")
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertNotIn("commandId", result)
        # No durable event is emitted because there is no valid command id.
        self.assertEqual(self.store.pending(), [])

    def test_missing_command_sequence_rejected(self):
        c = self.command()
        del c["commandSequence"]
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "invalidPayload")
        self.assertEqual(self.hardware.outputs["light_on"], False)

    def test_invalid_command_sequence_variants_rejected(self):
        for idx, seq in enumerate((True, 1.5, -1, "1")):
            c = self.command(commandId=f"cmd-seq-{idx}", commandSequence=seq)
            result = self.commands.apply(c, trusted=True)
            self.assertEqual(result["status"], "rejected", f"seq={seq!r}")
            self.assertEqual(result["reasonCode"], "invalidPayload", f"seq={seq!r}")
        self.assertEqual(self.hardware.outputs["light_on"], False)

    def test_light_state_revision_not_bumped_by_security_command(self):
        c = self.command(type="SetSecurityMode", payload={"mode": "armed"})
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "executed")
        self.assertEqual(self.store.state_revision(), 0)

    def test_legacy_ddl_migration_adds_columns_and_keeps_data(self):
        # Simulate a database created before the C11 corrections.
        self.store.close()
        db_path = self.directory / "agent.db"
        db_path.unlink(missing_ok=True)
        legacy = sqlite3.connect(str(db_path))
        legacy.executescript('''
            CREATE TABLE state (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE commands (
              id TEXT PRIMARY KEY, body TEXT NOT NULL, status TEXT NOT NULL,
              result TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
            CREATE TABLE events (
              seq INTEGER PRIMARY KEY AUTOINCREMENT, body TEXT NOT NULL,
              delivered INTEGER NOT NULL DEFAULT 0);
            INSERT INTO state VALUES ("light_on", "true");
            INSERT INTO commands VALUES ("cmd-old", "{}", "finished", NULL, "2026-01-01");
        ''')
        legacy.close()

        store = Store(db_path)
        try:
            self.assertTrue(store.get("light_on"))
            # New columns exist and a new command can be recorded.
            c = self.command(commandId="cmd-new", commandSequence=1)
            store.claim(c)
            store.finish("cmd-new", {"commandId": "cmd-new", "status": "executed"})
            row = store.command_record("cmd-old")
            self.assertEqual(row["status"], "finished")
        finally:
            store.close()

    def test_queue_full_rejection_is_transient_from_worker_thread(self):
        """Simulate the Azure SDK worker thread enqueueing to a full inbound
        queue; the main thread drains the rejected queue but persists nothing
        because overflow rejections are transient."""
        from casa_pi.agent import AdmissionQueue
        admission = AdmissionQueue(inbound_max=1, rejected_max=10)

        results = []

        def worker():
            for i in range(3):
                c = self.command(commandId=f"cmd-thread-{i}", commandSequence=i)
                results.append(admission.put(c))

        t = threading.Thread(target=worker)
        t.start()
        t.join()

        # First command fit in inbound; the rest were handed to rejected queue.
        self.assertEqual(results[0]["status"], "pending")
        for r in results[1:]:
            self.assertEqual(r["status"], "rejected")
            self.assertIn("correlationId", r)
            self.assertEqual(r["reasonCode"], "deviceRejected")

        # Main-thread drain: transient rejections, no durable events.
        drained = []
        for _ in range(10):
            try:
                cmd, meta = admission.rejected.get_nowait()
                drained.append(self.commands.reject_queue_full(cmd, admission_correlation_id=meta.get("correlationId")))
            except Exception:
                break

        self.assertEqual(len(drained), 2)
        for r in drained:
            self.assertEqual(r["status"], "rejected")
            self.assertEqual(r["reasonCode"], "deviceRejected")
            self.assertIn("correlationId", r)
        # Overflow is transient: no CommandResult events persisted.
        self.assertEqual(self.store.pending(), [])

    def test_overflow_replay_reinserts_stable_event(self):
        """With max_events=1 the events queue may drop old undelivered events,
        but an exact command replay must reinsert the same envelope from the
        durable outbox archive without a new physical effect or state revision."""
        self.store.close()
        self.store = Store(self.directory / "agent.db", max_events=1)
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)

        c = self.command(commandId="cmd-overflow", commandSequence=1)
        r1 = self.commands.apply(c, trusted=True)
        self.assertEqual(r1["status"], "executed")
        rev1 = r1["stateRevision"]

        # Emit higher-priority security events until the older CommandResult
        # is evicted from the bounded events queue.
        for _ in range(3):
            self.store.emit({"type": "MotionDetected", "roomId": "sala", "securityModeApplied": "disarmed"})

        pending_before = self.store.pending()
        self.assertTrue(all(e["type"] != "CommandResult" for _, e in pending_before))

        # Exact replay reinserts the archived CommandResult.
        r2 = self.commands.apply(c, trusted=True)
        self.assertEqual(r2["eventId"], r1["eventId"])
        self.assertEqual(r2["stateRevision"], rev1)
        self.assertEqual(self.hardware.outputs["light_on"], True)

        pending_after = self.store.pending()
        types = [e["type"] for _, e in pending_after]
        self.assertIn("CommandResult", types)

    def test_crash_after_effect_before_outcome_recover_unknown_no_second_effect(self):
        """Simulate a partial failure right after the physical effect but before
        the SQLite outcome transaction commits. State/revision must not advance
        and recovery must emit unknown without re-executing."""
        c = self.command(commandId="cmd-crash-mid", commandSequence=1)
        original_atomic = self.store.atomic_outcome
        calls = []

        def failing_atomic(command, result_payload, event, state_delta=None):
            calls.append(1)
            if len(calls) == 1:
                raise RuntimeError("simulated storage failure")
            return original_atomic(command, result_payload, event, state_delta)

        self.store.atomic_outcome = failing_atomic
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "unknown")
        self.assertEqual(result["reasonCode"], "unknown")
        # Because the outcome transaction never committed, state did not advance.
        self.assertEqual(self.store.state_revision(), 0)
        self.assertEqual(self.hardware.outputs["light_on"], True)  # effect happened

        # Recovery on a fresh Store load must not repeat the physical effect.
        self.store.close()
        self.hardware = SimulatedHardware(self.directory)
        self.store = Store(self.directory / "agent.db", 100)
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)
        self.assertEqual(self.hardware.outputs["light_on"], False)
        r2 = self.commands.apply(c, trusted=True)
        self.assertEqual(r2["status"], "unknown")
        self.assertEqual(self.hardware.outputs["light_on"], False)
        self.assertEqual(self.store.state_revision(), 0)

    def test_recovery_reuses_original_correlation_id(self):
        """A crash during execution preserves the admission correlationId in
        the unknown CommandResult envelope."""
        from uuid import uuid4
        corr = str(uuid4())
        c = self.command(commandId="cmd-corr", commandSequence=1)

        # Force the outcome transaction to fail once: the row stays 'executing'
        # but already carries the admission correlationId from the worker thread.
        original_atomic = self.store.atomic_outcome
        calls = []

        def failing_atomic(command, result_payload, event, state_delta=None):
            calls.append(1)
            if len(calls) == 1:
                raise RuntimeError("simulated storage failure")
            return original_atomic(command, result_payload, event, state_delta)

        self.store.atomic_outcome = failing_atomic
        self.commands.apply(c, trusted=True, admission_correlation_id=corr)
        self.store.close()

        # On restart recovery emits 'unknown' tied to the original correlationId.
        self.store = Store(self.directory / "agent.db", 100)
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "unknown")
        self.assertEqual(result["correlationId"], corr)
        events = self.store.pending()
        envelope = events[0][1]
        self.assertEqual(envelope["correlationId"], corr)
        validate(envelope, "telemetry-envelope.json")

    def test_admission_queue_does_not_mutate_command_body(self):
        """The network worker enqueue path stores the exact wire payload and
        keeps delivery metadata in a separate admission envelope."""
        from casa_pi.agent import AdmissionQueue
        q = AdmissionQueue(inbound_max=10)
        c1 = self.command(commandId="cmd-admission-1", commandSequence=1)
        c2 = self.command(commandId="cmd-admission-2", commandSequence=2)
        original1 = json.dumps(c1, sort_keys=True)
        original2 = json.dumps(c2, sort_keys=True)
        r1 = q.put(c1)
        r2 = q.put(c2)
        self.assertEqual(r1["status"], "pending")
        self.assertEqual(r2["status"], "pending")
        # Body is never mutated: no correlationId leaked into the wire payload.
        self.assertEqual(json.dumps(c1, sort_keys=True), original1)
        self.assertEqual(json.dumps(c2, sort_keys=True), original2)
        self.assertNotIn("correlationId", c1)
        drained = [q.inbound.get_nowait(), q.inbound.get_nowait()]
        self.assertEqual(json.dumps(drained[0][0], sort_keys=True), original1)
        self.assertEqual(json.dumps(drained[1][0], sort_keys=True), original2)
        # Delivery correlationIds are separate metadata, not body fields.
        self.assertNotEqual(drained[0][1]["correlationId"], drained[1][1]["correlationId"])

    def test_queue_full_rejection_does_not_overwrite_executed(self):
        """A saturated inbound queue must not replace an already-executed
        result when the same command is re-delivered."""
        from casa_pi.agent import AdmissionQueue
        c = self.command(commandId="cmd-saturated", commandSequence=1)
        executed = self.commands.apply(c, trusted=True)
        self.assertEqual(executed["status"], "executed")
        rev = executed["stateRevision"]
        event_id = executed["eventId"]

        admission = AdmissionQueue(inbound_max=1, rejected_max=10)
        admission.put(self.command(commandId="cmd-filler", commandSequence=2))
        response = admission.put(c)
        self.assertEqual(response["status"], "rejected")

        cmd, meta = admission.rejected.get_nowait()
        result = self.commands.reject_queue_full(cmd, admission_correlation_id=meta.get("correlationId"))
        self.assertEqual(result["status"], "executed")
        self.assertEqual(result["stateRevision"], rev)
        self.assertEqual(result["eventId"], event_id)

        record = self.store.command_record(c["commandId"])
        self.assertEqual(record["status"], "executed")
        self.assertEqual(record["result"]["status"], "executed")
        self.assertEqual(record["result"]["stateRevision"], rev)

    def test_driver_value_error_post_effect_unknown_no_second_effect(self):
        """A driver failure during/after the physical effect must emit unknown,
        must not bump the state revision, and must not retry the effect."""
        calls = []

        def failing_light(on):
            calls.append(on)
            self.hardware.outputs["light_on"] = bool(on)
            self.hardware.save()
            raise ValueError("simulated GPIO fault")

        self.hardware.light = failing_light
        c = self.command(commandId="cmd-driver-fail", commandSequence=1)
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "unknown")
        self.assertEqual(result["reasonCode"], "unknown")
        self.assertEqual(calls, [True])
        self.assertEqual(self.store.state_revision(), 0)

        # Recovery on restart must not repeat the physical effect.
        self.store.close()
        self.hardware = SimulatedHardware(self.directory)
        self.store = Store(self.directory / "agent.db", 100)
        self.commands = Commands(self.store, self.hardware, self.access, self.settings)
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "unknown")
        self.assertEqual(calls, [True])  # no second effect
        self.assertEqual(self.store.state_revision(), 0)

    def test_set_light_same_state_no_revision_bump(self):
        """Issuing SetLight with the same value must not advance stateRevision."""
        c1 = self.command(commandId="cmd-on-1", commandSequence=1, payload={"on": True})
        r1 = self.commands.apply(c1, trusted=True)
        self.assertEqual(r1["status"], "executed")
        rev1 = r1["stateRevision"]
        c2 = self.command(commandId="cmd-on-2", commandSequence=2, payload={"on": True})
        r2 = self.commands.apply(c2, trusted=True)
        self.assertEqual(r2["status"], "executed")
        self.assertEqual(r2["stateRevision"], rev1)

    def test_invalid_command_type_transient_no_schema_event(self):
        """A command with an unknown type cannot produce a schema-valid
        CommandResult, so it is rejected transiently without a durable event."""
        c = self.command(commandId="cmd-bogus", type="Bogus", payload={"x": 1})
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "invalidPayload")
        self.assertIn("correlationId", result)
        self.assertEqual(self.store.pending(), [])

    def test_set_lighting_mode_change_bumps_revision(self):
        """Changing the lighting authority from manual to auto is a real
        lighting state change and therefore bumps stateRevision."""
        manual = self.command(commandId="cmd-mode-manual", type="SetLightingMode", payload={"mode": "manual"}, commandSequence=1)
        r1 = self.commands.apply(manual, trusted=True)
        auto = self.command(commandId="cmd-mode-auto", type="SetLightingMode", payload={"mode": "auto"}, commandSequence=2)
        r2 = self.commands.apply(auto, trusted=True)
        self.assertGreater(r2["stateRevision"], r1["stateRevision"])

    # ------------------------------------------------------------------
    # A: no-op initial auto state must not bump stateRevision
    # ------------------------------------------------------------------

    def test_set_lighting_mode_auto_no_revision_bump_empty_store(self):
        """SetLightingMode auto on a fresh DB is a no-op because the effective
        default authority is already auto; stateRevision stays 0."""
        c = self.command(commandId="cmd-auto-empty", type="SetLightingMode",
                         payload={"mode": "auto"}, commandSequence=1)
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "executed")
        self.assertEqual(result["snapshot"]["lightingMode"], "auto")
        self.assertEqual(result["stateRevision"], 0)
        self.assertEqual(self.store.state_revision(), 0)
        events = self.store.pending()
        self.assertEqual(events[0][1]["payload"]["stateRevision"], 0)
        # Exact replay is stable.
        r2 = self.commands.apply(c, trusted=True)
        self.assertEqual(r2["stateRevision"], 0)
        self.assertEqual(r2["eventId"], result["eventId"])

    def test_set_light_false_forces_manual_once(self):
        """SetLight on=False from an empty DB forces manual authority, so the
        mode changes from the effective auto default and bumps revision once.
        A second identical command is a true no-op."""
        c1 = self.command(commandId="cmd-false-1", commandSequence=1, payload={"on": False})
        r1 = self.commands.apply(c1, trusted=True)
        self.assertEqual(r1["stateRevision"], 1)
        c2 = self.command(commandId="cmd-false-2", commandSequence=2, payload={"on": False})
        r2 = self.commands.apply(c2, trusted=True)
        self.assertEqual(r2["status"], "executed")
        self.assertEqual(r2["stateRevision"], 1)

    # ------------------------------------------------------------------
    # B: duplicates admitted before the original executes
    # ------------------------------------------------------------------

    def test_duplicate_pending_exact_does_not_cancel_original(self):
        """An exact duplicate of a command still waiting in the inbound queue
        must not be written to the rejected queue; the original must execute."""
        from casa_pi.agent import AdmissionQueue
        admission = AdmissionQueue(inbound_max=1, rejected_max=10)
        c = self.command(commandId="cmd-pending-dup", commandSequence=1)
        r1 = admission.put(c)
        r2 = admission.put(c)
        self.assertEqual(r1["status"], "pending")
        self.assertEqual(r2["status"], "pending")
        self.assertEqual(admission.inbound.qsize(), 1)
        self.assertEqual(admission.rejected.qsize(), 0)
        cmd, meta = admission.inbound.get_nowait()
        result = self.commands.apply(cmd, trusted=True,
                                     admission_correlation_id=meta.get("correlationId"))
        self.assertEqual(result["status"], "executed")
        self.assertEqual(self.hardware.outputs["light_on"], True)

    def test_duplicate_pending_changed_rejected_transient(self):
        """A changed duplicate of a command still in the inbound queue must be
        rejected transiently and must not cancel the original execution."""
        from casa_pi.agent import AdmissionQueue
        admission = AdmissionQueue(inbound_max=1, rejected_max=10)
        c = self.command(commandId="cmd-pending-changed", commandSequence=1)
        admission.put(c)
        changed = self.command(commandId="cmd-pending-changed", commandSequence=1,
                               payload={"on": False})
        r2 = admission.put(changed)
        self.assertEqual(r2["status"], "rejected")
        self.assertEqual(r2["reasonCode"], "duplicateChanged")
        self.assertEqual(admission.inbound.qsize(), 1)
        self.assertEqual(admission.rejected.qsize(), 0)
        cmd, meta = admission.inbound.get_nowait()
        result = self.commands.apply(cmd, trusted=True,
                                     admission_correlation_id=meta.get("correlationId"))
        self.assertEqual(result["status"], "executed")
        self.assertEqual(self.hardware.outputs["light_on"], True)

    def test_duplicate_pending_inbound_tail_stable(self):
        """A duplicate of any command still in the bounded inbound queue is
        deduplicated in memory, so the original executes exactly once."""
        from casa_pi.agent import AdmissionQueue
        admission = AdmissionQueue(inbound_max=10)
        cmds = [self.command(commandId=f"cmd-tail-{i}", commandSequence=i,
                             payload={"on": i % 2 == 0}) for i in range(10)]
        for c in cmds:
            admission.put(c)
        # Duplicate of the first item, still waiting deep in the queue.
        r = admission.put(cmds[0])
        self.assertEqual(r["status"], "pending")
        self.assertEqual(admission.inbound.qsize(), 10)
        processed = 0
        for _ in range(10):
            cmd, meta = admission.inbound.get_nowait()
            self.commands.apply(cmd, trusted=True,
                                admission_correlation_id=meta.get("correlationId"))
            admission.complete(cmd.get("commandId"))
            if cmd["commandId"] == cmds[0]["commandId"]:
                processed += 1
        self.assertEqual(processed, 1)

    # ------------------------------------------------------------------
    # C: legacy data compatibility
    # ------------------------------------------------------------------

    def test_legacy_body_correlation_id_recovery(self):
        """An 'executing' row created before C11 may carry its correlationId
        inside the body JSON. Recovery must reuse that UUID consistently."""
        self.store.close()
        db_path = self.directory / "agent.db"
        db_path.unlink(missing_ok=True)
        corr = "11111111-1111-1111-1111-111111111111"
        now = utcnow()
        body = {
            "commandId": "cmd-legacy",
            "deviceId": "raspberry-sim",
            "type": "SetLight",
            "commandSequence": 1,
            "issuedAtUtc": now.isoformat(),
            "expiresAtUtc": (now + timedelta(seconds=8)).isoformat(),
            "payload": {"on": True},
            "correlationId": corr,
        }
        legacy = sqlite3.connect(str(db_path))
        legacy.executescript('''
            CREATE TABLE state (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE commands (
                id TEXT PRIMARY KEY, body TEXT NOT NULL, status TEXT NOT NULL,
                result TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
            CREATE TABLE events (
                seq INTEGER PRIMARY KEY AUTOINCREMENT, body TEXT NOT NULL,
                delivered INTEGER NOT NULL DEFAULT 0);
        ''')
        legacy.execute(
            "INSERT INTO commands VALUES (?,?,?,NULL,?)",
            ("cmd-legacy", json.dumps(body, sort_keys=True), "executing", "2026-01-01"),
        )
        legacy.commit()
        legacy.close()

        store = Store(db_path, 100)
        try:
            commands = Commands(store, self.hardware, self.access, self.settings)
            incoming = {k: v for k, v in body.items() if k != "correlationId"}
            result = commands.apply(incoming, trusted=True)
            self.assertEqual(result["status"], "unknown")
            self.assertEqual(result["correlationId"], corr)
            events = store.pending()
            envelope = events[0][1]
            self.assertEqual(envelope["correlationId"], corr)
            validate(envelope, "telemetry-envelope.json")
            # No physical effect on recovery.
            self.assertEqual(self.hardware.outputs["light_on"], False)
        finally:
            store.close()

    def test_legacy_stored_correlation_takes_priority_over_body(self):
        """If both the correlation_id column and a legacy body correlationId
        are present, the explicit column wins."""
        self.store.close()
        db_path = self.directory / "agent.db"
        db_path.unlink(missing_ok=True)
        col_corr = "22222222-2222-2222-2222-222222222222"
        body_corr = "11111111-1111-1111-1111-111111111111"
        now = utcnow()
        body = {
            "commandId": "cmd-legacy-prio",
            "deviceId": "raspberry-sim",
            "type": "SetLight",
            "commandSequence": 1,
            "issuedAtUtc": now.isoformat(),
            "expiresAtUtc": (now + timedelta(seconds=8)).isoformat(),
            "payload": {"on": True},
            "correlationId": body_corr,
        }
        legacy = sqlite3.connect(str(db_path))
        legacy.executescript('''
            CREATE TABLE state (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE commands (
                id TEXT PRIMARY KEY, body TEXT NOT NULL, status TEXT NOT NULL,
                result TEXT, event_id TEXT, correlation_id TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP);
            CREATE TABLE events (
                seq INTEGER PRIMARY KEY AUTOINCREMENT, body TEXT NOT NULL,
                delivered INTEGER NOT NULL DEFAULT 0);
        ''')
        legacy.execute(
            "INSERT INTO commands VALUES (?,?,?,NULL,NULL,?,'2026-01-01')",
            ("cmd-legacy-prio", json.dumps(body, sort_keys=True), "executing", col_corr),
        )
        legacy.commit()
        legacy.close()

        store = Store(db_path, 100)
        try:
            commands = Commands(store, self.hardware, self.access, self.settings)
            incoming = {k: v for k, v in body.items() if k != "correlationId"}
            result = commands.apply(incoming, trusted=True)
            self.assertEqual(result["correlationId"], col_corr)
        finally:
            store.close()

    def test_cli_security_requires_sequence_and_valid_mode(self):
        """The local simulator CLI must emit schema-valid device-command JSON
        for SetSecurityMode, requiring --sequence and a valid mode."""
        import subprocess
        import sys
        import os
        agent_dir = Path(__file__).resolve().parents[1]
        env = os.environ.copy()
        env["PYTHONPATH"] = str(agent_dir / "src")
        # Missing sequence must fail.
        result = subprocess.run(
            [sys.executable, "simulator/control.py", "--data-dir", str(self.directory),
             "command", "SetSecurityMode", "--mode", "armed"],
            capture_output=True, text=True, cwd=str(agent_dir), env=env,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("--sequence", result.stderr)
        # Negative sequence must fail before writing any JSON.
        result = subprocess.run(
            [sys.executable, "simulator/control.py", "--data-dir", str(self.directory),
             "command", "SetSecurityMode", "--mode", "armed", "--sequence", "-1"],
            capture_output=True, text=True, cwd=str(agent_dir), env=env,
        )
        self.assertNotEqual(result.returncode, 0)
        # Invalid lighting mode must fail.
        result = subprocess.run(
            [sys.executable, "simulator/control.py", "--data-dir", str(self.directory),
             "command", "SetLightingMode", "--mode", "armed", "--sequence", "1"],
            capture_output=True, text=True, cwd=str(agent_dir), env=env,
        )
        self.assertNotEqual(result.returncode, 0)
        # Valid security command must print schema-valid JSON.
        result = subprocess.run(
            [sys.executable, "simulator/control.py", "--data-dir", str(self.directory),
             "command", "SetSecurityMode", "--mode", "disarmed", "--sequence", "5"],
            capture_output=True, text=True, cwd=str(agent_dir), env=env,
        )
        self.assertEqual(result.returncode, 0, msg=result.stderr)
        body = json.loads(result.stdout)
        validate(body, "device-command.json")
        self.assertEqual(body["type"], "SetSecurityMode")
        self.assertEqual(body["commandSequence"], 5)

    # ------------------------------------------------------------------
    # 1: wire-shape validation before replay
    # ------------------------------------------------------------------

    def test_replay_rejects_wire_with_internal_correlation_id(self):
        """A command body that carries the legacy internal correlationId field
        is schema-invalid as a new wire command and must be rejected
        transiently, even if a stored legacy row has the same content."""
        c = self.command(commandId="cmd-wire-meta", commandSequence=1)
        r1 = self.commands.apply(c, trusted=True)
        self.assertEqual(r1["status"], "executed")

        # Replay with the prohibited metadata injected.
        tainted = dict(c)
        tainted["correlationId"] = r1["correlationId"]
        result = self.commands.apply(tainted, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "invalidPayload")
        # Original result and physical state are untouched.
        self.assertEqual(self.store.command_record(c["commandId"])["result"]["status"], "executed")
        self.assertEqual(self.hardware.outputs["light_on"], True)

        # The cleaned wire command replays the original result without a new effect.
        cleaned = dict(c)
        r2 = self.commands.apply(cleaned, trusted=True)
        self.assertEqual(r2["eventId"], r1["eventId"])
        self.assertEqual(self.hardware.outputs["light_on"], True)

    def test_replay_rejects_type_looseness_and_extra_keys(self):
        """Canonical JSON comparison is type-preserving and payload shape is
        strict: 1 is not true, 1.0 is not 1, and extra keys are disallowed."""
        c = self.command(commandId="cmd-shape", commandSequence=1)
        r1 = self.commands.apply(c, trusted=True)

        for label, mutation in (
            ("on int", {"payload": {"on": 1}}),
            ("on float", {"payload": {"on": 1.0}}),
            ("payload extra", {"payload": {"on": True, "brightness": 50}}),
            ("top extra", {"protocolId": "x"}),
        ):
            mutated = dict(c)
            mutated.update(mutation)
            result = self.commands.apply(mutated, trusted=True)
            self.assertEqual(result["status"], "rejected", label)
            self.assertEqual(result["reasonCode"], "invalidPayload", label)

        # Original executed result and state are unchanged.
        self.assertEqual(self.store.command_record(c["commandId"])["result"]["status"], "executed")
        self.assertEqual(self.hardware.outputs["light_on"], True)

    def test_expired_valid_shape_replays_original_result(self):
        """A valid-shape replay of an already-executed command must return the
        same durable result even if the original window has expired."""
        c = self.command(commandId="cmd-expired-replay", commandSequence=1)
        r1 = self.commands.apply(c, trusted=True)
        self.assertEqual(r1["status"], "executed")
        self._advance(10)
        r2 = self.commands.apply(c, trusted=True)
        self.assertEqual(r2["eventId"], r1["eventId"])
        self.assertEqual(r2["status"], "executed")

    # ------------------------------------------------------------------
    # 2: AdmissionQueue lock / in-flight map
    # ------------------------------------------------------------------

    def test_admission_queue_inflight_map_releases_all_ids(self):
        """After the main thread drains and completes every command, the
        in-flight map must be empty."""
        from casa_pi.agent import AdmissionQueue
        admission = AdmissionQueue(inbound_max=10)
        for i in range(10):
            admission.put(self.command(commandId=f"cmd-release-{i}", commandSequence=i))
        for _ in range(10):
            cmd, meta = admission.inbound.get_nowait()
            admission.complete(cmd.get("commandId"))
        self.assertEqual(admission.inflight, {})

    def test_admission_queue_complete_does_not_race_put(self):
        """A duplicate put() that happens before the main thread has recorded
        completion must see the original id as still in flight."""
        from casa_pi.agent import AdmissionQueue
        from threading import Thread, Event
        admission = AdmissionQueue(inbound_max=1)
        c = self.command(commandId="cmd-race", commandSequence=1)
        admission.put(c)

        producer_got = []
        producer_started = Event()
        producer_can_finish = Event()

        def producer():
            producer_started.set()
            producer_can_finish.wait()
            producer_got.append(admission.put(c))

        t = Thread(target=producer)
        t.start()
        self.assertTrue(producer_started.wait(2))

        # Consumer gets the command but pauses before completing.
        cmd, meta = admission.inbound.get_nowait()
        producer_can_finish.set()
        t.join(2)

        # The duplicate arrived while the id was still in flight -> pending.
        self.assertEqual(producer_got[0]["status"], "pending")
        self.assertEqual(admission.inbound.qsize(), 0)

        # Now complete; a fresh duplicate is admitted normally.
        admission.complete(cmd.get("commandId"))
        r2 = admission.put(c)
        self.assertEqual(r2["status"], "pending")
        self.assertEqual(admission.inbound.qsize(), 1)

    # ------------------------------------------------------------------
    # 3: legacy terminal backfill and executing->unknown
    # ------------------------------------------------------------------

    def test_legacy_terminal_backfills_event_from_result_metadata(self):
        """A legacy terminal row with a complete result but no outbox archive
        can backfill its CommandResult envelope from stored metadata."""
        c = self.command(commandId="cmd-backfill", commandSequence=1)
        r1 = self.commands.apply(c, trusted=True)
        self.assertEqual(r1["status"], "executed")

        # Remove the outbox archive but keep the command row.
        self.store.db.execute("DELETE FROM outbox")
        # Trim all events.
        self.store.db.execute("DELETE FROM events")
        self.store.db.commit()
        self.assertEqual(self.store.pending(), [])

        # Exact replay re-creates the envelope from the durable result.
        r2 = self.commands.apply(c, trusted=True)
        self.assertEqual(r2["eventId"], r1["eventId"])
        events = self.store.pending()
        self.assertEqual(len(events), 1)
        envelope = events[0][1]
        self.assertEqual(envelope["eventId"], r1["eventId"])
        self.assertEqual(envelope["correlationId"], r1["correlationId"])
        validate(envelope, "telemetry-envelope.json")

    def test_legacy_incomplete_terminal_is_a_known_compat_limit(self):
        """A V0-style terminal row with no result/eventId cannot be safely
        backfilled as a v1 CommandResult. A replay returns the original row
        status but emits no fabricated event; a new, different command still
        executes normally."""
        self.store.close()
        db_path = self.directory / "agent.db"
        db_path.unlink(missing_ok=True)
        legacy = sqlite3.connect(str(db_path))
        legacy.executescript('''
            CREATE TABLE state (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE commands (
                id TEXT PRIMARY KEY, body TEXT NOT NULL, status TEXT NOT NULL,
                result TEXT, created_at TEXT DEFAULT CURRENT_TIMESTAMP);
            CREATE TABLE events (
                seq INTEGER PRIMARY KEY AUTOINCREMENT, body TEXT NOT NULL,
                delivered INTEGER NOT NULL DEFAULT 0);
        ''')
        body = json.dumps({"commandId": "cmd-v0", "deviceId": "raspberry-sim",
                           "type": "SetLight", "commandSequence": 1,
                           "issuedAtUtc": utcnow().isoformat(),
                           "expiresAtUtc": (utcnow() + timedelta(seconds=8)).isoformat(),
                           "payload": {"on": True}}, sort_keys=True)
        legacy.execute("INSERT INTO commands VALUES (?,?,'finished',NULL,?)",
                       ("cmd-v0", body, "2026-01-01"))
        legacy.commit()
        legacy.close()

        store = Store(db_path, 100)
        try:
            commands = Commands(store, self.hardware, self.access, self.settings)
            incoming = json.loads(body)
            result = commands.apply(incoming, trusted=True)
            # The row is preserved but we never present an incomplete legacy
            # status as a v1 confirmation; an explicit unknown transient is
            # returned instead and no event is fabricated.
            self.assertEqual(result["status"], "unknown")
            self.assertEqual(result["reasonCode"], "unknown")
            self.assertEqual(store.pending(), [])
            # A new, different command still advances normally.
            new_cmd = self.command(commandId="cmd-new", commandSequence=2)
            r2 = commands.apply(new_cmd, trusted=True)
            self.assertEqual(r2["status"], "executed")
            self.assertEqual(self.hardware.outputs["light_on"], True)
        finally:
            store.close()

    def test_executing_row_finalized_as_unknown_on_duplicate(self):
        """If the outcome transaction fails in-process, a subsequent duplicate
        must finalize the command as unknown durable without a second effect."""
        c = self.command(commandId="cmd-executing-unknown", commandSequence=1)
        original_atomic = self.store.atomic_outcome
        calls = []

        def failing_once(command, result_payload, event, state_delta=None):
            calls.append(1)
            if len(calls) == 1:
                raise RuntimeError("simulated storage failure")
            return original_atomic(command, result_payload, event, state_delta)

        self.store.atomic_outcome = failing_once
        r1 = self.commands.apply(c, trusted=True)
        self.assertEqual(r1["status"], "unknown")
        self.assertEqual(self.store.command_record(c["commandId"])["status"], "executing")
        self.assertEqual(self.hardware.outputs["light_on"], True)  # effect happened once

        # Second apply finalizes the executing row as unknown durable.
        r2 = self.commands.apply(c, trusted=True)
        self.assertEqual(r2["status"], "unknown")
        self.assertEqual(self.store.command_record(c["commandId"])["status"], "unknown")
        self.assertEqual(len(self.store.pending()), 1)
        self.assertEqual(self.hardware.outputs["light_on"], True)  # no second effect

    # ------------------------------------------------------------------
    # D1 — wire-shape guard complete for all command types
    # ------------------------------------------------------------------

    def test_set_security_mode_without_sequence_rejected(self):
        """device-command.json requires commandSequence globally; a missing
        sequence must be rejected before any effect or durable result."""
        c = self.command(commandId="cmd-sec-noseq", type="SetSecurityMode",
                         payload={"mode": "armed"}, commandSequence=0)
        del c["commandSequence"]
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "invalidPayload")
        self.assertEqual(self.store.command_record(c["commandId"]), None)

    def test_set_light_with_invalid_attempt_id_rejected(self):
        """attemptId is optional, but if present it must be a valid UUID."""
        c = self.command(commandId="cmd-light-bad-attempt", commandSequence=1,
                         attemptId="not-a-uuid")
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "invalidPayload")
        self.assertEqual(self.hardware.outputs["light_on"], False)

    def test_open_door_invalid_attempt_id_no_pulse(self):
        """OpenDoor with a non-UUID attemptId is schema-invalid and must not
        produce a door pulse."""
        from uuid import uuid4
        c = self.command(commandId="cmd-door-bad-attempt", type="OpenDoor",
                         commandSequence=0, attemptId="not-a-uuid",
                         payload={"doorId": "principal", "durationMs": 1500})
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "invalidPayload")
        self.assertEqual(self.hardware.outputs["door_pulses"], 0)

    def test_open_door_null_attempt_id_no_pulse(self):
        """OpenDoor with attemptId null is schema-valid but Access denies the
        business authorization without a pulse."""
        from uuid import uuid4
        c = self.command(commandId="cmd-door-null-attempt", type="OpenDoor",
                         commandSequence=0, attemptId=None,
                         payload={"doorId": "principal", "durationMs": 1500})
        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "rejected")
        self.assertEqual(result["reasonCode"], "invalidPayload")
        self.assertEqual(self.hardware.outputs["door_pulses"], 0)

    # ------------------------------------------------------------------
    # D2 — backfill only from complete, evidence-backed results
    # ------------------------------------------------------------------

    def test_backfill_rejects_bool_state_revision(self):
        """A stored result with a boolean stateRevision is not a complete v1
        CommandResult; replay must not backfill an invalid event."""
        c = self.command(commandId="cmd-bool-rev", commandSequence=1)
        r1 = self.commands.apply(c, trusted=True)
        # Mutate the stored result to simulate a corrupted legacy row.
        corrupted = dict(r1)
        corrupted["stateRevision"] = True
        self.store.finish(c["commandId"], corrupted)

        # Trim events and outbox so backfill would be required.
        self.store.db.execute("DELETE FROM outbox")
        self.store.db.execute("DELETE FROM events")
        self.store.db.commit()

        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "unknown")
        self.assertEqual(result["reasonCode"], "unknown")
        self.assertEqual(self.store.pending(), [])

    def test_backfill_rejects_partial_snapshot(self):
        """A stored executed result without a complete snapshot/appliedAtUtc
        must not be backfilled as a historical event."""
        c = self.command(commandId="cmd-partial-snap", commandSequence=1)
        r1 = self.commands.apply(c, trusted=True)
        corrupted = dict(r1)
        corrupted["snapshot"] = {"lightOn": True}  # missing lightingMode/source/appliedAtUtc
        self.store.finish(c["commandId"], corrupted)

        self.store.db.execute("DELETE FROM outbox")
        self.store.db.execute("DELETE FROM events")
        self.store.db.commit()

        result = self.commands.apply(c, trusted=True)
        self.assertEqual(result["status"], "unknown")
        self.assertEqual(self.store.pending(), [])

    def test_error_result_without_original_timestamp_not_backfilled(self):
        """Rejected/expired/unknown results carry no appliedAtUtc; the original
        event time is unknown, so trimming must not fabricate a historical
        envelope with the current time."""
        c = self.command(commandId="cmd-rej-nobackfill", commandSequence=1)
        r1 = self.commands.apply(c, trusted=True)
        # Change payload to get a rejected result with complete metadata.
        c2 = self.command(commandId="cmd-rej-nobackfill", commandSequence=1,
                          payload={"on": "yes"})
        r2 = self.commands.apply(c2, trusted=True)
        self.assertEqual(r2["status"], "rejected")

        self.store.db.execute("DELETE FROM outbox")
        self.store.db.execute("DELETE FROM events")
        self.store.db.commit()

        # Replay of the exact rejected command still returns the durable result.
        c3 = self.command(commandId="cmd-rej-nobackfill", commandSequence=1,
                          payload={"on": "yes"})
        result = self.commands.apply(c3, trusted=True)
        self.assertEqual(result["status"], "rejected")
        # No event is fabricated because the original timestamp is unknown.
        self.assertEqual(self.store.pending(), [])

    # ------------------------------------------------------------------
    # D3 — original durable correlation prevails over duplicate delivery
    # ------------------------------------------------------------------

    def test_executing_unknown_preserves_original_correlation(self):
        """When the original outcome fails to commit, a duplicate finalized as
        unknown must keep the original C1 correlation, not the duplicate's
        delivery correlation C2."""
        from uuid import uuid4
        c1_corr = str(uuid4())
        c2_corr = str(uuid4())
        c = self.command(commandId="cmd-c1-c2", commandSequence=1)
        original_atomic = self.store.atomic_outcome
        calls = []

        def failing_once(command, result_payload, event, state_delta=None):
            calls.append(1)
            if len(calls) == 1:
                raise RuntimeError("simulated storage failure")
            return original_atomic(command, result_payload, event, state_delta)

        self.store.atomic_outcome = failing_once
        self.commands.apply(c, trusted=True, admission_correlation_id=c1_corr)
        record = self.store.command_record(c["commandId"])
        self.assertEqual(record["status"], "executing")
        self.assertEqual(record["correlation_id"], c1_corr)

        # Duplicate arrives with a different delivery correlation.
        r2 = self.commands.apply(c, trusted=True, admission_correlation_id=c2_corr)
        self.assertEqual(r2["status"], "unknown")
        self.assertEqual(r2["correlationId"], c1_corr)
        events = self.store.pending()
        envelope = events[0][1]
        self.assertEqual(envelope["correlationId"], c1_corr)


if __name__ == "__main__":
    unittest.main()
