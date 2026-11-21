#!/usr/bin/env python3
"""Ensure the contract distinguishes independently authenticated services."""
import copy
import json
from pathlib import Path
import unittest
from jsonschema import Draft202012Validator, FormatChecker
from referencing import Registry, Resource

ROOT = Path(__file__).resolve().parents[1]
schemas = [json.loads(path.read_text()) for path in (ROOT / 'schemas').glob('*.json')]
registry = Registry().with_resources((schema['$id'], Resource.from_contents(schema)) for schema in schemas)
schema = next(item for item in schemas if item['title'] == 'JWT contract v1')
validator = Draft202012Validator(schema, registry=registry, format_checker=FormatChecker())
template = json.loads((ROOT / 'fixtures/jwt/service-token.json').read_text())['data']

class ServiceContractTests(unittest.TestCase):
    def test_only_consistent_service_identity_is_valid(self):
        for client in ('home', 'security', 'notifications', 'assistant'):
            token = copy.deepcopy(template)
            token['payload']['client_id'] = client
            token['payload']['sub'] = 'service:' + client
            self.assertTrue(validator.is_valid(token), client)
            token['payload']['sub'] = 'service:security' if client != 'security' else 'service:home'
            self.assertFalse(validator.is_valid(token), client)

if __name__ == '__main__': unittest.main()
