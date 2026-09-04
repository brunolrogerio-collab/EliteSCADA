# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION LOCKED / C12–C17 CONVERGED COMBINED GREEN AT 568e93eb / C18 RELEASED FROM EXACT BASE 568e93eb / WAVE13 PAUSED**

> GitHub is the official development memory. `PROJECT GOAL.md` governs permanent intent. `LAST CHANGE.md` is the fastest resume point. Revalidate live GitHub before acting; copied chat context is never product authority.

## 1. Permanent coordinator gates

The Coordinator owns integration, architecture preservation, validation sequencing and acceptance. Package DEVs do not merge directly to `main` and do not decide the accepted product baseline.

Permanent gates:

- backend is canonical authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- no Preview-only auth/licensing/runtime bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identity remains authoritative;
- lifecycle is `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- product changes require universal EliteSCADA CI plus impact-specific exact-head validation;
- diagnose failures before rerunning;
- do not weaken tests/security/contracts merely to obtain green;
- DEV delivery is not Coordinator acceptance;
- PR #212 stays DRAFT and must not be merged to `main` without later Product Owner authorization.

## 2. Current product authority

Repository:

`brunolrogerio-collab/EliteSCADA`

Coordinator branch:

`wave14/corrections-integration`

Integration PR:

`#212` — **OPEN / DRAFT / DO NOT MERGE TO main WITHOUT LATER PRODUCT OWNER AUTHORIZATION**

Last full C10 converged freeze before the C11 correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

### Accepted C12–C17 combined-green product checkpoint

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

Exact combined validation:

- EliteSCADA CI #1347 / `33882503111` — SUCCESS;
- Wave11 Active HMI Runtime #275 / `33882503088` — SUCCESS;
- Preview Licensing CI #297 / `33882503272` — SUCCESS;
- L3 Seven-Driver Lab #252 / `33882503050` — SUCCESS;
- Interop Lab Smoke #174 / `33882503053` — SUCCESS.

EliteSCADA CI #1347 initially hit the previously observed IEC-104 T2 timing test once. After diagnosis, only the failed backend job was rerun. It passed Backend build/test/smoke, then Chromium E2E passed. No product or test changes were made for that rerun.

Later Coordinator documentation commits may advance branch HEAD, but they do not replace `568e93...` as the exact C18 product base.

A new full Wave14 product freeze is not declared until C18 converges and C10 convergence cycle 2 passes.

## 3. C17 convergence correction closed

C16 composition at `607a60d0e930fc7080e09c0689c306c040c4ace6` exposed a real latent C17 Data Source new-mode race. The frontend could enter new mode before the fresh draft was installed, allowing immediate type selection to inherit stale entity identity/metadata and replace existing Sources.

The defect belonged to generic C17 Data Source authoring. Backend stable-id authority was not weakened and C16 contracts were not reopened.

Bounded correction branch:

`wave14/c17-convergence-datasource-new-race`

Accepted corrected candidate:

`705ac0a689d6ec4b3462f85e2082410f1d8b3baa` — `fix(c17): make new Data Source transition atomic`

Candidate exact gates:

- EliteSCADA CI #1346 / `33881471883` — SUCCESS;
- Wave11 Runtime #274 / `33881471880` — SUCCESS;
- Preview Licensing #296 / `33881471818` — SUCCESS;
- L3 #251 / `33881471893` — SUCCESS;
- Interop #173 / `33881471846` — SUCCESS.

The correction was composed into integration and the resulting product passed the five combined gates at `568e93...`.

**C17 convergence correction is ACCEPTED. C12–C17 are CONVERGED.**

Historical red/trace evidence remains valid history and must not be rewritten as a harmless flake.

## 4. Accepted package lineage through C17

- C12 — `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`;
- C13 — `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`;
- C14 — `70e311d3a359e7b00e8f0ed035478d51bb6ee001`;
- C15 corrected — `3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`;
- C16 — `6d9f971eb469d931ca56becff4d240088725f37a`;
- C17 historical — `6db4fb33f06159f108ca17ceca23a35ee158b228` plus convergence correction `705ac0a689d6ec4b3462f85e2082410f1d8b3baa`.

Combined authority through C17 is `568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`.

## 5. C18 — RELEASED / implementation authorized

Package:

**W14-C18 — Embeddable Alarm + Event Browser HMI Objects**

Branch:

`wave14/c18-hmi-alarm-event-browsers`

The branch was held at historical checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That HOLD is closed.

### Exact authorized C18 development base

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

Before changing product code, C18 DEV must move/start the package branch from this exact product base. Do not use the old parked SHA, `main`, or later documentation-only Coordinator commits as C18 product authority.

Binding handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

C18 must deliver first-class authored Alarm Browser and Event Browser HMI objects usable in Screen and Popup through normal Engineering Save/Publish/Activate/Active Runtime lifecycle, preserving C14 event semantics, backend authorization and pt-BR/en/es visible chrome.

No DEMO-only route/page, hidden package editing, private runtime wiring, EEE-specific logic or client-side authorization substitute is accepted.

Exact C18 candidate acceptance requires:

- EliteSCADA CI;
- Wave11 Active HMI Runtime;
- Preview Licensing CI;
- L3 Seven-Driver Lab;
- Interop Lab Smoke;
- package-specific authored Screen/Popup browser lifecycle tests.

## 6. C11 remains locked

C11 Pass 2 remains **IMPLEMENTATION LOCKED**.

Binding Product Owner decisions:

1. Popup X/Y is persisted authorable product functionality;
2. future living EEE Simulation must be buildable solely with normal generic EliteSCADA Engineering/Runtime tools;
3. no EEE-specific simulator service, special Driver, hidden DEMO runtime/package, private host logic or DEMO-only bypass;
4. if ordinary tools cannot build the simulation, that is a PRODUCT GAP before C11;
5. Trend, Alarm Browser and Event Browser are first-class authored HMI objects in Screen/Popup;
6. Trend supports persisted Multi-Pen;
7. C11 is not released for implementation.

## 7. Next coordinator route

1. keep PR #212 OPEN/DRAFT;
2. receive C18 DEV candidate based exactly on `568e93...`;
3. review exact C18 diff/architecture and deterministic package coverage;
4. require all five candidate gates green;
5. compose accepted C18 into integration without rewriting accepted history;
6. require all five combined gates green on the resulting exact product head;
7. execute C10 convergence cycle 2;
8. freeze a new exact product-code SHA;
9. revalidate affected C11 findings against that freeze;
10. only then consider explicit C11 release;
11. Wave13 packaging/signing remains paused until final Wave14 accepted bytes.

## 8. Preview and release boundaries

Issue #208 / PR #210 remain Preview infrastructure/history, not product authority.

Wave13 issue #205 / PR #207 remains paused. Do not sign stale pre-Wave14 product bytes.

PR #212 remains DRAFT and must not be merged to `main` without later Product Owner authorization.

## 9. Mandatory resume protocol

Read/revalidate in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 consolidated audit + HMI clarification on `wave14/c11-pass2-product-gap-audit`;
7. `docs/WAVE14-C18-DEV-HANDOFF.md` while C18 is active;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview history;
11. issue #205 / PR #207 only for paused Wave13 context.

Live GitHub wins over copied SHAs or conversation history.
