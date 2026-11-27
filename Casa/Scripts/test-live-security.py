#!/usr/bin/env python3
"""Check real authorized Security reads without sending events or performing actions."""
import importlib.util
import json
from pathlib import Path
import urllib.error
import urllib.request
import uuid

root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('identity_checks', root / 'Scripts/test-live-identity.py')
identity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(identity)
base = 'https://security-api.example.com/api/v1/security'

def get(path, token=None):
    headers = {'Accept': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    try:
        with urllib.request.urlopen(urllib.request.Request(base + path, headers=headers), timeout=90) as response:
            return response.status, json.loads(response.read())
    except urllib.error.HTTPError as error:
        return error.code, None

def main():
    account = json.loads((root / 'secrets/admin-bootstrap.json').read_text())
    status, session = identity.call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Login returned {status}'
    revoked = False
    try:
        prefix = f'/houses/{account["houseId"]}'
        assert get(prefix + '/activity')[0] == 401, 'Unauthenticated history must be denied'
        for path in ('activity', 'incidents'):
            status, page = get(prefix + '/' + path, session['accessToken'])
            assert status == 200 and isinstance(page.get('items'), list), f'{path} returned {status}'
            print(f'Authorized {path} returned {len(page["items"])} real records.')
        assert get(f'/houses/{uuid.uuid4()}/activity', session['accessToken'])[0] == 403, 'Other house must be denied'
        assert get(prefix + '/activity/' + str(uuid.uuid4()), session['accessToken'])[0] == 404, 'Unknown event must not exist'
        assert get(prefix + '/diagnostics/bus', session['accessToken'])[0] == 200, 'Security receiver cannot access its subscription'
        print('Security receiver accessed its subscription without consuming an event.')
        status, _ = get(prefix + '/mode', session['accessToken'])
        assert status in (200, 404), f'Mode returned {status}'
        print('Another house and an unknown event were rejected; mode reflects the associated controller or is absent.')
        status, _ = identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])
        assert status == 204, f'Logout returned {status}'
        revoked = True
        assert get(prefix + '/activity', session['accessToken'])[0] == 401, 'Revoked session must be denied'
        print('Security rejected the revoked session.')
    finally:
        if not revoked: identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('Security live check failed:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'Inspect service diagnostics.')
        raise SystemExit(1)
