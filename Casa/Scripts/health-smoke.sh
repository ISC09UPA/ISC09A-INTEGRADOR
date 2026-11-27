#!/usr/bin/env bash
# health-smoke.sh — local smoke test for Identity and Home APIs.
# Usage: ./Scripts/health-smoke.sh
# Env: IDENTITY_PORT, HOME_PORT, MAX_WAIT_SEC, POLL_INTERVAL_SEC
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

IDENTITY_PORT="${IDENTITY_PORT:-5001}"
HOME_PORT="${HOME_PORT:-5002}"
MAX_WAIT_SEC="${MAX_WAIT_SEC:-30}"
POLL_INTERVAL_SEC="${POLL_INTERVAL_SEC:-1}"

IDENTITY_URL="http://localhost:${IDENTITY_PORT}"
HOME_URL="http://localhost:${HOME_PORT}"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

is_port_listening() {
  local port="$1"
  lsof -Pi :"$port" -sTCP:LISTEN >/dev/null 2>&1
}

check_port_free() {
  local port="$1" name="$2"
  if is_port_listening "$port"; then
    echo "FAIL: $name port $port is already in use by another process" >&2
    return 1
  fi
}

wait_for_status() {
  local url="$1" expected="$2" name="$3"
  local i=0 code
  while [ "$i" -lt "$MAX_WAIT_SEC" ]; do
    code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 "$url" 2>/dev/null || echo "000")
    if [ "$code" = "$expected" ]; then
      echo "OK    $name $url -> HTTP $code"
      return 0
    fi
    sleep "$POLL_INTERVAL_SEC"
    i=$((i + POLL_INTERVAL_SEC))
  done
  echo "FAIL  $name $url -> HTTP $code (expected $expected) after ${MAX_WAIT_SEC}s" >&2
  return 1
}

verify_process_owns_port() {
  local pid="$1" port="$2" name="$3"
  if ! lsof -Pi :"$port" -sTCP:LISTEN 2>/dev/null | awk 'NR>1 {print $2}' | grep -qx "$pid"; then
    echo "FAIL  $name process $pid does not own port $port (TOCTOU / orphan detected)" >&2
    return 1
  fi
  echo "OK    $name process $pid owns port $port"
}

# ---------------------------------------------------------------------------
# Preflight: ports must be free, build must pass.
# ---------------------------------------------------------------------------

check_port_free "$IDENTITY_PORT" "Identity"
check_port_free "$HOME_PORT" "Home"

if ! ./Scripts/test-backend.sh >/dev/null 2>&1; then
  echo "FAIL: backend build or tests did not pass" >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Create an isolated temp directory for PIDs and logs.
# ---------------------------------------------------------------------------

SMOKE_TMP="${TMPDIR:-/tmp}/casa-smoke.XXXXXX"
SMOKE_TMP="$(mktemp -d "$SMOKE_TMP")"

IDENTITY_DLL="$REPO_ROOT/Backend/Identity/src/CasaDomotica.Identity.Api/bin/Debug/net10.0/CasaDomotica.Identity.Api.dll"
HOME_DLL="$REPO_ROOT/Backend/Home/src/CasaDomotica.Home.Api/bin/Debug/net10.0/CasaDomotica.Home.Api.dll"

IDENTITY_PID=""
HOME_PID=""

# shellcheck disable=SC2317
cleanup() {
  set +e
  if [ -n "${IDENTITY_PID:-}" ] && kill -0 "$IDENTITY_PID" 2>/dev/null; then
    kill "$IDENTITY_PID" 2>/dev/null
    wait "$IDENTITY_PID" 2>/dev/null
  fi
  if [ -n "${HOME_PID:-}" ] && kill -0 "$HOME_PID" 2>/dev/null; then
    kill "$HOME_PID" 2>/dev/null
    wait "$HOME_PID" 2>/dev/null
  fi
  rm -rf "$SMOKE_TMP"
}
trap cleanup EXIT INT TERM

# ---------------------------------------------------------------------------
# Start compiled binaries directly (no dotnet run parent / orphan risk).
# ---------------------------------------------------------------------------

echo "=== Starting smoke services ==="
echo "Identity -> $IDENTITY_URL"
echo "Home     -> $HOME_URL"

dotnet "$IDENTITY_DLL" --urls "$IDENTITY_URL" >"$SMOKE_TMP/identity.log" 2>&1 &
IDENTITY_PID=$!

dotnet "$HOME_DLL" --urls "$HOME_URL" >"$SMOKE_TMP/home.log" 2>&1 &
HOME_PID=$!

# ---------------------------------------------------------------------------
# Wait for readiness, verify ownership, assert expected statuses.
# ---------------------------------------------------------------------------

echo "=== Waiting for readiness ==="

wait_for_status "$IDENTITY_URL/health" "200" "Identity /health"
wait_for_status "$HOME_URL/health"     "200" "Home /health"

verify_process_owns_port "$IDENTITY_PID" "$IDENTITY_PORT" "Identity"
verify_process_owns_port "$HOME_PID"     "$HOME_PORT"     "Home"

echo "=== Asserting business endpoints are 501 ==="

identity_business_code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 "$IDENTITY_URL/api/v1/identity/me" 2>/dev/null || echo "000")
if [ "$identity_business_code" != "501" ]; then
  echo "FAIL  Identity business endpoint returned HTTP $identity_business_code (expected 501)" >&2
  exit 1
fi
echo "OK    Identity business endpoint -> HTTP $identity_business_code"

home_business_code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 2 "$HOME_URL/api/v1/houses/h1/status" 2>/dev/null || echo "000")
if [ "$home_business_code" != "501" ]; then
  echo "FAIL  Home business endpoint returned HTTP $home_business_code (expected 501)" >&2
  exit 1
fi
echo "OK    Home business endpoint -> HTTP $home_business_code"

echo "=== Smoke passed ==="
