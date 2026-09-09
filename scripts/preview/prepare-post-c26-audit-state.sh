#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_DIR="$ROOT/.preview"
API_URL="${SCADA_PREVIEW_API_URL:-http://127.0.0.1:5080}"
FAULT_PATH="EEE.P01.Fault"
INJECT_FAULT_COMMAND_ID="c1150000-0000-4000-8000-000000000008"

fail() {
  printf 'Post-C26 audit-state preparation failed: %s\n' "$*" >&2
  exit 1
}

[[ -n "${ELITESCADA_PREVIEW_ADMIN_PASSWORD:-}" ]] || fail "ELITESCADA_PREVIEW_ADMIN_PASSWORD is not set"
mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

LOGIN_HEADERS="$STATE_DIR/audit-login.headers"
LOGIN_BODY="$STATE_DIR/audit-login.json"
LOGIN_STATUS="$(
  printf '%s' "$ELITESCADA_PREVIEW_ADMIN_PASSWORD" \
    | node -e '
let password="";
process.stdin.setEncoding("utf8");
process.stdin.on("data", chunk => password += chunk);
process.stdin.on("end", () => process.stdout.write(JSON.stringify({username:"EliteSCADA", password})));
' \
    | curl --silent --show-error \
        --dump-header "$LOGIN_HEADERS" \
        --output "$LOGIN_BODY" \
        --write-out '%{http_code}' \
        --header 'Content-Type: application/json' \
        --request POST \
        --data-binary @- \
        "$API_URL/api/auth/login"
)"
[[ "$LOGIN_STATUS" == "200" ]] || fail "administrative login returned HTTP $LOGIN_STATUS"

ACCESS_TOKEN="$(node -e '
const fs=require("fs");
const line=fs.readFileSync(process.argv[1],"utf8").split(/\r?\n/).find(x => /^set-cookie:\s*elitescada_access=/i.test(x));
if (!line) process.exit(2);
const match=line.match(/elitescada_access=([^;]+)/i);
if (!match) process.exit(3);
process.stdout.write(match[1]);
' "$LOGIN_HEADERS")" || fail "access cookie was not issued"
AUTH_COOKIE="elitescada_access=$ACCESS_TOKEN"
rm -f "$LOGIN_HEADERS" "$LOGIN_BODY"

printf 'Injecting canonical P01 fault for useful audit state...\n'
curl --fail-with-body --silent --show-error \
  --header "Cookie: $AUTH_COOKIE" \
  --request POST \
  "$API_URL/api/commands/$INJECT_FAULT_COMMAND_ID/execute" >/dev/null

FAULT_FILE="$STATE_DIR/p01-fault.json"
FAULT_ACTIVE=false
for _ in $(seq 1 30); do
  curl --fail --silent --show-error --header "Cookie: $AUTH_COOKIE" \
    "$API_URL/api/tags/by-path/$FAULT_PATH" > "$FAULT_FILE"
  if node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
process.exit(j.current?.value === true ? 0 : 1);
' "$FAULT_FILE"; then
    FAULT_ACTIVE=true
    break
  fi
  sleep 1
done
[[ "$FAULT_ACTIVE" == true ]] || fail "P01 fault did not become active"

query_dataset() {
  local dataset="$1"
  local output="$2"
  local request
  request="{\"datasetKey\":\"$dataset\",\"timeRange\":{\"kind\":\"relative\",\"durationSeconds\":600,\"anchor\":\"now\"},\"page\":{\"limit\":25}}"
  for _ in $(seq 1 30); do
    local status
    status="$(curl --silent --show-error \
      --header "Cookie: $AUTH_COOKIE" \
      --header 'Content-Type: application/json' \
      --request POST --data-binary "$request" \
      --output "$output" --write-out '%{http_code}' \
      "$API_URL/api/historical/query")"
    [[ "$status" != "404" ]] || fail "Historical Query route is not mounted"
    if [[ "$status" == "200" ]] && node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
process.exit(j.datasetKey===process.argv[2] && Array.isArray(j.rows) && j.rows.length>0 ? 0 : 1);
' "$output" "$dataset"; then
      return 0
    fi
    sleep 1
  done
  fail "$dataset did not expose useful rows"
}

printf 'Proving operational event history...\n'
query_dataset 'operational.events' "$STATE_DIR/operational-events.json"
printf 'Proving alarm history...\n'
query_dataset 'alarm.events' "$STATE_DIR/alarm-events.json"

ACCESS_TOKEN=''
AUTH_COOKIE=''
printf 'Post-C26 audit state ready: P01 fault ACTIVE; Alarm and Operational Event histories populated.\n'
