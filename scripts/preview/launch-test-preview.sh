#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_DIR="$ROOT/.preview"
API_URL="http://127.0.0.1:5080"
WEB_URL="http://127.0.0.1:5173"
PROJECT_KEY="eee-demo"
PROJECT_NAME="EliteSCADA — EEE Demo"
FIXTURE_PACKAGE="$ROOT/preview/fixtures/EliteSCADA-EEE-Demo.escadapkg"
FIXTURE_CHECKSUM="$ROOT/preview/fixtures/EliteSCADA-EEE-Demo.escadapkg.sha256"
FIXTURE_PROVENANCE="$ROOT/preview/fixtures/EliteSCADA-EEE-Demo.provenance.json"
EXPECTED_STARTUP_SCREEN_ID="c1170000-0000-4000-8000-000000000001"
EXPECTED_SOURCE_PRODUCT_SHA="ff185ffd67fe4abc597af9184c21f86376ba6e17"
API_PID_FILE="$STATE_DIR/api.pid"
WEB_PID_FILE="$STATE_DIR/web.pid"
API_LOG="$STATE_DIR/api.log"
WEB_LOG="$STATE_DIR/web.log"
LOGIN_HEADERS="$STATE_DIR/login.headers"
LOGIN_BODY="$STATE_DIR/login.json"

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

fail() {
  printf 'C11 Test Preview failed: %s\n' "$*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "required command '$1' is unavailable in the devcontainer"
}

for command_name in dotnet npm node curl sha256sum od; do
  require_command "$command_name"
done

[[ -f "$FIXTURE_PACKAGE" ]] || fail "frozen C11 package is missing"
[[ -f "$FIXTURE_CHECKSUM" ]] || fail "frozen C11 package checksum is missing"
[[ -f "$FIXTURE_PROVENANCE" ]] || fail "frozen C11 package provenance is missing"

EXPECTED_SHA="$(awk 'NF {print $1; exit}' "$FIXTURE_CHECKSUM")"
ACTUAL_SHA="$(sha256sum "$FIXTURE_PACKAGE" | awk '{print $1}')"
PROVENANCE_SHA="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.packageSha256 ?? ""))' "$FIXTURE_PROVENANCE")"
PROVENANCE_PROJECT_KEY="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.projectKey ?? ""))' "$FIXTURE_PROVENANCE")"
PROVENANCE_PACKAGE_NAME="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.packageFileName ?? ""))' "$FIXTURE_PROVENANCE")"
PROVENANCE_SOURCE_SHA="$(node -e 'const p=require(process.argv[1]); process.stdout.write(String(p.sourceProductSha ?? ""))' "$FIXTURE_PROVENANCE")"

[[ "$EXPECTED_SHA" =~ ^[0-9a-f]{64}$ ]] || fail "frozen checksum file is invalid"
[[ "$ACTUAL_SHA" == "$EXPECTED_SHA" ]] || fail "frozen C11 package SHA-256 mismatch"
[[ "$PROVENANCE_SHA" == "$EXPECTED_SHA" ]] || fail "provenance SHA-256 does not match frozen package"
[[ "$PROVENANCE_PROJECT_KEY" == "$PROJECT_KEY" ]] || fail "provenance project key does not match C11 canonical project"
[[ "$PROVENANCE_PACKAGE_NAME" == "$(basename "$FIXTURE_PACKAGE")" ]] || fail "provenance package file name does not match fixture"
[[ "$PROVENANCE_SOURCE_SHA" == "$EXPECTED_SOURCE_PRODUCT_SHA" ]] || fail "provenance source product SHA is not the accepted post-C25 integration product"
printf 'Validated frozen C11 package: %s\n' "$ACTUAL_SHA"

if [[ -z "${ELITESCADA_PREVIEW_ADMIN_PASSWORD:-}" ]]; then
  fail "ELITESCADA_PREVIEW_ADMIN_PASSWORD is not set. Configure it as a GitHub Codespaces secret and rebuild/restart the Codespace."
fi

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
export Authentication__Local__Bootstrap__DisplayName='EliteSCADA C11 Test Preview'
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

if [[ "$LOGIN_STATUS" != "200" ]]; then
  fail "administrative login returned HTTP $LOGIN_STATUS. If this Codespace previously used a different Preview password, rebuild/reset its disposable database."
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

