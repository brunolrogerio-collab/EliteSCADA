#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_PID=""
WEB_PID=""

cleanup() {
  if [ -n "$WEB_PID" ]; then kill "$WEB_PID" 2>/dev/null || true; fi
  if [ -n "$API_PID" ]; then kill "$API_PID" 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM

cd "$ROOT"
echo "Starting EliteSCADA API on http://0.0.0.0:5080"
ASPNETCORE_URLS=http://0.0.0.0:5080 \
  dotnet run --project src/Scada.Api/Scada.Api.csproj --no-launch-profile &
API_PID=$!

cd "$ROOT/web/scada-web"
echo "Starting EliteSCADA Web on http://0.0.0.0:5173"
npm run dev -- --host 0.0.0.0 &
WEB_PID=$!

wait -n "$API_PID" "$WEB_PID"
