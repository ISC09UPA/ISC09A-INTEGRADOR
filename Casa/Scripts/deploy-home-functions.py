#!/usr/bin/env python3
"""Deploy the real IoT consumer using isolated host storage and scoped managed identities."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('deployment_helpers', ROOT / 'Scripts/deploy-identity.py')
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
az = helpers.az
GROUP = helpers.GROUP
APP = 'home-function-example'
HOST_STORAGE = 'sti9nhomefn26'
PROJECT = ROOT / 'Backend/Home.Functions/CasaDomotica.Home.Functions.csproj'

def container(storage_id, name):
    az('rest', '--method', 'put', '--url', f'https://management.azure.com{storage_id}/blobServices/default/containers/{name}?api-version=2023-05-01', '--body', json.dumps({'properties': {'publicAccess': 'None'}}), output=False)

def role(principal, name, scope):
    az('role', 'assignment', 'create', '--assignee-object-id', principal, '--assignee-principal-type', 'ServicePrincipal', '--role', name, '--scope', scope, output=False)

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--deploy', action='store_true'); args = parser.parse_args()
    if not args.deploy: parser.error('Specify --deploy to configure and publish the IoT consumer.')
    os.umask(0o077)
    if az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000': raise RuntimeError('Unexpected subscription.')
    existing = az('functionapp', 'show', '--resource-group', GROUP, '--name', APP)
    raw_site = az('rest', '--method', 'get', '--url', f'https://management.azure.com{existing["id"]}?api-version=2024-11-01')
    function_config = raw_site.get('properties', {}).get('functionAppConfig')
    if not isinstance(function_config, dict): raise RuntimeError('Function runtime configuration was not returned by ARM; no runtime overwrite will be attempted.')
    identity = az('functionapp', 'identity', 'assign', '--resource-group', GROUP, '--name', APP)
    principal = identity['principalId']
    stores = az('storage', 'account', 'list', '--resource-group', GROUP, '--query', '[].name')
    if HOST_STORAGE not in stores:
        available = az('storage', 'account', 'check-name', '--name', HOST_STORAGE)
        if not available['nameAvailable']: raise RuntimeError('The planned function host storage name is unavailable.')
        print('Creating isolated standard host storage for the IoT consumer.', flush=True)
        az('storage', 'account', 'create', '--resource-group', GROUP, '--name', HOST_STORAGE, '--location', 'westus', '--sku', 'Standard_LRS', '--kind', 'StorageV2', '--https-only', 'true', '--min-tls-version', 'TLS1_2', '--allow-blob-public-access', 'false', '--allow-shared-key-access', 'false', '--tags', 'project=casa-example', 'component=home-functions-host', output=False)
    host = az('storage', 'account', 'show', '--resource-group', GROUP, '--name', HOST_STORAGE)
    captures = az('storage', 'account', 'show', '--resource-group', GROUP, '--name', 'sti9ncasa-example')
    container(host['id'], 'function-releases')
    container(captures['id'], 'iot-quarantine')
    role(principal, 'Storage Blob Data Owner', host['id'])
    role(principal, 'Storage Table Data Contributor', host['id'])
    role(principal, 'Storage Blob Data Contributor', captures['id'] + '/blobServices/default/containers/iot-quarantine')
    az('functionapp', 'deployment', 'config', 'set', '--resource-group', GROUP, '--name', APP, '--deployment-storage-name', HOST_STORAGE, '--deployment-storage-container-name', 'function-releases', '--deployment-storage-auth-type', 'SystemAssignedIdentity', output=False)
    runtime = function_config.get('runtime', {})
    if runtime.get('name') != 'dotnet-isolated' or runtime.get('version') != '10.0':
        configuration = function_config; configuration['runtime'] = {'name': 'dotnet-isolated', 'version': '10.0'}
        az('rest', '--method', 'patch', '--url', f'https://management.azure.com{existing["id"]}?api-version=2024-11-01', '--body', json.dumps({'properties': {'functionAppConfig': configuration}}), output=False)
        az('functionapp', 'deployment', 'config', 'set', '--resource-group', GROUP, '--name', APP, '--deployment-storage-name', HOST_STORAGE, '--deployment-storage-container-name', 'function-releases', '--deployment-storage-auth-type', 'SystemAssignedIdentity', output=False)
    hub = az('iot', 'hub', 'show', '--resource-group', GROUP, '--name', 'iothub-casa-example')
    policy_names = az('iot', 'hub', 'policy', 'list', '--hub-name', 'iothub-casa-example', '--query', '[].keyName')
    if 'backend-events' not in policy_names:
        az('iot', 'hub', 'policy', 'create', '--hub-name', 'iothub-casa-example', '--name', 'backend-events', '--permissions', 'ServiceConnect', output=False)
    policy = az('iot', 'hub', 'policy', 'show', '--hub-name', 'iothub-casa-example', '--name', 'backend-events')
    endpoint = hub['properties']['eventHubEndpoints']['events']
    connection = f'Endpoint={endpoint["endpoint"]};SharedAccessKeyName=backend-events;SharedAccessKey={policy["primaryKey"]};EntityPath={endpoint["path"]}'
    az('iot', 'hub', 'consumer-group', 'create', '--hub-name', 'iothub-casa-example', '--name', 'home-functions-v1', output=False)
    vault = az('keyvault', 'show', '--resource-group', GROUP, '--name', helpers.VAULT)
    with tempfile.TemporaryDirectory(prefix='casa-iot-functions-') as temporary:
        directory = Path(temporary)
        helpers.set_secret('home-iot-events', connection, directory)
        for name in ['home-iot-events', 'home-ingestion-private']:
            role(principal, 'Key Vault Secrets User', vault['id'] + '/secrets/' + name)
        settings = {
            'AzureWebJobsStorage__accountName': HOST_STORAGE,
            'AzureWebJobsStorage__credential': 'managedidentity',
            'Home__BaseUrl': 'https://home-api.example.com',
            'Ingestion__PrivateKeyPem': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/home-ingestion-private/)',
            'IoTHubEvents': f'@Microsoft.KeyVault(SecretUri={vault["properties"]["vaultUri"]}secrets/home-iot-events/)',
            'IoTHubEventHubName': endpoint['path'],
            'IoTConsumerGroup': 'home-functions-v1',
            'Storage__AccountUrl': captures['primaryEndpoints']['blob'],
            'Logging__LogLevel__Default': 'Warning',
        }
        path = directory / 'settings.json'; path.write_text(json.dumps(settings))
        az('functionapp', 'config', 'appsettings', 'set', '--resource-group', GROUP, '--name', APP, '--settings', '@' + str(path), output=False)
        remove = ['AzureWebJobsStorage', 'DEPLOYMENT_STORAGE_CONNECTION_STRING']
        old_auth = function_config.get('deployment', {}).get('storage', {}).get('authentication', {})
        old_name = old_auth.get('storageAccountConnectionStringName')
        if old_name and old_name not in remove: remove.append(old_name)
        az('functionapp', 'config', 'appsettings', 'delete', '--resource-group', GROUP, '--name', APP, '--setting-names', *remove, output=False)
        publish = directory / 'publish'
        result = subprocess.run(['dotnet', 'publish', str(PROJECT), '-c', 'Release', '-p:UseAppHost=false', '-o', str(publish), '--verbosity', 'quiet'], cwd=ROOT, capture_output=True, text=True)
        if result.returncode:
            (ROOT / 'secrets/home-functions-publish.log').write_text(result.stdout + result.stderr)
            raise RuntimeError('Function publish failed; protected diagnostics retained locally.')
        archive = directory / 'functions.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            # Keep temporary credentials private (umask 077), but publish code with
            # readable Unix modes: the Flex runtime runs as a different user.
            # Preserving temporary-file mode 0600 prevents loading extensions.json.
            for item in sorted(publish.rglob('*')):
                name = item.relative_to(publish).as_posix()
                entry = zipfile.ZipInfo(name + '/' if item.is_dir() else name)
                entry.create_system = 3
                entry.external_attr = ((0o40755 if item.is_dir() else 0o100644) << 16)
                entry.compress_type = zipfile.ZIP_DEFLATED
                package.writestr(entry, b'' if item.is_dir() else item.read_bytes())
        print('Publishing the IoT Hub consumer to Functions Flex.', flush=True)
        az('functionapp', 'deployment', 'source', 'config-zip', '--resource-group', GROUP, '--name', APP, '--src', str(archive), '--build-remote', 'false', '--timeout', '600', output=False)
    functions = az('functionapp', 'function', 'list', '--resource-group', GROUP, '--name', APP, '--query', '[].name')
    if not any(name.endswith('/IngestIoTEvents') for name in functions): raise RuntimeError('Deployment completed, but the expected IoT trigger was not indexed.')
    print('IoT consumer deployed and indexed. Processing real device traffic still requires the registered physical device.', flush=True)

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(f'IoT function deployment stopped: {type(error).__name__}: {error}')
        raise SystemExit(1)
