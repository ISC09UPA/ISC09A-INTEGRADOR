#!/usr/bin/env python3
"""Adversarial audit: inject mutations into the matrix/fixtures and verify rejection.

This harness proves that matrix_check validates correspondence, not just path
existence. It works on temporary copies; canonical fixtures are never modified.
"""
import copy
import json
import shutil
import sys
import tempfile
from pathlib import Path

from matrix_check import build_registry, load_json, load_openapi_specs, validate_matrix

ROOT = Path(__file__).resolve().parent.parent
SCHEMAS = ROOT / "schemas"
OPENAPI = ROOT / "openapi"
FIXTURES = ROOT / "fixtures"
LINKS_FILE = Path(__file__).resolve().parent / "matrix_links.json"


def save_json(path, data):
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2)


def find_link(links, api_path, method, direction, status=None):
    for link in links:
        if (
            link["apiPath"] == api_path
            and link["method"].lower() == method.lower()
            and link["direction"] == direction
            and (status is None or link.get("status") == status)
        ):
            return link
    raise LookupError(f"link not found: {api_path} {method} {direction} {status}")


def run_audit(name, mutate_fn):
    """Run one audit case. Returns (name, passed, errors)."""
    with tempfile.TemporaryDirectory() as tmp:
        tmp_fixtures = Path(tmp) / "fixtures"
        tmp_links = Path(tmp) / "matrix_links.json"
        shutil.copytree(FIXTURES, tmp_fixtures)

        manifest = load_json(LINKS_FILE)
        links = copy.deepcopy(manifest["links"])
        mutate_fn(links, tmp_fixtures)

        save_json(tmp_links, {"version": manifest["version"], "links": links})

        registry = build_registry(SCHEMAS)
        specs = load_openapi_specs(OPENAPI)
        errors = validate_matrix(links, specs, registry, tmp_fixtures)

        passed = len(errors) > 0
        return name, passed, errors


def main():
    cases = []

    # 1. Mutate security in manifest: authz/check should be serviceBearerAuth, not userBearerAuth.
    def mutate_security(links, _fixtures_dir):
        link = find_link(links, "/identity/internal/authz/check", "post", "request")
        link["security"] = "userBearerAuth"

    cases.append(run_audit("security mismatch (authz/check request -> userBearerAuth)", mutate_security))

    # 2. Mutate response status in manifest: 200 -> 201 (not declared in OAS).
    def mutate_status(links, _fixtures_dir):
        link = find_link(links, "/identity/internal/authz/check", "post", "response", 200)
        link["status"] = 201

    cases.append(run_audit("status mismatch (authz/check 200 -> 201)", mutate_status))

    # 3. Mutate schema link in manifest: point login response to request schema.
    def mutate_schema_link(links, _fixtures_dir):
        link = find_link(links, "/identity/auth/login", "post", "response", 200)
        link["schema"] = "identity-login-request.json"

    cases.append(run_audit("schema link mismatch (login response schema -> request)", mutate_schema_link))

    # 4. Mutate fixture body while keeping file path valid: logout request missing refreshToken.
    def mutate_fixture_body(_links, fixtures_dir):
        path = fixtures_dir / "identity" / "logout-request.json"
        fixture = load_json(path)
        fixture["data"] = {"refreshToken": "short"}
        save_json(path, fixture)

    cases.append(run_audit("fixture body invalid (logout refreshToken too short)", mutate_fixture_body))

    # 5. Mutate fixture body: home command resource terminal result=null.
    def mutate_terminal_null(_links, fixtures_dir):
        path = fixtures_dir / "home-api" / "command-response-applied.json"
        fixture = load_json(path)
        fixture["data"]["result"] = None
        save_json(path, fixture)

    cases.append(run_audit("fixture body invalid (terminal result=null)", mutate_terminal_null))

    # 6. Mutate fixture body: authz check granted fixture used for 403 status (wrong shape).
    def mutate_status_shape(_links, fixtures_dir):
        path = fixtures_dir / "authz" / "check-response-denied.json"
        fixture = load_json(path)
        fixture["data"] = {"decision": "allowed"}  # invalid for problem-details.json
        save_json(path, fixture)

    cases.append(run_audit("fixture body shape mismatch (403 body -> 200 shape)", mutate_status_shape))

    print("=" * 80)
    print("Adversarial audit results")
    print("=" * 80)
    all_passed = True
    for name, passed, errors in cases:
        status = "REJECTED (OK)" if passed else "ACCEPTED (FAIL)"
        print(f"{status}: {name}")
        if passed:
            for e in errors[:3]:
                print(f"  -> {e}")
        else:
            all_passed = False
            print("  -> mutation was NOT rejected")
    print("-" * 80)
    if all_passed:
        print(f"All {len(cases)} adversarial cases were correctly rejected.")
    else:
        print("Some adversarial cases were incorrectly accepted.")
        sys.exit(1)


if __name__ == "__main__":
    main()
