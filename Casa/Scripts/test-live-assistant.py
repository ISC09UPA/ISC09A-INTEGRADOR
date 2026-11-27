#!/usr/bin/env python3
"""Exercise real account authorization without publishing a house report or sending mail."""
import importlib.util
import json
from pathlib import Path
import urllib.error
import urllib.request
import uuid

root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('identity_checks', root / 'Scripts/test-live-identity.py')
identity = importlib.util.module_from_spec(spec); spec.loader.exec_module(identity)
base = 'https://assistant-api.example.com/api/v1/assistant/houses/'

def ask(house, token=None):
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    request = urllib.request.Request(base + house + '/ask', data=json.dumps({'message': '¿Hay reportes de mi casa?'}).encode(), headers=headers, method='POST')
    try:
        with urllib.request.urlopen(request, timeout=90) as response: return response.status, json.loads(response.read())
    except urllib.error.HTTPError as error: return error.code, None

def main():
    account = json.loads((root / 'secrets/admin-bootstrap.json').read_text())
    status, session = identity.call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Login returned {status}'
    revoked = False
    try:
        assert ask(account['houseId'])[0] == 401, 'Unauthenticated requests must be denied'
        assert ask(str(uuid.uuid4()), session['accessToken'])[0] == 403, 'Other house must be denied'
        status, response = ask(account['houseId'], session['accessToken'])
        assert status == 200 and response.get('text'), f'Assistant returned {status}'
        print('Assistant responded for the authorized house; it denied unauthenticated and unrelated-house requests.')
        status, _ = identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])
        assert status == 204; revoked = True
        assert ask(account['houseId'], session['accessToken'])[0] == 401, 'Revoked session must be denied'
        print('Assistant rejected the revoked session.')
    finally:
        if not revoked: identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('Assistant live check failed:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'Check service diagnostics.')
        raise SystemExit(1)