PERSISTENCE_STATUS="$STATE_DIR/persistence-status.json"
curl --fail --silent --show-error \
  --header "Cookie: $AUTH_COOKIE" \
  "$API_URL/api/engineering/persistence/status" > "$PERSISTENCE_STATUS"
HAS_PROJECTS="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.enabled !== true) throw new Error("Engineering persistence is not enabled");
process.stdout.write(j.hasProjects === true ? "true" : "false");
' "$PERSISTENCE_STATUS")" || fail "Engineering persistence status validation failed"

if [[ "$HAS_PROJECTS" == "false" ]]; then
  printf 'Creating canonical C11 First Project through normal Engineering persistence API...\n'
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
[[ "$LIFECYCLE_STATUS" == "200" ]] || fail "canonical project lifecycle returned HTTP $LIFECYCLE_STATUS; rebuild disposable Preview state instead of mutating product authority"

ACTIVE_REVISION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
process.stdout.write(j.activeRevision == null ? "" : String(j.activeRevision));
' "$LIFECYCLE_FILE")"

if [[ -z "$ACTIVE_REVISION" ]]; then
  printf 'Inspecting and previewing the frozen C11 package...\n'
  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/vnd.elitescada.project-package' \
    --request POST \
    --data-binary "@$FIXTURE_PACKAGE" \
    "$API_URL/api/project-package/inspect" > "$STATE_DIR/package-inspect.json"

  node -e '
const fs=require("fs");
const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.manifest?.projectKey !== process.argv[2]) throw new Error("package projectKey mismatch");
if (j.manifest?.format !== "elitescada.project-package") throw new Error("unexpected package format");
if (j.engineering?.screens !== 6) throw new Error("canonical package must contain six screens");
if (j.engineering?.popups !== 2) throw new Error("canonical package must contain two popups");
if (j.engineering?.dynamos !== 9) throw new Error("canonical package must contain eight built-ins plus one application Dynamo");
' "$STATE_DIR/package-inspect.json" "$PROJECT_KEY" || fail "frozen C11 package inspection did not match canonical expectations"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/vnd.elitescada.project-package' \
    --request POST \
    --data-binary "@$FIXTURE_PACKAGE" \
    "$API_URL/api/project-package/import/preview?mode=CreateAndUpdate" > "$STATE_DIR/import-preview.json"

  node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.canApply !== true || Number(j.errorCount) !== 0) {
  throw new Error("package Import Preview is not clean");
}
' "$STATE_DIR/import-preview.json" || fail "frozen C11 package Import Preview rejected the package"

  WORKSPACE_FILE="$STATE_DIR/workspace.json"
  curl --fail --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    "$API_URL/api/engineering/workspace" > "$WORKSPACE_FILE"
  WORKSPACE_VERSION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (!Number.isInteger(j.changeVersion) || j.changeVersion < 0) throw new Error("invalid changeVersion");
process.stdout.write(String(j.changeVersion));
' "$WORKSPACE_FILE")"

  printf 'Applying frozen package through Engineering Modify authority...\n'
  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/vnd.elitescada.project-package' \
    --header "x-elitescada-workspace-version: $WORKSPACE_VERSION" \
    --request POST \
    --data-binary "@$FIXTURE_PACKAGE" \
    "$API_URL/api/project-package/import/apply?mode=CreateAndUpdate" > "$STATE_DIR/import-apply.json"

  SAVE_FILE="$STATE_DIR/save.json"
  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/json' \
    --request POST \
    --data-binary "{\"projectName\":\"$PROJECT_NAME\",\"savedBy\":\"C11 Test Preview bootstrap\"}" \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/save" > "$SAVE_FILE"
  REVISION="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (!Number.isInteger(j.revision) || j.revision < 1) throw new Error("Save did not return a revision");
process.stdout.write(String(j.revision));
' "$SAVE_FILE")"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/json' \
    --request POST \
    --data-binary '{"publishedBy":"C11 Test Preview bootstrap"}' \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/revisions/$REVISION/publish" > "$STATE_DIR/publish.json"

  curl --fail-with-body --silent --show-error \
    --header "Cookie: $AUTH_COOKIE" \
    --header 'Content-Type: application/json' \
    --request POST \
    --data-binary '{"activatedBy":"C11 Test Preview bootstrap"}' \
    "$API_URL/api/engineering/persistence/$PROJECT_KEY/published/activate" > "$STATE_DIR/activate.json"

  ACTIVE_REVISION="$REVISION"
  printf 'Activated persisted C11 canonical revision %s.\n' "$ACTIVE_REVISION"
