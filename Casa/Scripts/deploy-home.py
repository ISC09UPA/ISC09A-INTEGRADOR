#!/usr/bin/env python3
"""Deploy only Home to the project's existing Azure resources. Never print secrets."""
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
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
GROUP = 'rg-casa-example'
APP = 'home-api-example'
VAULT = 'kv-casa-example'
SERVER = 'sql-casa-example'
PROJECT = ROOT / 'Backend/Home/src/CasaDomotica.Home.Api/CasaDomotica.Home.Api.csproj'

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

def public_key(value, directory, label):
    path = directory / (label + '.pem')
    path.write_text(value)
    result = subprocess.run(['openssl', 'pkey', '-in', str(path), '-pubout'], capture_output=True, text=True, check=True)
    return result.stdout

def verify_routes(base, timeout=120):
    """Verify current protected routes without creating sessions or sending device data.

    App Service can return 404/503 while a new instance is warming up, even after
    /ready responds. Retry those transient statuses, but never accept 200.
    """
    checks = [
        ('capture upload', 'POST', f'/internal/captures/{uuid.uuid4()}/upload', b''),
        ('video session', 'GET', f'/api/v1/houses/{uuid.uuid4()}/video/sessions/{uuid.uuid4()}', None),
        ('video playlist', 'GET', f'/api/v1/houses/{uuid.uuid4()}/video/sessions/{uuid.uuid4()}/playlist.m3u8', None),
        ('video segment', 'POST', f'/internal/video/{uuid.uuid4()}/segments/0', b''),
    ]
    pending = list(checks)
    deadline = time.time() + timeout
    while pending and time.time() < deadline:
        remaining = []
        for name, method, path, body in pending:
            request = urllib.request.Request(base.rstrip('/') + path, data=body, method=method)
            try:
                urllib.request.urlopen(request, timeout=20).close()
            except urllib.error.HTTPError as error:
                if error.code == 401:
                    continue
                if error.code in (404, 503):
                    remaining.append((name, method, path, body))
                    continue
                raise RuntimeError(f'{name} route returned {error.code}; expected 401')
            raise RuntimeError(f'{name} route unexpectedly allowed an unauthenticated request')
        pending = remaining
        if pending: time.sleep(5)
    if pending:
        raise RuntimeError('protected routes did not return 401 before timeout: ' + ', '.join(name for name, _, _, _ in pending))
    print('Home protected capture and video routes verified.', flush=True)

