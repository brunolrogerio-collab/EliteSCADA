#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

usage() {
  cat <<'EOF'
Usage: apply-network-profile.sh --install-directory PATH --instance-id ID \
  --web-bind 127.0.0.1|0.0.0.0 --api-bind 127.0.0.1|0.0.0.0 \
  --web-port PORT --api-port PORT --database-port PORT
  [--new-installation --source-instance-id ID --confirm-isolated-secrets]

Run from an administrator account that can use Docker Compose. The script changes
only the supported stable-Docker .env profile; it never opens firewall rules or
deletes volumes. For a new instance, use another directory and its own unique .env.
EOF
}

fail() { printf 'Profile not applied: %s\n' "$1" >&2; exit 1; }

install_directory=''
instance_id=''
web_bind=''
api_bind=''
web_port=''
api_port=''
database_port=''
confirm_isolated_secrets=0
new_installation=0
source_instance_id=''
while (($#)); do
  case "$1" in
    --install-directory) install_directory=${2-}; shift 2 ;;
    --instance-id) instance_id=${2-}; shift 2 ;;
    --web-bind) web_bind=${2-}; shift 2 ;;
    --api-bind) api_bind=${2-}; shift 2 ;;
    --web-port) web_port=${2-}; shift 2 ;;
    --api-port) api_port=${2-}; shift 2 ;;
    --database-port) database_port=${2-}; shift 2 ;;
    --confirm-isolated-secrets) confirm_isolated_secrets=1; shift ;;
    --new-installation) new_installation=1; shift ;;
    --source-instance-id) source_instance_id=${2-}; shift 2 ;;
    --help|-h) usage; exit 0 ;;
    *) usage; fail "unknown option $1" ;;
  esac
done

[[ -n "$install_directory" && -n "$instance_id" && -n "$web_bind" && -n "$api_bind" && -n "$web_port" && -n "$api_port" && -n "$database_port" ]] || { usage; fail 'all profile values are required'; }
[[ "$instance_id" =~ ^[a-z0-9][a-z0-9-]{0,47}$ ]] || fail 'instance ID must use 1–48 lowercase letters, numbers or hyphens and start with a letter or number'
if ((new_installation)); then
  [[ "$source_instance_id" =~ ^[a-z0-9][a-z0-9-]{0,47}$ && "$source_instance_id" != "$instance_id" ]] || fail 'a new installation needs a different, valid source instance ID'
  ((confirm_isolated_secrets)) || fail 'a new installation requires --confirm-isolated-secrets after preparing its own .env and unique secrets'
else
  [[ -z "$source_instance_id" && $confirm_isolated_secrets -eq 0 ]] || fail 'new-installation options can only be used when provisioning a second installation'
