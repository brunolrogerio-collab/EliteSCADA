#!/bin/sh
# Host-only encrypted backup. Does not read or touch demo data.
set -eu
umask 077
cd /opt/scadivo/portal
destination=/var/backups/scadivo-portal
password=/etc/scadivo/portal-backup.key
test -s "$password"
mkdir -p "$destination"
chmod 700 "$destination"
staging=$(mktemp -d /var/backups/scadivo-portal/staging.XXXXXX)
trap 'rm -f "$staging/database.dump" "$staging/keys.tar.gz" "$staging/manifest.txt"; rmdir "$staging"' EXIT
docker compose -f compose.yml exec -T database pg_dump -U scadivo_portal -d scadivo_portal -Fc > "$staging/database.dump"
docker compose -f compose.yml exec -T api tar czf - -C /app/keys . > "$staging/keys.tar.gz"
date -u '+SCADIVO portal backup %Y-%m-%dT%H:%M:%SZ' > "$staging/manifest.txt"
archive="$destination/portal-$(date -u +%Y%m%dT%H%M%SZ).tar.gz.enc"
tar czf - -C "$staging" database.dump keys.tar.gz manifest.txt | openssl enc -aes-256-cbc -salt -pbkdf2 -iter 200000 -pass file:"$password" -out "$archive"
test -s "$archive"
# This fixed dedicated directory contains only this job's encrypted archives.
find /var/backups/scadivo-portal -maxdepth 1 -type f -name 'portal-????????T??????Z.tar.gz.enc' -mtime +14 -delete
printf 'Encrypted portal backup completed: %s\n' "$archive"
