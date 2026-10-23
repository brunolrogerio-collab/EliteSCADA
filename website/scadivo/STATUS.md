# SCADIVO portal — issue #741

Owner: this Codex conversation. Product licensing and SCADA Authority are outside this implementation.

## Current package

- Phase 1: public bilingual presentation and unified demo are deployed.
- Phase 2: deployed independent PostgreSQL database, persistent application keys, same-origin `/portal` hosting, private networking, encrypted daily backup and a successful disposable database restoration check.
- Phase 3 pilot: deployed independent portal accounts, first-party cookie sessions, administrator-issued invitations, account profile/password and editable PT/EN message templates. Bounded SMTP outbox delivery for invitations and support notices is deployed and enabled; the noreply credential is mounted read-only and authenticated delivery passed SPF/DKIM/DMARC checks.
- Phase 5 foundation: deployed searchable FAQ, private support conversations and an authenticated release catalog (empty until approved official artifacts exist).

Public registration and email confirmation are enabled in production as a limited account pilot by the owner's explicit decision on 23 October 2026. New users must confirm their email before sign-in. Password recovery is not implemented; the login screen directs users to support. Finalized privacy/terms and retention policy, recovery, offsite backup automation and monitoring/alerting remain open #741 launch work. Public community, payments and product-side license renewal remain subsequent packages. No demo Authority/user database is copied into this portal.

## Validation profile

- Build the isolated portal project and container.
- Integration: anonymous/member/admin access; antiforgery; invitation redemption; cross-account support isolation; login lockout; persistence across restart.
- Browser: owner sign-in, FAQ, support, templates, invitations, and PT/EN layouts at desktop/mobile widths.
- Deployment: HTTPS on `/portal`, no public database port, verified backup restoration, existing site/demo remain accessible.

## Foundation decisions

- Hostname: `scadivo.com`, path `/portal`; same-origin access avoids the third-party-cookie failure previously encountered in the demo.
- External transport: HTTPS/TCP 443. A future `license-api.scadivo.com` will have its own contract and no database exposure.
- Identity: ASP.NET Core Identity, unrelated to SCADA Security Authority.
- Global portal roles: Owner and Member. Organization membership is separate from global administration.
- PostgreSQL: independent database/container/volume; never the demo's TimescaleDB volume.
- Distribution: do not invent installers or expose private repository artifacts. The official release catalog starts empty.
- Signing: no product private signing key is included or deployed.

Integration is by draft PR; no self-merge or change to product contracts.

## Evidence

Published at `https://scadivo.com/portal/` on 23 October 2026. Real PostgreSQL integration tests passed. Live HTTPS owner login and portal UI passed in PT/EN at desktop/mobile widths, with no page errors/overflow. The public site regression passed ten language/viewport combinations. Encrypted backup restoration recovered one owner and six FAQ articles into a disposable database; the active database was not replaced. API and database have no host-published ports. See README for paths, operation, rollback and remaining launch gates.

The portal API was updated on 23 October 2026 to image `20261023-0e00d54a5c13`; only the API container was recreated. The database remained healthy, the new backup passed a disposable restore check, and a post-deployment public check passed with the demo's 22 screens and anonymous Runtime available. The email outbox had zero rows at activation; public registration and recovery remain disabled.
