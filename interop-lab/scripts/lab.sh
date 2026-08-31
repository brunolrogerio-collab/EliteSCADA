#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

BASE=(docker compose -f compose.yaml)
CIP=(docker compose -f compose.yaml -f compose.cip.yaml)
OPCUA=(docker compose -f compose.yaml -f compose.opcua.yaml)
IEC104=(docker compose -f compose.yaml -f compose.iec104.yaml)
DNP3=(docker compose -f compose.yaml -f compose.dnp3.yaml)
ALL=(docker compose -f compose.yaml -f compose.cip.yaml -f compose.opcua.yaml -f compose.iec104.yaml -f compose.dnp3.yaml)
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
    "${BASE[@]}" up -d --build --wait --wait-timeout 180
    wait_for_node_red
    ;;
  stop)
    "${BASE[@]}" down --remove-orphans
    ;;
  reset)
    "${ALL[@]}" down -v --remove-orphans
    ;;
  logs)
    "${ALL[@]}" logs -f
    ;;
  status)
    "${ALL[@]}" ps
    ;;
  smoke)
    smoke
    ;;
  all-start)
    "${ALL[@]}" up -d --build --wait --wait-timeout 180
    wait_for_node_red
    ;;
  all-stop)
    "${ALL[@]}" down --remove-orphans
    ;;
  cip-start)
    "${CIP[@]}" up -d --build --wait --wait-timeout 180 cip-controllogix cip-compactlogix
    ;;
  cip-stop)
    "${CIP[@]}" stop cip-controllogix cip-compactlogix
    ;;
  cip-status)
    "${CIP[@]}" ps cip-controllogix cip-compactlogix
    ;;
  opcua-start)
    "${OPCUA[@]}" up -d --build --wait --wait-timeout 180 opcua-peer node-red
    ;;
  opcua-stop)
    "${OPCUA[@]}" stop opcua-peer
    ;;
  opcua-status)
    "${OPCUA[@]}" ps opcua-peer
    ;;
  opcua-smoke)
    "${OPCUA[@]}" exec -T node-red node /data/opcua-smoke.js
    ;;
  iec104-start)
    "${IEC104[@]}" up -d --build --wait --wait-timeout 180 iec104-lib60870
    ;;
  iec104-stop)
    "${IEC104[@]}" stop iec104-lib60870
    ;;
  iec104-status)
    "${IEC104[@]}" ps iec104-lib60870
    ;;
  dnp3-start)
    "${DNP3[@]}" up -d --build --wait --wait-timeout 180 dnp3-dnp3py
    ;;
  dnp3-stop)
    "${DNP3[@]}" stop dnp3-dnp3py
    ;;
  dnp3-status)
    "${DNP3[@]}" ps dnp3-dnp3py
    ;;
  *)
    cat <<'EOF'
Usage: bash scripts/lab.sh <command>

Commands:
  start          Build/start Node-RED + Mosquitto
  stop           Stop/remove base lab containers
  reset          Stop/remove all lab containers and volumes
  status         Show all known lab peer status
  logs           Follow all known lab logs
  smoke          Verify Node-RED health and MQTT round trip
  all-start      Build/start all currently available protocol peers
  all-stop       Stop/remove all currently available protocol peers
  cip-start      Build/start ControlLogix + CompactLogix peers
  cip-stop       Stop CIP peers
  cip-status     Show CIP peer status
  opcua-start    Build/start open62541 + Node-RED client plane
  opcua-stop     Stop open62541 peer
  opcua-status   Show OPC UA peer status
  opcua-smoke    Browse/read/write/subscription smoke via node-opcua
  iec104-start   Build/start lib60870 IEC-104 outstation
  iec104-stop    Stop IEC-104 peer
  iec104-status  Show IEC-104 peer status
  dnp3-start     Build/start dnp3py outstation
  dnp3-stop      Stop DNP3 peer
  dnp3-status    Show DNP3 peer status
EOF
    ;;
esac
