#!/usr/bin/env bash
set -euo pipefail

# DIAGNOSTIC ONLY — MUST NEVER MERGE.
# Passive long-run probe for RECHECK-SIM-PUMP-LEVEL. Product source is not modified.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_DIR="$ROOT/.preview"
DIAG_DIR="$STATE_DIR/diagnostics/post-c26-long-run"
SAMPLE_DIR="$DIAG_DIR/.sample"
API_URL="http://127.0.0.1:5080"
WEB_URL="http://127.0.0.1:5173"
LEVEL_PATH="EEE.Process.LevelPct"
DURATION_SECONDS="${ELITESCADA_DIAGNOSTIC_DURATION_SECONDS:-1200}"
INTERVAL_SECONDS="${ELITESCADA_DIAGNOSTIC_INTERVAL_SECONDS:-5}"
STALE_THRESHOLD_SECONDS="${ELITESCADA_DIAGNOSTIC_STALE_THRESHOLD_SECONDS:-90}"
JSONL="$DIAG_DIR/samples.jsonl"
SUMMARY="$DIAG_DIR/summary.json"
DIRECT_WS_STATUS="$DIAG_DIR/ws-direct.json"
PROXY_WS_STATUS="$DIAG_DIR/ws-proxy.json"
WS_PROBE="$ROOT/scripts/preview/ws-byte-probe.mjs"
API_LOG="$STATE_DIR/api.log"
WEB_LOG="$STATE_DIR/web.log"
DIRECT_WS_PID=""
PROXY_WS_PID=""
LOGIN_HEADERS=""
LOGIN_BODY=""
AUTH_COOKIE=""

fail() {
  printf 'Post-C26 long-run diagnostic failed: %s\n' "$*" >&2
  exit 1
}

for numeric in "$DURATION_SECONDS" "$INTERVAL_SECONDS" "$STALE_THRESHOLD_SECONDS"; do
  [[ "$numeric" =~ ^[0-9]+$ ]] || fail "diagnostic timing values must be positive integers"
  (( numeric > 0 )) || fail "diagnostic timing values must be greater than zero"
done
(( DURATION_SECONDS > 900 )) || fail "duration must exceed 15 minutes to cover the reported late-freeze window"

for command_name in bash curl node git ps stat tail grep; do
  command -v "$command_name" >/dev/null 2>&1 || fail "required command '$command_name' is unavailable"
done
[[ -f "$WS_PROBE" ]] || fail "WebSocket probe helper is missing"

rm -rf "$DIAG_DIR"
mkdir -p "$SAMPLE_DIR"
chmod 700 "$DIAG_DIR" "$SAMPLE_DIR"
: > "$JSONL"
chmod 600 "$JSONL"

cleanup() {
  set +e
  if [[ -n "$DIRECT_WS_PID" ]]; then kill "$DIRECT_WS_PID" 2>/dev/null || true; wait "$DIRECT_WS_PID" 2>/dev/null || true; fi
  if [[ -n "$PROXY_WS_PID" ]]; then kill "$PROXY_WS_PID" 2>/dev/null || true; wait "$PROXY_WS_PID" 2>/dev/null || true; fi
  [[ -n "$LOGIN_HEADERS" ]] && rm -f "$LOGIN_HEADERS"
  [[ -n "$LOGIN_BODY" ]] && rm -f "$LOGIN_BODY"
  rm -rf "$SAMPLE_DIR"
  AUTH_COOKIE=''
  unset ELITESCADA_DIAG_AUTH_COOKIE 2>/dev/null || true
}
trap cleanup EXIT

# CI has no Codespaces secret. Generate an ephemeral local password only when one was not supplied.
if [[ -z "${ELITESCADA_PREVIEW_ADMIN_PASSWORD:-}" ]]; then
  ELITESCADA_PREVIEW_ADMIN_PASSWORD="$(node -e 'process.stdout.write(require("node:crypto").randomBytes(36).toString("base64url"))')"
  export ELITESCADA_PREVIEW_ADMIN_PASSWORD
fi
PREVIEW_ADMIN_PASSWORD="$ELITESCADA_PREVIEW_ADMIN_PASSWORD"

printf 'Launching/validating the exact post-C26 Preview through its canonical ensure path...\n'
bash "$ROOT/scripts/preview/ensure-post-c26-preview.sh"