def run_operator(environment, *arguments):
    for attempt in range(6):
        result = subprocess.run(['dotnet', 'run', '--no-launch-profile', '--project', str(PROJECT), '--', *arguments], cwd=ROOT, env=environment, capture_output=True, text=True)
        if result.returncode == 0: break
        if "not currently available" not in result.stderr + result.stdout or attempt == 5: break
        print('Azure SQL is resuming; retrying the idempotent migration.', flush=True)
        time.sleep(10)
    if result.returncode:
        log = ROOT / 'secrets/home-operator.log'
        log.write_text(result.stdout + result.stderr)
        raise RuntimeError('Home operator failed; protected diagnostic log retained locally.')
    print('Home database operation completed.', flush=True)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--deploy', action='store_true', help='Migrate, configure and publish Home to existing Azure resources.')
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
    with tempfile.TemporaryDirectory(prefix='casa-home-deploy-') as temporary:
        directory = Path(temporary)
        admin_password = secret('sql-admin-password')
        if 'home-service-private' in names:
            signing_key = secret('home-service-private')
        else:
            pem = directory / 'signing.pem'
            subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(pem)], check=True, capture_output=True)
            signing_key = pem.read_text()
            set_secret('home-service-private', signing_key, directory)
        identity_public = public_key(secret('identity-jwt-private'), directory, 'identity-public-source')
        home_public = public_key(signing_key, directory, 'home-public-source')
        if 'home-ingestion-private' in names:
            ingestion_private = secret('home-ingestion-private')
        else:
            path = directory / 'ingestion.pem'
            subprocess.run(['openssl', 'ecparam', '-name', 'prime256v1', '-genkey', '-noout', '-out', str(path)], capture_output=True, check=True)
            ingestion_private = path.read_text()
            set_secret('home-ingestion-private', ingestion_private, directory)
        ingestion_public = public_key(ingestion_private, directory, 'ingestion-public-source')
        signalr = az('signalr', 'key', 'list', '--resource-group', GROUP, '--name', 'signalr-casa-example')
        set_secret('home-signalr-connection', signalr['primaryConnectionString'], directory)
        iot = az('iot', 'hub', 'show', '--resource-group', GROUP, '--name', 'iothub-casa-example')
        storage = az('storage', 'account', 'show', '--resource-group', GROUP, '--name', 'sti9ncasa-example')
        bus = az('servicebus', 'namespace', 'show', '--resource-group', GROUP, '--name', 'sb-casa-example')
        # Private HLS segments. No public access; Home reads/writes through its managed identity.
        az('storage', 'container', 'create', '--account-name', 'sti9ncasa-example', '--name', 'video-streams', '--auth-mode', 'login', '--public-access', 'off', output=False)
        subscription_events = {
            'security-v1': ['MotionDetected', 'CaptureAvailable', 'AccessRequested', 'DeviceStateChanged', 'HouseMembershipChanged'],
            'notifications-v1': ['SecurityIncidentCreated', 'AccessRecorded', 'HouseMembershipChanged'],
        }
        for subscription, kinds in subscription_events.items():
            az('servicebus', 'topic', 'subscription', 'create', '--resource-group', GROUP, '--namespace-name', 'sb-casa-example', '--topic-name', 'house-events-v1', '--name', subscription, output=False)
            expression = 'type IN (' + ','.join("'" + kind + "'" for kind in kinds) + ')'
            az('servicebus', 'topic', 'subscription', 'rule', 'create', '--resource-group', GROUP, '--namespace-name', 'sb-casa-example', '--topic-name', 'house-events-v1', '--subscription-name', subscription, '--name', 'domain-events', '--filter-type', 'SqlFilter', '--filter-sql-expression', expression, output=False)
            rules = az('servicebus', 'topic', 'subscription', 'rule', 'list', '--resource-group', GROUP, '--namespace-name', 'sb-casa-example', '--topic-name', 'house-events-v1', '--subscription-name', subscription)
            if any(rule['name'] == '$Default' for rule in rules):
                az('servicebus', 'topic', 'subscription', 'rule', 'delete', '--resource-group', GROUP, '--namespace-name', 'sb-casa-example', '--topic-name', 'house-events-v1', '--subscription-name', subscription, '--name', '$Default', output=False)
        existing_connection = secret('home-db-connection') if 'home-db-connection' in names else ''
        match = re.search(r'(?:^|;)Password=([^;]+)', existing_connection, flags=re.I)
        runtime_password = match.group(1) if match else secrets.token_urlsafe(36)
        base = f'Server=tcp:{server["fullyQualifiedDomainName"]},1433;Database=HomeDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
        admin_connection = base + f'User ID={server["administratorLogin"]};Password={admin_password};'
        runtime_connection = base + f'User ID=home_api;Password={runtime_password};'
        environment = os.environ.copy()
        environment.update({'ConnectionStrings__HomeDb': admin_connection, 'Runtime__Password': runtime_password, 'Logging__LogLevel__Default': 'Warning'})
        local_ip = str(ipaddress.ip_address(urllib.request.urlopen('https://api.ipify.org', timeout=15).read().decode().strip()))
        rule = 'home-deploy-local'
        existing_rules = {entry['name']: entry for entry in az('sql', 'server', 'firewall-rule', 'list', '--resource-group', GROUP, '--server', SERVER)}
        if rule in existing_rules and (existing_rules[rule]['startIpAddress'] != local_ip or existing_rules[rule]['endIpAddress'] != local_ip):
            raise RuntimeError('Temporary rule has a different address; inspect it before retrying.')
        az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', rule, '--start-ip-address', local_ip, '--end-ip-address', local_ip, output=False)
        try:
            print('Applying Home migration to Azure SQL.', flush=True)
            run_operator(environment, '--migrate', '--provision-runtime')
            print('Verifying Azure SQL isolation, restart persistence and concurrency for expired command keys.', flush=True)
            run_operator({**environment, 'ConnectionStrings__HomeDb': runtime_connection}, '--verify-sql-isolation')
        finally:
            az('sql', 'server', 'firewall-rule', 'delete', '--resource-group', GROUP, '--server', SERVER, '--name', rule, output=False)
        for index, address in enumerate(filter(None, app.get('outboundIpAddresses', '').split(','))):
            address = str(ipaddress.ip_address(address))
            az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', f'home-api-egress-{index}', '--start-ip-address', address, '--end-ip-address', address, output=False)
        set_secret('home-db-connection', runtime_connection, directory)
        identity = az('webapp', 'identity', 'assign', '--resource-group', GROUP, '--name', APP)
        for name in ('home-service-private', 'home-db-connection', 'home-signalr-connection'):
            az('role', 'assignment', 'create', '--assignee-object-id', identity['principalId'], '--assignee-principal-type', 'ServicePrincipal', '--role', 'Key Vault Secrets User', '--scope', f'{vault["id"]}/secrets/{name}', output=False)
        for role, scope in [
            ('IoT Hub Data Contributor', iot['id']),
            ('Azure Service Bus Data Sender', bus['id'] + '/topics/house-events-v1'),
            ('Storage Blob Data Contributor', storage['id'] + '/blobServices/default/containers/captures'),
            ('Storage Blob Data Contributor', storage['id'] + '/blobServices/default/containers/video-streams'),
        ]:
            az('role', 'assignment', 'create', '--assignee-object-id', identity['principalId'], '--assignee-principal-type', 'ServicePrincipal', '--role', role, '--scope', scope, output=False)
        configuration = {
            'ConnectionStrings__HomeDb': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/home-db-connection/)',
            'Identity__ServicePrivateKeyPem': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/home-service-private/)',
            'ConnectionStrings__SignalR': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/home-signalr-connection/)',
            'Jwt__PublicKeyPem': identity_public,
            'Jwt__KeyId': 'identity-v1',
            'Ingestion__PublicKeyPem': ingestion_public,
            'Identity__BaseUrl': 'https://identity-api.example.com',
            'IoT__HostName': iot['properties']['hostName'],
            'ServiceBus__Namespace': 'sb-casa-example.servicebus.windows.net',
            'Storage__AccountUrl': storage['primaryEndpoints']['blob'],
            'Capture__UploadBaseUrl': f'https://{app["defaultHostName"]}',
            'Video__UploadBaseUrl': f'https://{app["defaultHostName"]}',
            'Video__PlaybackBaseUrl': f'https://{app["defaultHostName"]}',
            'ASPNETCORE_ENVIRONMENT': 'Production',
            'Logging__LogLevel__Default': 'Warning',
            'Cors__Origins__0': 'http://localhost:8089',
            'Cors__Origins__1': 'http://127.0.0.1:8089',
        }
        pin = directory / 'home-public-pin.json'
        pin.write_text(json.dumps({'Services__HomePublicKeyPem': home_public, 'Cors__Origins__0': 'http://localhost:8089', 'Cors__Origins__1': 'http://127.0.0.1:8089'}))
        az('webapp', 'config', 'appsettings', 'set', '--resource-group', GROUP, '--name', 'identity-api-example', '--settings', '@' + str(pin), output=False)
        print('Updating the Identity application with the current authenticated-service contract.', flush=True)
        identity_update = subprocess.run(['python3', str(ROOT / 'Scripts/deploy-identity.py'), '--deploy'], cwd=ROOT, check=True)
        settings = directory / 'settings.json'
        settings.write_text(json.dumps(configuration))
        az('webapp', 'config', 'appsettings', 'set', '--resource-group', GROUP, '--name', APP, '--settings', '@' + str(settings), output=False)
        az('webapp', 'config', 'set', '--resource-group', GROUP, '--name', APP, '--linux-fx-version', 'DOTNETCORE|10.0', '--startup-file', 'dotnet CasaDomotica.Home.Api.dll', output=False)
        az('webapp', 'update', '--resource-group', GROUP, '--name', APP, '--https-only', 'true', output=False)
        publish = directory / 'publish'
        # App Service runs Linux x64. Publishing all native runtimes adds hundreds
        # of megabytes and can exhaust the free plan during deployment.
        result = subprocess.run(['dotnet', 'publish', str(PROJECT), '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'false', '-o', str(publish), '--verbosity', 'quiet'], cwd=ROOT, capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError('Home release publish failed. Run a local build for diagnostics.')
        archive = directory / 'home.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            for path in publish.rglob('*'):
                if path.is_file(): package.write(path, path.relative_to(publish))
        print('Publishing Home application.', flush=True)
        # Azure's Linux startup tracker can continue waiting after OneDeploy has succeeded;
        # the /ready loop below checks the live API and SQL connection instead.
        az('webapp', 'deploy', '--resource-group', GROUP, '--name', APP, '--src-path', str(archive), '--type', 'zip', '--track-status', 'false', output=False)
        endpoint = f'https://{app["defaultHostName"]}/ready'
        observed_addresses = set()
        for _ in range(12):
            try:
                with urllib.request.urlopen(endpoint, timeout=20) as result:
                    if result.status == 200:
                        verify_routes(f'https://{app["defaultHostName"]}')
                        print('Home readiness verified against Azure SQL.', flush=True)
                        print(f'https://{app["defaultHostName"]}', flush=True)
                        return
            except urllib.error.HTTPError as error:
                try:
                    diagnostic = json.loads(error.read())
                    if diagnostic.get('dependencyErrorCode') == 40615 and diagnostic.get('dependencyClientAddress'):
                        address = str(ipaddress.ip_address(diagnostic['dependencyClientAddress']))
                        if address not in observed_addresses:
                            print('Adding the exact application egress address observed by Azure SQL.', flush=True)
                            az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', 'home-api-observed-' + str(len(observed_addresses)), '--start-ip-address', address, '--end-ip-address', address, output=False)
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
        print(f'Home deployment stopped: {type(error).__name__}: {error}', flush=True)
        raise SystemExit(1)
