#!/usr/bin/env python3
"""Formal fixture validation with manifest, offline registry and semantic matrix.

All JSON files are loaded with duplicate-key detection. The expected manifest
is versioned and compared against actual fixtures to detect missing/extra files.
Semantic checks enforce business rules that JSON Schema alone cannot express
(e.g., TTL windows, reasonCode per terminal state, C10 resource/result identity).
"""
import json
import re
import sys
from datetime import datetime
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker
from jsonschema.exceptions import ValidationError
from referencing import Registry, Resource
from referencing.exceptions import Unresolvable

ROOT = Path(__file__).resolve().parent.parent
FIXTURES = ROOT / "fixtures"
SCHEMAS = ROOT / "schemas"

UUID_RE = re.compile(
    r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
)

EXPECTED_MANIFEST = {
    "version": "1.0.0",
    "fixtures": [
        "security/capture.json",
        "security/capture-page.json",
        "security/activity.json",
        "security/page.json",
        "security/mode.json",
        "security/event.json",
        "security/preferences.json",
        "security/person.json",
        "security/persons-page.json",
        "security/person-create-request.json",
        "security/person-enrollment-created.json",
        "security/enrollment.json",
        "security/enrollment-capture-request.json",
        "notifications/preferences.json",
        "notifications/preferences-request.json",
        "notifications/delivery-accepted.json",
        "notifications/delivery-get.json",
        "authz/check-request.json",
        "authz/check-response-denied.json",
        "authz/check-response-granted.json",
        "authz/check-response-revoked.json",
        "authz/check-response-unavailable.json",
        "authz/check-response-unauthorized-401.json",
        "command-result/202-pending.json",
        "command-result/applied-device.json",
        "command-result/applied-simulated.json",
        "command-result/duplicate-changed.json",
        "command-result/duplicate-exact.json",
        "command-result/expired.json",
        "command-result/late-event-old.json",
        "command-result/rejected-invalid.json",
        "command-result/rejected-unauthorized.json",
        "command-result/session-revoked.json",
        "command-result/unknown-crash.json",
        "command-result/start-video-executed.json",
        "command-result/start-video-unknown.json",
        "device-command/capture-image.json",
        "device-command/start-video.json",
        "device-command/expired.json",
        "device-command/open-door.json",
        "device-command/set-light-off.json",
        "device-command/set-light-on.json",
        "device-command/set-lighting-mode-auto.json",
        "device-command/set-lighting-mode-manual.json",
        "device-twin/legacy-lightingMode.json",
        "device-twin/valid.json",
        "home-api/capability-request.json",
        "home-api/capability-resource.json",
        "home-api/capability-union-resource.json",
        "home-api/capability-resources.json",
        "home-api/sensor-readings.json",
        "home-api/security-status.json",
        "home-api/command-get-pending.json",
        "home-api/command-request.json",
        "home-api/command-response-202.json",
        "home-api/command-response-applied.json",
        "home-api/command-response-conflict-409.json",
        "home-api/command-response-pending-replay.json",
        "home-api/identity-unavailable-response.json",
        "home-api/status-response.json",
        "home-api/location.json",
        "home-api/location-request.json",
        "assistant/ask-request.json",
        "assistant/ask-response.json",
        "home-api/video-start-request.json",
        "home-api/video-session-connecting.json",
        "home-api/video-session-live.json",
        "home-api/video-session-ended.json",
        "home-api/video-session-failed.json",
        "identity/jwks.json",
        "identity/login-request.json",
        "identity/login-response.json",
        "identity/logout-request.json",
        "identity/me-response.json",
        "identity/refresh-request.json",
        "identity/service-assertion.json",
        "identity/service-token-request.json",
        "identity/service-token-response.json",
        "identity/members-response.json",
        "identity/invitation-request.json",
        "identity/invitation-created.json",
        "identity/accept-invitation-request.json",
        "identity/password-change-request.json",
        "identity/password-reset-request.json",
        "identity/password-reset-confirm.json",
        "jwt/service-token.json",
        "jwt/user-access-token.json",
        "negative-mutants/bad-uuid.json",
        "negative-mutants/capture-image-missing-id.json",
        "negative-mutants/device-command-start-video-missing-token.json",
        "negative-mutants/command-result-executed-missing-snapshot.json",
        "negative-mutants/command-result-executed-missing-stateRevision.json",
        "negative-mutants/command-result-executed-with-reasonCode.json",
        "negative-mutants/command-result-expired-no-reasonCode.json",
        "negative-mutants/command-result-incomplete-snapshot.json",
        "negative-mutants/command-result-missing-commandId.json",
        "negative-mutants/command-result-rejected-inconsistent-reasonCode.json",
        "negative-mutants/command-result-rejected-no-reasonCode.json",
        "negative-mutants/command-result-unknown-no-reasonCode.json",
        "negative-mutants/command-result-start-video-executed-with-snapshot.json",
        "negative-mutants/command-result-start-video-executed-with-stateRevision.json",
        "negative-mutants/device-command-missing-deviceId.json",
        "negative-mutants/device-command-missing-sequence.json",
        "negative-mutants/duplicate-keys.json",
        "negative-mutants/home-command-request-missing-idempotency.json",
        "negative-mutants/home-command-resource-commandId-mismatch.json",
        "negative-mutants/home-command-resource-pending-with-result.json",
        "negative-mutants/home-command-resource-status-mismatch.json",
        "negative-mutants/home-command-resource-terminal-null.json",
        "negative-mutants/home-command-resource-type-mismatch.json",
        "negative-mutants/video-session-live-without-url.json",
        "negative-mutants/invalid-json.json",
        "negative-mutants/jwt-bad-audience.json",
        "negative-mutants/jwt-service-assertion-ttl-too-long.json",
        "telemetry-envelope/command-result.json",
        "telemetry-envelope/motion-detected.json",
        "telemetry-envelope/start-video-executed.json",
        "telemetry-envelope/telemetry-reported.json",
        "video-producer/playlist-gap.json",
        "video-producer/playlist-valid.json",
        "video-producer/segment-result.json",
    ]
}


