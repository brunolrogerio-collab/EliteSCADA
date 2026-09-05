#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_DIR="$ROOT/.preview"
API_URL="http://127.0.0.1:5080"
WEB_URL="http://127.0.0.1:5173"
PROJECT_KEY="e2e-wave11"
PROJECT_NAME="EliteSCADA Wave 11 Demo"
FIXTURE_METADATA="$ROOT/preview/fixtures/EliteSCADA-Wave11-Demo.json"
FIXTURE_ENCODED="$ROOT/preview/fixtures/EliteSCADA-Wave11-Demo.escadapkg.base64"
FIXTURE_PACKAGE="$STATE_DIR/EliteSCADA-Wave11-Demo.escadapkg"
API_PID_FILE="$STATE_DIR/api.pid"
WEB_PID_FILE="$STATE_DIR/web.pid"
API_LOG="$STATE_DIR/api.log"
WEB_LOG="$STATE_DIR/web.log"
LOGIN_HEADERS="$STATE_DIR/login.headers"
LOGIN_BODY="$STATE_DIR/login.json"

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

fail() {
  printf 'Test Preview failed: %s\n' "$*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command '$1' is unavailable in the devcontainer"
}

for command_name in dotnet npm node curl base64 sha256sum od; do
  require_command "$command_name"
done

if [[ -z "${ELITESCADA_PREVIEW_ADMIN_PASSWORD:-}" ]]; then
  fail "ELITESCADA_PREVIEW_ADMIN_PASSWORD is not set. Configure it as a GitHub Codespaces secret and rebuild/restart the Codespace."
fi

# Copy the Codespaces secret into a non-exported shell variable, then remove the
# original environment variable so long-lived child processes cannot inherit it.
PREVIEW_ADMIN_PASSWORD="$ELITESCADA_PREVIEW_ADMIN_PASSWORD"
unset ELITESCADA_PREVIEW_ADMIN_PASSWORD

stop_process() {
  local pid_file="$1"
  if [[ ! -f "$pid_file" ]]; then
    return 0
  fi

  local pid
  pid="$(cat "$pid_file" 2>/dev/null || true)"
  if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    for _ in $(seq 1 20); do
      if ! kill -0 "$pid" 2>/dev/null; then
        break
      fi
      sleep 0.25
    done
    if kill -0 "$pid" 2>/dev/null; then
      kill -9 "$pid" 2>/dev/null || true
    fi
  fi
  rm -f "$pid_file"
}

