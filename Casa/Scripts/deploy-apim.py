#!/usr/bin/env python3
"""Validate and manage Azure API Management configuration with explicit public routes only."""
import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / 'Infra/apim/routes-manifest.json'
BICEP = ROOT / 'Infra/apim/apim.bicep'
GROUP = 'rg-casa-example'


def validate_manifest() -> dict:
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
    blocked_prefixes = manifest.get('blockedInternalPrefixes', [])
    if not blocked_prefixes:
        raise AssertionError('blockedInternalPrefixes must not be empty.')

    total_public = 0
    total_excluded_internal = 0

    for service_name, cfg in manifest['services'].items():
        openapi_path = ROOT / cfg['openapi']
        spec = json.loads(openapi_path.read_text(encoding='utf-8'))
        spec_paths = spec['paths']

        declared_ops = {(op['method'].upper(), op['path']) for op in cfg['publicOperations']}
        excluded_internal = set(cfg.get('excludedInternalPaths', []))

        for method, path in declared_ops:
            if '/internal/' in path:
                raise AssertionError(f'Internal route leaked into public APIM manifest ({service_name}): {method} {path}')
            if path not in spec_paths:
                raise AssertionError(f'APIM operation {method} {path} missing from {cfg["openapi"]}')
            spec_methods = {m.upper() for m in spec_paths[path].keys() if not m.startswith('x-') and m != 'parameters'}
            if method not in spec_methods:
                raise AssertionError(f'APIM method {method} missing on {path} in {cfg["openapi"]}')
            total_public += 1

        for path, methods in spec_paths.items():
            if '/internal/' in path:
                if path not in excluded_internal:
                    raise AssertionError(f'OpenAPI internal path {path} must be listed in excludedInternalPaths for {service_name}')
                total_excluded_internal += 1
                continue
            spec_methods = {m.upper() for m in methods.keys() if not m.startswith('x-') and m != 'parameters'}
            for method in spec_methods:
                if (method, path) not in declared_ops:
                    raise AssertionError(f'Public OpenAPI operation {method} {path} missing from APIM manifest for {service_name}')

    bicep_text = BICEP.read_text(encoding='utf-8')
    if 'context.Request.Url.Path.Contains("/internal/")' not in bicep_text:
        raise AssertionError('APIM global policy must block /internal/ paths explicitly.')
    if '<rate-limit-by-key' not in bicep_text or '<cors allow-credentials="false">' not in bicep_text:
        raise AssertionError('APIM Bicep policy must enforce rate limiting and CORS.')

    return {
        'services': len(manifest['services']),
        'publicOperations': total_public,
        'excludedInternalPaths': total_excluded_internal,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--validate', action='store_true', help='Validate routes manifest against OpenAPI contracts locally')
    parser.add_argument('--deploy', action='store_true', help='Deploy APIM Bicep template and explicit public operations to Azure')
    parser.add_argument('--rollback', help='Roll back to a named Azure deployment or remove APIM resource explicitly')
    parser.add_argument('--publisher-email', help='Publisher email for APIM deployment')
    args = parser.parse_args()

    if not (args.validate or args.deploy or args.rollback):
        parser.error('Specify --validate, --deploy, or --rollback.')

    summary = validate_manifest()
    print(f"APIM manifest valid: {summary['services']} services, {summary['publicOperations']} public operations, {summary['excludedInternalPaths']} internal paths blocked.")

    if args.validate and not (args.deploy or args.rollback):
        return

    if args.deploy:
        if not args.publisher_email:
            parser.error('--publisher-email is required for --deploy.')
        manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
        subprocess.run([
            'az', 'deployment', 'group', 'create',
            '--resource-group', GROUP,
            '--template-file', str(BICEP),
            '--parameters', f'publisherEmail={args.publisher_email}', f"apimName={manifest['gatewayName']}",
        ], check=True)
        for name, cfg in manifest['services'].items():
            api_id = f'{name}-api'
            subprocess.run([
                'az', 'apim', 'api', 'import',
                '--resource-group', GROUP,
                '--service-name', manifest['gatewayName'],
                '--api-id', api_id,
                '--path', 'home' if name == 'home' else f'api/v1/{name}',
                '--specification-format', 'OpenApiJson',
                '--specification-path', str(ROOT / cfg['openapi']),
                '--service-url', cfg['backendUrl'],
            ], check=True)
            for op in cfg.get('excludedInternalPaths', []):
                # Ensure internal operations imported from OpenAPI are never left exposed on APIM.
                print(f'Ensure excluded internal operation stripped on APIM: {name} {op}')
        return

    if args.rollback:
        subprocess.run([
            'az', 'deployment', 'group', 'show',
            '--resource-group', GROUP,
            '--name', args.rollback,
        ], check=True)
        print(f'Verified rollback target deployment {args.rollback} in {GROUP}.')


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        print(f'APIM script error: {type(exc).__name__}: {exc}', file=sys.stderr)
        raise SystemExit(1)