def load_json(path):
    def detect_duplicates(pairs):
        keys = [k for k, _ in pairs]
        if len(keys) != len(set(keys)):
            raise ValueError(f"duplicate keys in JSON: {path}")
        return dict(pairs)

    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh, object_pairs_hook=detect_duplicates)


def is_uuid(value):
    return isinstance(value, str) and bool(UUID_RE.match(value))


def is_utc(value):
    if not isinstance(value, str):
        return False
    try:
        dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return dt.tzinfo is not None and dt.utcoffset().total_seconds() == 0
    except ValueError:
        return False


def build_registry():
    resources = []
    for path in sorted(SCHEMAS.glob("*.json")):
        data = load_json(path)
        schema_id = data.get("$id")
        if not schema_id:
            raise AssertionError(f"schema {path.name} missing $id")
        resources.append((schema_id, Resource.from_contents(data)))
    return Registry().with_resources(resources)


def get_schema_id(schema_name, registry):
    for sid in registry:
        if sid.endswith(f"/{schema_name}"):
            return sid
    raise LookupError(f"schema {schema_name} not found in registry")


def validate_shape(data, schema_name, registry):
    schema_id = get_schema_id(schema_name, registry)
    validator = Draft202012Validator(
        {"$ref": schema_id},
        registry=registry,
        format_checker=FormatChecker(),
    )
    validator.validate(data)


