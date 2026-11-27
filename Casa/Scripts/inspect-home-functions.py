#!/usr/bin/env python3
"""Show sanitized startup diagnostics for the project's Home IoT consumer."""
import importlib.util
import json
import os
from pathlib import Path
import re
from datetime import datetime, timedelta, timezone
from urllib.parse import urlencode

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('deployment_helpers', ROOT / 'Scripts/deploy-identity.py')
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)

def redact(value):
    text = str(value)
    text = re.sub(r'https?://\S+', '[url]', text)
    text = re.sub(r'(?i)(SharedAccessKey|AccountKey|password|token|sig)=([^;\s&]+)', r'\1=[redacted]', text)
    text = re.sub(r'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+', '[token]', text)
    return text

def main():
    os.umask(0o077)
    if helpers.az('account', 'show')['id'] != '00000000-0000-0000-0000-000000000000':
        raise RuntimeError('Unexpected subscription.')
    query = 'exceptions | where timestamp > ago(1h) | order by timestamp desc | take 1 | project timestamp, details'
    result = helpers.az('monitor', 'app-insights', 'query', '--app', '10dae6a1-fe43-41ed-aad2-71c5d3357f49', '--analytics-query', query)
    for table in result.get('tables', []):
        for row in table.get('rows', []):
            print(json.dumps([redact(item) for item in row]))
    site = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-casa-example/providers/Microsoft.Web/sites/home-function-example'
    now = datetime.now(timezone.utc)
    window = urlencode({'api-version': '2024-11-01', 'startTime': (now - timedelta(hours=2)).isoformat(), 'endTime': now.isoformat()})
    try:
        report = helpers.az('rest', '--method', 'get', '--url', f'https://management.azure.com{site}/detectors/FunctionFlexDeployment?{window}')
    except RuntimeError:
        print('The optional deployment detector is unavailable; runtime exception results are shown above.')
        return
    (ROOT / 'secrets/home-functions-diagnostic.json').write_text(json.dumps(report))
    for dataset in report.get('properties', {}).get('dataset', []):
        table = dataset.get('table', {})
        print('Detector:', redact(table.get('tableName', '')))
        for row in table.get('rows', [])[:15]:
            print(json.dumps([redact(item) for item in row])[:4000])

if __name__ == '__main__':
    main()
