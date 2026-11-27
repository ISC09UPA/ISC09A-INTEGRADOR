#!/usr/bin/env python3
"""Publish Home code only after its migrations and configuration are already applied."""
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

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--publish', action='store_true')
    if not parser.parse_args().publish: parser.error('Specify --publish.')
    if home.az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000': raise RuntimeError('Unexpected subscription.')
    app = home.az('webapp', 'show', '--resource-group', home.GROUP, '--name', home.APP)
    with tempfile.TemporaryDirectory(prefix='casa-home-linux-') as temp:
        output = Path(temp) / 'publish'
        result = subprocess.run(['dotnet', 'publish', str(home.PROJECT), '-c', 'Release', '-r', 'linux-x64', '--self-contained', 'false', '-o', str(output), '--verbosity', 'quiet'], cwd=root, capture_output=True, text=True)
        if result.returncode or not (output / 'libSkiaSharp.so').is_file(): raise RuntimeError('Linux Home package did not build with its image decoder.')
        archive = Path(temp) / 'home.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
            for file in output.rglob('*'):
                if file.is_file(): package.write(file, file.relative_to(output))
        print(f'Publishing Linux-only Home package ({archive.stat().st_size // (1024 * 1024)} MiB).', flush=True)
        home.az('webapp', 'deploy', '--resource-group', home.GROUP, '--name', home.APP, '--src-path', str(archive), '--type', 'zip', '--track-status', 'false', output=False)
        base = f'https://{app["defaultHostName"]}'
        for _ in range(18):
            try:
                with urllib.request.urlopen(base + '/ready', timeout=20) as response:
                    if response.status != 200: continue
                # An older healthy deployment would return 404 for the new routes;
                # verify the current protected contract before declaring success.
                home.verify_routes(base)
                print('Home readiness and protected routes verified.', flush=True); return
            except (OSError, TimeoutError): pass
            time.sleep(5)
        raise RuntimeError('Publish submitted but current Home version was not verified.')

if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(f'Home code publish stopped: {type(error).__name__}: {error}', flush=True)
        raise SystemExit(1)
