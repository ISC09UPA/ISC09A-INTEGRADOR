#!/usr/bin/env python3
"""Verify authorized Home reads against the real account without creating telemetry."""
import importlib.util
import json
from pathlib import Path
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
import uuid

root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('identity_checks', root / 'Scripts/test-live-identity.py')
identity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(identity)
base = 'https://home-api.example.com/api/v1'

def get(path, token=None):
    headers = {'Accept': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    try:
        with urllib.request.urlopen(urllib.request.Request(base + path, headers=headers), timeout=90) as response:
            return response.status, json.loads(response.read())
    except urllib.error.HTTPError as error:
        content = error.read()
        return error.code, json.loads(content) if content else None

def post(path, body, token):
    headers = {'Accept': 'application/json', 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token}
    request = urllib.request.Request(base + path, data=json.dumps(body).encode(), headers=headers, method='POST')
    try:
        with urllib.request.urlopen(request, timeout=90) as response:
            content = response.read(); return response.status, json.loads(content) if content else None
    except urllib.error.HTTPError as error: return error.code, None

def main():
    account = json.loads((root / 'secrets/admin-bootstrap.json').read_text())
    status, session = identity.call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Login returned {status}'
    revoked = False
    try:
        path = f'/houses/{account["houseId"]}/telemetry'
        assert get(path)[0] == 401, 'Unauthenticated telemetry must be denied'
        status, readings = get(path, session['accessToken'])
        assert status == 200 and isinstance(readings, list), f'Telemetry returned {status}'
        assert all({'sensorId', 'value', 'occurredAtUtc', 'receivedAtUtc'} <= item.keys() for item in readings)
        print(f'Authenticated telemetry returned {len(readings)} real readings; unauthenticated request rejected.')
        security_path = f'/houses/{account["houseId"]}/security'
        assert get(security_path)[0] == 401, 'Unauthenticated security must be denied'
        status, state = get(security_path, session['accessToken'])
        assert status == 404 or status == 200 and state.get('mode') in ('armed', 'disarmed'), f'Security returned {status}'
        print('Security state is authorized and reflects a real report when available.')
        resources = f'/houses/{account["houseId"]}/resources'
        assert get(resources)[0] == 401, 'Unauthenticated resource listing must be denied'
        status, items = get(resources, session['accessToken'])
        assert status == 200 and isinstance(items, list), f'Resource listing returned {status}'
        assert get(f'/houses/{uuid.uuid4()}/resources', session['accessToken'])[0] == 403, 'Other house must be denied'
        missing = str(uuid.uuid4()); key = str(uuid.uuid4())
        body = {'type':'OpenDoor','payload':{},'idempotencyKey':key,'expiresAtUtc':(datetime.now(timezone.utc)+timedelta(seconds=10)).isoformat()}
        status, _ = post(f'/houses/{account["houseId"]}/resources/{missing}/commands', body, session['accessToken'])
        assert status == 404, f'Unknown resource returned {status}'
        assert get(f'/houses/{account["houseId"]}/commands/by-key/{key}', session['accessToken'])[0] == 404, 'Unknown resource must never create an order'
        future_key = str(uuid.uuid4())
        future_deadline = (datetime.now(timezone.utc) + timedelta(seconds=30)).strftime('%Y-%m-%dT%H:%M:%SZ')
        assert get(f'/houses/{account["houseId"]}/commands/by-key/{future_key}?expiresAtUtc={future_deadline}', session['accessToken'])[0] == 404, 'Unexpired by-key query must return 404'
        expired_key = str(uuid.uuid4())
        past_deadline = (datetime.now(timezone.utc) - timedelta(seconds=5)).strftime('%Y-%m-%dT%H:%M:%SZ')
        exp_status, exp_problem = get(f'/houses/{account["houseId"]}/commands/by-key/{expired_key}?expiresAtUtc={past_deadline}', session['accessToken'])
        assert exp_status == 422 and (exp_problem or {}).get('code') == 'requestExpired', f'Expired by-key query returned {exp_status}'
        exp_persisted_status, exp_persisted_problem = get(f'/houses/{account["houseId"]}/commands/by-key/{expired_key}', session['accessToken'])
        assert exp_persisted_status == 422 and (exp_persisted_problem or {}).get('code') == 'requestExpired', f'Persisted expired key returned {exp_persisted_status}'
        print(f'Authorized resources returned {len(items)} mappings; another house, unknown device and expired command recovery tombstones behaved correctly.')
        status, _ = identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])
        assert status == 204, f'Logout returned {status}'
        revoked = True
        assert get(path, session['accessToken'])[0] == 401, 'Revoked session must be denied by Home'
        print('Home rejected the revoked session.')
    finally:
        if not revoked:
            identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('Home live check failed:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'See service diagnostics.')
        raise SystemExit(1)
