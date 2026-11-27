#!/usr/bin/env python3
"""Provision distinct service signing keys and pin public keys in Identity; never print secrets."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('deployment_helpers', ROOT / 'Scripts/deploy-identity.py')
helpers = importlib.util.module_from_spec(spec); spec.loader.exec_module(helpers)

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--provision', action='store_true'); args = parser.parse_args()
    if not args.provision: parser.error('Specify --provision.')
    os.umask(0o077)
    if helpers.az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000': raise RuntimeError('Unexpected subscription.')
    names = {entry['name'] for entry in helpers.az('keyvault', 'secret', 'list', '--vault-name', helpers.VAULT)}
    if 'home-service-private' not in names: raise RuntimeError('Existing Home service key is required.')
    with tempfile.TemporaryDirectory(prefix='casa-service-identities-') as temporary:
        directory = Path(temporary); pins = {}; fingerprints = set()
        for label in ('Home', 'Security', 'Notifications', 'Assistant'):
            name = label.lower() + '-service-private'
            if name in names: value = helpers.secret(name)
            else:
                key = directory / (name + '.pem')
                subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(key)], check=True, capture_output=True)
                value = key.read_text(); helpers.set_secret(name, value, directory)
            source = directory / (name + '.source'); source.write_text(value)
            public = subprocess.run(['openssl', 'pkey', '-in', str(source), '-pubout'], check=True, capture_output=True, text=True).stdout
            if public in fingerprints: raise RuntimeError('Two service identities use the same key; refusing to configure them.')
            fingerprints.add(public); pins[f'Services__{label}PublicKeyPem'] = public
        settings = directory / 'pins.json'; settings.write_text(json.dumps(pins))
        helpers.az('webapp', 'config', 'appsettings', 'set', '--resource-group', helpers.GROUP, '--name', helpers.APP, '--settings', '@' + str(settings), output=False)
    print('Four distinct service public keys pinned in Identity; private keys remain in Key Vault.')
    print('New services must receive access only to their own key when deployed.')

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(f'Service identity provisioning stopped: {type(error).__name__}: {error}')
        raise SystemExit(1)
