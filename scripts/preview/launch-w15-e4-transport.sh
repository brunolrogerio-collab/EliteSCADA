#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_DIR="$ROOT/.preview"
API_URL="http://127.0.0.1:5080"
WEB_URL="http://127.0.0.1:5173"
API_PID_FILE="$STATE_DIR/api.pid"
WEB_PID_FILE="$STATE_DIR/web.pid"
API_LOG="$STATE_DIR/api.log"
WEB_LOG="$STATE_DIR/web.log"

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

fail() {
  printf 'W15 E4 harness failed: %s\n' "$*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command '$1' is unavailable"
}

for command_name in dotnet npm node curl od; do
  require_command "$command_name"
done

ACTUAL_SDK="$(dotnet --version)" || fail "dotnet SDK resolution failed under repository global.json"
[[ "$ACTUAL_SDK" =~ ^10\.0\.4[0-9][0-9]$ ]] || fail "repository global.json resolved an unexpected SDK '$ACTUAL_SDK'; expected a compatible .NET 10.0.4xx feature-band SDK"
printf 'Resolved repository SDK via global.json: %s\n' "$ACTUAL_SDK"

stop_process() {
  local pid_file="$1"
  if [[ ! -f "$pid_file" ]]; then return 0; fi
  local pid
  pid="$(cat "$pid_file" 2>/dev/null || true)"
  if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    for _ in $(seq 1 20); do
      if ! kill -0 "$pid" 2>/dev/null; then break; fi
      sleep 0.25
    done
  fi
  rm -f "$pid_file"
}

wait_for_url() {
  local pid_file="$1"
  local url="$2"
  local log_file="$3"
  local label="$4"
  for _ in $(seq 1 180); do
    if curl --fail --silent --show-error "$url" >/dev/null 2>&1; then return 0; fi
    if [[ -f "$pid_file" ]]; then
      local pid
      pid="$(cat "$pid_file" 2>/dev/null || true)"
      if [[ -n "$pid" ]] && ! kill -0 "$pid" 2>/dev/null; then
        printf '%s exited before readiness. Last log lines:\n' "$label" >&2
        tail -n 120 "$log_file" >&2 || true
        return 1
      fi
    fi
    sleep 1
  done
  printf '%s did not become ready. Last log lines:\n' "$label" >&2
  tail -n 120 "$log_file" >&2 || true
  return 1
}

printf 'Checking private TimescaleDB service...\n'
DB_READY=false
for _ in $(seq 1 90); do
  if (exec 3<>/dev/tcp/timescaledb/5432) 2>/dev/null; then
    exec 3>&-
    exec 3<&-
    DB_READY=true
    break
  fi
  sleep 1
done
[[ "$DB_READY" == true ]] || fail "TimescaleDB did not become reachable on the private devcontainer network"

stop_process "$WEB_PID_FILE"
stop_process "$API_PID_FILE"
: > "$API_LOG"
: > "$WEB_LOG"
chmod 600 "$API_LOG" "$WEB_LOG"

JWT_SIGNING_KEY="$(od -An -N48 -tx1 /dev/urandom | tr -d ' \n')"
[[ ${#JWT_SIGNING_KEY} -ge 64 ]] || fail "could not generate an ephemeral JWT signing key"

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="$API_URL"
export ConnectionStrings__EliteScada='Host=timescaledb;Port=5432;Database=elitescada_e4;Username=postgres;Password=elitescada-e4-db;Pooling=false'
export ConnectionStrings__Historian="$ConnectionStrings__EliteScada"
export Historian__Provider=timescaledb
export Authentication__Enabled=true
export Authentication__Jwt__Issuer='EliteSCADA.W15E4'
export Authentication__Jwt__Audience='EliteSCADA.Web'
export Authentication__Jwt__SigningKey="$JWT_SIGNING_KEY"
export Authentication__Local__Enabled=true
export Authentication__Local__SecureCookie=true
export Authentication__Local__AccessTokenMinutes=480

printf 'Starting EliteSCADA API on %s...\n' "$API_URL"
(
  cd "$ROOT"
  exec dotnet run --project src/Scada.Api/Scada.Api.csproj --no-launch-profile
) >> "$API_LOG" 2>&1 &
echo "$!" > "$API_PID_FILE"
wait_for_url "$API_PID_FILE" "$API_URL/health" "$API_LOG" 'EliteSCADA API' || fail "API startup failed"

printf 'Starting EliteSCADA Web with API kept internal...\n'
export SCADA_API_PROXY="$API_URL"
(
  cd "$ROOT/web/scada-web"
  exec npm run dev -- --host 0.0.0.0 --port 5173
) >> "$WEB_LOG" 2>&1 &
echo "$!" > "$WEB_PID_FILE"
wait_for_url "$WEB_PID_FILE" "$WEB_URL" "$WEB_LOG" 'EliteSCADA Web' || fail "Web startup failed"

printf '\nW15 E4 transport harness is ready.\n'
printf '  Product baseline: 50b2750c73623b7ffef77f0ca93755c3e8278676\n'
printf '  Web: %s (use Codespaces forwarded port 5173)\n' "$WEB_URL"
printf '  API: %s (internal only)\n' "$API_URL"
printf '  Logs: %s and %s\n' "$API_LOG" "$WEB_LOG"
printf 'Complete normal first-run/bootstrap in the browser; no repository credential is embedded.\n'
