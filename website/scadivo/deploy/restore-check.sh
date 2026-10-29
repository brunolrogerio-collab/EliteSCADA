#!/bin/sh
# Restores the latest backup to a disposable database in the portal PostgreSQL only.
set -eu
umask 077
cd /opt/scadivo/portal
archive=${1:?Pass the exact encrypted backup path}
case "$archive" in /var/backups/scadivo-portal/portal-*.tar.gz.enc) ;; *) exit 2 ;; esac
test -s "$archive"
staging=$(mktemp -d /var/backups/scadivo-portal/check.XXXXXX)
cleanup() {
  docker compose -f compose.yml exec -T database dropdb -U scadivo_portal --if-exists portal_restore_check >/dev/null
  rm -f "$staging/database.dump" "$staging/keys.tar.gz" "$staging/manifest.txt"
  rmdir "$staging"
}
trap cleanup EXIT
openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -pass file:/etc/scadivo/portal-backup.key -in "$archive" | tar xzf - -C "$staging"
tar tzf "$staging/keys.tar.gz" | grep -q 'key-'
if [ -s "$staging/online-lease-signing-key.tar.gz" ]; then
  tar tzf "$staging/online-lease-signing-key.tar.gz" | grep -q 'online-lease.pem'
fi
docker compose -f compose.yml exec -T database createdb -U scadivo_portal portal_restore_check
docker compose -f compose.yml exec -T database pg_restore -U scadivo_portal -d portal_restore_check --exit-on-error < "$staging/database.dump"
docker compose -f compose.yml exec -T database psql -U scadivo_portal -d portal_restore_check -v ON_ERROR_STOP=1 -c 'SELECT count(*) AS restored_accounts FROM "AspNetUsers"; SELECT count(*) AS restored_faq FROM "FaqArticles";'
printf 'Portal backup decrypted, keys present, database restored successfully. Production database unchanged.\n'
