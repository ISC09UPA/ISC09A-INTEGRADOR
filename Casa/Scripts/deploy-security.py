#!/usr/bin/env python3
"""Migrate and publish Security to its existing project resources without printing secrets."""
import argparse
import importlib.util
import ipaddress
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('home_deploy', ROOT / 'Scripts/deploy-home.py')
home = importlib.util.module_from_spec(spec)
spec.loader.exec_module(home)
GROUP, VAULT, SERVER = home.GROUP, home.VAULT, home.SERVER
APP = 'security-api-example'
PROJECT = ROOT / 'Backend/Security/src/CasaDomotica.Security.Api/CasaDomotica.Security.Api.csproj'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--deploy', action='store_true')
    if not parser.parse_args().deploy: parser.error('Specify --deploy.')
    os.umask(0o077)
    (ROOT / 'secrets').mkdir(exist_ok=True)
    if home.az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000': raise RuntimeError('Unexpected Azure subscription.')
    app = home.az('webapp', 'show', '--resource-group', GROUP, '--name', APP)
    server = home.az('sql', 'server', 'show', '--resource-group', GROUP, '--name', SERVER)
    vault = home.az('keyvault', 'show', '--resource-group', GROUP, '--name', VAULT)
    bus = home.az('servicebus', 'namespace', 'show', '--resource-group', GROUP, '--name', 'sb-casa-example')
    storage = home.az('storage', 'account', 'show', '--resource-group', GROUP, '--name', 'sti9ncasa-example')
    vision = home.az('cognitiveservices', 'account', 'show', '--resource-group', GROUP, '--name', 'vision-casa-example')
    names = {item['name'] for item in home.az('keyvault', 'secret', 'list', '--vault-name', VAULT)}
    if 'security-service-private' not in names: raise RuntimeError('Security service signing key is missing.')
    with tempfile.TemporaryDirectory(prefix='casa-security-deploy-') as temp:
        directory = Path(temp)
        identity_public = home.public_key(home.secret('identity-jwt-private'), directory, 'identity-public-source')
        previous = home.secret('security-db-connection') if 'security-db-connection' in names else ''
        match = re.search(r'(?:^|;)Password=([^;]+)', previous, flags=re.I)
        password = match.group(1) if match else secrets.token_urlsafe(36)
        base = f'Server=tcp:{server["fullyQualifiedDomainName"]},1433;Database=SecurityDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
        admin_connection = base + f'User ID={server["administratorLogin"]};Password={home.secret("sql-admin-password")};'
        runtime_connection = base + f'User ID=security_api;Password={password};'
        environment = os.environ.copy()
        environment.update({'ConnectionStrings__SecurityDb': admin_connection, 'Runtime__Password': password, 'Logging__LogLevel__Default': 'Warning'})
        ip = str(ipaddress.ip_address(urllib.request.urlopen('https://api.ipify.org', timeout=15).read().decode().strip()))
        rule = 'security-deploy-local'
        existing = {item['name']: item for item in home.az('sql', 'server', 'firewall-rule', 'list', '--resource-group', GROUP, '--server', SERVER)}
        if rule in existing and (existing[rule]['startIpAddress'] != ip or existing[rule]['endIpAddress'] != ip): raise RuntimeError('Temporary firewall rule belongs to another address.')
        home.az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', rule, '--start-ip-address', ip, '--end-ip-address', ip, output=False)
        try:
            print('Applying Security migration to Azure SQL.', flush=True)
            for attempt in range(6):
                result = subprocess.run(['dotnet', 'run', '--no-launch-profile', '--project', str(PROJECT), '--', '--migrate', '--provision-runtime'], cwd=ROOT, env=environment, capture_output=True, text=True)
                if result.returncode == 0: break
                if 'not currently available' not in result.stderr + result.stdout or attempt == 5: break
                print('Azure SQL is resuming; retrying Security migration.', flush=True); time.sleep(10)
            if result.returncode:
                (ROOT / 'secrets/security-operator.log').write_text(result.stdout + result.stderr)
                raise RuntimeError('Security migration failed; protected diagnostic log retained locally.')
            print('Security database operation completed.', flush=True)
        finally:
            home.az('sql', 'server', 'firewall-rule', 'delete', '--resource-group', GROUP, '--server', SERVER, '--name', rule, output=False)
        for index, address in enumerate(filter(None, app.get('outboundIpAddresses', '').split(','))):
            address = str(ipaddress.ip_address(address))
            home.az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', f'security-api-egress-{index}', '--start-ip-address', address, '--end-ip-address', address, output=False)
        home.set_secret('security-db-connection', runtime_connection, directory)
        vision_key = home.az('cognitiveservices', 'account', 'keys', 'list', '--resource-group', GROUP, '--name', 'vision-casa-example')['key1']
        home.set_secret('security-vision-api-key', vision_key, directory)
        principal = home.az('webapp', 'identity', 'assign', '--resource-group', GROUP, '--name', APP)['principalId']
        for name in ('security-service-private', 'security-db-connection', 'security-vision-api-key'):
            home.az('role', 'assignment', 'create', '--assignee-object-id', principal, '--assignee-principal-type', 'ServicePrincipal', '--role', 'Key Vault Secrets User', '--scope', f'{vault["id"]}/secrets/{name}', output=False)
        for role, scope in [('Azure Service Bus Data Receiver', bus['id'] + '/topics/house-events-v1/subscriptions/security-v1'), ('Azure Service Bus Data Sender', bus['id'] + '/topics/house-events-v1'), ('Storage Blob Data Contributor', storage['id'] + '/blobServices/default/containers/captures')]:
            home.az('role', 'assignment', 'create', '--assignee-object-id', principal, '--assignee-principal-type', 'ServicePrincipal', '--role', role, '--scope', scope, output=False)
        settings = {
            'ConnectionStrings__SecurityDb': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/security-db-connection/)',
            'Identity__ServicePrivateKeyPem': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/security-service-private/)',
            'Jwt__PublicKeyPem': identity_public, 'Jwt__KeyId': 'identity-v1',
            'Identity__BaseUrl': 'https://identity-api.example.com', 'Home__BaseUrl': 'https://home-api.example.com',
            'ServiceBus__Namespace': 'sb-casa-example.servicebus.windows.net', 'ASPNETCORE_ENVIRONMENT': 'Production',
            'Storage__AccountUrl': storage['primaryEndpoints']['blob'], 'Vision__Endpoint': vision['properties']['endpoint'],
            'Vision__ApiKey': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/security-vision-api-key/)',
            'Logging__LogLevel__Default': 'Warning', 'Cors__Origins__0': 'http://localhost:8089', 'Cors__Origins__1': 'http://127.0.0.1:8089',
        }
        path = directory / 'settings.json'; path.write_text(json.dumps(settings))
        home.az('webapp', 'config', 'appsettings', 'set', '--resource-group', GROUP, '--name', APP, '--settings', '@' + str(path), output=False)
        home.az('webapp', 'config', 'set', '--resource-group', GROUP, '--name', APP, '--linux-fx-version', 'DOTNETCORE|10.0', '--startup-file', 'dotnet CasaDomotica.Security.Api.dll', output=False)
        home.az('webapp', 'update', '--resource-group', GROUP, '--name', APP, '--https-only', 'true', output=False)
        publish = directory / 'publish'
        result = subprocess.run(['dotnet', 'publish', str(PROJECT), '-c', 'Release', '-o', str(publish), '--verbosity', 'quiet'], cwd=ROOT, capture_output=True, text=True)
        if result.returncode: raise RuntimeError('Security release build failed; run a local build for diagnostics.')
        archive = directory / 'security.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            for file in publish.rglob('*'):
                if file.is_file(): package.write(file, file.relative_to(publish))
        print('Publishing Security application.', flush=True)
        home.az('webapp', 'deploy', '--resource-group', GROUP, '--name', APP, '--src-path', str(archive), '--type', 'zip', '--track-status', 'false', output=False)
        for _ in range(18):
            try:
                with urllib.request.urlopen(f'https://{app["defaultHostName"]}/ready', timeout=20) as response:
                    if response.status == 200: print('Security readiness verified against Azure SQL.', flush=True); return
            except (urllib.error.URLError, TimeoutError): pass
            time.sleep(5)
        raise RuntimeError('Security deployment submitted but readiness was not verified.')

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(f'Security deployment stopped: {type(error).__name__}: {error}', flush=True)
        raise SystemExit(1)
