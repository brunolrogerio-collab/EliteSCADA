#!/usr/bin/env bash
set -Eeuo pipefail

workspace=/workspace
runtime_state="${PREVIEW_RUNTIME_STATE_DIR:-/preview-state}"
evidence="${PREVIEW_EVIDENCE_DIR:-/preview-evidence}"
logs="$evidence/logs"

mkdir -p "$runtime_state" "$logs"
chmod 700 "$runtime_state"

create_secret() {
  local path="$1"
  if [[ ! -s "$path" ]]; then
    local temporary="${path}.tmp.$$"
    openssl rand -base64 48 | tr -d '\n' > "$temporary"
    chmod 600 "$temporary"
    mv "$temporary" "$path"
  fi
}

create_secret "$runtime_state/jwt-signing-key"
create_secret "$runtime_state/historical-query-cursor-key"
export Authentication__Jwt__SigningKey="$(<"$runtime_state/jwt-signing-key")"
export HistoricalQuery__CursorKeyBase64="$(<"$runtime_state/historical-query-cursor-key")"

api_pid=''
web_pid=''
cleanup() {
  [[ -z "$api_pid" ]] || kill "$api_pid" 2>/dev/null || true
  [[ -z "$web_pid" ]] || kill "$web_pid" 2>/dev/null || true
  [[ -z "$api_pid" ]] || wait "$api_pid" 2>/dev/null || true
  [[ -z "$web_pid" ]] || wait "$web_pid" 2>/dev/null || true
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

cd "$workspace"
if [[ "${PREVIEW_USE_PREPARED_DEPENDENCIES:-0}" == "1" ]]; then
  expected_key="${PREVIEW_DEPENDENCY_KEY:-}"
  if [[ ! "$expected_key" =~ ^[0-9a-f]{64}$ ]]; then
    echo 'ENVIRONMENT_PREP_PROVENANCE_MISMATCH: prepared dependency key is missing or invalid.' >&2
    exit 78
  fi
  verify_dependency_marker() {
    local marker="$1"
    if [[ ! -s "$marker" || "$(<"$marker")" != "$expected_key" ]]; then
      echo "ENVIRONMENT_PREP_PROVENANCE_MISMATCH: prepared dependency marker does not match $expected_key: $marker" >&2
      exit 78
    fi
  }
  verify_dependency_marker web/scada-web/node_modules/.preview-dependency-key
  verify_dependency_marker "${NUGET_PACKAGES:-/root/.nuget/packages}/.preview-dependency-key"
  verify_dependency_marker src/Scada.Api/obj/.preview-dependency-key
  if [[ ! -x web/scada-web/node_modules/.bin/vite || ! -s src/Scada.Api/obj/project.assets.json ]]; then
    echo 'ENVIRONMENT_PREP_REQUIRED: prepared Node or NuGet assets are absent; run local-audit.ps1 prepare.' >&2
    exit 78
  fi
else
  if [[ ! -x web/scada-web/node_modules/.bin/vite ]]; then
    npm --prefix web/scada-web ci --no-audit --no-fund
  fi
  dotnet restore src/Scada.Api/Scada.Api.csproj
fi

dotnet build src/Scada.Api/Scada.Api.csproj --no-restore --configuration Release

dotnet run --project src/Scada.Api/Scada.Api.csproj --no-build --no-restore --configuration Release \
  > >(tee -a "$logs/api.log") 2>&1 &
api_pid=$!

npm --prefix web/scada-web run dev -- --host 0.0.0.0 \
  > >(tee -a "$logs/web.log") 2>&1 &
web_pid=$!

echo "EliteSCADA preview services started; health is served through Web port 5173."
set +e
wait -n "$api_pid" "$web_pid"
service_status=$?
set -e
echo "A preview service exited with status $service_status; stopping its sibling."
exit "$service_status"
