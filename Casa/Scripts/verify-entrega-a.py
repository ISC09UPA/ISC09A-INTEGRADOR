#!/usr/bin/env python3
"""Reproducible local verification suite for Entrega A (backend, contracts, APIM manifest, mobile)."""
import argparse
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]

CHECKS = [
    ('Identity .NET tests', ['dotnet', 'test', 'Backend/Identity/CasaDomotica.Identity.sln', '--no-restore', '--verbosity', 'quiet'], ROOT),
    ('Home .NET tests', ['dotnet', 'test', 'Backend/Home/CasaDomotica.Home.sln', '--no-restore', '--verbosity', 'quiet'], ROOT),
    ('Security Domain .NET tests', ['dotnet', 'test', 'Backend/Security/tests/CasaDomotica.Security.Domain.Tests/CasaDomotica.Security.Domain.Tests.csproj', '--no-restore', '--verbosity', 'quiet'], ROOT),
    ('Security Infra .NET tests', ['dotnet', 'test', 'Backend/Security/tests/CasaDomotica.Security.Infrastructure.Tests/CasaDomotica.Security.Infrastructure.Tests.csproj', '--no-restore', '--verbosity', 'quiet'], ROOT),
    ('Assistant .NET tests', ['dotnet', 'test', 'Backend/Assistant.Tests/CasaDomotica.Assistant.Tests.csproj', '--no-restore', '--verbosity', 'quiet'], ROOT),
    ('Notifications .NET tests', ['dotnet', 'test', 'Backend/Notifications/CasaDomotica.Notifications.sln', '--no-restore', '--verbosity', 'quiet'], ROOT),
    ('Contracts schemas & OpenAPI', ['../.venv/bin/python', 'tests/validate_contracts.py'], ROOT / 'contracts/v1'),
    ('Contracts fixtures', ['../.venv/bin/python', 'tests/validate_fixtures.py'], ROOT / 'contracts/v1'),
    ('Contracts matrix', ['../.venv/bin/python', 'tests/matrix_check.py'], ROOT / 'contracts/v1'),
    ('Contracts adversarial audit', ['../.venv/bin/python', 'tests/adversarial_audit.py'], ROOT / 'contracts/v1'),
    ('Contracts service authz', ['../.venv/bin/python', '-m', 'unittest', 'tests/test_service_authz_contract.py'], ROOT / 'contracts/v1'),
    ('APIM explicit public routes', ['python3', 'Scripts/deploy-apim.py', '--validate'], ROOT),
    ('Mobile TypeScript', ['npm', 'run', 'typecheck'], ROOT / 'Frontend/mobile'),
    ('Mobile ESLint', ['npm', 'run', 'lint'], ROOT / 'Frontend/mobile'),
    ('Mobile Jest suite', ['npm', 'test', '--', '--runInBand', '--no-watchman'], ROOT / 'Frontend/mobile'),
]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--with-export', action='store_true', help='Also run Android Expo bundle export')
    args = parser.parse_args()

    checks = list(CHECKS)
    if args.with_export:
        checks.append(('Mobile Android bundle export', ['npx', 'expo', 'export', '--platform', 'android', '--output-dir', '/private/tmp/casa-android-stage10'], ROOT / 'Frontend/mobile'))

    for label, cmd, cwd in checks:
        print(f'==> {label}', flush=True)
        result = subprocess.run(cmd, cwd=cwd, env=os.environ.copy())
        if result.returncode != 0:
            print(f'FAILED: {label}', file=sys.stderr)
            raise SystemExit(result.returncode)

    print('All Entrega A local verification checks passed.')


if __name__ == '__main__':
    main()
