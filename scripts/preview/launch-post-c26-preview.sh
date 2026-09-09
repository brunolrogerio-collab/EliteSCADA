#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_DIR="$ROOT/.preview"
API_URL="http://127.0.0.1:5080"
WEB_URL="http://127.0.0.1:5173"
PROJECT_KEY="eee-demo"
PROJECT_NAME="EliteSCADA — EEE Demo"
LEVEL_PATH="EEE.Process.LevelPct"
FIXTURE_PACKAGE="$ROOT/preview/fixtures/EliteSCADA-EEE-Demo.escadapkg"
FIXTURE_CHECKSUM="$ROOT/preview/fixtures/EliteSCADA-EEE-Demo.escadapkg.sha256"
FIXTURE_PROVENANCE="$ROOT/preview/fixtures/EliteSCADA-EEE-Demo.provenance.json"
EXPECTED_PACKAGE_SHA="e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d"
EXPECTED_SOURCE_PRODUCT_SHA="08e2530671de10d48933c4b712a1a1abc9e41dce"
EXPECTED_GENERATOR_C11_SHA="38c4de56affd0e36edd7682d61aad0a53e11213e"
EXPECTED_STARTUP_SCREEN_ID="c1170000-0000-4000-8000-000000000001"
FIXTURE_MARKER="$STATE_DIR/active-fixture.sha256"
API_PID_FILE="$STATE_DIR/api.pid"
WEB_PID_FILE="$STATE_DIR/web.pid"
API_LOG="$STATE_DIR/api.log"
WEB_LOG="$STATE_DIR/web.log"
LOGIN_HEADERS="$STATE_DIR/login.headers"
LOGIN_BODY="$STATE_DIR/login.json"

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

fail() {
  printf 'Post-C26 Preview failed: %s\n' "$*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command '$1' is unavailable in the devcontainer"
}

for command_name in dotnet npm node curl sha256sum od unzip; do
  require_command "$command_name"
done

[[ -f "$FIXTURE_PACKAGE" ]] || fail "frozen post-C26 package is missing"
[[ -f "$FIXTURE_CHECKSUM" ]] || fail "frozen post-C26 package checksum is missing"
[[ -f "$FIXTURE_PROVENANCE" ]] || fail "frozen post-C26 package provenance is missing"

EXPECTED_SHA="$(awk 'NF {print $1; exit}' "$FIXTURE_CHECKSUM")"
ACTUAL_SHA="$(sha256sum "$FIXTURE_PACKAGE" | awk '{print $1}')"
PROVENANCE_SHA="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.packageSha256 ?? ""))' "$FIXTURE_PROVENANCE")"
PROVENANCE_PROJECT_KEY="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.projectKey ?? ""))' "$FIXTURE_PROVENANCE")"
PROVENANCE_PACKAGE_NAME="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.packageFileName ?? ""))' "$FIXTURE_PROVENANCE")"
PROVENANCE_SOURCE_SHA="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.sourceProductSha ?? ""))' "$FIXTURE_PROVENANCE")"
PROVENANCE_C11_SHA="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.c11DemoCommitSha ?? ""))' "$FIXTURE_PROVENANCE")"

[[ "$EXPECTED_SHA" == "$EXPECTED_PACKAGE_SHA" ]] || fail "committed checksum is not the accepted post-C26 package SHA-256"
[[ "$ACTUAL_SHA" == "$EXPECTED_PACKAGE_SHA" ]] || fail "frozen post-C26 package SHA-256 mismatch"
[[ "$PROVENANCE_SHA" == "$EXPECTED_PACKAGE_SHA" ]] || fail "provenance SHA-256 does not match frozen package"
[[ "$PROVENANCE_PROJECT_KEY" == "$PROJECT_KEY" ]] || fail "provenance project key mismatch"
[[ "$PROVENANCE_PACKAGE_NAME" == "$(basename "$FIXTURE_PACKAGE")" ]] || fail "provenance package filename mismatch"
[[ "$PROVENANCE_SOURCE_SHA" == "$EXPECTED_SOURCE_PRODUCT_SHA" ]] || fail "provenance is not bound to the accepted C26 product"
[[ "$PROVENANCE_C11_SHA" == "$EXPECTED_GENERATOR_C11_SHA" ]] || fail "provenance is not bound to the exact C11 generator SHA"
if unzip -Z1 "$FIXTURE_PACKAGE" | grep -E '\.escadalib$'; then
  fail "frozen post-C26 package must remain self-contained and cannot depend on .escadalib files"
