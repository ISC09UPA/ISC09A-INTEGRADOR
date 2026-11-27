#!/usr/bin/env python3
"""Exercise the authorized real account; never prints passwords or tokens and sends no email."""
import base64
import json
from pathlib import Path
import urllib.error
import urllib.request
ROOT = Path(__file__).resolve().parents[1]
BASE = 'https://identity-api.example.com/api/v1/identity'

def call(path, method='GET', body=None, token=None):
    headers = {'Accept': 'application/json'}
    data = None
    if body is not None:
        headers['Content-Type'] = 'application/json'; data = json.dumps(body).encode()
    if token: headers['Authorization'] = 'Bearer ' + token
    request = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=90) as response:
            content = response.read(); return response.status, json.loads(content) if content else None
    except urllib.error.HTTPError as error:
        content = error.read(); return error.code, json.loads(content) if content else None

def main():
    account = json.loads((ROOT / 'secrets/admin-bootstrap.json').read_text())
    assert account.get('status') == 'created', 'Real administrator must exist before this test.'
    status, _ = call('/auth/login', 'POST', {'email': account['email'], 'password': 'deliberately-invalid-application-password'})
    assert status == 401, f'Wrong password status was {status}'; print('Wrong password rejected: 401')
    status, session = call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Real login status was {status}'
    assert session['userId'] == account['userId'] and session['expiresIn'] == 300
    payload = session['accessToken'].split('.')[1]
    claims = json.loads(base64.urlsafe_b64decode(payload + '=' * (-len(payload) % 4)))
    assert set(claims['aud']) == {'Identity.Public', 'Home.Public', 'Security.Public', 'Notifications.Public', 'Assistant.Public'}, 'Unexpected public audiences'
    status, profile = call('/me', token=session['accessToken'])
    assert status == 200 and profile['userId'] == account['userId']
    assert any(house['houseId'] == account['houseId'] and house['role'] == 'admin' for house in profile['houses'])
    print('Real SQL-backed login and administrator membership verified: 200')
    status, rotated = call('/auth/refresh', 'POST', {'refreshToken': session['refreshToken']})
    assert status == 200 and rotated['refreshToken'] != session['refreshToken'], f'Refresh status was {status}'; print('Refresh token rotated: 200')
    status, _ = call('/auth/refresh', 'POST', {'refreshToken': session['refreshToken']})
    assert status == 401, f'Replay status was {status}'; print('Consumed refresh token rejected: 401')
    status, _ = call('/me', token=rotated['accessToken'])
    assert status == 401, f'Revoked session status was {status}'; print('Replay revoked the session family: 401')
    status, second = call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Second login status was {status}'
    status, _ = call('/auth/logout', 'POST', {'refreshToken': second['refreshToken']})
    assert status == 204, f'Logout status was {status}'; print('Logout revocation persisted: 204')
    status, _ = call('/me', token=second['accessToken'])
    assert status == 401; print('Access denied after logout: 401')
    print('Identity live authentication checks passed. No credentials or tokens were printed.')

if __name__ == '__main__':
    try: main()
    except Exception as error:
        # Avoid dumping response objects, request bodies or URLs containing credentials.
        print('Identity live checks stopped:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'Check the protected service diagnostics.')
        raise SystemExit(1)
