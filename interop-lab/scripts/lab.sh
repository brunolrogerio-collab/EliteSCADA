#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

BASE=(docker compose -f compose.yaml)
CIP=(docker compose -f compose.yaml -f compose.cip.yaml)
NODE_RED_URL="http://127.0.0.1:${NODE_RED_PORT:-1880}"

wait_for_node_red() {
  for _ in $(seq 1 60); do
    if curl -fsS "$NODE_RED_URL/lab/health" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  echo "Node-RED lab health endpoint did not become ready." >&2
  return 1
}

smoke() {
  wait_for_node_red
  curl -fsS -X POST "$NODE_RED_URL/lab/reset" >/dev/null

  local token="interop-smoke-$(date +%s)-$$"
  local body
  body=$(printf '{"topic":"elitescada/lab/smoke","payload":{"token":"%s"},"qos":1,"retain":false}' "$token")

  curl -fsS \
    -H 'content-type: application/json' \
    -X POST \
    -d "$body" \
    "$NODE_RED_URL/lab/mqtt/publish" >/dev/null

  for _ in $(seq 1 20); do
    if curl -fsS "$NODE_RED_URL/lab/mqtt/last" | grep -F "$token" >/dev/null; then
      echo "Interop lab smoke PASS: $token"
      return 0
    fi
    sleep 1
  done

  echo "Interop lab smoke FAIL: Node-RED did not observe MQTT token $token" >&2
  return 1
}

case "${1:-help}" in
  start)
    "${BASE[@]}" up -d --build
    wait_for_node_red
    ;;
  stop)
    "${BASE[@]}" down --remove-orphans
    ;;
  reset)
    "${BASE[@]}" down -v --remove-orphans
    ;;
  logs)
    "${BASE[@]}" logs -f
    ;;
  status)
    "${BASE[@]}" ps
    ;;
  smoke)
    smoke
    ;;
  cip-start)
    "${CIP[@]}" up -d --build cip-controllogix cip-compactlogix
    ;;
  cip-stop)
    "${CIP[@]}" stop cip-controllogix cip-compactlogix
    ;;
  cip-status)
    "${CIP[@]}" ps cip-controllogix cip-compactlogix
    ;;
  *)
    cat <<'EOF'
Usage: bash scripts/lab.sh <command>

Commands:
  start       Build/start Node-RED + Mosquitto
  stop        Stop/remove base lab containers
  reset       Stop/remove base lab containers and volumes
  status      Show base lab status
  logs        Follow base lab logs
  smoke       Verify Node-RED health and MQTT round trip
  cip-start   Build/start ControlLogix + CompactLogix simulator peers
  cip-stop    Stop CIP simulator peers
  cip-status  Show CIP simulator status
EOF
    ;;
esac
