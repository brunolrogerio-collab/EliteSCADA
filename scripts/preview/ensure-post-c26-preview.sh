#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_DIR="$ROOT/.preview"
LOCK_FILE="$STATE_DIR/post-attach.lock"
API_PID_FILE="$STATE_DIR/api.pid"
WEB_PID_FILE="$STATE_DIR/web.pid"
WEB_LOG="$STATE_DIR/web.log"

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

fail() {
  printf 'Post-C26 Preview resume failed: %s\n' "$*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command '$1' is unavailable in the devcontainer"
}

for command_name in bash curl flock pgrep pkill python3; do
  require_command "$command_name"
done

PYTHON_EXECUTABLE="$(command -v python3)"
if ! "$PYTHON_EXECUTABLE" -I -S -c 'import ast, json, sys' >/dev/null 2>&1; then
  fail "python3 is present but its standard library is incomplete under EliteSCADA isolation flags (-I -S); rebuild the Codespace from the current Preview branch"
fi
printf 'Validated isolated Server Script Python runtime: %s (%s)\n' "$PYTHON_EXECUTABLE" "$("$PYTHON_EXECUTABLE" --version 2>&1)"

# postAttach can be invoked more than once when Codespaces reconnects or when more
# than one VS Code client attaches. Serialize the recovery so two launchers cannot
# race for API/Web ports or rewrite the ephemeral auth state concurrently.
exec 9>"$LOCK_FILE"
flock -w 180 9 || fail "another Preview attach/recovery is still running after 180 seconds"

stop_pid_file() {
  local pid_file="$1"
  [[ -f "$pid_file" ]] || return 0

  local pid
  pid="$(cat "$pid_file" 2>/dev/null || true)"
  if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    for _ in $(seq 1 20); do
      kill -0 "$pid" 2>/dev/null || break
      sleep 0.25
    done
    kill -0 "$pid" 2>/dev/null && kill -9 "$pid" 2>/dev/null || true
  fi
  rm -f "$pid_file"
}

terminate_pattern() {
  local pattern="$1"
  pgrep -f "$pattern" >/dev/null 2>&1 || return 0
  pkill -TERM -f "$pattern" 2>/dev/null || true
  for _ in $(seq 1 20); do
    pgrep -f "$pattern" >/dev/null 2>&1 || return 0
    sleep 0.25
  done
  pkill -KILL -f "$pattern" 2>/dev/null || true
}

port_is_open() {
  local port="$1"
  (exec 3<>"/dev/tcp/127.0.0.1/$port") >/dev/null 2>&1
}

printf 'Normalizing post-C26 Preview processes before attach/resume...\n'
stop_pid_file "$WEB_PID_FILE"
stop_pid_file "$API_PID_FILE"

# npm and dotnet run can leave a child behind if only their recorded parent PID
# disappears during a Codespaces stop/reconnect. Kill only processes belonging to
# this disposable Preview workspace, never arbitrary system listeners.
terminate_pattern "$ROOT/web/scada-web/node_modules/.*vite"
terminate_pattern "dotnet run --project src/Scada.Api/Scada.Api.csproj"
terminate_pattern "$ROOT/src/Scada.Api/bin/.*/Scada.Api"

for _ in $(seq 1 20); do
  if ! port_is_open 5173 && ! port_is_open 5080; then
    break
  fi
  sleep 0.25
done

port_is_open 5173 && fail "Web port 5173 is still occupied after controlled Preview cleanup"
port_is_open 5080 && fail "internal API port 5080 is still occupied after controlled Preview cleanup"

# Keep the lock in this wrapper while the canonical launcher runs, but explicitly
# close descriptor 9 for the launcher process. Otherwise the long-lived API/Web
# descendants inherit the flock and make every later Codespaces attach look like
# another recovery is permanently running. Bind the backend to the exact Python
# interpreter that passed the same -I -S standard-library preflight used by the
# product's isolated Server Script executor.
ServerScripts__PythonExecutable="$PYTHON_EXECUTABLE" \
  bash "$ROOT/scripts/preview/launch-post-c26-preview.sh" 9>&-

port_is_open 5080 || fail "EliteSCADA API did not remain listening on fixed internal port 5080"
port_is_open 5173 || fail "EliteSCADA Web did not remain listening on fixed port 5173"
curl --fail --silent --show-error http://127.0.0.1:5080/health >/dev/null || fail "API health check failed after attach/resume"
curl --fail --silent --show-error http://127.0.0.1:5173/ >/dev/null || fail "Web health check failed after attach/resume"

# Vite normally falls forward to 5174+ when its requested port is occupied. That is
# forbidden for this Preview because Codespaces must expose one stable Web port.
if [[ -f "$WEB_LOG" ]] && grep -Eiq 'port 5173 is in use|trying another one|localhost:517[4-9]|0\.0\.0\.0:517[4-9]' "$WEB_LOG"; then
  fail "Vite attempted to migrate away from fixed Preview port 5173"
fi

printf 'Post-C26 Preview attach/resume is healthy on fixed ports: API 5080 (internal), Web 5173.\n'