else
  printf 'Persisted Active revision %s already exists; preserving current Preview state.\n' "$ACTIVE_REVISION"
fi

RUNTIME_FILE="$STATE_DIR/runtime-application.json"
for _ in $(seq 1 30); do
  if curl --fail --silent --show-error \
      --header "Cookie: $AUTH_COOKIE" \
      "$API_URL/api/runtime/application" > "$RUNTIME_FILE" 2>/dev/null; then
    if node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
const ok =
  j.mode === "engineering" &&
  j.projectKey === process.argv[2] &&
  Number(j.revision) === Number(process.argv[3]) &&
  j.package?.startupScreenId === process.argv[4] &&
  Array.isArray(j.package?.screens) && j.package.screens.length === 6 &&
  Array.isArray(j.package?.popups) && j.package.popups.length === 2 &&
  Array.isArray(j.package?.dynamos) &&
  j.package.dynamos.filter(x => x?.metadata?.builtinLibrary === "true").length === 8 &&
  j.package.dynamos.filter(x => x?.metadata?.builtinLibrary !== "true").map(x => x?.key).includes("eee.dynamo.pump");
if (!ok) process.exit(1);
' "$RUNTIME_FILE" "$PROJECT_KEY" "$ACTIVE_REVISION" "$EXPECTED_STARTUP_SCREEN_ID"; then
      break
    fi
  fi
  sleep 0.5
done

node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
if (j.mode !== "engineering") throw new Error("Runtime mode is not engineering");
if (j.projectKey !== process.argv[2]) throw new Error("Runtime projectKey mismatch");
if (Number(j.revision) !== Number(process.argv[3])) throw new Error("Runtime revision mismatch");
if (j.package?.startupScreenId !== process.argv[4]) throw new Error("Runtime startup screen mismatch");
if (!Array.isArray(j.package?.screens) || j.package.screens.length !== 6) throw new Error("Runtime screen count mismatch");
if (!Array.isArray(j.package?.popups) || j.package.popups.length !== 2) throw new Error("Runtime popup count mismatch");
const builtins=(j.package?.dynamos ?? []).filter(x => x?.metadata?.builtinLibrary === "true");
const app=(j.package?.dynamos ?? []).filter(x => x?.metadata?.builtinLibrary !== "true");
if (builtins.length !== 8) throw new Error("Runtime built-in Dynamo count mismatch");
if (app.length !== 1 || app[0]?.key !== "eee.dynamo.pump") throw new Error("Runtime application Dynamo mismatch");
' "$RUNTIME_FILE" "$PROJECT_KEY" "$ACTIVE_REVISION" "$EXPECTED_STARTUP_SCREEN_ID" \
  || fail "Active Runtime does not match the frozen C11 canonical EEE application"

LICENSE_FILE="$STATE_DIR/licensing.json"
curl --fail --silent --show-error \
  --header "Cookie: $AUTH_COOKIE" \
  "$API_URL/api/licensing/status" > "$LICENSE_FILE"
LICENSE_STATE="$(node -e '
const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));
process.stdout.write(String(j.license?.state ?? "unknown"));
' "$LICENSE_FILE")"
[[ "$LICENSE_STATE" == "Demo" ]] || fail "Preview expected official Demo licensing state but received '$LICENSE_STATE'"

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

printf '\nEliteSCADA C11 canonical Test Preview is ready.\n'
printf '  Web: %s (Codespaces forwards port 5173 privately by default)\n' "$WEB_URL"
printf '  Login: EliteSCADA\n'
printf '  Runtime project: %s / Active revision %s\n' "$PROJECT_KEY" "$ACTIVE_REVISION"
printf '  Frozen package SHA-256: %s\n' "$ACTUAL_SHA"
printf '  Licensing: Demo\n'
printf 'Use the forwarded “EliteSCADA Web — C11 Test Preview” port in the Codespaces Ports panel.\n'
