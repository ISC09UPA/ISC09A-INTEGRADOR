#!/usr/bin/env python3
"""Deploy only Identity to the project's existing Azure resources. Never print secrets."""
import argparse
import ipaddress
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import tempfile
import time
import urllib.request
import urllib.error
import zipfile

ROOT = Path(__file__).resolve().parents[1]
GROUP = 'rg-casa-example'
APP = 'identity-api-example'
VAULT = 'kv-casa-example'
SERVER = 'sql-casa-example'
PROJECT = ROOT / 'Backend/Identity/src/CasaDomotica.Identity.Api/CasaDomotica.Identity.Api.csproj'

def az(*args, output=True):
    result = subprocess.run(['az', *args, '--output', 'json' if output else 'none'], capture_output=True, text=True)
    if result.returncode:
        # Keep provider diagnostics protected; print command names only.
        folder = ROOT / 'secrets'
        folder.mkdir(exist_ok=True)
        (folder / 'azure-command-error.log').write_text(result.stderr)
        raise RuntimeError(f'Azure command failed: {args[0]} {args[1] if len(args) > 1 else ""}; protected diagnostics retained locally')
    return json.loads(result.stdout) if output and result.stdout.strip() else None

def secret(name):
    return az('keyvault', 'secret', 'show', '--vault-name', VAULT, '--name', name)['value']

def set_secret(name, value, directory):
    path = directory / name
    path.write_text(value)
    az('keyvault', 'secret', 'set', '--vault-name', VAULT, '--name', name, '--file', str(path), output=False)