fi
[[ "$web_bind" == 127.0.0.1 || "$web_bind" == 0.0.0.0 ]] || fail 'unsupported Web bind address'
[[ "$api_bind" == 127.0.0.1 || "$api_bind" == 0.0.0.0 ]] || fail 'unsupported API bind address'
for value in "$web_port" "$api_port" "$database_port"; do
  [[ "$value" =~ ^[0-9]{1,5}$ ]] && ((10#$value >= 1 && 10#$value <= 65535)) || fail 'ports must be integers from 1 through 65535'
done
[[ "$web_port" != "$api_port" && "$web_port" != "$database_port" && "$api_port" != "$database_port" ]] || fail 'Web, API and database host ports must be different'
command -v docker >/dev/null 2>&1 || fail 'Docker CLI was not found'
command -v python3 >/dev/null 2>&1 || fail 'Python 3 is required for local port preflight and safe .env editing'

root=$(cd -- "$install_directory" 2>/dev/null && pwd -P) || fail 'installation directory does not exist'
compose="$root/docker-compose.stable.yml"
env_file="$root/.env"
audit_file="$root/network-profile-audit.log"
[[ -f "$compose" ]] || fail 'supported docker-compose.stable.yml was not found in the installation directory'
[[ -f "$env_file" ]] || fail 'installation .env was not found; create it from the documented example, without copying secrets'

read_env() {
  python3 - "$env_file" "$1" "$2" <<'PY'
import pathlib, re, sys
path, name, default = sys.argv[1:]
lines = pathlib.Path(path).read_text(encoding="utf-8-sig").splitlines()
matches = [line for line in lines if re.match(r"^\s*" + re.escape(name) + r"=", line)]
if len(matches) > 1:
    raise SystemExit("duplicate " + name + " in .env")
if not matches:
    print(default)
else:
    print(matches[0].split("=", 1)[1].strip().strip("\"'") )
PY
}

old_id=$(read_env SCADIVO_INSTALLATION_ID elitescada-stable) || fail 'could not read installation ID from .env'
old_web_bind=$(read_env SCADIVO_WEB_BIND 127.0.0.1) || fail 'could not read Web bind from .env'
old_api_bind=$(read_env SCADIVO_API_BIND 127.0.0.1) || fail 'could not read API bind from .env'
old_web_port=$(read_env ELITESCADA_WEB_PORT 18080) || fail 'could not read Web port from .env'
old_api_port=$(read_env ELITESCADA_API_PORT 15080) || fail 'could not read API port from .env'
old_database_port=$(read_env ELITESCADA_DB_PORT 15432) || fail 'could not read database port from .env'

project_owners() {
  local project=$1 container owner
  local -a ids=()
  local ids_output
  ids_output=$(docker compose --project-name "$project" -f "$compose" ps --all --quiet 2>/dev/null) || fail 'Docker Compose could not inspect the selected project'
  mapfile -t ids <<<"$ids_output"
  for container in "${ids[@]}"; do
    [[ -n "$container" ]] || continue
    owner=$(docker inspect --format '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}' "$container" 2>/dev/null) || fail 'Docker could not inspect a Compose container'
    [[ -n "$owner" && "$owner" != '<no value>' ]] && printf '%s\n' "$(realpath -m -- "$owner")"
  done
}

old_owners_output=$(project_owners "$old_id") || fail 'Docker Compose could not inspect the selected project'
new_owners_output=$(project_owners "$instance_id") || fail 'Docker Compose could not inspect the selected project'
mapfile -t old_owners <<<"$old_owners_output"
mapfile -t new_owners <<<"$new_owners_output"
same_old=0; same_new=0; other_new=0
for owner in "${old_owners[@]}"; do [[ "$owner" == "$root" ]] && same_old=1; done
for owner in "${new_owners[@]}"; do
  if [[ "$owner" == "$root" ]]; then same_new=1; else other_new=1; fi
done
[[ "$instance_id" == "$old_id" || $same_old -eq 0 ]] || fail 'changing the ID of a data-bearing installation in place is blocked; keep its ID or prepare another directory with its own .env and unique secrets'
[[ "$instance_id" == "$old_id" || $confirm_isolated_secrets -eq 1 ]] || fail 'for another installation, use a separate directory and independent .env secrets, then repeat with --confirm-isolated-secrets'
[[ $other_new -eq 0 ]] || fail 'this instance ID is already used by a different Compose directory; choose another ID'
if ((new_installation)); then
  [[ "$old_id" == "$instance_id" ]] || fail 'for a new installation, set SCADIVO_INSTALLATION_ID to the new ID in that directory .env before running this script; existing IDs are never changed in place'
  [[ $same_new -eq 0 ]] || fail 'the target instance already has containers in this directory; use a fresh directory'
  for volume in "$instance_id-database" "$instance_id-protected-material" "$instance_id-host-keys"; do
    if docker volume inspect "$volume" >/dev/null 2>&1; then
      fail "volume $volume already exists; a new installation must use a fresh unique ID so it cannot attach to existing data"
    fi
  done
else
  [[ "$instance_id" == "$old_id" ]] || fail 'instance IDs are not changed in place; prepare a separate installation directory and set its unique ID in that .env first'
fi

check_bind() {
  python3 - "$1" "$2" <<'PY'
import socket, sys
sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
try:
    sock.bind((sys.argv[1], int(sys.argv[2])))
except OSError:
    raise SystemExit(1)
finally:
    sock.close()
PY
}

check_docker_bindings() {
  python3 - "$1" "$2" <<'PY'
import json, subprocess, sys
address, port = sys.argv[1], int(sys.argv[2])
ids = subprocess.run(["docker", "ps", "--quiet"], check=True, capture_output=True, text=True).stdout.split()
if not ids:
    raise SystemExit(0)
containers = json.loads(subprocess.run(["docker", "inspect", *ids], check=True, capture_output=True, text=True).stdout)
for container in containers:
    for bindings in (container.get("NetworkSettings", {}).get("Ports") or {}).values():
        for binding in bindings or []:
            if int(binding.get("HostPort", "0")) != port:
                continue
            bound_address = binding.get("HostIp", "")
            if address == "0.0.0.0" or bound_address in {"", "0.0.0.0", "::", address}:
                raise SystemExit(1)
PY
}

check_endpoint() {
  local name=$1 address=$2 port=$3 old_address=$4 old_port=$5
  if [[ "$instance_id" == "$old_id" && "$address" == "$old_address" && "$port" == "$old_port" ]]; then return 0; fi
  check_bind "$address" "$port" || fail "$name port $port is already occupied on $address; no service was stopped"
  check_docker_bindings "$address" "$port" || fail "$name port $port is already published by another Docker container; no service was stopped"
}
check_endpoint Web "$web_bind" "$web_port" "$old_web_bind" "$old_web_port"
check_endpoint API "$api_bind" "$api_port" "$old_api_bind" "$old_api_port"
check_endpoint Database 127.0.0.1 "$database_port" 127.0.0.1 "$old_database_port"

candidate="$root/.env.scadivo-network-candidate"
backup="$root/.env.network-backup-$(date -u +%Y%m%d-%H%M%S)-$$"
cleanup() { rm -f -- "$candidate"; }
trap cleanup EXIT

python3 - "$env_file" "$candidate" "$instance_id" "$web_bind" "$api_bind" "$web_port" "$api_port" "$database_port" <<'PY'
import pathlib, re, sys
source, target, instance, web_bind, api_bind, web, api, database = sys.argv[1:]
path = pathlib.Path(source)
lines = path.read_text(encoding="utf-8-sig").splitlines()
values = {
    "SCADIVO_INSTALLATION_ID": instance,
    "SCADIVO_WEB_BIND": web_bind,
    "SCADIVO_API_BIND": api_bind,
    "ELITESCADA_WEB_PORT": web,
    "ELITESCADA_API_PORT": api,
    "ELITESCADA_DB_PORT": database,
}
for key, value in values.items():
    indexes = [i for i, line in enumerate(lines) if re.match(r"^\s*" + re.escape(key) + r"=", line)]
    if len(indexes) > 1:
        raise SystemExit("duplicate " + key + " in .env; no change made")
    if indexes:
        lines[indexes[0]] = key + "=" + value
    else:
        lines.append(key + "=" + value)
pathlib.Path(target).write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
chmod --reference="$env_file" "$candidate"
docker compose --project-name "$instance_id" --env-file "$candidate" -f "$compose" config --quiet || fail 'Docker Compose rejected the candidate configuration; no service was stopped'

actor=$(id -un 2>/dev/null || printf unknown)
audit() { printf '%s actor=%s result=%s instance=%s web=%s api=%s database=%s\n' "$(date -u --iso-8601=seconds)" "$actor" "$1" "$instance_id" "$web_port" "$api_port" "$database_port" >>"$audit_file"; }
cp -p -- "$env_file" "$backup"
audit started
mv -f -- "$candidate" "$env_file"

rollback() {
  local message=$1
  if [[ -f "$backup" ]]; then
    cp -p -- "$backup" "$env_file"
    if [[ $same_old -eq 1 ]]; then
      docker compose --project-name "$old_id" -f "$compose" up -d >/dev/null 2>&1 || true
    else
      docker compose --project-name "$instance_id" -f "$compose" down >/dev/null 2>&1 || true
    fi
    audit rolled-back
  fi
  fail "$message; previous .env and data volumes were preserved. See the local backup and network-profile-audit.log"
}

if ! docker compose --project-name "$instance_id" -f "$compose" up -d; then rollback 'Docker Compose could not apply the profile'; fi

deadline=$((SECONDS + 120))
healthy=0
while ((SECONDS < deadline)); do
  if curl --fail --silent --show-error --max-time 3 "http://127.0.0.1:$web_port/healthz" >/dev/null 2>&1 \
    && curl --fail --silent --show-error --max-time 3 "http://127.0.0.1:$api_port/health" >/dev/null 2>&1 \
    && docker compose --project-name "$instance_id" -f "$compose" exec -T database pg_isready -U elitescada -d elitescada >/dev/null 2>&1; then
    healthy=1
    break
  fi
  sleep 3
done
((healthy == 1)) || rollback 'Health checks did not pass within 120 seconds'

audit applied
printf 'Network profile applied. Web: http://localhost:%s — API: http://localhost:%s\n' "$web_port" "$api_port"
printf 'Previous .env backup: %s\n' "$backup"
if [[ "$instance_id" != "$old_id" ]]; then printf 'Separate installation: keep its own directory, .env secrets and backup.\n'; fi
printf 'No firewall rules were changed. Remote access needs separately configured TLS/reverse-proxy and firewall policy.\n'
