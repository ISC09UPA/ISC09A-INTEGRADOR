#!/usr/bin/env python3
"""Formal contract validation with offline registry and reference traversal.

- Every schema is checked against the JSON Schema 2020-12 meta-schema.
- All local $ref targets are resolved through the explicit $id registry.
- Internal fragment pointers (same document) are resolved against the schema itself.
- OpenAPI 3.1.0 documents are validated formally.
- No network access is used.
"""
import json
import re
import sys
from pathlib import Path

from jsonschema import Draft202012Validator
from openapi_spec_validator import openapi_v31_spec_validator, validate_spec
from referencing import Registry, Resource
from referencing.exceptions import Unresolvable

ROOT = Path(__file__).resolve().parent.parent
SCHEMAS = ROOT / "schemas"
OPENAPI = ROOT / "openapi"

SECRET_PATTERNS = [
    re.compile(r"AccountKey=", re.I),
    re.compile(r"SharedAccessKey=", re.I),
    re.compile(r"-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    re.compile(r"IOTHUB_DEVICE_CONNECTION_STRING"),
]


def load_json(path):
    def detect_duplicates(pairs):
        keys = [k for k, _ in pairs]
        if len(keys) != len(set(keys)):
            raise ValueError(f"duplicate keys in JSON: {path}")
        return dict(pairs)

    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh, object_pairs_hook=detect_duplicates)


def check_no_secrets(path, text):
    for pattern in SECRET_PATTERNS:
        if pattern.search(text):
            raise AssertionError(f"possible secret/credential pattern found in {path}")


def resolve_fragment(data, fragment):
    """Resolve a JSON pointer fragment against a loaded JSON document."""
    if not fragment or fragment == "/":
        return data
    if not fragment.startswith("/"):
        raise ValueError(f"invalid fragment: {fragment}")
    parts = fragment[1:].split("/")
    current = data
    for part in parts:
        part = part.replace("~1", "/").replace("~0", "~")
        if isinstance(current, dict):
            if part not in current:
                raise LookupError(f"pointer part {part!r} not found")
            current = current[part]
        elif isinstance(current, list):
            idx = int(part)
            current = current[idx]
        else:
            raise LookupError(f"cannot traverse into {type(current)}")
    return current


def build_registry():
    resources = []
    for path in sorted(SCHEMAS.glob("*.json")):
        data = load_json(path)
        schema_id = data.get("$id")
        if not schema_id:
            raise AssertionError(f"schema {path.name} missing $id")
        resources.append((schema_id, Resource.from_contents(data)))
    return Registry().with_resources(resources)


def collect_refs(obj):
    refs = []
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k == "$ref" and isinstance(v, str) and not v.startswith("http"):
                refs.append(v)
            else:
                refs.extend(collect_refs(v))
    elif isinstance(obj, list):
        for item in obj:
            refs.extend(collect_refs(item))
    return refs


def validate_schema(path, data, registry):
    if "$schema" not in data and "$id" not in data:
        raise AssertionError("schema should declare $schema or $id")
    if "title" not in data:
        raise AssertionError("schema should declare title")
    Draft202012Validator.check_schema(data)
    schema_id = data.get("$id", f"file://{path}")
    for ref in collect_refs(data):
        if "#" in ref:
            base, fragment = ref.split("#", 1)
        else:
            base, fragment = ref, ""
        if not base:
            # Same-document fragment pointer; resolve against the current schema.
            try:
                resolve_fragment(data, fragment)
            except Exception as exc:
                raise AssertionError(f"unresolved same-document ref {ref}: {exc}")
        else:
            try:
                registry.resolver(base_uri=schema_id).lookup(ref)
            except Unresolvable as exc:
                raise AssertionError(f"unresolved ref {ref}: {exc}")


def validate_openapi(path, data):
    validate_spec(
        data,
        base_uri=f"file://{path}",
        validator=openapi_v31_spec_validator,
    )


def main():
    registry = build_registry()
    results = []
    failures = []

    for path in sorted(SCHEMAS.glob("*.json")):
        try:
            data = load_json(path)
            validate_schema(path, data, registry)
            check_no_secrets(path, path.read_text())
            results.append((path.relative_to(ROOT), True, None))
        except Exception as exc:
            failures.append((path.relative_to(ROOT), str(exc)))
            results.append((path.relative_to(ROOT), False, str(exc)))

    for path in sorted(OPENAPI.glob("*.json")):
        try:
            data = load_json(path)
            validate_openapi(path, data)
            check_no_secrets(path, path.read_text())
            results.append((path.relative_to(ROOT), True, None))
        except Exception as exc:
            failures.append((path.relative_to(ROOT), str(exc)))
            results.append((path.relative_to(ROOT), False, str(exc)))

    print("=" * 70)
    print("Formal contract validation results")
    print("=" * 70)
    for rel, ok, err in results:
        status = "PASS" if ok else "FAIL"
        print(f"{status}: {rel}")
        if err:
            print(f"  -> {err}")
    print("-" * 70)
    print(f"Total: {len(results)}, failures: {len(failures)}")
    if failures:
        sys.exit(1)
    print("All contracts passed formal validation (offline registry + fragment pointers).")


if __name__ == "__main__":
    main()