fi
printf 'Validated post-C26 frozen package: %s\n' "$ACTUAL_SHA"

if [[ -z "${ELITESCADA_PREVIEW_ADMIN_PASSWORD:-}" ]]; then
  fail "ELITESCADA_PREVIEW_ADMIN_PASSWORD is not set. Configure it as a GitHub Codespaces secret and rebuild/restart the Codespace."
fi
PREVIEW_ADMIN_PASSWORD="$ELITESCADA_PREVIEW_ADMIN_PASSWORD"
unset ELITESCADA_PREVIEW_ADMIN_PASSWORD

stop_process() {
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
        tail -n 100 "$log_file" >&2 || true
        return 1
      fi
    fi
    sleep 1
  done
  printf '%s did not become ready. Last log lines:\n' "$label" >&2
  tail -n 100 "$log_file" >&2 || true
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
rm -f "$LOGIN_HEADERS" "$LOGIN_BODY"
: > "$API_LOG"
: > "$WEB_LOG"
chmod 600 "$API_LOG" "$WEB_LOG"

JWT_SIGNING_KEY="$(od -An -N48 -tx1 /dev/urandom | tr -d ' \n')"
[[ ${#JWT_SIGNING_KEY} -ge 64 ]] || fail "could not generate an ephemeral JWT signing key"
HISTORICAL_CURSOR_KEY="$(node -e 'process.stdout.write(require("node:crypto").randomBytes(48).toString("base64"))')"
[[ -n "$HISTORICAL_CURSOR_KEY" ]] || fail "could not generate an ephemeral Historical Query cursor key"

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="$API_URL"
export ConnectionStrings__EliteScada='Host=timescaledb;Port=5432;Database=elitescada_preview;Username=postgres;Password=elitescada-preview-db;Pooling=false'
export ConnectionStrings__Historian="$ConnectionStrings__EliteScada"
export Historian__Provider=timescaledb
export HistoricalQuery__Enabled=true
export HistoricalQuery__CursorKeyBase64="$HISTORICAL_CURSOR_KEY"
export EngineeringRuntime__ProjectKey="$PROJECT_KEY"
export Authentication__Enabled=true
export Authentication__Jwt__Issuer='EliteSCADA.PostC26Preview'
export Authentication__Jwt__Audience='EliteSCADA.Web'
export Authentication__Jwt__SigningKey="$JWT_SIGNING_KEY"
export Authentication__Local__Enabled=true
export Authentication__Local__SecureCookie=true
export Authentication__Local__AccessTokenMinutes=480
export Authentication__Local__Bootstrap__Username='EliteSCADA'
export Authentication__Local__Bootstrap__DisplayName='EliteSCADA Post-C26 Preview'
export Authentication__Local__Bootstrap__Roles__0='developer'

start_api() {
  (
    cd "$ROOT"
    exec dotnet run --project src/Scada.Api/Scada.Api.csproj --no-launch-profile
  ) >> "$API_LOG" 2>&1 &
  echo "$!" > "$API_PID_FILE"
}

export Authentication__Local__Bootstrap__Password="$PREVIEW_ADMIN_PASSWORD"
printf 'Bootstrapping persistent local identity store...\n'
start_api
wait_for_process_url "$API_PID_FILE" "$API_URL/health" "$API_LOG" 'EliteSCADA API bootstrap' || fail "API bootstrap failed"
stop_process "$API_PID_FILE"
unset Authentication__Local__Bootstrap__Password

printf 'Starting EliteSCADA API without bootstrap password in its process environment...\n'
start_api
wait_for_process_url "$API_PID_FILE" "$API_URL/health" "$API_LOG" 'EliteSCADA API' || fail "API startup failed"

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
[[ "$LOGIN_STATUS" == "200" ]] || fail "administrative login returned HTTP $LOGIN_STATUS; rebuild disposable Preview state if its identity was initialized with another password"

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
curl --fail --silent --show-error --header "Cookie: $AUTH_COOKIE" "$API_URL/api/auth/me" > "$AUTH_ME"
node -e '
const fs=require("fs"); const p=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (!Array.isArray(p.roles) || !p.roles.some(r => String(r).toLowerCase() === "developer")) process.exit(1);
' "$AUTH_ME" || fail "Preview administrator does not have the developer role"

PERSISTENCE_STATUS="$STATE_DIR/persistence-status.json"
curl --fail --silent --show-error --header "Cookie: $AUTH_COOKIE" "$API_URL/api/engineering/persistence/status" > "$PERSISTENCE_STATUS"
HAS_PROJECTS="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.enabled !== true) throw new Error("Engineering persistence is not enabled");
process.stdout.write(j.hasProjects === true ? "true" : "false");
' "$PERSISTENCE_STATUS")" || fail "Engineering persistence status validation failed"

if [[ "$HAS_PROJECTS" == "false" ]]; then
  printf 'Creating canonical First Project through normal Engineering persistence API...\n'
  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/json' \
    --request POST \
    --data-binary "{\"projectKey\":\"$PROJECT_KEY\",\"projectName\":\"$PROJECT_NAME\"}" \
    "$API_URL/api/engineering/persistence/projects/first" > "$STATE_DIR/first-project.json"
fi

LIFECYCLE_FILE="$STATE_DIR/lifecycle.json"
LIFECYCLE_STATUS="$(
  curl --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --output "$LIFECYCLE_FILE" \
    --write-out '%{http_code}' \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/lifecycle"
)"
[[ "$LIFECYCLE_STATUS" == "200" ]] || fail "canonical project lifecycle returned HTTP $LIFECYCLE_STATUS"

ACTIVE_REVISION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
process.stdout.write(j.activeRevision == null ? "" : String(j.activeRevision));
' "$LIFECYCLE_FILE")"

if [[ -n "$ACTIVE_REVISION" ]]; then
  [[ -f "$FIXTURE_MARKER" ]] || fail "an Active revision already exists without a post-C26 fixture marker; rebuild disposable Preview state"
  [[ "$(cat "$FIXTURE_MARKER")" == "$EXPECTED_PACKAGE_SHA" ]] || fail "Preview state belongs to another frozen package; rebuild disposable Preview state"
else
  printf 'Inspecting and importing the frozen post-C26 package...\n'
  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/vnd.elitescada.project-package' \
    --request POST --data-binary "@$FIXTURE_PACKAGE" \
    "$API_URL/api/project-package/inspect" > "$STATE_DIR/package-inspect.json"
  node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.manifest?.projectKey !== process.argv[2]) throw new Error("package projectKey mismatch");
if (j.manifest?.format !== "elitescada.project-package") throw new Error("unexpected package format");
if (j.engineering?.screens !== 6 || j.engineering?.popups !== 2 || j.engineering?.dynamos !== 9) {
  throw new Error("canonical package entity counts mismatch");
}
' "$STATE_DIR/package-inspect.json" "$PROJECT_KEY" || fail "frozen package inspection did not match canonical expectations"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/vnd.elitescada.project-package' \
    --request POST --data-binary "@$FIXTURE_PACKAGE" \
    "$API_URL/api/project-package/import/preview?mode=CreateAndUpdate" > "$STATE_DIR/import-preview.json"
  node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.canApply !== true || Number(j.errorCount) !== 0) throw new Error("package Import Preview is not clean");
' "$STATE_DIR/import-preview.json" || fail "frozen package Import Preview rejected the package"

  curl --fail --silent --show-error --header "Cookie: $AUTH_COOKIE" "$API_URL/api/engineering/workspace" > "$STATE_DIR/workspace.json"
  WORKSPACE_VERSION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (!Number.isInteger(j.changeVersion) || j.changeVersion < 0) throw new Error("invalid changeVersion");
process.stdout.write(String(j.changeVersion));
' "$STATE_DIR/workspace.json")"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/vnd.elitescada.project-package' \
    --header "x-elitescada-workspace-version: $WORKSPACE_VERSION" \
    --request POST --data-binary "@$FIXTURE_PACKAGE" \
    "$API_URL/api/project-package/import/apply?mode=CreateAndUpdate" > "$STATE_DIR/import-apply.json"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/json' \
    --request POST \
    --data-binary "{\"projectName\":\"$PROJECT_NAME\",\"savedBy\":\"Post-C26 Preview bootstrap\"}" \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/save" > "$STATE_DIR/save.json"
  REVISION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (!Number.isInteger(j.revision) || j.revision < 1) throw new Error("Save did not return a revision");
process.stdout.write(String(j.revision));
' "$STATE_DIR/save.json")"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" --header 'Content-Type: application/json' \
    --request POST --data-binary '{"publishedBy":"Post-C26 Preview bootstrap"}' \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/revisions/$REVISION/publish" > "$STATE_DIR/publish.json"
  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" --header 'Content-Type: application/json' \
    --request POST --data-binary '{"activatedBy":"Post-C26 Preview bootstrap"}' \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/published/activate" > "$STATE_DIR/activate.json"

  ACTIVE_REVISION="$REVISION"
  printf '%s\n' "$EXPECTED_PACKAGE_SHA" > "$FIXTURE_MARKER"
  chmod 600 "$FIXTURE_MARKER"
fi

RUNTIME_FILE="$STATE_DIR/runtime-application.json"
RUNTIME_OK=false
for _ in $(seq 1 40); do
  if curl --fail --silent --show-error --header "Cookie: $AUTH_COOKIE" "$API_URL/api/runtime/application" > "$RUNTIME_FILE" 2>/dev/null; then
    if node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
const builtins=(j.package?.dynamos ?? []).filter(x => x?.metadata?.builtinLibrary === "true");
const app=(j.package?.dynamos ?? []).filter(x => x?.metadata?.builtinLibrary !== "true");
const ok=j.mode==="engineering" && j.projectKey===process.argv[2] &&
  Number(j.revision)===Number(process.argv[3]) &&
  j.package?.startupScreenId===process.argv[4] &&
  Array.isArray(j.package?.screens) && j.package.screens.length===6 &&
  Array.isArray(j.package?.popups) && j.package.popups.length===2 &&
  builtins.length===8 && app.length===1 && app[0]?.key==="eee.dynamo.pump";
process.exit(ok ? 0 : 1);
' "$RUNTIME_FILE" "$PROJECT_KEY" "$ACTIVE_REVISION" "$EXPECTED_STARTUP_SCREEN_ID"; then
      RUNTIME_OK=true
      break
    fi
  fi
  sleep 0.5
done
[[ "$RUNTIME_OK" == true ]] || fail "Active Runtime does not match the frozen post-C26 canonical EEE application"

printf 'Proving canonical EEE simulation is changing live TAG values...\n'
LEVEL_EVIDENCE="$STATE_DIR/dynamic-level.json"
FIRST_LEVEL=""
CHANGED_LEVEL=""
for _ in $(seq 1 30); do
  curl --fail --silent --show-error --header "Cookie: $AUTH_COOKIE" \
    "$API_URL/api/tags/by-path/$LEVEL_PATH" > "$LEVEL_EVIDENCE"
  CURRENT_LEVEL="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.current?.value == null) process.exit(2);
process.stdout.write(String(j.current.value));
' "$LEVEL_EVIDENCE")" || true
  if [[ -n "$CURRENT_LEVEL" ]]; then
    if [[ -z "$FIRST_LEVEL" ]]; then
      FIRST_LEVEL="$CURRENT_LEVEL"
    elif [[ "$CURRENT_LEVEL" != "$FIRST_LEVEL" ]]; then
      CHANGED_LEVEL="$CURRENT_LEVEL"
      break
    fi
  fi
  sleep 1
done
[[ -n "$FIRST_LEVEL" && -n "$CHANGED_LEVEL" ]] || fail "canonical EEE simulation did not produce an observable LevelPct change"

printf 'Proving Historical Query is mounted and backed by TimescaleDB data...\n'
HISTORICAL_FILE="$STATE_DIR/historical-query.json"
HISTORICAL_REQUEST='{"datasetKey":"historian.samples","timeRange":{"kind":"relative","durationSeconds":600,"anchor":"now"},"page":{"limit":25}}'
HISTORICAL_OK=false
for _ in $(seq 1 30); do
  HISTORICAL_STATUS="$(
    curl --silent --show-error \
      --header "Cookie: $AUTH_COOKIE" \
      --header 'Content-Type: application/json' \
      --request POST --data-binary "$HISTORICAL_REQUEST" \
      --output "$HISTORICAL_FILE" --write-out '%{http_code}' \
      "$API_URL/api/historical/query"
  )"
  if [[ "$HISTORICAL_STATUS" == "404" ]]; then
    fail "Historical Query route is not mounted"
  fi
  if [[ "$HISTORICAL_STATUS" == "200" ]] && node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