LOGIN_HEADERS="$(mktemp)"
LOGIN_BODY="$(mktemp)"
chmod 600 "$LOGIN_HEADERS" "$LOGIN_BODY"
LOGIN_STATUS="$(
  printf '%s' "$PREVIEW_ADMIN_PASSWORD" \
    | node -e '
let password="";
process.stdin.setEncoding("utf8");
process.stdin.on("data", chunk => password += chunk);
process.stdin.on("end", () => process.stdout.write(JSON.stringify({username:"EliteSCADA", password})));
' \
    | curl --silent --show-error --max-time 10 \
        --dump-header "$LOGIN_HEADERS" \
        --output "$LOGIN_BODY" \
        --write-out '%{http_code}' \
        --header 'Content-Type: application/json' \
        --request POST \
        --data-binary @- \
        "$API_URL/api/auth/login"
)"
[[ "$LOGIN_STATUS" == "200" ]] || fail "diagnostic login returned HTTP $LOGIN_STATUS"

ACCESS_TOKEN="$(node -e '
const fs=require("fs");
const lines=fs.readFileSync(process.argv[1],"utf8").split(/\r?\n/);
const line=lines.find(x => /^set-cookie:\s*elitescada_access=/i.test(x));
const match=line?.match(/elitescada_access=([^;]+)/i);
if (!match) process.exit(2);
process.stdout.write(match[1]);
' "$LOGIN_HEADERS")" || fail "diagnostic login did not issue the access cookie"
AUTH_COOKIE="elitescada_access=$ACCESS_TOKEN"
ACCESS_TOKEN=''
rm -f "$LOGIN_HEADERS" "$LOGIN_BODY"
LOGIN_HEADERS=''
LOGIN_BODY=''
PREVIEW_ADMIN_PASSWORD=''
unset ELITESCADA_PREVIEW_ADMIN_PASSWORD

capture_auth() {
  local url="$1"
  local output="$2"
  local status
  status="$(curl --silent --max-time 4 --header "Cookie: $AUTH_COOKIE" --output "$output" --write-out '%{http_code}' "$url" 2>/dev/null || true)"
  [[ "$status" =~ ^[0-9]{3}$ ]] || status="000"
  printf '%s' "$status"
}

capture_public() {
  local url="$1"
  local output="$2"
  local status
  status="$(curl --silent --max-time 4 --output "$output" --write-out '%{http_code}' "$url" 2>/dev/null || true)"
  [[ "$status" =~ ^[0-9]{3}$ ]] || status="000"
  printf '%s' "$status"
}

printf 'Starting passive realtime probes on API:5080 and Web proxy:5173...\n'
ELITESCADA_DIAG_AUTH_COOKIE="$AUTH_COOKIE" node "$WS_PROBE" 5080 "$DIRECT_WS_STATUS" direct-api >/dev/null 2>&1 &
DIRECT_WS_PID="$!"
ELITESCADA_DIAG_AUTH_COOKIE="$AUTH_COOKIE" node "$WS_PROBE" 5173 "$PROXY_WS_STATUS" web-proxy >/dev/null 2>&1 &
PROXY_WS_PID="$!"

START_EPOCH="$(date +%s)"
SAMPLE_INDEX=0
printf 'Collecting correlated evidence for %ss at %ss intervals (stale threshold %ss)...\n' \
  "$DURATION_SECONDS" "$INTERVAL_SECONDS" "$STALE_THRESHOLD_SECONDS"

