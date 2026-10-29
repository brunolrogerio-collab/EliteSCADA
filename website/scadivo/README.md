# SCADIVO website and customer portal

Issue: [#741](https://github.com/brunolrogerio-collab/EliteSCADA/issues/741). Implementation owner: the website Codex conversation. This package does not change product licensing, SCADA Security Authority, demo users, or historical tag databases.

## Delivered pilot

| Address | Purpose |
| --- | --- |
| `https://scadivo.com/` | Public PT/EN/ES presentation, real demo captures, free 200-tag offering, local/web/Windows/Linux/Docker/edge positioning |
| `https://scadivo.com/demo` | Existing public Runtime demonstration, unchanged by this portal package |
| `https://scadivo.com/portal/` | Independent customer portal with public account pilot, same-origin HTTPS cookie |

The portal includes profile/password management, administrator-issued personal invitations, private support conversations, searchable FAQ, editable automatic-message templates, administrator account list and audit records. The public site and portal support Portuguese, English and Spanish. Users choose a preferred interface language during registration and can change it in their profile; confirmation, recovery and account messages follow that preference, while contact receipts follow the language selected on the public site. Public self-registration requires profile type, activity area, company/school affiliation and preferred language (enter “Autônomo/independente” when applicable); phone, address and date of birth remain optional. New users receive a one-use email-confirmation link and cannot sign in before confirming. Recovery sends a one-use reset link only to eligible confirmed accounts. Organizations and memberships have separate tables, not global administrator privileges. The public site contact form routes Sales, Support and General Contact messages to their respective business mailboxes without creating accounts.

The authenticated download catalog accepts official GitHub release-file URLs with SHA-256 metadata. Administration → Downloads now creates and edits catalog entries as drafts or published releases. The official distribution pipeline (#363/#408) and the release manager's artifact approval/checksum verification remain prerequisites for publishing real installers; the Portal does not build an installer or verify a remote file's hash by itself. The catalog starts empty, with no placeholder installer.

The Portal provides the account and DiscourseConnect SSO integration for the separately deployed SCADIVO Community. Licensing endpoints are part of this same Portal backend, with PostgreSQL in a separate private service. Product activation uses prepare → explicit confirmation → confirm; subsequent renewals use rotating device credentials. Checkout and payment webhooks are not implemented. See [integration status and remaining work](INTEGRATION_STATUS.md) and [the activation contract](portal/ACTIVATION_CONFIRMATION.md).

### Account access administration

Portal Owners and delegated Portal Admins can suspend or reactivate a non-owner account from the account list. Suspension preserves the profile and support history, revokes portal sessions on their next request, and blocks password login, password recovery and new DiscourseConnect sign-ins. Reactivation rotates the security stamp again, so a cookie saved before suspension cannot resume the account. Each action is recorded in the audit log. Owner accounts are protected from this control.

Administration → Accounts also grants or removes the global `Admin` role for SCADIVO employees. A recipient must have accepted their account, confirmed their email and be active. Both `Owner` and `Admin` can manage accounts, support, templates, FAQ, download releases and license entitlements, including delegating administration. Global staff administration is independent of customer organization roles. The Owner role cannot be granted, removed or suspended through these controls. Changes run in a transaction, are audited and rotate the target security stamp, invalidating previous Portal sessions. A removed administrator may sign in again with ordinary account access. This does not grant community/forum administrator or moderator flags.

### Publishing downloads

1. Produce and approve the official artifact through the product distribution process, then publish the file in the official GitHub release.
2. Open Administration → Downloads → New download; choose version and platform (Windows x64, Linux x64/ARM64, or Docker/OCI file bundle).
3. Enter the HTTPS release-file URL, the approved artifact's SHA-256 and notes in Portuguese, English and Spanish. The language selector preserves all three note drafts while editing. Incomplete entries can be saved as unpublished drafts.
4. Check publication and save to list it for authenticated accounts. Uncheck publication and save to withdraw it, preserving the catalog record. Changes and download requests are audited.

`GET/POST /portal/api/admin/downloads` and `PUT /portal/api/admin/downloads/{id}` require Portal Owner/Admin; writes require antiforgery. Drafts are excluded from the customer catalog and file endpoint. The API rejects other repositories/hosts, credentials, nonstandard ports, query/fragment URLs and invalid SHA-256 values. Version/platform is unique. Withdrawing an entry disables Portal discovery and redirects; a public GitHub asset remains accessible to someone who already knows its direct URL. This stage registers official files by link; it does not upload binaries to the VPS or grant public access to a private repository.

The account migration defaults existing accounts to not suspended. Cookie validation checks the current account and security stamp on every authenticated request, with one identity lookup per validation. Discourse retains its own sessions: suspending a portal account does not terminate a forum session that was already open; forum moderation must handle that session separately until administrative session synchronization is implemented.

### Pilot scope and remaining gates

- Self-service registration, email confirmation and password recovery are enabled in production as a limited public pilot by the owner's explicit decision. New accounts remain unable to sign in until the one-use confirmation link is completed. Recovery accepts an email address, gives the same response whether or not an eligible account exists, and sends a 24-hour one-use reset link only to confirmed accounts. Reset tokens are generated at delivery time, never stored in the outbox, and password reset invalidates the token. Download availability depends on approved published artifacts; community and licensing availability are configured per environment. Checkout remains unavailable.
- This is an explicit early-pilot decision while the broader #741 launch gates remain open: finalized privacy/terms and retention policy, offsite backup automation and monitoring/alerting. Do not describe this as a full commercial launch. Registration stays configurable in production Compose with a default of `false`; the private production environment opts in.
- Transactional SMTP delivery is deployed and enabled for administrative invitations, support-ticket receipts and staff replies. This branch adds branded HTML/text delivery for public-site contact receipts/team notifications and registration confirmation. The sender creates fresh one-use invitation/confirmation tokens at send time; it never stores them in the outbox or audit log. Historical `awaiting-transport` rows are deliberately not sent automatically.
- Public community: the Portal supports the separately deployed forum through SSO. Runtime availability comes from the configured session response; forum moderation, backup, monitoring and existing-session termination remain separate operational responsibilities.
- Commercial licensing: pricing, named/concurrent seats, machine limits/transfers, cancellation/refund and conversion rules are still product-owner decisions. Future remote license transport is HTTPS/TCP 443, with its own contract and coordinator review.

This is a limited public account-registration pilot, not a claim that all phases of #741 are complete. Account confirmation, indistinguishable recovery responses, confirmed-account eligibility, reset-token non-persistence and one-use password reset are covered by integration tests; the SMTP domain and a contact receipt were separately verified with real delivery. Legal/privacy review, retention, offsite backup automation and monitoring/alerting remain outstanding.

### Community single sign-on bridge

The portal implements the DiscourseConnect provider endpoint at `/portal/api/community/sso`. This endpoint is **disabled unless explicitly configured**. It accepts an incoming challenge only after verifying its HMAC-SHA256 signature and validating the callback as HTTPS on the configured forum host and exact `/session/sso_login` path. If the visitor is not signed in, it sends them to the portal login with the short-lived challenge preserved in the URL fragment; after sign-in (and profile completion if needed), the browser resumes the challenge. Only an accepted account with a confirmed email and, for members, a complete required profile can receive a signed SSO response. The endpoint echoes the one-use nonce and rate-limits by user or client IP.

Public registration asks the person to choose an available portal username; sign-in continues to accept either that username or the email address. DiscourseConnect sends the confirmed email for account identity, the stable internal portal user ID, that username, the selected display name and the UI locale. It does not send the password, organization/school, activity area, phone, address, birth date, marketing preference, memberships or portal roles. Discourse receives no administrator/moderator flags. Existing accounts whose portal username is still their email keep a pseudonymous `scadivo_…` community username until they choose a separate username in their portal profile. To mirror later portal changes on every community login, enable Discourse `auth_overrides_username` and `auth_overrides_name`; these settings also prevent users from changing those fields only in the forum. Keep email addresses private in the public forum UI.

To activate only after a forum exists and its authentication flow is ready, set these two values in the private portal deployment environment:

```text
PORTAL_COMMUNITY_ENABLED=true
PORTAL_COMMUNITY_DISCOURSE_URL=https://community.scadivo.com
```

The file `./secrets/community-sso/shared-secret` must contain at least 32 UTF-8 bytes; Compose mounts its directory read-only at `/run/secrets/community-sso` in the API container. Configure the same random secret in Discourse's `discourse_connect_secret` and set its `discourse_connect_url` to `https://scadivo.com/portal/api/community/sso`. Keep the secret out of Git, image layers, `.env`, command arguments and logs. Leave `PORTAL_COMMUNITY_ENABLED` unset/false until end-to-end tests and the production gates pass. With the flag off, the portal UI does not show the community link and the SSO endpoint returns not found.

The forum login link starts DiscourseConnect. A signed-in portal user returns to the forum without entering a second password; an unauthenticated user is sent through the portal login and then returned to the original forum challenge. The pending request is held only in the current tab for up to 25 minutes, without storing a password or portal profile fields. Never enable SSO before testing a separate administrative recovery path, because DiscourseConnect replaces local forum authentication. The portal cookie and the forum identity remain separate; this integration does not merge their databases.

### Transactional email implementation

The PR adds SMTP delivery for administrative invitations, support-ticket receipts, staff replies, public site contact receipts/team notifications, and registration confirmation. A new contact form submission enters two outbox messages; the worker is signalled to deliver promptly and drains available items. Temporary failures retry up to five attempts, and only fixed error categories are recorded. Contact content/address data is removed from outbox rows after success or terminal failure. Confirmation and invitation tokens are generated at send time and are not persisted. The initial outbox rows marked `awaiting-transport` remain untouched; only new notifications are queued after SMTP is enabled. Delivery is at-least-once, with a stable message ID for duplicate recognition if the process stops after SMTP acceptance but before the database commit.

Customer-facing automated messages are sent from `noreply@scadivo.com` without a `Reply-To` header. Both HTML and plain-text versions say not to reply and point to the public support form. The HTML card has a 640 px Outlook fallback plus a responsive 100%/640 px layout for other clients; regression tests assert both. The internal website-contact notification is the exception: its `Reply-To` points to the visitor so the team can respond directly.

The production sender is `noreply@scadivo.com`; the support mailbox is `support@scadivo.com`. The account's official settings use `mail.scadivo.com:465` with implicit TLS and SMTP authentication. A TLS handshake and certificate validation from the VPS succeeded on 23 October 2026; the certificate covered `*.scadivo.com`. An authenticated message sent from the VPS was delivered to Gmail; the received message showed SPF PASS, DKIM PASS (`scadivo.com`), DMARC PASS and TLS. No SPF or DMARC record was changed during this verification.

The noreply password is installed at `/opt/scadivo/portal/secrets/portal-mail/smtp-password`, owned by UID 1654 and mode 0400; the directory is UID 1654 and mode 0700. The directory is mounted read-only and `PORTAL_EMAIL_ENABLED=true` in the private environment. Do not put the password in `.env`, command arguments, terminal output, GitHub or repository files. The bounded worker sends supported invitation, support and confirmation templates. Before activation on 23 October 2026, the outbox contained zero rows; no queued or historical message was sent by activation. To confirm SMTP acceptance without restarting the portal or connecting to PostgreSQL, the following one-off command is available from `/opt/scadivo/portal`:

```sh
docker compose -f compose.yml exec -T scadivo-portal-api dotnet /app/Scadivo.Portal.dll --send-test-email
```

The command's test message goes to `support@scadivo.com`. A successful result means the SMTP server accepted it; separately confirm arrival in the mailbox and inspect SPF/DKIM results. The production sender was separately tested end to end as described above. Account confirmation and password recovery use the same authenticated sender; reset tokens are created just before send and are not written to outbox or audit records.

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

Tests require a **disposable** PostgreSQL database. Set `PORTAL_TEST_DATABASE`; the default is localhost port 15441 with the explicitly nonproduction test credential in the fixture. Never point tests at the production portal or demo. Tests cover antiforgery, secure cookies, roles, one-use invitation redemption, email confirmation before new-account login, non-enumerating recovery responses, confirmed-account eligibility, one-use password reset, reset-email token non-persistence, profile requirements, cross-user support isolation, FAQ/templates/audit, login lockout, community SSO challenge/signature/callback validation, logout and re-entry, rejection of unconfirmed/locked accounts, database persistence across a fresh host, and the online licensing activation/renewal rotation, retry idempotency, credential-reuse response, activation cap, password-change invalidation, and disabled-by-default signer.

The licensing API routes, `ESLEASE1` payload, activation/renewal contract, replay behavior and disabled-by-default signing boundary are documented in [`portal/LICENSING_API.md`](portal/LICENSING_API.md). Signing is disabled by default in a fresh deployment. The authenticated `/portal/api/licensing/status` endpoint reports runtime signer availability; do not infer the live setting from the Compose default or historical PR notes.

The Licenses tab groups entitlements by named application and shows license codes, capacities, terms, expiry and linked installation counts. Accounts can manage their organizations/applications and deactivate their own installations; organization management follows the API's Owner/Admin roles. Portal Owners have an additional Administration → Licenses page for explicitly creating, extending and revoking entitlements. These screens support PT-BR/EN/ES and report activation availability from the server. They do not process payments or activate a product from the browser.

Focused UI checks use synthetic API fixtures, independently of the product's browser tests:

```powershell
npm ci --prefix tests/browser
npx --prefix tests/browser playwright install chromium
npm test --prefix tests/browser
```

These checks cover member/organization/Portal Owner actions, typed entitlement payloads, confirmed revocation and a 360 px layout in all three languages. Real persistence, authorization and antiforgery remain covered by the PostgreSQL integration suite.

### Local Docker homologation

Use the isolated stack for a live, persistent preview of site and portal changes:

```powershell
docker compose -f deploy/compose.homolog.yml up -d --build
```

- Site and portal: `http://localhost:18084/` and `http://localhost:18084/portal/`.
- Local email inbox: `http://localhost:18083/`; confirmation messages are captured there and are never sent to real mailboxes.
- The isolated PostgreSQL and Data Protection key volumes are not published to the host. API, database and Mailpit use a private internal network; only the site and local Mailpit UI attach to the loopback-published front network. The stack has separate volumes, aliases and containers from production and the demo.
- The local preview alone routes email to Mailpit and uses a development-only HTTP cookie policy. Production stays HTTPS-only; public registration is currently opted in as a limited pilot by explicit owner decision, while the Compose default remains disabled so a fresh installation is closed unless deliberately enabled.
- A local-only owner account is bootstrapped as `scadivo.owner` with the test password shown in this homologation Compose file. Never reuse it outside localhost.
- `docker compose -f deploy/compose.homolog.yml down` stops the preview while preserving its volumes. Do not add `-v` unless you intend to erase only this homologation database and key ring.

To review licensing changes without replacing an existing local preview, use a separate Compose project and loopback ports. It gets its own database/key volumes, synthetic owner and Mailpit inbox; signing stays disabled:

```powershell
$env:SCADIVO_HOMOLOG_PORT = '18087'
$env:SCADIVO_HOMOLOG_MAIL_PORT = '18089'
docker compose -p scadivo-licensing-ui-homolog -f deploy/compose.homolog.yml up -d --build
```

Open `http://localhost:18087/portal/` and the local email inbox at `http://localhost:18089/`. The same command with `down` in place of `up -d --build` stops only that isolated preview and preserves its volumes. Port defaults remain 18084/18083 for the original preview.

## Current production layout

- Public site remains the existing EasyPanel-managed Swarm service `scadivo_site`.
- Site source remains `/etc/easypanel/projects/scadivo/site/code`; the tracked `site/` folder is the corresponding source in this package.
- Portal is a separate Docker Compose project `scadivo_portal` at `/opt/scadivo/portal`, managed over the existing authorized SSH connection. It is not a new EasyPanel service entry.
- API and PostgreSQL have **no host-published ports**. Only the existing website exposes the portal through `/portal/`, using the external `easypanel` overlay alias `scadivo_portal_api`.
- The API Compose service is named `scadivo-portal-api`. Compose automatically publishes its service name on the shared overlay: do not rename it to generic `api`, which shadows the demo API from the demo web container. The explicit site upstream alias remains `scadivo_portal_api`.
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

The Compose file mounts the optional SMTP password directory read-only. On the existing host, create that directory only if it is absent, owned by UID 1654 and mode 0700; do not rerun `initialize-host.sh`. Keep `PORTAL_EMAIL_ENABLED=false` until the noreply password is securely installed and the test message is accepted. Then enable the worker through the private `.env` and update only the portal API with the normal deployment procedure above.

Database migrations run before serving traffic. Review any subsequent destructive migration separately. An image rollback alone is not a database rollback. Initial schema is additive and does not touch the product database.

After deployment, run the read-only check `node deploy/check-public-services.mjs` from this directory. It verifies the site, portal session, demo API, optional sign-in configuration, anonymous Runtime permissions, and actual demo screen package without credentials. An HTTP 200 for `/demo` or the demo index alone does not verify its API connectivity. Check the portal API's shared-network DNS aliases for generic names before declaring the demo unaffected.

If Node requires the operating system's trusted CA store on Windows, run `node --use-system-ca deploy/check-public-services.mjs`; certificate verification remains enabled.

When upgrading the original portal deployment from service `api` to `scadivo-portal-api`, back up using the existing script before replacing the Compose/backup files, then run `docker compose -f compose.yml up -d --no-deps --remove-orphans --wait --wait-timeout 180 scadivo-portal-api`. This replaces only the portal API container and removes its old conflicting DNS alias; it preserves the portal database, keys, and all demo containers.

The shared DNS collision was corrected in production on 23 October 2026. Before the correction, the demo web container resolved `api` to the portal overlay address and could not connect on its own API's port 5080. After the portal service rename, it resolved its original private API again. Anonymous Runtime served 22 screens; existing-account sign-in and Engineering capabilities passed. Demo container IDs/start times were unchanged, and the portal backup succeeded with the renamed service. No demo configuration, Application, license, or Authority was replaced.

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
- Public portal live HTTPS UI: owner login, all tabs, FAQ search, template preview, PT/EN/ES, logout; desktop 1440 and widths 320/390/768; no page errors or horizontal overflow.
- Public marketing site: PT/EN/ES at widths 320/390/768/1024/1440, images/links/FAQ checked; no historical commercial branding or evaluation-time messaging.
- Anonymous APIs enforce authentication; database/API are not host-published; encrypted backup and disposable restore passed.
- Existing public site and demo routes remain operational. No product application import, demo restart or license change was made.
- Spanish release from commit `9ad523119ea1bbf229ffe81ee0fc5f0355423305` is live: site image `easypanel/scadivo/site:20261023-9ad52311`; portal API image `scadivo-portal:20261023-9ad52311`. The static site and portal API were updated without changing the demo service. An encrypted backup completed before the additive database migration; the portal database and persistent key ring were preserved. Production checks confirmed the Spanish public site/contact, an anonymous portal session, public registration and configured email delivery, the Spanish localization migration, six Spanish FAQ articles and nine Spanish email templates. The current signed-in owner's language remains their saved preference; Spanish is available through the language selector/profile preference. The embedded product Runtime itself was not changed.
- All checks passed for PR #749 commit `9ad5231`, including the full Chromium end-to-end workflow. PR #749 remains an unmerged draft; `main` was not changed. Outlook-specific markup is covered by the portal integration suite; visual confirmation in the owner's Outlook remains a manual follow-up.

Review and future integration remain through the draft PR and umbrella issue #741. Do not close #741 on this foundation package.
