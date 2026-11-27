#!/usr/bin/env python3
"""Verify distinct real service keys, replay protection and action isolation without displaying credentials."""
import base64
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
def load(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path); module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module); return module
identity = load('identity_checks', 'Scripts/test-live-identity.py')
deployment = load('deployment_helpers', 'Scripts/deploy-identity.py')

def b64(value): return base64.urlsafe_b64encode(value).rstrip(b'=').decode()
def assertion(keypath, client):
    now = int(time.time())
    header = b64(json.dumps({'alg':'ES256','typ':'JWT','kid':client+'-v1'}, separators=(',', ':')).encode())
    body = b64(json.dumps({'iss':'service:'+client,'sub':'service:'+client,'aud':'Identity.TokenEndpoint','client_id':client,'jti':str(uuid.uuid4()),'iat':now,'exp':now+60}, separators=(',', ':')).encode())
    signed = (header+'.'+body).encode()
    der = subprocess.run(['openssl','dgst','-sha256','-sign',str(keypath)], input=signed, capture_output=True, check=True).stdout
    # ASN.1 ECDSA SEQUENCE of two INTEGERs; ES256 JWT uses the fixed-size raw pair.
    if len(der) < 8 or der[0] != 0x30 or der[2] != 0x02: raise ValueError('Unexpected signature format')
    rlen = der[3]; r = der[4:4+rlen]; pos = 4+rlen
    if der[pos] != 0x02: raise ValueError('Unexpected signature format')
    slen = der[pos+1]; s = der[pos+2:pos+2+slen]
    raw = int.from_bytes(r,'big').to_bytes(32,'big') + int.from_bytes(s,'big').to_bytes(32,'big')
    return signed.decode()+'.'+b64(raw)

def main():
    os.umask(0o077)
    if deployment.az('account','show')['id'] != '00000000-0000-0000-0000-000000000000': raise RuntimeError('Unexpected subscription.')
    account = json.loads((ROOT/'secrets/admin-bootstrap.json').read_text())
    status, session = identity.call('/auth/login','POST',{'email':account['email'],'password':account['password']})
    assert status == 200, f'Login returned {status}'
    try:
        with tempfile.TemporaryDirectory(prefix='casa-service-test-') as tmp:
            keys = {}
            for client in ('home','security','notifications','assistant'):
                path = Path(tmp)/(client+'.pem'); path.write_text(deployment.secret(client+'-service-private')); keys[client] = path
            forged = assertion(keys['home'], 'security')
            status, _ = identity.call('/internal/token','POST',{'grantType':'urn:ietf:params:oauth:grant-type:jwt-bearer','assertion':forged})
            assert status == 401, f'Cross-service key accepted: {status}'
            for client, action in [('home','home:read'),('security','security:manage'),('notifications','notifications:manage'),('assistant','assistant:read')]:
                signed = assertion(keys[client], client)
                status, token = identity.call('/internal/token','POST',{'grantType':'urn:ietf:params:oauth:grant-type:jwt-bearer','assertion':signed})
                assert status == 200, f'{client} token returned {status}'
                request = {'userId':account['userId'],'sessionId':session['sessionId'],'houseId':account['houseId'],'action':action,'correlationId':str(uuid.uuid4())}
                status, decision = identity.call('/internal/authz/check','POST',request,token['accessToken'])
                assert status == 200 and decision.get('decision') == 'allowed', f'{client} authorization returned {status}'
                if client == 'home':
                    request['action'] = 'home:control:capture'
                    status, decision = identity.call('/internal/authz/check','POST',request,token['accessToken'])
                    assert status == 200 and decision.get('decision') == 'allowed', f'Capture authorization returned {status}'
                request['action'] = 'security:manage' if client != 'security' else 'notifications:manage'
                status, _ = identity.call('/internal/authz/check','POST',request,token['accessToken'])
                assert status == 403, f'{client} cross-service action returned {status}'
                if client == 'home':
                    status, _ = identity.call('/internal/token','POST',{'grantType':'urn:ietf:params:oauth:grant-type:jwt-bearer','assertion':signed})
                    assert status == 401, f'Assertion replay returned {status}'
            print('Four distinct service identities verified; key substitution, replay and cross-service actions rejected.')
    finally:
        identity.call('/auth/logout','POST',{'refreshToken':session['refreshToken']},session['accessToken'])

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print('Service authorization check stopped:', type(error).__name__, str(error) if isinstance(error, AssertionError) else 'Inspect protected diagnostics.')
        raise SystemExit(1)