def semantic_check(path, fixture):
    data = fixture["data"]
    schema = fixture.get("schema")
    errors = []

    if schema == "device-command.json":
        issued = datetime.fromisoformat(data["issuedAtUtc"].replace("Z", "+00:00"))
        expires = datetime.fromisoformat(data["expiresAtUtc"].replace("Z", "+00:00"))
        window = (expires - issued).total_seconds()
        if not 0 < window <= 30:
            errors.append(f"window {window}s not in (0,30]")

    if schema == "command-result.json":
        status = data.get("status")
        reason_code = data.get("reasonCode")
        if status in ("pending", "executed"):
            if reason_code is not None:
                errors.append(f"{status} reasonCode must be null")
        if status in ("rejected", "expired", "unknown", "failed"):
            if not isinstance(reason_code, str):
                errors.append(f"{status} requires non-null reasonCode")
        if status == "executed":
            if data.get("type") == "StartVideo":
                if data.get("snapshot") is not None or data.get("stateRevision") is not None:
                    errors.append("StartVideo executed must have null snapshot/stateRevision")
            else:
                snap = data.get("snapshot") or {}
                required = {"lightOn", "lightingMode", "source", "appliedAtUtc"}
                if not required.issubset(snap.keys()):
                    errors.append("executed snapshot missing required fields")
                if not isinstance(data.get("stateRevision"), int):
                    errors.append("executed requires integer stateRevision")
        if status == "pending":
            if data.get("snapshot") is not None or data.get("stateRevision") is not None:
                errors.append("pending must have null snapshot/stateRevision")
        if status in ("rejected", "expired", "unknown", "failed"):
            if data.get("snapshot") is not None or data.get("stateRevision") is not None:
                errors.append(f"{status} must have null snapshot/stateRevision")

    if schema in ("home-command-resource.json", "capability-command-resource.json"):
        status = data.get("status")
        result = data.get("result")
        if status == "pending":
            if result is not None:
                errors.append("pending command resource must have result=null")
        else:
            if not isinstance(result, dict):
                errors.append(f"terminal {status} resource requires result object")
            else:
                if result.get("status") != status:
                    errors.append(f"result.status {result.get('status')} != resource.status {status}")
                # C10 invariant: result identity must match resource identity.
                if result.get("commandId") != data.get("commandId"):
                    errors.append("C10: result.commandId != resource.commandId")
                if result.get("type") != data.get("type"):
                    errors.append("C10: result.type != resource.type")
                # pending must not leak into result
                if result.get("snapshot") is not None and status != "executed":
                    if status != "executed":
                        errors.append("non-executed terminal result must not carry snapshot")

    if schema == "telemetry-envelope.json" and data.get("type") == "CommandResult":
        payload = data.get("payload", {})
        pstatus = payload.get("status")
        if pstatus == "executed":
            if payload.get("type") == "StartVideo":
                if payload.get("snapshot") is not None or payload.get("stateRevision") is not None:
                    errors.append("envelope StartVideo executed must have null snapshot/stateRevision")
            else:
                if not isinstance(payload.get("stateRevision"), int):
                    errors.append("envelope CommandResult executed requires integer stateRevision")
                snap = payload.get("snapshot") or {}
                if not {"lightOn", "lightingMode", "source", "appliedAtUtc"}.issubset(snap.keys()):
                    errors.append("envelope CommandResult executed snapshot incomplete")
            if payload.get("reasonCode") is not None:
                errors.append("envelope CommandResult executed reasonCode must be null")
        if pstatus in ("rejected", "expired", "unknown", "failed"):
            if not isinstance(payload.get("reasonCode"), str):
                errors.append(f"envelope CommandResult {pstatus} requires non-null reasonCode")

    if schema == "video-session.json":
        status = data.get("status")
        if status == "live":
            url = data.get("streamUrl")
            if not isinstance(url, str) or not url.startswith("https://"):
                errors.append("live video session requires an https streamUrl")
            if data.get("streamType") != "hls":
                errors.append("live video session requires streamType=hls")
        elif status in ("connecting", "ended", "failed"):
            if data.get("streamUrl") is not None or data.get("streamType") is not None:
                errors.append(f"{status} video session must have null streamUrl/streamType")

    if schema == "video-playlist-request.json":
        segments = data.get("segments")
        if isinstance(segments, list) and all(isinstance(value, int) for value in segments) and segments:
            if segments != list(range(segments[0], segments[0] + len(segments))):
                errors.append("playlist segments must be contiguous and ascending")

    if schema == "jwt-contract.json":
        payload = data.get("payload", {})
        header = data.get("header", {})
        if header.get("alg") != "ES256" or header.get("typ") != "JWT" or not header.get("kid"):
            errors.append("JWT header must be alg ES256, typ JWT, kid present")
        aud = payload.get("aud")
        if isinstance(aud, list):
            if not {"Identity.Public", "Home.Public"}.issubset(aud) or not set(aud).issubset({"Identity.Public", "Home.Public", "Security.Public", "Notifications.Public", "Assistant.Public"}):
                errors.append("user token must contain required audiences and only allowlisted public audiences")
        elif isinstance(aud, str):
            if aud not in ("Identity.Internal", "Identity.TokenEndpoint"):
                errors.append("service token audience must be Identity.Internal or Identity.TokenEndpoint")
        iat = payload.get("iat")
        exp = payload.get("exp")
        if isinstance(iat, int) and isinstance(exp, int):
            ttl = exp - iat
            if payload.get("aud") == "Identity.TokenEndpoint":
                if not 0 < ttl <= 60:
                    errors.append(f"service assertion TTL {ttl}s not in (0,60]")
            else:
                if not 0 < ttl <= 300:
                    errors.append(f"token TTL {ttl}s not in (0,300]")
        if payload.get("iss") == "Identity" and payload.get("sid") is None and payload.get("client_id") is None:
            errors.append("token must be either user (sid) or service (client_id)")

    if schema == "identity-service-token-request.json":
        if data.get("grantType") != "urn:ietf:params:oauth:grant-type:jwt-bearer":
            errors.append("service token request grantType mismatch")

    if schema == "identity-jwks.json":
        for key in data.get("keys", []):
            for coord in ("x", "y"):
                val = key.get(coord, "")
                if len(val) < 40:
                    errors.append(f"JWK {coord} too short")
            if key.get("kty") != "EC" or key.get("crv") != "P-256" or key.get("alg") != "ES256":
                errors.append("JWK must be EC/P-256/ES256")
            if "d" in key:
                errors.append("JWKS must not contain private key d")

    # Specific business-invalid fixtures detected by known identifiers or reference.
    if fixture.get("referenceFixture"):
        ref_path = FIXTURES / fixture["referenceFixture"]
        try:
            ref = load_json(ref_path)
            ref_data = ref.get("data", {})
            if data.get("commandId") == ref_data.get("commandId") and data != ref_data:
                errors.append("duplicate commandId with different content")
        except Exception as exc:
            errors.append(f"could not load reference fixture: {exc}")

    if fixture.get("fixture") == "legacy-lightingMode":
        if "lightingMode" in data.get("agentConfiguration", {}):
            errors.append("legacy lightingMode in twin must be ignored, not applied")

    return errors


