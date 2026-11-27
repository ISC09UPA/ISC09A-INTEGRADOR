#!/usr/bin/env python3
"""Check authorized Notifications endpoints and dry-run probe without sending real emails."""
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
base = 'https://notify-api.example.com/api/v1/notifications'


def call(path, method='GET', body=None, token=None):
    headers = {'Accept': 'application/json'}
    data = None
    if body is not None:
        headers['Content-Type'] = 'application/json'
        data = json.dumps(body).encode('utf-8')
    if token:
        headers['Authorization'] = 'Bearer ' + token
    try:
        with urllib.request.urlopen(urllib.request.Request(base + path, data=data, headers=headers, method=method), timeout=90) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        raw = error.read()
        try:
            payload = json.loads(raw) if raw else None
        except ValueError:
            payload = None
        return error.code, payload


def main():
    account = json.loads((root / 'secrets/admin-bootstrap.json').read_text())
    status, session = identity.call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Login returned {status}'
    revoked = False
    try:
        prefix = f'/houses/{account["houseId"]}'
        assert call(prefix + '/preferences')[0] == 401, 'Unauthenticated preferences must be denied'

        status, prefs = call(prefix + '/preferences', token=session['accessToken'])
        assert status == 200 and isinstance(prefs.get('email'), bool), f'Preferences returned {status}'
        print('Authorized preferences read succeeded.')

        assert call(f'/houses/{uuid.uuid4()}/preferences', token=session['accessToken'])[0] == 403, 'Other house must be denied'
        assert call(prefix + '/deliveries/' + str(uuid.uuid4()), token=session['accessToken'])[0] == 404, 'Unknown delivery must not exist'
        assert call(prefix + '/diagnostics/bus', token=session['accessToken'])[0] == 200, 'Notifications receiver cannot access notifications-v1'
        assert call(prefix + '/diagnostics/email-provider', token=session['accessToken'])[0] == 200, 'Email provider configuration is not ready'

        status, dry = call(prefix + '/recipient/verify', method='POST', body={'email': account['email'], 'dryRun': True}, token=session['accessToken'])
        assert status == 202 and dry.get('status') == 'dryRun', f'Dry-run verification probe returned {status}'
        print('Notifications bus, email provider configuration, and dry-run probe verified without sending real mail.')

        status, _ = identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])
        assert status == 204, f'Logout returned {status}'
        revoked = True
        assert call(prefix + '/preferences', token=session['accessToken'])[0] == 401, 'Revoked session must be denied'
        print('Notifications rejected the revoked session.')
    finally:
        if not revoked:
            identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print('Notifications live check failed:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'Inspect service diagnostics.')
        raise SystemExit(1)
