#!/usr/bin/env python3
"""Migrate and publish Notifications Functions to its existing project resources without printing secrets."""
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
APP = 'notify-function-example'
HOST_STORAGE = 'sti9nnotifyfn26'
PROJECT = ROOT / 'Backend/Notifications/src/CasaDomotica.Notifications.Functions/CasaDomotica.Notifications.Functions.csproj'


def container(storage_id, name):
    home.az(
        'rest',
        '--method',
        'put',
        '--url',
        f'https://management.azure.com{storage_id}/blobServices/default/containers/{name}?api-version=2023-05-01',
        '--body',
        json.dumps({'properties': {'publicAccess': 'None'}}),
        output=False,
    )


def role(principal, role_name, scope):
    home.az(
        'role',
        'assignment',
        'create',
        '--assignee-object-id',
        principal,
        '--assignee-principal-type',
        'ServicePrincipal',
        '--role',
        role_name,
        '--scope',
        scope,
        output=False,
    )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--deploy', action='store_true')
    if not parser.parse_args().deploy:
        parser.error('Specify --deploy.')
    os.umask(0o077)
    (ROOT / 'secrets').mkdir(exist_ok=True)
    if home.az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000':
        raise RuntimeError('Unexpected Azure subscription.')

    existing = home.az('functionapp', 'show', '--resource-group', GROUP, '--name', APP)
    raw_site = home.az('rest', '--method', 'get', '--url', f'https://management.azure.com{existing["id"]}?api-version=2024-11-01')
    function_config = raw_site.get('properties', {}).get('functionAppConfig')
    if not isinstance(function_config, dict):
        raise RuntimeError('Function runtime configuration was not returned by ARM.')

    principal = home.az('functionapp', 'identity', 'assign', '--resource-group', GROUP, '--name', APP)['principalId']
    stores = home.az('storage', 'account', 'list', '--resource-group', GROUP, '--query', '[].name')
    if HOST_STORAGE not in stores:
        available = home.az('storage', 'account', 'check-name', '--name', HOST_STORAGE)
        if not available['nameAvailable']:
            raise RuntimeError('The planned notifications host storage name is unavailable.')
        print('Creating isolated standard host storage for Notifications Functions.', flush=True)
        home.az(
            'storage',
            'account',
            'create',
            '--resource-group',
            GROUP,
            '--name',
            HOST_STORAGE,
            '--location',
            'westus',
            '--sku',
            'Standard_LRS',
            '--kind',
            'StorageV2',
            '--https-only',
            'true',
            '--min-tls-version',
            'TLS1_2',
            '--allow-blob-public-access',
            'false',
            '--allow-shared-key-access',
            'false',
            '--tags',
            'project=casa-example',
            'component=notifications-functions-host',
            output=False,
        )

    host = home.az('storage', 'account', 'show', '--resource-group', GROUP, '--name', HOST_STORAGE)
    container(host['id'], 'function-releases')
    role(principal, 'Storage Blob Data Owner', host['id'])
    role(principal, 'Storage Table Data Contributor', host['id'])
    home.az(
        'functionapp',
        'deployment',
        'config',
        'set',
        '--resource-group',
        GROUP,
        '--name',
        APP,
        '--deployment-storage-name',
        HOST_STORAGE,
        '--deployment-storage-container-name',
        'function-releases',
        '--deployment-storage-auth-type',
        'SystemAssignedIdentity',
        output=False,
    )

    runtime = function_config.get('runtime', {})
    if runtime.get('name') != 'dotnet-isolated' or runtime.get('version') != '10.0':
        configuration = function_config
        configuration['runtime'] = {'name': 'dotnet-isolated', 'version': '10.0'}
        home.az(
            'rest',
            '--method',
            'patch',
            '--url',
            f'https://management.azure.com{existing["id"]}?api-version=2024-11-01',
            '--body',
            json.dumps({'properties': {'functionAppConfig': configuration}}),
            output=False,
        )

    server = home.az('sql', 'server', 'show', '--resource-group', GROUP, '--name', SERVER)
    vault = home.az('keyvault', 'show', '--resource-group', GROUP, '--name', VAULT)
    bus = home.az('servicebus', 'namespace', 'show', '--resource-group', GROUP, '--name', 'sb-casa-example')
    names = {item['name'] for item in home.az('keyvault', 'secret', 'list', '--vault-name', VAULT)}
    if 'notifications-service-private' not in names:
        raise RuntimeError('Notifications service signing key is missing.')

    acs_keys = home.az('communication', 'list-key', '--resource-group', GROUP, '--name', 'acs-casa-example')
    acs_connection = acs_keys.get('primaryConnectionString', '')
    if not acs_connection:
        raise RuntimeError('ACS connection string could not be retrieved.')

    domains = home.az('communication', 'email', 'domain', 'list', '--resource-group', GROUP, '--email-service-name', 'email-casa-example')
    if not domains:
        raise RuntimeError('Email domain resource was not found.')
    sender_domain = domains[0].get('fromSenderDomain') or domains[0].get('mailFromSenderDomain') or ''
    if not sender_domain:
        raise RuntimeError('Managed sender domain was not found.')
    sender_address = f'DoNotReply@{sender_domain}'

    with tempfile.TemporaryDirectory(prefix='casa-notifications-deploy-') as temp:
        directory = Path(temp)
        identity_public = home.public_key(home.secret('identity-jwt-private'), directory, 'identity-public-source')
        previous = home.secret('notifications-db-connection') if 'notifications-db-connection' in names else ''
        match = re.search(r'(?:^|;)Password=([^;]+)', previous, flags=re.I)
        password = match.group(1) if match else secrets.token_urlsafe(36)
        base = f'Server=tcp:{server["fullyQualifiedDomainName"]},1433;Database=NotificationsDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
        admin_connection = base + f'User ID={server["administratorLogin"]};Password={home.secret("sql-admin-password")};'
        runtime_connection = base + f'User ID=notifications_api;Password={password};'
        environment = os.environ.copy()
        environment.update({
            'ConnectionStrings__NotificationsDb': admin_connection,
            'Runtime__Password': password,
            'Logging__LogLevel__Default': 'Warning',
        })

        publish = directory / 'publish'
        build_result = subprocess.run(['dotnet', 'publish', str(PROJECT), '-c', 'Release', '-p:UseAppHost=false', '-o', str(publish), '--verbosity', 'quiet'], cwd=ROOT, capture_output=True, text=True)
        if build_result.returncode:
            (ROOT / 'secrets/notifications-publish.log').write_text(build_result.stdout + build_result.stderr)
            raise RuntimeError('Notifications publish failed; protected diagnostics retained locally.')

        ip = str(ipaddress.ip_address(urllib.request.urlopen('https://api.ipify.org', timeout=15).read().decode().strip()))
        rule = 'notifications-deploy-local'
        existing_rules = {item['name']: item for item in home.az('sql', 'server', 'firewall-rule', 'list', '--resource-group', GROUP, '--server', SERVER)}
        if rule in existing_rules and (existing_rules[rule]['startIpAddress'] != ip or existing_rules[rule]['endIpAddress'] != ip):
            raise RuntimeError('Temporary firewall rule belongs to another address.')
        home.az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', rule, '--start-ip-address', ip, '--end-ip-address', ip, output=False)
        try:
            print('Applying Notifications migration to Azure SQL.', flush=True)
            operator_dll = publish / 'CasaDomotica.Notifications.Functions.dll'
            for attempt in range(6):
                result = subprocess.run(
                    ['dotnet', str(operator_dll), '--migrate', '--provision-runtime'],
                    cwd=ROOT,
                    env=environment,
                    capture_output=True,
                    text=True,
                )
                if result.returncode == 0:
                    break
                if 'not currently available' not in result.stderr + result.stdout or attempt == 5:
                    break
                print('Azure SQL is resuming; retrying Notifications migration.', flush=True)
                time.sleep(10)
            if result.returncode:
                (ROOT / 'secrets/notifications-operator.log').write_text(result.stdout + result.stderr)
                raise RuntimeError('Notifications migration failed; protected diagnostic log retained locally.')
            print('Notifications database operation completed.', flush=True)
        finally:
            home.az('sql', 'server', 'firewall-rule', 'delete', '--resource-group', GROUP, '--server', SERVER, '--name', rule, output=False)

        outbound_csv = raw_site.get('properties', {}).get('possibleOutboundIpAddresses') or existing.get('possibleOutboundIpAddresses') or ''
        for index, address in enumerate(filter(None, outbound_csv.split(','))):
            address = str(ipaddress.ip_address(address))
            home.az('sql', 'server', 'firewall-rule', 'create', '--resource-group', GROUP, '--server', SERVER, '--name', f'notifications-fn-egress-{index}', '--start-ip-address', address, '--end-ip-address', address, output=False)

        home.set_secret('notifications-db-connection', runtime_connection, directory)
        home.set_secret('acs-email-connection', acs_connection, directory)
        for name in ('notifications-service-private', 'notifications-db-connection', 'acs-email-connection'):
            role(principal, 'Key Vault Secrets User', f'{vault["id"]}/secrets/{name}')
        role(principal, 'Azure Service Bus Data Receiver', bus['id'] + '/topics/house-events-v1/subscriptions/notifications-v1')

        settings = {
            'AzureWebJobsStorage__accountName': HOST_STORAGE,
            'AzureWebJobsStorage__credential': 'managedidentity',
            'ConnectionStrings__NotificationsDb': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/notifications-db-connection/)',
            'Identity__ServicePrivateKeyPem': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/notifications-service-private/)',
            'Jwt__PublicKeyPem': identity_public,
            'Jwt__KeyId': 'identity-v1',
            'Identity__BaseUrl': 'https://identity-api.example.com',
            'ServiceBusConnection__fullyQualifiedNamespace': 'sb-casa-example.servicebus.windows.net',
            'Email__ConnectionString': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/acs-email-connection/)',
            'Email__SenderAddress': sender_address,
            'Logging__LogLevel__Default': 'Warning',
            'Cors__Origins__0': 'http://localhost:8089',
            'Cors__Origins__1': 'http://127.0.0.1:8089',
        }
        path = directory / 'settings.json'
        path.write_text(json.dumps(settings))
        home.az('functionapp', 'config', 'appsettings', 'set', '--resource-group', GROUP, '--name', APP, '--settings', '@' + str(path), output=False)

        archive = directory / 'notifications.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            for item in sorted(publish.rglob('*')):
                name = item.relative_to(publish).as_posix()
                entry = zipfile.ZipInfo(name + '/' if item.is_dir() else name)
                entry.create_system = 3
                entry.external_attr = ((0o40755 if item.is_dir() else 0o100644) << 16)
                entry.compress_type = zipfile.ZIP_DEFLATED
                package.writestr(entry, b'' if item.is_dir() else item.read_bytes())

        print('Publishing Notifications Functions to Flex Consumption.', flush=True)
        home.az('functionapp', 'deployment', 'source', 'config-zip', '--resource-group', GROUP, '--name', APP, '--src', str(archive), '--build-remote', 'false', '--timeout', '600', output=False)

    functions = home.az('functionapp', 'function', 'list', '--resource-group', GROUP, '--name', APP, '--query', '[].name')
    if not any(name.endswith('/ConsumeNotificationEvents') for name in functions):
        raise RuntimeError('Deployment completed, but ConsumeNotificationEvents was not indexed.')

    observed_addresses = set()
    for _ in range(18):
        try:
            with urllib.request.urlopen(f'https://{existing["properties"]["defaultHostName"]}/ready', timeout=20) as response:
                if response.status == 200:
                    print('Notifications readiness verified against Azure SQL.', flush=True)
                    return
        except urllib.error.HTTPError as error:
            try:
                diagnostic = json.loads(error.read())
                if diagnostic.get('dependencyErrorCode') == 40615 and diagnostic.get('dependencyClientAddress'):
                    address = str(ipaddress.ip_address(diagnostic['dependencyClientAddress']))
                    if address not in observed_addresses:
                        print('Adding the exact Notifications egress address observed by Azure SQL.', flush=True)
                        home.az(
                            'sql', 'server', 'firewall-rule', 'create',
                            '--resource-group', GROUP,
                            '--server', SERVER,
                            '--name', f'notifications-fn-observed-{len(observed_addresses)}',
                            '--start-ip-address', address,
                            '--end-ip-address', address,
                            output=False,
                        )
                        observed_addresses.add(address)
            except (ValueError, TypeError):
                pass
        except (urllib.error.URLError, TimeoutError):
            pass
        time.sleep(5)
    raise RuntimeError('Notifications deployment submitted but readiness was not verified.')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(f'Notifications deployment stopped: {type(error).__name__}: {error}', flush=True)
        raise SystemExit(1)
