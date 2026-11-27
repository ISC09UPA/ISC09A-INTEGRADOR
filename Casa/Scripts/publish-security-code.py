#!/usr/bin/env python3
"""Publish Security code only after its migrations and configuration are applied."""
import argparse
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
import zipfile

root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('home_deploy', root / 'Scripts/deploy-home.py')
home = importlib.util.module_from_spec(spec); spec.loader.exec_module(home)
project = root / 'Backend/Security/src/CasaDomotica.Security.Api/CasaDomotica.Security.Api.csproj'
app_name = 'security-api-example'

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--publish', action='store_true')
    if not parser.parse_args().publish: parser.error('Specify --publish.')
    if home.az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000': raise RuntimeError('Unexpected subscription.')
    app = home.az('webapp', 'show', '--resource-group', home.GROUP, '--name', app_name)
    with tempfile.TemporaryDirectory(prefix='casa-security-linux-') as temp:
        output = Path(temp) / 'publish'
        result = subprocess.run(['dotnet', 'publish', str(project), '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'false', '-o', str(output), '--verbosity', 'quiet'], cwd=root, capture_output=True, text=True)
        if result.returncode: raise RuntimeError('Linux Security package did not build.')
        archive = Path(temp) / 'security.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            for file in output.rglob('*'):
                if file.is_file(): package.write(file, file.relative_to(output))
        print(f'Publishing Linux-only Security package ({archive.stat().st_size // (1024 * 1024)} MiB).', flush=True)
        home.az('webapp', 'deploy', '--resource-group', home.GROUP, '--name', app_name, '--src-path', str(archive), '--type', 'zip', '--track-status', 'false', output=False)
        base = f'https://{app["defaultHostName"]}'
        path = '/api/v1/security/houses/' + str(uuid.uuid4()) + '/captures/' + str(uuid.uuid4()) + '/deletion'
        for _ in range(18):
            try:
                with urllib.request.urlopen(base + '/ready', timeout=20) as response:
                    if response.status != 200: continue
                try: urllib.request.urlopen(base + path, timeout=20).close()
                except urllib.error.HTTPError as error:
                    if error.code == 401: print('Security readiness and deletion status route verified.', flush=True); return
            except (OSError, TimeoutError): pass
            time.sleep(5)
        raise RuntimeError('Publish submitted but current Security version was not verified.')

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(f'Security code publish stopped: {type(error).__name__}: {error}', flush=True)
        raise SystemExit(1)
