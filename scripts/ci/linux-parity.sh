#!/usr/bin/env bash
# Repository-controlled Linux counterpart to scripts/ci/local-parity.ps1.
# The caller owns database reset; this runner only consumes the Compose service.
set -euo pipefail

mode="${1:-e2e}"
root="/workspace"
web="${root}/web/scada-web"
marker="${web}/node_modules/.elitescada-package-lock.sha256"

ensure_web_dependencies() {
  local expected
  expected="$(sha256sum "${web}/package-lock.json" | awk '{print $1}')"
  if [[ ! -f "${marker}" ]] || [[ "$(<"${marker}")" != "${expected}" ]]; then
    # node_modules is a named Docker volume.  Clear its contents, never the
    # mount point itself, so cached Linux dependencies remain reusable.
    find "${web}/node_modules" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
    (cd "${web}" && npm ci --no-audit --no-fund)
    printf '%s' "${expected}" > "${marker}"
  fi
}

run_e2e() {
  ensure_web_dependencies
  cd "${web}"
  npx playwright test
}

run_local_auth() {
  ensure_web_dependencies
  cd "${web}"
  npx playwright test --project=chromium-local-auth
}

run_all() {
  export ConnectionStrings__EliteScada="Host=timescaledb;Port=5432;Database=elitescada_test;Username=postgres;Password=postgres;Pooling=false"
  cd "${root}"
  dotnet restore ScadaPlatform.sln
  dotnet build ScadaPlatform.sln --no-restore --configuration Release
  dotnet test ScadaPlatform.sln --no-build --configuration Release --verbosity normal

  export ConnectionStrings__EliteScada="Host=timescaledb;Port=5432;Database=postgres;Username=postgres;Password=postgres;Pooling=false"
  python3 scripts/ci/runtime-smoke.py

  ensure_web_dependencies
  cd "${web}"
  npm run build
  # The runtime smoke deliberately uses the disposable `postgres` database.
  # Chromium must always boot from its own clean E2E database.
  export ConnectionStrings__EliteScada="Host=timescaledb;Port=5432;Database=elitescada_e2e;Username=postgres;Password=postgres;Pooling=false"
  npx playwright test
}

case "${mode}" in
  e2e) run_e2e ;;
  local-auth) run_local_auth ;;
  all) run_all ;;
  versions) ensure_web_dependencies; node --version; npm --version; dotnet --version; (cd "${web}" && npx --no-install playwright --version) ;;
  *) echo "usage: linux-parity.sh {local-auth|e2e|all|versions}" >&2; exit 64 ;;
esac