while true; do
  NOW_EPOCH="$(date +%s)"
  ELAPSED=$(( NOW_EPOCH - START_EPOCH ))
  (( ELAPSED <= DURATION_SECONDS )) || break
  SAMPLE_INDEX=$(( SAMPLE_INDEX + 1 ))
  TS="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"

  HEALTH_BODY="$SAMPLE_DIR/health.json"
  DIRECT_BODY="$SAMPLE_DIR/tag-direct.json"
  PROXY_BODY="$SAMPLE_DIR/tag-proxy.json"
  RUNTIME_DIAG_BODY="$SAMPLE_DIR/runtime-diagnostics.json"
  APP_BODY="$SAMPLE_DIR/runtime-application.json"
  WEB_BODY="$SAMPLE_DIR/web.txt"

  HEALTH_STATUS="$(capture_public "$API_URL/health" "$HEALTH_BODY")"
  DIRECT_STATUS="$(capture_auth "$API_URL/api/tags/by-path/$LEVEL_PATH" "$DIRECT_BODY")"
  PROXY_STATUS="$(capture_auth "$WEB_URL/api/tags/by-path/$LEVEL_PATH" "$PROXY_BODY")"
  RUNTIME_DIAG_STATUS="$(capture_auth "$API_URL/api/diagnostics/runtime" "$RUNTIME_DIAG_BODY")"
  APP_STATUS="$(capture_auth "$API_URL/api/runtime/application" "$APP_BODY")"
  WEB_STATUS="$(capture_public "$WEB_URL/" "$WEB_BODY")"

  API_PID="$(cat "$STATE_DIR/api.pid" 2>/dev/null || true)"
  WEB_PID="$(cat "$STATE_DIR/web.pid" 2>/dev/null || true)"
  API_ALIVE=false
  WEB_ALIVE=false
  [[ -n "$API_PID" ]] && kill -0 "$API_PID" 2>/dev/null && API_ALIVE=true
  [[ -n "$WEB_PID" ]] && kill -0 "$WEB_PID" 2>/dev/null && WEB_ALIVE=true
  API_RSS_KB="$(ps -o rss= -p "$API_PID" 2>/dev/null | tr -d ' ' || true)"
  WEB_RSS_KB="$(ps -o rss= -p "$WEB_PID" 2>/dev/null | tr -d ' ' || true)"
  API_LOG_BYTES="$(stat -c '%s' "$API_LOG" 2>/dev/null || printf '0')"
  WEB_LOG_BYTES="$(stat -c '%s' "$WEB_LOG" 2>/dev/null || printf '0')"

  TS="$TS" ELAPSED="$ELAPSED" SAMPLE_INDEX="$SAMPLE_INDEX" \
  HEALTH_STATUS="$HEALTH_STATUS" DIRECT_STATUS="$DIRECT_STATUS" PROXY_STATUS="$PROXY_STATUS" \
  RUNTIME_DIAG_STATUS="$RUNTIME_DIAG_STATUS" APP_STATUS="$APP_STATUS" WEB_STATUS="$WEB_STATUS" \
  API_ALIVE="$API_ALIVE" WEB_ALIVE="$WEB_ALIVE" API_RSS_KB="$API_RSS_KB" WEB_RSS_KB="$WEB_RSS_KB" \
  API_LOG_BYTES="$API_LOG_BYTES" WEB_LOG_BYTES="$WEB_LOG_BYTES" \
  node - "$DIRECT_BODY" "$PROXY_BODY" "$RUNTIME_DIAG_BODY" "$APP_BODY" "$DIRECT_WS_STATUS" "$PROXY_WS_STATUS" "$JSONL" <<'NODE'
