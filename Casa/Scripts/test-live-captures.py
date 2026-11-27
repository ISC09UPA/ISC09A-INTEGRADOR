#!/usr/bin/env python3
"""Verify capture endpoints and Vision authentication without creating images or records."""
import importlib.util
import json
from pathlib import Path
import urllib.error
import urllib.request
import uuid

root = Path(__file__).resolve().parents[1]
def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, root / filename)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module); return module
identity = load('identity_checks', 'Scripts/test-live-identity.py')
security = load('security_checks', 'Scripts/test-live-security.py')
deployment = load('home_deploy', 'Scripts/deploy-home.py')
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl): return None
opener = urllib.request.build_opener(NoRedirect())

def call(url, method, token=None, data=None, headers=None):
    supplied = dict(headers or {})
    if token: supplied['Authorization'] = 'Bearer ' + token
    request = urllib.request.Request(url, data=data, headers=supplied, method=method)
    try:
        with opener.open(request, timeout=90) as response: return response.status, response.read()
    except urllib.error.HTTPError as error: return error.code, error.read()

def main():
    account = json.loads((root / 'secrets/admin-bootstrap.json').read_text())
    status, session = identity.call('/auth/login', 'POST', {'email': account['email'], 'password': account['password']})
    assert status == 200, f'Login returned {status}'
    try:
        prefix = f'/houses/{account["houseId"]}'
        status, page = security.get(prefix + '/captures', session['accessToken'])
        assert status == 200 and isinstance(page.get('items'), list), f'Capture listing returned {status}'
        print(f'Authorized capture listing returned {len(page["items"])} real records.', flush=True)
        status, retention = security.get(prefix + '/capture-retention', session['accessToken'])
        assert status == 200 and 'retentionDays' in retention, f'Retention lookup returned {status}'
        print('Authorized retention policy was read without changing it; automatic deletion is', 'disabled.' if retention['retentionDays'] is None else 'configured.', flush=True)
        assert security.get(prefix + '/diagnostics/capture-storage', session['accessToken'])[0] == 200, 'Private storage is unavailable to Security'
        print('Security accessed its private capture container.', flush=True)
        missing = str(uuid.uuid4()); path = security.base + prefix + '/captures/' + missing
        assert call(path + '/content', 'GET', session['accessToken'])[0] == 404, 'Unknown capture content must not exist'
        assert call(path + '/deletion', 'GET', session['accessToken'])[0] == 404, 'Unknown capture deletion status must not exist'
        assert call(path + '/analysis', 'POST', session['accessToken'], b'')[0] == 404, 'Unknown capture cannot be analyzed'
        assert call(path, 'DELETE', session['accessToken'])[0] == 404, 'Unknown capture cannot be deleted'
        assert security.get(f'/houses/{uuid.uuid4()}/captures', session['accessToken'])[0] == 403, 'Other house must be denied'
        upload = 'https://home-api.example.com/internal/captures/' + str(uuid.uuid4()) + '/upload'
        status, _ = call(upload, 'POST', data=b'x', headers={'Content-Type': 'image/png', 'X-Capture-Token': 'x' * 43})
        assert status == 401, f'Unknown upload ticket returned {status}'
        print(f'Capture listing returned {len(page["items"])} real records; private storage and authorization checks passed.')
    finally:
        identity.call('/auth/logout', 'POST', {'refreshToken': session['refreshToken']}, session['accessToken'])
    # An empty request is not an image and does not create a record; the provider's
    # invalid-image response establishes that the configured key reaches Vision.
    key = deployment.secret('security-vision-api-key')
    endpoint = 'https://westus.api.cognitive.microsoft.com/computervision/imageanalysis:analyze?api-version=2024-02-01&features=caption&language=en'
    rejected, _ = call(endpoint, 'POST', data=b'', headers={'Content-Type': 'application/octet-stream', 'Ocp-Apim-Subscription-Key': 'invalid'})
    assert rejected == 401, f'Vision invalid-key check returned {rejected}'
    status, raw = call(endpoint, 'POST', data=b'', headers={'Content-Type': 'application/octet-stream', 'Ocp-Apim-Subscription-Key': key})
    try: error_code = json.loads(raw).get('error', {}).get('code', '')
    except (ValueError, AttributeError): error_code = ''
    assert status == 400 and error_code in ('InvalidImage', 'InvalidImageSize', 'InvalidRequest', 'BadArgument'), f'Vision empty-image check returned {status} ({error_code})'
    print('Vision accepted authentication and rejected an empty image; no image was submitted.')

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('Capture live check failed:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'Inspect protected diagnostics.')
        raise SystemExit(1)
