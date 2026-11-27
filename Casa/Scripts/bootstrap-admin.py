#!/usr/bin/env python3
"""Provision the operator-authorized real account without printing its credentials."""
import argparse
import importlib.util
import ipaddress
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('identity_deployment', ROOT / 'Scripts/deploy-identity.py')
deployment = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deployment)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--email', required=True)
    args = parser.parse_args()
    os.umask(0o077)
    if deployment.az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000':
        raise RuntimeError('Unexpected subscription; no changes performed.')
    folder = ROOT / 'secrets'
    folder.mkdir(exist_ok=True)
    path = folder / 'admin-bootstrap.json'
    if path.exists():
        record = json.loads(path.read_text())
        if record['email'].casefold() != args.email.casefold(): raise RuntimeError('Existing local bootstrap credentials belong to a different email.')
    else:
        record = {'email': args.email, 'password': secrets.token_urlsafe(24), 'status': 'pending'}
        path.write_text(json.dumps(record, indent=2))
    account = deployment.az('sql', 'server', 'show', '--resource-group', deployment.GROUP, '--name', deployment.SERVER)
    password = deployment.secret('sql-admin-password')
    connection = f'Server=tcp:{account["fullyQualifiedDomainName"]},1433;Database=IdentityDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;User ID={account["administratorLogin"]};Password={password};'
    environment = os.environ.copy()
    environment.update({'ConnectionStrings__IdentityDb': connection, 'Bootstrap__Email': record['email'], 'Bootstrap__Password': record['password'], 'Bootstrap__HouseName': 'Casa Domótica', 'Logging__LogLevel__Default': 'Warning'})
    address = str(ipaddress.ip_address(urllib.request.urlopen('https://api.ipify.org', timeout=15).read().decode().strip()))
    rule = 'identity-bootstrap-local'
    existing = deployment.az('sql', 'server', 'firewall-rule', 'list', '--resource-group', deployment.GROUP, '--server', deployment.SERVER)
    if any(item['name'] == rule and (item['startIpAddress'] != address or item['endIpAddress'] != address) for item in existing): raise RuntimeError('Existing temporary rule has another address; inspect it first.')
    deployment.az('sql', 'server', 'firewall-rule', 'create', '--resource-group', deployment.GROUP, '--server', deployment.SERVER, '--name', rule, '--start-ip-address', address, '--end-ip-address', address, output=False)
    try:
        result = subprocess.run(['dotnet', 'run', '--no-launch-profile', '--project', str(deployment.PROJECT), '--', '--bootstrap-admin'], env=environment, cwd=ROOT, capture_output=True, text=True)
        if result.returncode:
            (folder / 'admin-bootstrap.log').write_text(result.stdout + result.stderr)
            raise RuntimeError('Administrator bootstrap failed; diagnostic log is protected locally.')
        identifiers = re.search(r'UserId=([a-fA-F0-9-]+); HouseId=([a-fA-F0-9-]+)', result.stdout)
        if not identifiers: raise RuntimeError('Bootstrap result did not contain account identifiers.')
        record.update({'status': 'created', 'userId': identifiers.group(1), 'houseId': identifiers.group(2)})
        path.write_text(json.dumps(record, indent=2))
        print('Real administrator account created. Credentials retained in a protected, Git-ignored local file.')
        print('UserId:', record['userId'])
        print('HouseId:', record['houseId'])
    finally:
        deployment.az('sql', 'server', 'firewall-rule', 'delete', '--resource-group', deployment.GROUP, '--server', deployment.SERVER, '--name', rule, output=False)

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(f'Administrator bootstrap stopped: {type(error).__name__}: {error}')
        raise SystemExit(1)
