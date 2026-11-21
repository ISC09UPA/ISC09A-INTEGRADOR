#!/usr/bin/env python3
"""Real endpoint/security/schema/fixture matrix verification.

Loads the OpenAPI contracts, builds the explicit link manifest, and validates
that every fixture matches the exact schema reference, HTTP status and security
declared by the corresponding OAS operation.
"""
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker
from referencing import Registry, Resource
from referencing.exceptions import Unresolvable

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SCHEMAS = ROOT / "schemas"
DEFAULT_OPENAPI = ROOT / "openapi"
DEFAULT_FIXTURES = ROOT / "fixtures"
DEFAULT_LINKS_FILE = Path(__file__).resolve().parent / "matrix_links.json"


def load_json(path):
    def detect_duplicates(pairs):
        keys = [k for k, _ in pairs]
        if len(keys) != len(set(keys)):
            raise ValueError(f"duplicate keys in JSON: {path}")
        return dict(pairs)

    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh, object_pairs_hook=detect_duplicates)


def build_registry(schemas_dir):
    resources = []
    for path in sorted(Path(schemas_dir).glob("*.json")):
        data = load_json(path)
        schema_id = data.get("$id")
        if not schema_id:
            raise AssertionError(f"schema {path.name} missing $id")
        resources.append((schema_id, Resource.from_contents(data)))
    return Registry().with_resources(resources)


def load_openapi_specs(openapi_dir):
    specs = {}
    for path in sorted(Path(openapi_dir).glob("*.json")):
        data = load_json(path)
        for p, item in data.get("paths", {}).items():
            for method, op in item.items():
                if method.lower() not in {"get", "post", "put", "patch", "delete", "head", "options"}:
                    continue
                specs[(p, method.lower())] = {
                    "spec": data,
                    "operation": op,
                    "path": p,
                    "method": method.lower(),
                }
    return specs


def resolve_pointer(doc, pointer):
    """Resolve a JSON pointer (e.g. /components/responses/Forbidden) inside doc."""
    if not pointer or pointer == "/":
        return doc
    if not pointer.startswith("/"):
        raise ValueError(f"invalid pointer: {pointer}")
    current = doc
    for part in pointer[1:].split("/"):
        part = part.replace("~1", "/").replace("~0", "~")
        if isinstance(current, dict):
            current = current[part]
        elif isinstance(current, list):
            current = current[int(part)]
        else:
            raise LookupError(f"cannot traverse into {type(current)}")
    return current


def resolve_oas_ref(spec, obj):
    """If obj is a {$ref: '#/...'}, follow the pointer within the spec."""
    if isinstance(obj, dict) and "$ref" in obj and obj["$ref"].startswith("#"):
        return resolve_pointer(spec, obj["$ref"][1:])
    return obj


def get_security(op, spec):
    """Return expected security name from operation.security or spec.security."""
    sec = op.get("security")
    if sec is None:
        sec = spec.get("security")
    if sec is None or sec == []:
        return "public"
    if len(sec) == 1 and isinstance(sec[0], dict):
        keys = list(sec[0].keys())
        if len(keys) == 1:
            return keys[0]
    return f"unexpected:{sec}"


def get_schema_ref_from_oas(obj):
    """Extract the $ref from application/json content, or None."""
    content = obj.get("content", {})
    media = content.get("application/json", {})
    schema = media.get("schema", {})
    return schema.get("$ref")


def schema_basename(ref):
    if not ref:
        return None
    if "#" in ref:
        ref = ref.split("#", 1)[0]
    return Path(ref).name


def get_fixture_data(fixture_path):
    fixture = load_json(fixture_path)
    return fixture, fixture.get("data"), fixture.get("schema"), fixture.get("httpStatus")


def validate_data_against_schema_ref(data, ref, registry):
    if ref is None:
        raise AssertionError("OAS does not declare a schema $ref for this direction")
    basename = schema_basename(ref)
    schema_id = None
    for sid in registry:
        if sid.endswith(f"/{basename}"):
            schema_id = sid
            break
    if not schema_id:
        raise AssertionError(f"schema {basename} not found in registry")
    validator = Draft202012Validator(
        {"$ref": schema_id},
        registry=registry,
        format_checker=FormatChecker(),
    )
    validator.validate(data)
    return basename


