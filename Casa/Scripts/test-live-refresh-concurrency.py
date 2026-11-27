#!/usr/bin/env python3
"""Exercise concurrent refresh against the real administrator's separate test session."""
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
from pathlib import Path
import threading

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('identity_checks', ROOT / 'Scripts/test-live-identity.py')
identity = importlib.util.module_from_spec(spec); spec.loader.exec_module(identity)

def main():
    account = json.loads((ROOT / 'secrets/admin-bootstrap.json').read_text())
    status, session = identity.call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Login status {status}'
    barrier = threading.Barrier(2)
    def refresh():
        barrier.wait(timeout=10)
        return identity.call('/auth/refresh', 'POST', {'refreshToken': session['refreshToken']})
    try:
        with ThreadPoolExecutor(max_workers=2) as pool: results = list(pool.map(lambda _: refresh(), range(2)))
        statuses = sorted(status for status, _ in results)
        assert statuses == [200, 401], f'Concurrent refresh statuses {statuses}'
        rotated = next(body for status, body in results if status == 200)
        status, _ = identity.call('/me', token=rotated['accessToken'])
        assert status == 401, f'Replay should revoke the family, got {status}'
        print('Concurrent refresh: one rotation accepted, replay rejected and family revoked.')
    finally:
        identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']})

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('Concurrent refresh check stopped:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'Inspect protected service diagnostics.')
        raise SystemExit(1)
