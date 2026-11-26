"""Constructores de eventos conformes a contracts/v1 y contracts/v2.

Cada función devuelve un dict listo para serializar. Los nombres y campos están
tomados de los esquemas, no inventados.
"""
from __future__ import annotations

import uuid
from datetime import datetime, timedelta, timezone
from typing import Any


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso(value: datetime | None = None) -> str:
    moment = value or utc_now()
    return moment.astimezone(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def new_id() -> str:
    return str(uuid.uuid4())


def envelope(
    schema_version: int,
    type_: str,
    payload: dict[str, Any],
    *,
    event_id: str | None = None,
    correlation_id: str | None = None,
    occurred_at: str | None = None,
) -> dict[str, Any]:
    return {
        "schemaVersion": schema_version,
        "type": type_,
        "eventId": event_id or new_id(),
        "correlationId": correlation_id or new_id(),
        "occurredAtUtc": occurred_at or iso(),
        "payload": payload,
    }


def light_snapshot(light_on: bool, lighting_mode: str, applied_at: str | None = None) -> dict[str, Any]:
    return {
        "lightOn": bool(light_on),
        "lightingMode": lighting_mode,
        "source": "device",
        "appliedAtUtc": applied_at or iso(),
    }


def command_result(
    command_id: str,
    type_: str,
    status: str,
    *,
    snapshot: dict[str, Any] | None = None,
    revision: int | None = None,
    reason_code: str | None = None,
    reason: str | None = None,
    event_id: str | None = None,
    correlation_id: str | None = None,
) -> dict[str, Any]:
    return {
        "commandId": command_id,
        "status": status,
        "type": type_,
        "reasonCode": reason_code,
        "reason": reason,
        "snapshot": snapshot,
        "stateRevision": revision,
        "eventId": event_id or new_id(),
        "correlationId": correlation_id or new_id(),
    }


def result_event(payload: dict[str, Any], *, correlation_id: str) -> dict[str, Any]:
    return envelope(
        1,
        "CommandResult",
        payload,
        event_id=payload["eventId"],
        correlation_id=correlation_id,
    )


def device_state(
    light_id: str,
    revision: int,
    security_mode: str,
    snapshot: dict[str, Any],
    *,
    correlation_id: str | None = None,
    occurred_at: str | None = None,
) -> dict[str, Any]:
    return envelope(
        2,
        "DeviceStateReported",
        {
            "lightId": light_id,
            "snapshot": snapshot,
            "stateRevision": revision,
            "securityModeApplied": security_mode,
        },
        correlation_id=correlation_id,
        occurred_at=occurred_at,
    )


def telemetry(
    light_percent: float,
    motion: bool,
    light_on: bool,
    security_mode: str,
    *,
    correlation_id: str | None = None,
) -> dict[str, Any]:
    return envelope(
        1,
        "TelemetryReported",
        {
            "lightPercent": max(0.0, min(100.0, round(float(light_percent), 1))),
            "motion": bool(motion),
            "lightOn": bool(light_on),
            "securityModeApplied": security_mode,
        },
        correlation_id=correlation_id,
    )


def motion_detected(room_id: str, security_mode: str, *, correlation_id: str | None = None) -> dict[str, Any]:
    return envelope(
        1,
        "MotionDetected",
        {
            "roomId": room_id,
            "securityModeApplied": security_mode,
            "configurationVersion": 0,
        },
        correlation_id=correlation_id,
    )


def access_requested(
    room_id: str,
    method: str,
    template_id: int | None,
    *,
    attempt_id: str | None = None,
    correlation_id: str | None = None,
) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "attemptId": attempt_id or new_id(),
        "roomId": room_id,
        "method": method,
        "expiresAtUtc": iso(utc_now() + timedelta(seconds=30)),
    }
    if template_id is not None:
        payload["templateId"] = str(template_id)
    return envelope(1, "AccessRequested", payload, correlation_id=correlation_id)


def capture_uploaded(capture_id: str, blob_name: str, *, correlation_id: str | None = None) -> dict[str, Any]:
    return envelope(
        1,
        "CaptureUploaded",
        {"captureId": capture_id, "blobName": blob_name},
        correlation_id=correlation_id,
    )
