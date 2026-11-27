#!/usr/bin/env bash
# verify-agent.sh — run raspberry-agent unit tests and validate emitted fixtures
# against the real C07/C11 contract schemas (offline, no Azure).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
AGENT_DIR="$REPO_ROOT/RaspberryPi"
CONTRACTS_DIR="$REPO_ROOT/contracts"

# Use the contract validation venv if available, otherwise fall back to current.
if [ -f "$CONTRACTS_DIR/.venv/bin/python" ]; then
  PYTHON="$CONTRACTS_DIR/.venv/bin/python"
  SITE_PACKAGES="$($PYTHON -c 'import sysconfig; print(sysconfig.get_path("purelib"))')"
  PYTHONPATH="$AGENT_DIR/src:$AGENT_DIR/tests:$SITE_PACKAGES"
else
  PYTHON="python3"
  PYTHONPATH="$AGENT_DIR/src:$AGENT_DIR/tests"
  echo "WARNING: contract validation venv not found; schema validation may fail closed." >&2
fi
export PYTHONPATH

cd "$AGENT_DIR"

echo "=== Agent unit tests ==="
$PYTHON -m unittest discover -s tests -v

echo "=== Contract schema validation ==="
cd "$CONTRACTS_DIR"
$PYTHON v1/tests/validate_contracts.py
$PYTHON v1/tests/validate_fixtures.py

echo "=== Agent verification complete ==="
