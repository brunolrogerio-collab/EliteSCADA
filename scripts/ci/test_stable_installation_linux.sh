#!/usr/bin/env bash
set -Eeuo pipefail

repo_root=$(git rev-parse --show-toplevel)
nonce=$(python3 -c 'import secrets; print(secrets.token_hex(6))')
run_tag="${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-1}-$nonce"
run_tag=$(printf '%s' "$run_tag" | tr '[:upper:]' '[:lower:]' | tr -cd 'a-z0-9-')
project_a="scad-ci-${run_tag}-a"
project_b="scad-ci-${run_tag}-b"
temp_root=$(realpath -m "${RUNNER_TEMP:-${TMPDIR:-/tmp}}")
work_root="$temp_root/scadivo-installation-linux-$run_tag"
[[ "$work_root" == "$temp_root"/scadivo-installation-linux-* ]] || { echo 'Unsafe test directory.' >&2; exit 1; }
install_a="$work_root/instance-a"
install_b="$work_root/instance-b"
safe_cleanup_a=0
safe_cleanup_b=0
safe_remove_work_root=0

cleanup() {
  local status=$?
  trap - EXIT
  for entry in "$install_b:$project_b:$safe_cleanup_b" "$install_a:$project_a:$safe_cleanup_a"; do
    directory=${entry%%:*}
    remainder=${entry#*:}
    project=${remainder%%:*}
    safe_cleanup=${remainder##*:}
    if [[ "$safe_cleanup" == 1 && -f "$directory/docker-compose.stable.yml" && -f "$directory/.env" ]]; then
      expected_directory=$(realpath -m "$directory")
      containers=$(docker ps -aq --filter "label=com.docker.compose.project=$project" 2>/dev/null || true)
      safe_to_remove=1
      for container in $containers; do
        owner=$(docker inspect --format '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}' "$container" 2>/dev/null || true)
        if [[ "$(realpath -m "$owner")" != "$expected_directory" ]]; then
          echo "Skipping cleanup for $project because a container is not owned by this temporary install directory." >&2
          safe_to_remove=0
          break
        fi
      done
      if [[ "$safe_to_remove" == 1 ]]; then
        docker compose --project-name "$project" --env-file "$directory/.env" \
          -f "$directory/docker-compose.stable.yml" down --volumes --remove-orphans >/dev/null 2>&1 || true
      fi
    fi
  done
  if [[ "$safe_remove_work_root" == 1 ]]; then rm -rf -- "$work_root"; fi
  exit "$status"
}
trap cleanup EXIT

command -v docker >/dev/null
docker compose version
mkdir "$work_root"
safe_remove_work_root=1
mkdir "$install_a" "$install_b"
git -C "$repo_root" archive HEAD | tar -x -C "$install_a"
git -C "$repo_root" archive HEAD | tar -x -C "$install_b"

assert_unused_project() {
  local project=$1
  local containers
  containers=$(docker ps -aq --filter "label=com.docker.compose.project=$project")
  [[ -z "$containers" ]] || { echo "Refusing to reuse existing Compose project $project." >&2; return 1; }
  for suffix in database protected-material host-keys; do
    if docker volume inspect "${project}-${suffix}" >/dev/null 2>&1; then
      echo "Refusing to reuse existing volume ${project}-${suffix}." >&2
      return 1
    fi
  done
}

assert_unused_project "$project_a"
safe_cleanup_a=1
assert_unused_project "$project_b"
safe_cleanup_b=1

write_env() {
  local directory=$1 instance=$2 web=$3 api=$4 database=$5
  python3 - "$directory/docker-stable.env.example" "$directory/.env" "$instance" "$web" "$api" "$database" <<'PY'
import pathlib, secrets, sys, uuid

source, destination, instance, web, api, database = sys.argv[1:]
values = {
    "SCADIVO_INSTALLATION_ID": instance,
    "SCADIVO_WEB_BIND": "127.0.0.1",
    "SCADIVO_API_BIND": "127.0.0.1",
    "ELITESCADA_WEB_PORT": web,
    "ELITESCADA_API_PORT": api,
    "ELITESCADA_DB_PORT": database,
    "ELITESCADA_DB_PASSWORD": secrets.token_urlsafe(36),
    "ELITESCADA_HISTORICAL_CURSOR_KEY_BASE64": __import__("base64").b64encode(secrets.token_bytes(32)).decode("ascii"),
    "ELITESCADA_JWT_ISSUER": f"scadivo-ci-{instance}",
    "ELITESCADA_JWT_AUDIENCE": f"scadivo-ci-{instance}-browser",
    "ELITESCADA_JWT_SIGNING_KEY": secrets.token_urlsafe(48),
    "ELITESCADA_MACHINE_ID": str(uuid.uuid4()),
}
lines = pathlib.Path(source).read_text(encoding="utf-8").splitlines()
for key, value in values.items():
    matching = [index for index, line in enumerate(lines) if line.startswith(key + "=")]
    if len(matching) > 1:
        raise SystemExit(f"duplicate {key} in environment example")
    if matching:
        lines[matching[0]] = f"{key}={value}"
    else:
        lines.append(f"{key}={value}")
pathlib.Path(destination).write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
  chmod 600 "$directory/.env"
}

choose_ports() {
  python3 - <<'PY'
import socket
ports = []
while len(ports) < 3:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        listener.bind(("127.0.0.1", 0))
        port = listener.getsockname()[1]
    if port not in ports:
        ports.append(port)
print(*ports)
PY
}

apply_profile() {
  local directory=$1 instance=$2 web=$3 api=$4 database=$5
  shift 5
  bash "$directory/web/scada-web/public/installation/apply-network-profile.sh" \
    --install-directory "$directory" --instance-id "$instance" \
    --web-bind 127.0.0.1 --api-bind 127.0.0.1 \
    --web-port "$web" --api-port "$api" --database-port "$database" "$@"
}

assert_healthy() {
  local directory=$1 project=$2 web=$3 api=$4
  curl --fail --silent --show-error --max-time 10 "http://127.0.0.1:$web/healthz" >/dev/null
  curl --fail --silent --show-error --max-time 10 "http://127.0.0.1:$api/health" >/dev/null
  docker compose --project-name "$project" --env-file "$directory/.env" \
    -f "$directory/docker-compose.stable.yml" exec -T database \
    pg_isready -U elitescada -d elitescada >/dev/null
}

echo 'Starting first isolated stable-Docker installation on the Linux host.'
read -r a_web a_api a_db < <(choose_ports)
write_env "$install_a" "$project_a" "$a_web" "$a_api" "$a_db"
apply_profile "$install_a" "$project_a" "$a_web" "$a_api" "$a_db"
assert_healthy "$install_a" "$project_a" "$a_web" "$a_api"

echo 'Starting a second installation with separate ports, project ID, credentials and volumes.'
read -r b_web b_api b_db < <(choose_ports)
write_env "$install_b" "$project_b" "$b_web" "$b_api" "$b_db"
apply_profile "$install_b" "$project_b" "$b_web" "$b_api" "$b_db" \
  --new-installation --source-instance-id "$project_a" --confirm-isolated-secrets
assert_healthy "$install_a" "$project_a" "$a_web" "$a_api"
assert_healthy "$install_b" "$project_b" "$b_web" "$b_api"

echo 'Changing the first installation ports while both installations are running.'
read -r a_web_new a_api_new a_db_new < <(choose_ports)
apply_profile "$install_a" "$project_a" "$a_web_new" "$a_api_new" "$a_db_new"
assert_healthy "$install_a" "$project_a" "$a_web_new" "$a_api_new"
assert_healthy "$install_b" "$project_b" "$b_web" "$b_api"

echo 'Holding one proposed port and checking preflight rejects it without stopping either installation.'
read -r occupied_port unused_api unused_db < <(choose_ports)
python3 -m http.server "$occupied_port" --bind 127.0.0.1 >/dev/null 2>&1 &
listener_pid=$!
for _ in {1..30}; do
  if python3 - "$occupied_port" <<'PY'
import socket, sys
with socket.create_connection(("127.0.0.1", int(sys.argv[1])), timeout=0.2):
    pass
PY
  then break; fi
  sleep 0.2
done
if apply_profile "$install_a" "$project_a" "$occupied_port" "$a_api_new" "$a_db_new"; then
  kill "$listener_pid" 2>/dev/null || true
  wait "$listener_pid" 2>/dev/null || true
  echo 'Occupied-port preflight unexpectedly succeeded.' >&2
  exit 1
fi
kill "$listener_pid" 2>/dev/null || true
wait "$listener_pid" 2>/dev/null || true
assert_healthy "$install_a" "$project_a" "$a_web_new" "$a_api_new"
assert_healthy "$install_b" "$project_b" "$b_web" "$b_api"

echo 'PASS: Linux-host stable Docker profile supports two isolated installations, port changes and fail-safe collision rejection.'
