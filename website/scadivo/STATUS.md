# SCADIVO portal — issue #741

Owner: this Codex conversation. Product licensing and SCADA Authority are outside this implementation.

## Current package

- Phase 1: public bilingual presentation and unified demo are deployed.
- Phase 2: deployed independent PostgreSQL database, persistent application keys, same-origin `/portal` hosting, private networking, encrypted daily backup and a successful disposable database restoration check.
- Phase 3 pilot: deployed independent portal accounts, first-party cookie sessions, administrator-issued invitations, account profile/password and editable PT/EN message templates. The current PR adds a bounded SMTP outbox worker for invitations and support notices; it is not yet deployed or enabled because the private noreply credential and real delivery confirmation are still pending.
- Phase 5 foundation: deployed searchable FAQ, private support conversations and an authenticated release catalog (empty until approved official artifacts exist).

Public registration, email confirmation/recovery, public community, payments and product-side license renewal remain subsequent packages. The SMTP implementation must be deployed and verified before it is described as active. Public registration requires the separate email and legal decisions recorded in #741. No demo Authority/user database is copied into this portal.

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
