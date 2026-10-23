#!/bin/sh
# First deployment only. Existing installations must never be reset by this script.
set -eu
umask 077
cd /opt/scadivo/portal
test ! -e .env
test ! -e secrets/portal-owner
test -f compose.yml
mkdir -p secrets /etc/scadivo
chmod 700 secrets /etc/scadivo
database_password=$(openssl rand -hex 32)
printf 'Aa1!%s\n' "$(openssl rand -base64 27)" > secrets/portal-owner
chown 1654:1654 secrets/portal-owner
chmod 400 secrets/portal-owner
printf 'DATABASE_PASSWORD=%s\nPORTAL_IMAGE=scadivo-portal:20261023-foundation.2\n' "$database_password" > .env
chmod 600 .env
if test ! -e /etc/scadivo/portal-backup.key; then
  openssl rand -base64 48 > /etc/scadivo/portal-backup.key
  chmod 600 /etc/scadivo/portal-backup.key
fi
docker compose -f compose.yml config --quiet
docker compose -f compose.yml up -d --wait --wait-timeout 180