const ok=j.datasetKey==="historian.samples" && Array.isArray(j.rows) && j.rows.length>0;
process.exit(ok ? 0 : 1);
' "$HISTORICAL_FILE"; then
    HISTORICAL_OK=true
    break
  fi
  sleep 1
done
[[ "$HISTORICAL_OK" == true ]] || fail "Historical Query did not return valid historian.samples rows"

LICENSE_FILE="$STATE_DIR/licensing.json"
curl --fail --silent --show-error --header "Cookie: $AUTH_COOKIE" "$API_URL/api/licensing/status" > "$LICENSE_FILE"
LICENSE_STATE="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
process.stdout.write(String(j.license?.state ?? "unknown"));
' "$LICENSE_FILE")"
[[ "$LICENSE_STATE" == "Demo" ]] || fail "Preview expected official Demo licensing state but received '$LICENSE_STATE'"

ACCESS_TOKEN=''
AUTH_COOKIE=''
PREVIEW_ADMIN_PASSWORD=''
JWT_SIGNING_KEY=''
HISTORICAL_CURSOR_KEY=''
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
unset HistoricalQuery__Enabled
unset HistoricalQuery__CursorKeyBase64
unset EngineeringRuntime__ProjectKey
unset ASPNETCORE_URLS
unset ASPNETCORE_ENVIRONMENT

