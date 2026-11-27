#!/usr/bin/env python3
"""Publish the read-only assistant using its own managed identity."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('deployment_helpers', ROOT / 'Scripts/deploy-identity.py')
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
az = helpers.az
GROUP = helpers.GROUP
APP = 'assistant-api-example'

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--deploy', action='store_true'); args = parser.parse_args()
    if not args.deploy: parser.error('Specify --deploy.')
    os.umask(0o077)
    if az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000': raise RuntimeError('Unexpected subscription.')
    app = az('webapp', 'show', '--resource-group', GROUP, '--name', APP)
    ai = az('cognitiveservices', 'account', 'show', '--resource-group', GROUP, '--name', 'openai-casa-example')
    models = az('cognitiveservices', 'account', 'deployment', 'list', '--resource-group', GROUP, '--name', 'openai-casa-example')
    if not any(model['name'] == 'casa-asistente-nano' and model['properties']['provisioningState'] == 'Succeeded' for model in models): raise RuntimeError('Configured assistant model is unavailable.')
    principal = az('webapp', 'identity', 'assign', '--resource-group', GROUP, '--name', APP)['principalId']
    az('role', 'assignment', 'create', '--assignee-object-id', principal, '--assignee-principal-type', 'ServicePrincipal', '--role', 'Cognitive Services OpenAI User', '--scope', ai['id'], output=False)
    config = {'Services__IdentityUrl': 'https://identity-api.example.com', 'Services__HomeUrl': 'https://home-api.example.com', 'Services__SecurityUrl': 'https://security-api.example.com', 'OpenAI__Endpoint': ai['properties']['endpoint'], 'OpenAI__Deployment': 'casa-asistente-nano', 'ASPNETCORE_ENVIRONMENT': 'Production', 'Logging__LogLevel__Default': 'Warning'}
    with tempfile.TemporaryDirectory(prefix='casa-assistant-') as temporary:
        directory = Path(temporary)
        settings = directory / 'settings.json'; settings.write_text(json.dumps(config))
        az('webapp', 'config', 'appsettings', 'set', '--resource-group', GROUP, '--name', APP, '--settings', '@' + str(settings), output=False)
        az('webapp', 'config', 'set', '--resource-group', GROUP, '--name', APP, '--linux-fx-version', 'DOTNETCORE|10.0', '--startup-file', 'dotnet CasaDomotica.Assistant.Api.dll', output=False)
        az('webapp', 'update', '--resource-group', GROUP, '--name', APP, '--https-only', 'true', output=False)
        publish = directory / 'publish'
        result = subprocess.run(['dotnet', 'publish', str(ROOT / 'Backend/Assistant/CasaDomotica.Assistant.Api.csproj'), '-c', 'Release', '-p:UseAppHost=false', '-o', str(publish), '--verbosity', 'quiet'], cwd=ROOT, capture_output=True, text=True)
        if result.returncode: raise RuntimeError('Assistant publish failed; run local build for diagnostics.')
        archive = directory / 'assistant.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            for path in sorted(publish.rglob('*')):
                if path.is_file():
                    entry = zipfile.ZipInfo(path.relative_to(publish).as_posix()); entry.create_system = 3; entry.external_attr = 0o100644 << 16; entry.compress_type = zipfile.ZIP_DEFLATED
                    package.writestr(entry, path.read_bytes())
        print('Publishing Assistant application.', flush=True)
        az('webapp', 'deploy', '--resource-group', GROUP, '--name', APP, '--src-path', str(archive), '--type', 'zip', '--track-status', 'false', output=False)
    for _ in range(12):
        try:
            with urllib.request.urlopen('https://' + app['defaultHostName'] + '/ready', timeout=20) as response:
                if response.status == 200:
                    print('Assistant readiness endpoint responded. Real model calls require a real house report.'); return
        except (OSError, TimeoutError): pass
        time.sleep(5)
    raise RuntimeError('Assistant health did not become ready.')

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(f'Assistant deployment stopped: {type(error).__name__}: {error}')
        raise SystemExit(1)