const fs = require('node:fs');
const [directPath, proxyPath, diagnosticsPath, appPath, directWsPath, proxyWsPath, outputPath] = process.argv.slice(2);
const parse = (file) => {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch { return null; }
};
const numberOrNull = (value) => value == null || value === '' || Number.isNaN(Number(value)) ? null : Number(value);
const direct = parse(directPath);
const proxy = parse(proxyPath);
const diagnostics = parse(diagnosticsPath);
const app = parse(appPath);
const directWs = parse(directWsPath);
const proxyWs = parse(proxyWsPath);
const record = {
  sample: Number(process.env.SAMPLE_INDEX),
  timestampUtc: process.env.TS,
  elapsedSeconds: Number(process.env.ELAPSED),
  http: {
    health: Number(process.env.HEALTH_STATUS),
    directTag: Number(process.env.DIRECT_STATUS),
    proxyTag: Number(process.env.PROXY_STATUS),
    runtimeDiagnostics: Number(process.env.RUNTIME_DIAG_STATUS),
    runtimeApplication: Number(process.env.APP_STATUS),
    webRoot: Number(process.env.WEB_STATUS)
  },
  process: {
    apiAlive: process.env.API_ALIVE === 'true',
    webAlive: process.env.WEB_ALIVE === 'true',
    apiRssKb: numberOrNull(process.env.API_RSS_KB),
    webRssKb: numberOrNull(process.env.WEB_RSS_KB),
    apiLogBytes: numberOrNull(process.env.API_LOG_BYTES),
    webLogBytes: numberOrNull(process.env.WEB_LOG_BYTES)
  },
  directTag: direct?.current ? {
    value: direct.current.value ?? null,
    quality: direct.current.quality ?? null,
    timestamp: direct.current.timestamp ?? null,
    source: direct.current.source ?? null,
    tagId: direct.tag?.id ?? null
  } : null,
  proxyTag: proxy?.current ? {
    value: proxy.current.value ?? null,
    quality: proxy.current.quality ?? null,
    timestamp: proxy.current.timestamp ?? null,
    source: proxy.current.source ?? null,
    tagId: proxy.tag?.id ?? null
  } : null,
  runtime: diagnostics?.runtime ? {
    mode: diagnostics.runtime.mode ?? null,
    projectKey: diagnostics.runtime.projectKey ?? null,
    revision: diagnostics.runtime.revision ?? null,
    activatedAtUtc: diagnostics.runtime.activatedAtUtc ?? null,
    activeAlarmCount: diagnostics.runtime.activeAlarmCount ?? null
  } : null,
  historian: diagnostics?.historian ? {
    provider: diagnostics.historian.provider ?? null,
    writtenSamples: diagnostics.historian.writtenSamples ?? null,
    pendingSamples: diagnostics.historian.pendingSamples ?? null
  } : null,
  application: app ? {
    mode: app.mode ?? null,
    projectKey: app.projectKey ?? null,
    revision: app.revision ?? null,
    activatedAtUtc: app.activatedAtUtc ?? null
  } : null,
  websocket: {
    direct: directWs,
    proxy: proxyWs
  }
};
fs.appendFileSync(outputPath, `${JSON.stringify(record)}\n`);
NODE

  if (( SAMPLE_INDEX == 1 || SAMPLE_INDEX % 12 == 0 )); then
    printf '  sample=%d elapsed=%ss health=%s tag=%s proxy=%s runtime=%s web=%s\n' \
      "$SAMPLE_INDEX" "$ELAPSED" "$HEALTH_STATUS" "$DIRECT_STATUS" "$PROXY_STATUS" "$RUNTIME_DIAG_STATUS" "$WEB_STATUS"
  fi
  sleep "$INTERVAL_SECONDS"
done

kill "$DIRECT_WS_PID" 2>/dev/null || true
kill "$PROXY_WS_PID" 2>/dev/null || true
wait "$DIRECT_WS_PID" 2>/dev/null || true
wait "$PROXY_WS_PID" 2>/dev/null || true
DIRECT_WS_PID=''
PROXY_WS_PID=''

# Preserve bounded, secret-free process evidence.
tail -n 300 "$API_LOG" > "$DIAG_DIR/api-tail.log" 2>/dev/null || true
tail -n 300 "$WEB_LOG" > "$DIAG_DIR/web-tail.log" 2>/dev/null || true
grep -Ei 'server script|python|script.*(fail|error|timeout)|exception|fatal|unhandled' "$API_LOG" \
  | tail -n 300 > "$DIAG_DIR/api-script-error-tail.log" 2>/dev/null || true