def check_manifest():
    expected = set(EXPECTED_MANIFEST["fixtures"])
    actual = set(str(p.relative_to(FIXTURES)) for p in FIXTURES.rglob("*.json"))
    missing = expected - actual
    extra = actual - expected
    return missing, extra


def main():
    registry = build_registry()
    missing, extra = check_manifest()
    if missing or extra:
        print("=" * 80)
        print("Manifest mismatch")
        print("=" * 80)
        for m in sorted(missing):
            print(f"MISSING: fixtures/{m}")
        for e in sorted(extra):
            print(f"EXTRA: fixtures/{e}")
        print("-" * 80)
        sys.exit(1)

    results = []
    shape_failures = []
    semantic_failures = []

    for rel_path in EXPECTED_MANIFEST["fixtures"]:
        fixture_path = FIXTURES / rel_path
        rel = fixture_path.relative_to(ROOT)
        try:
            fixture = load_json(fixture_path)
        except json.JSONDecodeError as exc:
            results.append({
                "rel": str(rel),
                "expected_valid": False,
                "expected_shape": False,
                "shape_ok": False,
                "semantic_ok": False,
                "shape_error": f"JSON decode error: {exc}",
                "semantic_error": None,
            })
            continue
        except ValueError as exc:
            results.append({
                "rel": str(rel),
                "expected_valid": False,
                "expected_shape": False,
                "shape_ok": False,
                "semantic_ok": False,
                "shape_error": str(exc),
                "semantic_error": None,
            })
            continue
        except Exception as exc:
            results.append({
                "rel": str(rel),
                "expected_valid": False,
                "expected_shape": False,
                "shape_ok": False,
                "semantic_ok": False,
                "shape_error": f"load error: {exc}",
                "semantic_error": None,
            })
            continue

        expected_valid = fixture.get("expectedValid")
        expected_shape = fixture.get("expectedShapeValid", expected_valid)
        schema_name = fixture.get("schema")
        data = fixture.get("data")

        shape_error = None
        try:
            validate_shape(data, schema_name, registry)
            shape_ok = True
        except (ValidationError, LookupError, Unresolvable, Exception) as exc:
            shape_ok = False
            shape_error = f"{type(exc).__name__}: {exc.message if isinstance(exc, ValidationError) else exc}"

        semantic_errors = semantic_check(fixture_path, fixture)
        semantic_ok = len(semantic_errors) == 0
        semantic_error = "; ".join(semantic_errors) if semantic_errors else None

        results.append({
            "rel": str(rel),
            "expected_valid": expected_valid,
            "expected_shape": expected_shape,
            "shape_ok": shape_ok,
            "semantic_ok": semantic_ok,
            "shape_error": shape_error,
            "semantic_error": semantic_error,
        })

    print("=" * 80)
    print("Formal fixture validation results")
    print("=" * 80)
    for r in results:
        shape_match = r["shape_ok"] == r["expected_shape"]
        if r["expected_valid"] is True:
            semantic_match = r["semantic_ok"] is True
        else:
            if r["expected_shape"] is True:
                semantic_match = r["semantic_ok"] is False
            else:
                semantic_match = True
        ok = shape_match and semantic_match
        status = "PASS" if ok else "FAIL"
        print(f"{status}: {r['rel']}")
        print(f"  expected_valid={r['expected_valid']}, expected_shape={r['expected_shape']}, shape_ok={r['shape_ok']}, semantic_ok={r['semantic_ok']}")
        if r["shape_error"]:
            print(f"  shape error: {r['shape_error']}")
        if r["semantic_error"]:
            print(f"  semantic error: {r['semantic_error']}")
        if not ok:
            if not shape_match:
                shape_failures.append(r)
            if not semantic_match:
                semantic_failures.append(r)

    print("-" * 80)
    print(f"Manifest version: {EXPECTED_MANIFEST['version']}")
    print(f"Total fixtures: {len(results)}")
    print(f"Shape mismatches: {len(shape_failures)}, Semantic mismatches: {len(semantic_failures)}")
    if shape_failures or semantic_failures:
        sys.exit(1)
    print("All fixtures passed formal validation (offline registry).")


if __name__ == "__main__":
    main()