printf 'Starting EliteSCADA Web with the API proxy kept inside the app container...\n'
export SCADA_API_PROXY="$API_URL"
(
  cd "$ROOT/web/scada-web"
  exec npm run dev -- --host 0.0.0.0 --port 5173
) >> "$WEB_LOG" 2>&1 &
echo "$!" > "$WEB_PID_FILE"
wait_for_process_url "$WEB_PID_FILE" "$WEB_URL" "$WEB_LOG" 'EliteSCADA Web' || fail "Web startup failed"

printf '\nEliteSCADA post-C26 Preview is ready.\n'
printf '  Web: %s (Codespaces forwards port 5173 privately by default)\n' "$WEB_URL"
printf '  Login: EliteSCADA\n'
printf '  Runtime project: %s / Active revision %s\n' "$PROJECT_KEY" "$ACTIVE_REVISION"
printf '  Frozen package SHA-256: %s\n' "$ACTUAL_SHA"
printf '  Historical Query: ENABLED / historian.samples verified\n'
printf '  Simulation: RUNNING / %s changed %s -> %s\n' "$LEVEL_PATH" "$FIRST_LEVEL" "$CHANGED_LEVEL"
printf '  Licensing: Demo\n'
printf 'Use the forwarded “EliteSCADA Web — Post-C26 Preview” port in the Codespaces Ports panel.\n'
