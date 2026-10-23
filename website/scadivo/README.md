# SCADIVO website and customer portal

Issue: [#741](https://github.com/brunolrogerio-collab/EliteSCADA/issues/741). Implementation owner: the website Codex conversation. This package does not change product licensing, SCADA Security Authority, demo users, or historical tag databases.

## Delivered pilot

| Address | Purpose |
| --- | --- |
| `https://scadivo.com/` | Public PT/EN presentation, real demo captures, free 200-tag offering, local/web/Windows/Linux/Docker/edge positioning |
| `https://scadivo.com/demo` | Existing public Runtime demonstration, unchanged by this portal package |
| `https://scadivo.com/portal/` | Independent invitation-only customer portal, same-origin HTTPS cookie |

The portal includes profile/password management, administrator-issued personal invitations, private support conversations, searchable bilingual FAQ, editable PT/EN message templates, administrator account list and audit records. Organizations and memberships have separate tables, not global administrator privileges.

The authenticated download catalog accepts only approved official GitHub release URLs with SHA-256 metadata. It starts empty because no release artifact is currently approved; no placeholder installer is offered. Publication management, the official distribution pipeline (#363/#408), and artifact verification before catalog insertion remain follow-up work.

Community and licensing areas explicitly display their pending state. There is no checkout, payment webhook, product activation API, signing key, license emission or automatic product renewal in this package.

### Deliberately not activated

- Public registration, email confirmation and email password recovery: require the email transport/domain authentication and legal decisions in #741.
- Automatic email delivery: templates and notification references are stored, but there is no sender/worker. The UI states that nothing was automatically sent. A future worker must generate fresh one-use action tokens; invitation tokens are not stored in the outbox or audit log.
- Public community: requires moderation rules, account recovery and SSO integration.
- Commercial licensing: pricing, named/concurrent seats, machine limits/transfers, cancellation/refund and conversion rules are still product-owner decisions. Future remote license transport is HTTPS/TCP 443, with its own contract and coordinator review.

This is a closed pilot, not a claim that all phases of #741 are complete. Independent security review, verified email delivery/recovery, legal policies, offsite backup automation and monitoring/alerting are launch gates.

## Code boundaries

- `site/`: static public site and same-origin Nginx reverse proxy.
- `portal/`: standalone ASP.NET Core 10 application, Identity, EF Core PostgreSQL migrations and static portal UI.
- `deploy/`: independent production Compose stack and encrypted host backup/restore-check procedures.
- `tests/`: real PostgreSQL integration tests; no product project reference other than the standalone portal.
- `.github/workflows/scadivo-portal.yml`: focused path-filtered build, integration test and image build. No product acceptance or self-merge is implied.

Run .NET commands from this directory so the nested `global.json` selects the installed SDK. No root solution/global SDK changes are needed.

```powershell
dotnet build portal/Scadivo.Portal.csproj
node --check portal/wwwroot/app.js
dotnet test tests/Scadivo.Portal.Tests.csproj --configuration Release
docker build -f portal/Dockerfile -t scadivo-portal:local .
```

Tests require a **disposable** PostgreSQL database. Set `PORTAL_TEST_DATABASE`; the default is localhost port 15441 with the explicitly nonproduction test credential in the fixture. Never point tests at the production portal or demo. Tests cover antiforgery, secure cookies, roles, one-use invitation redemption, cross-user support isolation, FAQ/templates/audit, login lockout, and database persistence across a fresh host.

For local UI development use HTTPS with a development certificate. Secure cookies and antiforgery are intentionally not relaxed for HTTP. Production uses the existing valid public certificate and never disables certificate verification.

## Current production layout

- Public site remains the existing EasyPanel-managed Swarm service `scadivo_site`.
- Site source remains `/etc/easypanel/projects/scadivo/site/code`; the tracked `site/` folder is the corresponding source in this package.
- Portal is a separate Docker Compose project `scadivo_portal` at `/opt/scadivo/portal`, managed over the existing authorized SSH connection. It is not a new EasyPanel service entry.
- API and PostgreSQL have **no host-published ports**. Only the existing website exposes the portal through `/portal/`, using the external `easypanel` overlay alias `scadivo_portal_api`.
- Database volume: `scadivo_portal_database`. Key-ring volume: `scadivo_portal_keys`. Neither is a demo volume.
- Root-only environment: `/opt/scadivo/portal/.env`. Initial owner secret: `/opt/scadivo/portal/secrets/portal-owner`, readonly UID 1654. This is only used for a completely empty database; restarting never resets a password.
- Initial owner username: `scadivo.owner`. Its private password is delivered outside GitHub/repository; change it in **My account**. Demo engineering credentials do not sign in to this portal.
- API runs as UID 1654, readonly root filesystem, writable key volume/tmpfs, dropped capabilities and bounded log/memory settings.
- Trusted proxy network is explicitly `10.11.0.0/16`. Configure an equivalent trusted range if hosting changes; do not trust arbitrary forwarded headers.
- Secure HttpOnly SameSite=Strict cookie, 8-hour nonsliding session, antiforgery on all API writes, Identity password hashing/lockout and per-IP authentication throttling. No browser-stored bearer tokens, raw passwords, invitation tokens or private license keys are logged.

### Deployment and rollback

`initialize-host.sh` is **first deployment only** and refuses an existing environment or initial secret. Do not rerun it to troubleshoot an installation. Normal updates preserve all volumes and secrets:

```sh
cd /opt/scadivo/portal
./backup.sh
# Load the reviewed new image, update PORTAL_IMAGE in the private .env, then:
docker compose -f compose.yml up -d --wait --wait-timeout 180
docker compose -f compose.yml ps
```

Database migrations run before serving traffic. Review any subsequent destructive migration separately. An image rollback alone is not a database rollback. Initial schema is additive and does not touch the product database.

Public site rollback image: `easypanel/scadivo/site:before-portal-20261023`; source archive: `/var/backups/scadivo-site-before-portal-20261023.tar.gz`. Its rollback removes only the portal entry/proxy, not portal data. Never remove the demo or its volumes as part of website deployment.

## Backups and recovery

- `/opt/scadivo/portal/backup.sh` makes a PostgreSQL custom dump and archives the Identity Data Protection key ring, then encrypts them with OpenSSL AES-256-CBC + PBKDF2 (200,000 iterations).
- Encryption password is a generated root-only file `/etc/scadivo/portal-backup.key`, outside images and Git. Backups require this key to recover; keep an independent protected copy.
- Encrypted backups: `/var/backups/scadivo-portal`, directory mode 0700, files 0600. The dedicated daily job in `/etc/cron.d/scadivo-portal-backup` runs at **03:17 UTC** and retains 14 days of portal backups. This is separate from the demo's five-day telemetry retention.
- Log: `/var/log/scadivo-portal-backup.log`, rotated by `/etc/logrotate.d/scadivo-portal`.
- `restore-check.sh /var/backups/scadivo-portal/portal-EXACT_TIMESTAMP.tar.gz.enc` decrypts a specified archive, validates key-ring presence, restores into disposable `portal_restore_check`, checks account/FAQ rows, then drops **only that check database**. It never replaces the active database.
- First production backup and restoration check passed on 23 October 2026, restoring one owner and six FAQ articles. The active portal remained unchanged.
- A protected manual off-host recovery copy is delivered to the owner locally. Automated offsite destination, periodic full-host recovery exercises and backup-failure alerts remain launch work; same-host backups alone do not protect against total VPS loss.

For disaster recovery: provision the same stack without publishing it, decrypt the protected archive, restore `database.dump` to a fresh database and `keys.tar.gz` to the key-ring volume (UID 1654, directory 0700), then start the reviewed image and validate HTTPS/login/support before switching the public proxy. Never overwrite a running database or reuse the demo volume. Re-establish secrets through protected files, not command history or issue comments.

## Verification — 23 October 2026

- Standalone Release build and PostgreSQL integration suite: passed.
- Docker image build and Nginx configuration validation: passed.
- NuGet vulnerability report (including transitives): none reported by configured official source at verification time; not a security certification.
- Public portal live HTTPS UI: owner login, all tabs, FAQ search, template preview, PT/EN, logout; desktop 1440 and widths 320/390/768; no page errors or horizontal overflow.
- Public marketing site: PT/EN at widths 320/390/768/1024/1440, images/links/FAQ checked; no historical commercial branding or evaluation-time messaging.
- Anonymous APIs enforce authentication; database/API are not host-published; encrypted backup and disposable restore passed.
- Existing public site and demo routes remain operational. No product application import, demo restart or license change was made.

Review and future integration remain through the draft PR and umbrella issue #741. Do not close #741 on this foundation package.
