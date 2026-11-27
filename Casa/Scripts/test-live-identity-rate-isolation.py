#!/usr/bin/env python3
"""Check that failed service assertions do not consume the user login budget."""
import importlib.util
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('identity_checks', root / 'Scripts/test-live-identity.py')
identity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(identity)
account = json.loads((root / 'secrets/admin-bootstrap.json').read_text())

try:
    for _ in range(11):
        status, _ = identity.call('/internal/token', 'POST', {'grantType': 'invalid', 'assertion': 'invalid'})
        assert status == 401, f'Invalid service assertion returned {status}'
    status, session = identity.call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Login after service assertions returned {status}'
    try:
        print('Eleven rejected service assertions did not exhaust the user login limit.')
    finally:
        identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])
except Exception as error:
    print('Rate-isolation check failed:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'Inspect service diagnostics.')
    raise SystemExit(1)