def run_operator(environment, *arguments):
    for attempt in range(6):
        result = subprocess.run(['dotnet', 'run', '--no-launch-profile', '--project', str(PROJECT), '--', *arguments], cwd=ROOT, env=environment, capture_output=True, text=True)
        if result.returncode == 0: break
        if "not currently available" not in result.stderr + result.stdout or attempt == 5: break
        print('Azure SQL is resuming; retrying the idempotent migration.', flush=True)
        time.sleep(10)
    if result.returncode:
        log = ROOT / 'secrets/identity-operator.log'
        log.write_text(result.stdout + result.stderr)
        raise RuntimeError('Identity operator failed; protected diagnostic log retained locally.')
    print('Identity database operation completed.', flush=True)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--deploy', action='store_true', help='Migrate, configure and publish Identity to existing Azure resources.')
    options = parser.parse_args()
    if not options.deploy:
        parser.error('Specify --deploy to perform the deployment.')
    os.umask(0o077)
    (ROOT / 'secrets').mkdir(exist_ok=True)
    account = az('account', 'show')
    if account['id'] != '00000000-0000-0000-0000-000000000000':
        raise RuntimeError('Unexpected subscription; no changes performed.')
    app = az('webapp', 'show', '--resource-group', GROUP, '--name', APP)
    server = az('sql', 'server', 'show', '--resource-group', GROUP, '--name', SERVER)
    vault = az('keyvault', 'show', '--resource-group', GROUP, '--name', VAULT)
    names = {entry['name'] for entry in az('keyvault', 'secret', 'list', '--vault-name', VAULT)}
    with tempfile.TemporaryDirectory(prefix='casa-identity-deploy-') as temporary:
        directory = Path(temporary)
        admin_password = secret('sql-admin-password')
        if 'identity-jwt-private' in names:
            signing_key = secret('identity-jwt-private')
        else:
            pem = directory / 'signing.pem'
            subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(pem)], check=True, capture_output=True)
            signing_key = pem.read_text()
            set_secret('identity-jwt-private', signing_key, directory)
        existing_connection = secret('identity-db-connection') if 'identity-db-connection' in names else ''
        match = re.search(r'(?:^|;)Password=([^;]+)', existing_connection, flags=re.I)
        runtime_password = match.group(1) if match else secrets.token_urlsafe(36)
        base = f'Server=tcp:{server["fullyQualifiedDomainName"]},1433;Database=IdentityDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
        admin_connection = base + f'User ID={server["administratorLogin"]};Password={admin_password};'
        runtime_connection = base + f'User ID=identity_api;Password={runtime_password};'
        environment = os.environ.copy()
        environment.update({'ConnectionStrings__IdentityDb': admin_connection, 'Runtime__Password': runtime_password, 'Logging__LogLevel__Default': 'Warning'})
        local_ip = str(ipaddress.ip_address(urllib.request.urlopen('https://api.ipify.org', timeout=15).read().decode().strip()))
        rule = 'identity-deploy-local'
        existing_rules = {entry['name']: entry for entry in az('sql', 'server', 'firewall-rule', 'list', '--resource-group', GROUP, '--server', SERVER)}
        if rule in existing_rules and (existing_rules[rule]['startIpAddress'] != local_ip or existing_rules[rule]['endIpAddress'] != local_ip):
            raise RuntimeError('Temporary rule has a different address; inspect it before retrying.')
        az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', rule, '--start-ip-address', local_ip, '--end-ip-address', local_ip, output=False)
        try:
            print('Applying Identity migration to Azure SQL.', flush=True)
            run_operator(environment, '--migrate', '--provision-runtime')
        finally:
            az('sql', 'server', 'firewall-rule', 'delete', '--resource-group', GROUP, '--server', SERVER, '--name', rule, output=False)
        for index, address in enumerate(filter(None, app.get('outboundIpAddresses', '').split(','))):
            address = str(ipaddress.ip_address(address))
            az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', f'identity-api-egress-{index}', '--start-ip-address', address, '--end-ip-address', address, output=False)
        set_secret('identity-db-connection', runtime_connection, directory)
        identity = az('webapp', 'identity', 'assign', '--resource-group', GROUP, '--name', APP)
        for name in ('identity-jwt-private', 'identity-db-connection'):
            az('role', 'assignment', 'create', '--assignee-object-id', identity['principalId'], '--assignee-principal-type', 'ServicePrincipal', '--role', 'Key Vault Secrets User', '--scope', f'{vault["id"]}/secrets/{name}', output=False)
        configuration = {
            'ConnectionStrings__IdentityDb': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/identity-db-connection/)',
            'Jwt__PrivateKeyPem': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/identity-jwt-private/)',
            'Jwt__KeyId': 'identity-v1',
            'ASPNETCORE_ENVIRONMENT': 'Production',
            'Logging__LogLevel__Default': 'Warning',
        }
        settings = directory / 'settings.json'
        settings.write_text(json.dumps(configuration))
        az('webapp', 'config', 'appsettings', 'set', '--resource-group', GROUP, '--name', APP, '--settings', '@' + str(settings), output=False)
        az('webapp', 'config', 'set', '--resource-group', GROUP, '--name', APP, '--linux-fx-version', 'DOTNETCORE|10.0', '--startup-file', 'dotnet CasaDomotica.Identity.Api.dll', output=False)
        az('webapp', 'update', '--resource-group', GROUP, '--name', APP, '--https-only', 'true', output=False)
        publish = directory / 'publish'
        result = subprocess.run(['dotnet', 'publish', str(PROJECT), '-c', 'Release', '-o', str(publish), '--verbosity', 'quiet'], cwd=ROOT, capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError('Identity release publish failed. Run a local build for diagnostics.')
        archive = directory / 'identity.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            for path in publish.rglob('*'):
                if path.is_file(): package.write(path, path.relative_to(publish))
        print('Publishing Identity application.', flush=True)
        # OneDeploy can complete while the optional Linux startup tracker waits indefinitely.
        # The explicit /ready check below verifies the actual API and SQL connection.
        az('webapp', 'deploy', '--resource-group', GROUP, '--name', APP, '--src-path', str(archive), '--type', 'zip', '--track-status', 'false', output=False)
        endpoint = f'https://{app["defaultHostName"]}/ready'
        observed_addresses = set()
        for _ in range(12):
            try:
                with urllib.request.urlopen(endpoint, timeout=20) as result:
                    if result.status == 200:
                        print('Identity readiness verified against Azure SQL.', flush=True)
                        print(f'https://{app["defaultHostName"]}', flush=True)
                        return
            except urllib.error.HTTPError as error:
                try:
                    diagnostic = json.loads(error.read())
                    if diagnostic.get('dependencyErrorCode') == 40615 and diagnostic.get('dependencyClientAddress'):
                        address = str(ipaddress.ip_address(diagnostic['dependencyClientAddress']))
                        if address not in observed_addresses:
                            print('Adding the exact application egress address observed by Azure SQL.', flush=True)
                            az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', 'identity-api-observed-' + str(len(observed_addresses)), '--start-ip-address', address, '--end-ip-address', address, output=False)
                            observed_addresses.add(address)
                except (ValueError, TypeError):
                    pass
                time.sleep(5)
            except (OSError, TimeoutError):
                time.sleep(5)
        raise RuntimeError('Deployment submitted, but readiness was not verified. Inspect app status and Key Vault reference resolution.')

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(f'Identity deployment stopped: {type(error).__name__}: {error}', flush=True)
        raise SystemExit(1)
