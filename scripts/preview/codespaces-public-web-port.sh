#!/usr/bin/env bash
set -Eeuo pipefail

fail() {
  printf 'Codespaces Preview readiness blocked: %s\n' "$1" >&2
  exit 1
}

[[ "${CODESPACES:-}" == "true" ]] || fail 'this lifecycle hook must run inside GitHub Codespaces.'
[[ -n "${CODESPACE_NAME:-}" ]] || fail 'CODESPACE_NAME is unavailable; refusing to target another Codespace.'
command -v gh >/dev/null 2>&1 || fail 'GitHub CLI is unavailable in the devcontainer.'
gh auth status --hostname github.com >/dev/null 2>&1 || fail 'GitHub CLI has no usable Codespaces authentication.'

codespace="$CODESPACE_NAME"
ports_json() {
  gh codespace ports --codespace "$codespace" --json sourcePort,visibility
}

has_web_port=false
for attempt in $(seq 1 24); do
  if has_web_port="$(gh codespace ports --codespace "$codespace" --json sourcePort,visibility --jq 'any(.[]; (.sourcePort | tostring) == "5173")' 2>/dev/null)"; then
    [[ "$has_web_port" == "true" ]] && break
  fi
  sleep 5
done
[[ "$has_web_port" == "true" ]] || fail 'Web port 5173 did not appear in the Codespaces forwarder within 120 seconds.'

gh codespace ports visibility 5173:public --codespace "$codespace" \
  || fail 'GitHub rejected the request to make only Web port 5173 public.'

verified=false
for attempt in $(seq 1 12); do
  if ports="$(ports_json 2>/dev/null)"; then
    web_visibility="$(gh codespace ports --codespace "$codespace" --json sourcePort,visibility --jq '[.[] | select((.sourcePort | tostring) == "5173") | .visibility] | first // "missing"' 2>/dev/null || printf missing)"
    public_internal="$(gh codespace ports --codespace "$codespace" --json sourcePort,visibility --jq '[.[] | select((.sourcePort | tostring) == "5080" or (.sourcePort | tostring) == "5432") | select(.visibility == "public")] | length' 2>/dev/null || printf 1)"
    if [[ "$web_visibility" == "public" && "$public_internal" == "0" ]]; then
      verified=true
      break
    fi
  fi
  sleep 5
done

[[ "$verified" == "true" ]] || fail 'Could not verify 5173=public and 5080/5432 not-public from the live Codespaces port list.'
printf 'Codespaces Preview port policy verified for %s: 5173=public; 5080/5432 are not public.\n' "$codespace"