wait_for_process_url() {
  local pid_file="$1"
  local url="$2"
  local log_file="$3"
  local label="$4"

  for _ in $(seq 1 180); do
    if curl --fail --silent --show-error "$url" >/dev/null 2>&1; then
      return 0
    fi

    if [[ -f "$pid_file" ]]; then
      local pid
      pid="$(cat "$pid_file" 2>/dev/null || true)"
      if [[ -n "$pid" ]] && ! kill -0 "$pid" 2>/dev/null; then
        printf '%s exited before becoming ready. Last log lines:\n' "$label" >&2
        tail -n 80 "$log_file" >&2 || true
        return 1
      fi
    fi
    sleep 1
  done

  printf '%s did not become ready. Last log lines:\n' "$label" >&2
  tail -n 80 "$log_file" >&2 || true
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

EXPECTED_SHA="$(node -e 'const m=require(process.argv[1]); process.stdout.write(m.packageSha256)' "$FIXTURE_METADATA")"
EXPECTED_SIZE="$(node -e 'const m=require(process.argv[1]); process.stdout.write(String(m.packageSizeBytes))' "$FIXTURE_METADATA")"
METADATA_PROJECT_KEY="$(node -e 'const m=require(process.argv[1]); process.stdout.write(m.projectKey)' "$FIXTURE_METADATA")"
[[ "$METADATA_PROJECT_KEY" == "$PROJECT_KEY" ]] || fail "fixture project key does not match Preview configuration"

base64 --decode "$FIXTURE_ENCODED" > "$FIXTURE_PACKAGE"
ACTUAL_SIZE="$(wc -c < "$FIXTURE_PACKAGE" | tr -d '[:space:]')"
ACTUAL_SHA="$(sha256sum "$FIXTURE_PACKAGE" | awk '{print $1}')"
[[ "$ACTUAL_SIZE" == "$EXPECTED_SIZE" ]] || fail "fixture size mismatch: expected $EXPECTED_SIZE bytes, got $ACTUAL_SIZE"
[[ "$ACTUAL_SHA" == "$EXPECTED_SHA" ]] || fail "fixture SHA-256 mismatch"
printf 'Validated Wave 11 Demo fixture: %s\n' "$ACTUAL_SHA"

stop_process "$WEB_PID_FILE"
stop_process "$API_PID_FILE"
rm -f "$LOGIN_HEADERS" "$LOGIN_BODY"
: > "$API_LOG"
: > "$WEB_LOG"
chmod 600 "$API_LOG" "$WEB_LOG"

JWT_SIGNING_KEY="$(od -An -N48 -tx1 /dev/urandom | tr -d ' \n')"
[[ ${#JWT_SIGNING_KEY} -ge 64 ]] || fail "could not generate an ephemeral JWT signing key"

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="$API_URL"
export ConnectionStrings__EliteScada='Host=timescaledb;Port=5432;Database=elitescada_preview;Username=postgres;Password=elitescada-preview-db;Pooling=false'
export ConnectionStrings__Historian="$ConnectionStrings__EliteScada"
export Historian__Provider=timescaledb
export EngineeringRuntime__ProjectKey="$PROJECT_KEY"
export Authentication__Enabled=true
export Authentication__Jwt__Issuer='EliteSCADA.TestPreview'
export Authentication__Jwt__Audience='EliteSCADA.Web'
export Authentication__Jwt__SigningKey="$JWT_SIGNING_KEY"
export Authentication__Local__Enabled=true
export Authentication__Local__SecureCookie=true
export Authentication__Local__AccessTokenMinutes=480
export Authentication__Local__Bootstrap__Username='EliteSCADA'
export Authentication__Local__Bootstrap__DisplayName='EliteSCADA Test Preview'
export Authentication__Local__Bootstrap__Roles__0='developer'

start_api() {
  (
    cd "$ROOT"
    exec dotnet run --project src/Scada.Api/Scada.Api.csproj --no-launch-profile
  ) >> "$API_LOG" 2>&1 &
  echo "$!" > "$API_PID_FILE"
}

# First boot may create the empty PostgreSQL identity store. The protected password is
# supplied only to this short-lived bootstrap process. The long-lived API is restarted
# without either the Codespaces secret name or bootstrap password in its environment.
export Authentication__Local__Bootstrap__Password="$PREVIEW_ADMIN_PASSWORD"
printf 'Bootstrapping persistent local identity store...\n'
start_api
wait_for_process_url "$API_PID_FILE" "$API_URL/health" "$API_LOG" 'EliteSCADA API bootstrap' || fail "API bootstrap failed"
stop_process "$API_PID_FILE"
unset Authentication__Local__Bootstrap__Password

printf 'Starting EliteSCADA API without bootstrap password in its process environment...\n'
start_api
wait_for_process_url "$API_PID_FILE" "$API_URL/health" "$API_LOG" 'EliteSCADA API' || fail "API startup failed"

# Build the login JSON from stdin so arbitrary password characters are escaped correctly
# and the password never appears in the curl command line.
LOGIN_STATUS="$(
  printf '%s' "$PREVIEW_ADMIN_PASSWORD" \
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

if [[ "$LOGIN_STATUS" != "200" ]]; then
  fail "administrative login returned HTTP $LOGIN_STATUS. If this Codespace previously used a different Preview password, rebuild/reset its temporary database."
fi

ACCESS_TOKEN="$(node -e '
const fs=require("fs");
const lines=fs.readFileSync(process.argv[1],"utf8").split(/\r?\n/);
const line=lines.find(x => /^set-cookie:\s*elitescada_access=/i.test(x));
if (!line) process.exit(2);
const match=line.match(/elitescada_access=([^;]+)/i);
if (!match) process.exit(3);
process.stdout.write(match[1]);
' "$LOGIN_HEADERS")" || fail "local login succeeded but the access cookie was not issued"
[[ -n "$ACCESS_TOKEN" ]] || fail "local login succeeded but the access cookie was empty"
AUTH_COOKIE="elitescada_access=$ACCESS_TOKEN"
rm -f "$LOGIN_HEADERS" "$LOGIN_BODY"

AUTH_ME="$STATE_DIR/auth-me.json"
curl --fail --silent --show-error \
  --header "Cookie: $AUTH_COOKIE" \
  "$API_URL/api/auth/me" > "$AUTH_ME"
node -e '
const fs=require("fs");
const profile=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (!Array.isArray(profile.roles) || !profile.roles.some(r => String(r).toLowerCase() === "developer")) {
  throw new Error("Preview administrator does not have the developer role");
}
' "$AUTH_ME" || fail "Preview administrator role validation failed"
rm -f "$AUTH_ME"

LIFECYCLE_FILE="$STATE_DIR/lifecycle.json"
curl --fail --silent --show-error \
  --header "Cookie: $AUTH_COOKIE" \
  "$API_URL/api/engineering/persistence/$PROJECT_KEY/lifecycle" > "$LIFECYCLE_FILE"
ACTIVE_REVISION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
process.stdout.write(j.activeRevision == null ? "" : String(j.activeRevision));
' "$LIFECYCLE_FILE")"

if [[ -z "$ACTIVE_REVISION" ]]; then
  printf 'Importing validated Demo through the normal Engineering package API...\n'
  WORKSPACE_FILE="$STATE_DIR/workspace.json"
  curl --fail --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    "$API_URL/api/engineering/workspace" > "$WORKSPACE_FILE"
  WORKSPACE_VERSION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (!Number.isInteger(j.changeVersion) || j.changeVersion < 0) throw new Error("invalid changeVersion");
process.stdout.write(String(j.changeVersion));
' "$WORKSPACE_FILE")"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/vnd.elitescada.project-package' \
    --request POST \
    --data-binary "@$FIXTURE_PACKAGE" \
    "$API_URL/api/project-package/import/preview" > "$STATE_DIR/import-preview.json"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/vnd.elitescada.project-package' \
    --header "x-elitescada-workspace-version: $WORKSPACE_VERSION" \
    --request POST \
    --data-binary "@$FIXTURE_PACKAGE" \
    "$API_URL/api/project-package/import/apply" > "$STATE_DIR/import-apply.json"

  SAVE_FILE="$STATE_DIR/save.json"
  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/json' \
    --request POST \
    --data-binary "{\"projectName\":\"$PROJECT_NAME\",\"savedBy\":\"Test Preview bootstrap\"}" \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/save" > "$SAVE_FILE"
  REVISION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (!Number.isInteger(j.revision) || j.revision < 1) throw new Error("save did not return a revision");
process.stdout.write(String(j.revision));
' "$SAVE_FILE")"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/json' \
    --request POST \
    --data-binary '{"publishedBy":"Test Preview bootstrap"}' \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/revisions/$REVISION/publish" > "$STATE_DIR/publish.json"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/json' \
    --request POST \
    --data-binary '{"activatedBy":"Test Preview bootstrap"}' \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/published/activate" > "$STATE_DIR/activate.json"

  ACTIVE_REVISION="$REVISION"
  printf 'Activated persisted Demo revision %s.\n' "$ACTIVE_REVISION"
else
  printf 'Persisted Active revision %s already exists; preserving current Preview state.\n' "$ACTIVE_REVISION"
fi

RUNTIME_FILE="$STATE_DIR/runtime.json"
curl --fail --silent --show-error \
  --header "Cookie: $AUTH_COOKIE" \
  "$API_URL/api/engineering/persistence/$PROJECT_KEY/runtime" > "$RUNTIME_FILE"
node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.consistent !== true) throw new Error("persisted Active revision and live Runtime are inconsistent");
if (!j.live || String(j.live.projectKey).toLowerCase() !== "e2e-wave11") throw new Error("live Runtime is not mounted from the Preview project");
' "$RUNTIME_FILE" || fail "Active HMI Runtime consistency validation failed"

LICENSE_FILE="$STATE_DIR/licensing.json"
curl --fail --silent --show-error \
  --header "Cookie: $AUTH_COOKIE" \
  "$API_URL/api/licensing/status" > "$LICENSE_FILE"
LICENSE_STATE="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
process.stdout.write(String(j.license?.state ?? "unknown"));
' "$LICENSE_FILE")"
[[ "$LICENSE_STATE" == "Demo" ]] || fail "Preview expected official Demo licensing state but received '$LICENSE_STATE'"

# Clear authentication/bootstrap material from the launcher before starting the Web process.
ACCESS_TOKEN=''
AUTH_COOKIE=''
PREVIEW_ADMIN_PASSWORD=''
JWT_SIGNING_KEY=''
unset Authentication__Jwt__SigningKey
unset Authentication__Local__Bootstrap__Username
unset Authentication__Local__Bootstrap__DisplayName
unset Authentication__Local__Bootstrap__Roles__0
unset Authentication__Local__Enabled
unset Authentication__Local__SecureCookie
unset Authentication__Local__AccessTokenMinutes
unset Authentication__Enabled
unset Authentication__Jwt__Issuer
unset Authentication__Jwt__Audience
unset ConnectionStrings__EliteScada
unset ConnectionStrings__Historian
unset Historian__Provider
unset EngineeringRuntime__ProjectKey
unset ASPNETCORE_URLS
unset ASPNETCORE_ENVIRONMENT

printf 'Starting EliteSCADA Web with API proxy kept inside the app container...\n'
export SCADA_API_PROXY="$API_URL"
(
  cd "$ROOT/web/scada-web"
  exec npm run dev -- --host 0.0.0.0 --port 5173
) >> "$WEB_LOG" 2>&1 &
echo "$!" > "$WEB_PID_FILE"
wait_for_process_url "$WEB_PID_FILE" "$WEB_URL" "$WEB_LOG" 'EliteSCADA Web' || fail "Web startup failed"

printf '\nEliteSCADA Test Preview is ready.\n'
printf '  Web: %s (Codespaces forwards port 5173 privately by default)\n' "$WEB_URL"
printf '  Login: EliteSCADA\n'
printf '  Runtime project: %s / Active revision %s\n' "$PROJECT_KEY" "$ACTIVE_REVISION"
printf '  Licensing: Demo\n'
printf 'Use the forwarded “EliteSCADA Web — Test Preview” port in the Codespaces Ports panel.\n'