chmod 600 "$DIAG_DIR"/*.log 2>/dev/null || true

STALE_THRESHOLD_SECONDS="$STALE_THRESHOLD_SECONDS" node - "$JSONL" "$DIRECT_WS_STATUS" "$PROXY_WS_STATUS" "$SUMMARY" <<'NODE'
const fs = require('node:fs');
const [jsonlPath, directWsPath, proxyWsPath, summaryPath] = process.argv.slice(2);
const threshold = Number(process.env.STALE_THRESHOLD_SECONDS);
const records = fs.readFileSync(jsonlPath, 'utf8').trim().split(/\n/).filter(Boolean).map(JSON.parse);
if (records.length < 2) throw new Error('insufficient diagnostic samples');
const readJson = (p) => { try { return JSON.parse(fs.readFileSync(p, 'utf8')); } catch { return null; } };
const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);
function changeStats(selector) {
  const points = records.filter(r => selector(r) !== null && selector(r) !== undefined).map(r => ({ elapsed: r.elapsedSeconds, value: selector(r) }));
  let changes = 0;
  let lastChangeElapsed = points[0]?.elapsed ?? null;
  let previous = points[0]?.value;
  let maxUnchangedSeconds = 0;
  let unchangedSince = points[0]?.elapsed ?? null;
  for (const point of points.slice(1)) {
    if (!eq(point.value, previous)) {
      changes += 1;
      lastChangeElapsed = point.elapsed;
      if (unchangedSince != null) maxUnchangedSeconds = Math.max(maxUnchangedSeconds, point.elapsed - unchangedSince);
      unchangedSince = point.elapsed;
      previous = point.value;
    }
  }
  const end = records.at(-1).elapsedSeconds;
  if (unchangedSince != null) maxUnchangedSeconds = Math.max(maxUnchangedSeconds, end - unchangedSince);
  return {
    observations: points.length,
    changes,
    firstValue: points[0]?.value ?? null,
    lastValue: points.at(-1)?.value ?? null,
    lastChangeElapsed,
    staleAtEndSeconds: lastChangeElapsed == null ? null : end - lastChangeElapsed,
    maxUnchangedSeconds
  };
}
const direct = changeStats(r => r.http.directTag === 200 ? r.directTag?.value : null);
const proxy = changeStats(r => r.http.proxyTag === 200 ? r.proxyTag?.value : null);
const written = changeStats(r => r.http.runtimeDiagnostics === 200 ? r.historian?.writtenSamples : null);
const endElapsed = records.at(-1).elapsedSeconds;
const finalWindow = records.slice(-3);
const apiFinalBad = finalWindow.some(r => r.http.health !== 200 || !r.process.apiAlive);
const webFinalBad = finalWindow.some(r => r.http.webRoot !== 200 || !r.process.webAlive);
const directFresh = direct.changes > 0 && direct.staleAtEndSeconds != null && direct.staleAtEndSeconds < threshold;
const proxyFresh = proxy.changes > 0 && proxy.staleAtEndSeconds != null && proxy.staleAtEndSeconds < threshold;
const wsDirect = readJson(directWsPath);
const wsProxy = readJson(proxyWsPath);
const wsAge = (ws) => ws?.lastDataAtUtc ? Math.max(0, (Date.now() - Date.parse(ws.lastDataAtUtc)) / 1000) : null;
const wsDirectAge = wsAge(wsDirect);
const wsProxyAge = wsAge(wsProxy);
const wsDirectFresh = (wsDirect?.bytesReceived ?? 0) > 0 && wsDirectAge != null && wsDirectAge < threshold;
const wsProxyFresh = (wsProxy?.bytesReceived ?? 0) > 0 && wsProxyAge != null && wsProxyAge < threshold;
let classification = 'NO_FREEZE_OBSERVED';
if (apiFinalBad) classification = 'API_PROCESS_OR_HTTP_STALE';
else if (!directFresh) classification = 'DIRECT_TAG_PIPELINE_STALE';
else if (webFinalBad || !proxyFresh) classification = 'WEB_PROXY_TAG_PATH_STALE';
else if (!wsDirectFresh) classification = 'REALTIME_HUB_OR_AUTH_PATH_STALE';
else if (!wsProxyFresh) classification = 'WEB_PROXY_REALTIME_PATH_STALE';
const last = records.at(-1);
const summary = {
  schema: 'EliteSCADA.Wave14.PostC26LongRunDiagnostic.v1',
  diagnosticOnly: true,
  mustNeverMerge: true,
  classification,
  durationObservedSeconds: endElapsed,
  staleThresholdSeconds: threshold,
  samples: records.length,
  httpFailures: {
    health: records.filter(r => r.http.health !== 200).length,
    directTag: records.filter(r => r.http.directTag !== 200).length,
    proxyTag: records.filter(r => r.http.proxyTag !== 200).length,
    runtimeDiagnostics: records.filter(r => r.http.runtimeDiagnostics !== 200).length,
    runtimeApplication: records.filter(r => r.http.runtimeApplication !== 200).length,
    webRoot: records.filter(r => r.http.webRoot !== 200).length
  },
  directLevel: direct,
  proxyLevel: proxy,
  historianWrittenSamples: written,
  websocketDirect: { ...wsDirect, staleAtEndSeconds: wsDirectAge },
  websocketProxy: { ...wsProxy, staleAtEndSeconds: wsProxyAge },
  finalRuntime: last.runtime,
  finalApplication: last.application,
  finalHistorian: last.historian,
  finalProcess: last.process
};
fs.writeFileSync(summaryPath, `${JSON.stringify(summary, null, 2)}\n`, { mode: 0o600 });
process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
NODE

CLASSIFICATION="$(node -e 'const s=require(process.argv[1]); process.stdout.write(s.classification)' "$SUMMARY")"
printf '\nPost-C26 long-run diagnostic classification: %s\n' "$CLASSIFICATION"
printf 'Evidence: %s\n' "$DIAG_DIR"

if [[ "$CLASSIFICATION" != "NO_FREEZE_OBSERVED" ]]; then
  exit 3
fi
