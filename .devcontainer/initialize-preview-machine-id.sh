#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATE_DIR="$ROOT/.preview"
MACHINE_ID_FILE="$STATE_DIR/machine-id"

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

if [[ ! -s "$MACHINE_ID_FILE" ]]; then
  machine_id=""
  if [[ -r /proc/sys/kernel/random/uuid ]]; then
    machine_id="$(tr -d -- '-\r\n' < /proc/sys/kernel/random/uuid | tr '[:upper:]' '[:lower:]')"
  elif command -v uuidgen >/dev/null 2>&1; then
    machine_id="$(uuidgen | tr -d -- '-\r\n' | tr '[:upper:]' '[:lower:]')"
  fi

  if [[ ! "$machine_id" =~ ^[0-9a-f]{32}$ ]] || [[ "$machine_id" == "00000000000000000000000000000000" ]]; then
    printf 'Could not generate a valid disposable Codespaces machine identity.\n' >&2
    exit 1
  fi

  printf '%s\n' "$machine_id" > "$MACHINE_ID_FILE"
fi

machine_id="$(tr -d '[:space:]-' < "$MACHINE_ID_FILE" | tr '[:upper:]' '[:lower:]')"
if [[ ! "$machine_id" =~ ^[0-9a-f]{32}$ ]] || [[ "$machine_id" == "00000000000000000000000000000000" ]]; then
  printf 'Preview machine identity is invalid; delete .preview/machine-id and rebuild the Codespace.\n' >&2
  exit 1
fi

printf '%s\n' "$machine_id" > "$MACHINE_ID_FILE"
chmod 644 "$MACHINE_ID_FILE"
printf 'Prepared disposable Codespaces machine identity for fail-closed product licensing.\n'