def validate_matrix(links, specs, registry, fixtures_dir):
    """Validate a list of links and return a list of error strings."""
    errors = []
    fixtures_dir = Path(fixtures_dir)

    for link in links:
        key = (link["apiPath"], link["method"].lower())
        if key not in specs:
            errors.append(f"{link['apiPath']} {link['method']}: not found in OpenAPI specs")
            continue
        entry = specs[key]
        op = entry["operation"]
        spec = entry["spec"]

        # Security check
        declared_sec = get_security(op, spec)
        expected_sec = link.get("security", "public")
        if declared_sec != expected_sec:
            errors.append(
                f"{link['apiPath']} {link['method']}: security mismatch OAS={declared_sec} manifest={expected_sec}"
            )

        fixture_path = fixtures_dir / link.get("fixture") if link.get("fixture") else None
        oas_ref = None
        try:
            if link["direction"] == "request":
                body = resolve_oas_ref(spec, op.get("requestBody", {}))
                oas_ref = get_schema_ref_from_oas(body)
                if fixture_path:
                    fixture, data, fixture_schema, _ = get_fixture_data(fixture_path)
                    oas_schema = validate_data_against_schema_ref(data, oas_ref, registry)
                    if fixture_schema and fixture_schema != oas_schema:
                        errors.append(
                            f"{link['apiPath']} {link['method']} request: fixture schema {fixture_schema} != OAS schema {oas_schema}"
                        )
                    link_schema = link.get("schema")
                    if link_schema and link_schema != oas_schema:
                        errors.append(
                            f"{link['apiPath']} {link['method']} request: manifest schema {link_schema} != OAS schema {oas_schema}"
                        )
            elif link["direction"] == "response":
                status = link.get("status")
                response = resolve_oas_ref(spec, op.get("responses", {}).get(str(status)))
                if response is None:
                    errors.append(f"{link['apiPath']} {link['method']}: status {status} not declared in OAS")
                    continue
                oas_ref = get_schema_ref_from_oas(response)
                if fixture_path:
                    fixture, data, fixture_schema, fixture_status = get_fixture_data(fixture_path)
                    if fixture_status and fixture_status != status:
                        errors.append(
                            f"{link['apiPath']} {link['method']} response {status}: fixture httpStatus {fixture_status} != link status {status}"
                        )
                    oas_schema = validate_data_against_schema_ref(data, oas_ref, registry)
                    if fixture_schema and fixture_schema != oas_schema:
                        errors.append(
                            f"{link['apiPath']} {link['method']} response {status}: fixture schema {fixture_schema} != OAS schema {oas_schema}"
                        )
                    link_schema = link.get("schema")
                    if link_schema and link_schema != oas_schema:
                        errors.append(
                            f"{link['apiPath']} {link['method']} response {status}: manifest schema {link_schema} != OAS schema {oas_schema}"
                        )
                elif oas_ref:
                    errors.append(
                        f"{link['apiPath']} {link['method']} response {status}: OAS declares schema but manifest has no fixture"
                    )
        except Exception as exc:
            errors.append(f"{link['apiPath']} {link['method']} {link.get('direction')} {link.get('status', '')}: {exc}")

    return errors


def print_matrix(links, specs):
    print(f"{'Endpoint':<55} {'Security':<20} {'Direction':<10} {'Status':<8} {'Schema':<35} {'Fixture':<45}")
    print("-" * 100)
    for link in links:
        key = (link["apiPath"], link["method"].lower())
        entry = specs.get(key, {})
        op = entry.get("operation", {})
        spec = entry.get("spec", {})
        direction = link["direction"]
        status = str(link.get("status", ""))
        expected_sec = link.get("security", "public")

        # Determine OAS schema for display
        oas_ref = None
        if direction == "request":
            body = resolve_oas_ref(spec, op.get("requestBody", {}))
            oas_ref = get_schema_ref_from_oas(body)
        elif direction == "response":
            response = resolve_oas_ref(spec, op.get("responses", {}).get(str(link.get("status"))))
            if response:
                oas_ref = get_schema_ref_from_oas(response)
        schema_name = schema_basename(oas_ref)
        if schema_name is None:
            schema_name = link.get("schema", "-") if link.get("schema") else "-"
        fixture = link.get("fixture", "-")
        print(f"{link['apiPath']:<55} {expected_sec:<20} {direction:<10} {status:<8} {schema_name:<35} {fixture:<45}")


def main():
    registry = build_registry(DEFAULT_SCHEMAS)
    specs = load_openapi_specs(DEFAULT_OPENAPI)
    manifest = load_json(DEFAULT_LINKS_FILE)
    links = manifest.get("links", [])

    print("=" * 100)
    print("Endpoint / security / schema / fixture matrix (loaded from OpenAPI)")
    print("=" * 100)
    print_matrix(links, specs)

    errors = validate_matrix(links, specs, registry, DEFAULT_FIXTURES)

    print("-" * 100)
    if errors:
        print("Matrix errors:")
        for e in errors:
            print(f"  - {e}")
        sys.exit(1)

    print(f"Matrix valid: {len(links)} links verified against OpenAPI (security, status, schema, fixture content).")


if __name__ == "__main__":
    main()
