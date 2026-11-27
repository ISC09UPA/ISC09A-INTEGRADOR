#!/usr/bin/env python3
"""Verify private video routes against the real account without creating a session.

The check uses an intentionally missing camera and session. It never uploads video,
creates a producer token, or prints credentials.
"""
import importlib.util
import json
import urllib.error
import urllib.request
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('identity_checks', ROOT / 'Scripts/test-live-identity.py')
identity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(identity)

BASE = 'https://home-api.example.com'
API = BASE + '/api/v1'

def call(url, method='GET', body=None, token=None, headers=None, raw=None):
    request_headers = {'Accept': 'application/json'}
    if headers:
        request_headers.update(headers)
    data = raw
    if body is not None:
        request_headers['Content-Type'] = 'application/json'
        data = json.dumps(body).encode()
    if token:
        request_headers['Authorization'] = 'Bearer ' + token
    request = urllib.request.Request(url, data=data, headers=request_headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=90) as response:
            content = response.read()
            return response.status, json.loads(content) if content else None
    except urllib.error.HTTPError as error:
        content = error.read()
        return error.code, json.loads(content) if content else None

def main():
    account = json.loads((ROOT / 'secrets/admin-bootstrap.json').read_text())
    status, session = identity.call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Login returned {status}'
    token = session['accessToken']
    house = account['houseId']
    missing = str(uuid.uuid4())
    try:
        assert call(f'{API}/houses/{house}/video/sessions/{missing}')[0] == 401
        assert call(f'{API}/houses/{house}/video/sessions/{missing}', token=token)[0] == 404
        assert call(f'{API}/houses/{house}/video/sessions/{missing}/playlist.m3u8', token=token)[0] == 404
        assert call(f'{API}/houses/{house}/video/sessions/{missing}/segments/0.ts', token=token)[0] == 404
        key = str(uuid.uuid4())
        body = {'resourceId': 'missing-camera-' + key, 'idempotencyKey': key, 'correlationId': str(uuid.uuid4())}
        assert call(f'{API}/houses/{house}/video/sessions', 'POST', body, token)[0] == 404, 'A missing camera must not create a session'
        assert call(f'{API}/houses/{house}/video/sessions/by-key/{key}', token=token)[0] == 404
        status, _ = call(f'{BASE}/internal/video/{missing}/segments/0', 'POST', headers={
            'Content-Type': 'video/mp2t',
            'X-Segment-Duration-Ms': '2000',
            'X-Video-Token': 'X' * 43,
        }, raw=b'\x47' + b'\x00' * 187)
        assert status == 401, f'Invalid producer token returned {status}'
        print('Live video checks passed: unauthorized and missing resources were rejected; no session or stream was fabricated.')
    finally:
        identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, token)

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print('Live video check failed:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'See protected diagnostics.')
        raise SystemExit(1)
