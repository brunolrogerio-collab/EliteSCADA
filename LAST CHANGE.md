# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C11 IMPLEMENTATION LOCKED / C12–C17 CONVERGED COMBINED GREEN / C18 RELEASED FROM EXACT BASE 568e93eb / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. DEV delivery is not Coordinator acceptance. Diagnose a red gate before rerun. Documentation-only commits do not redefine product-code authority.

## 1. Current integration authority

Coordinator branch:

`wave14/corrections-integration`

Draft integration PR:

`#212` — **OPEN / DRAFT / DO NOT MERGE TO main WITHOUT LATER PRODUCT OWNER AUTHORIZATION**

Last full C10 converged freeze before this correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

### New accepted C12–C17 combined-green product checkpoint

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

This exact product-code SHA contains the accepted C12+C13+C14+C15+C16+C17 correction composition and passed all five required combined gates:

- EliteSCADA CI #1347 / `33882503111` — **SUCCESS**;
- Wave 11 Active HMI Runtime #275 / `33882503088` — **SUCCESS**;
- Preview Licensing CI #297 / `33882503272` — **SUCCESS**;
- L3 Seven-Driver Lab #252 / `33882503050` — **SUCCESS**;
- Interop Lab Smoke #174 / `33882503053` — **SUCCESS**.

EliteSCADA CI #1347 initially failed the known IEC-104 T2 timing test `Adapter_T2FlushesPendingReceiveAcknowledgementWithoutFaultingSession`. Diagnosis established unchanged affected test/product lineage and prior identical transient evidence. The failed backend job alone was rerun once. The rerun passed Build, Test and Runtime smoke, then downstream Chromium E2E also passed. No product or test code was changed to obtain the green result.

`568e93...` is therefore the binding C18 development base even when later Coordinator documentation commits advance the integration branch HEAD.

## 2. C17 convergence correction accepted

Historical C17 candidate remains:

`6db4fb33f06159f108ca17ceca23a35ee158b228`

C16 composition at `607a60d0e930fc7080e09c0689c306c040c4ace6` exposed the latent C17 Data Source new-mode stale-draft identity race. The retained Wave11 trace proved that immediate `New Data Source -> choose type` could preserve the previously selected Source id/metadata before the fresh draft effect ran, causing later Sources to replace one another.

Backend stable identity semantics were not weakened. C16 contracts were not reopened.

Bounded C17 corrective candidate:

`705ac0a689d6ec4b3462f85e2082410f1d8b3baa` — `fix(c17): make new Data Source transition atomic`

It passed all five exact-candidate gates:

- EliteSCADA CI #1346 / `33881471883` — SUCCESS;
- Wave 11 Active HMI Runtime #274 / `33881471880` — SUCCESS;
- Preview Licensing CI #296 / `33881471818` — SUCCESS;
- L3 Seven-Driver Lab #251 / `33881471893` — SUCCESS;
- Interop Lab Smoke #173 / `33881471846` — SUCCESS.

The correction was then composed into integration and the combined product was validated at `568e93...` as recorded above.

Classification closed:

**C17 REAL PRODUCT RACE CORRECTED / C12–C17 CONVERGENCE ACCEPTED.**

Historical evidence remains on issue #211 and C17 PR #249; do not erase or reinterpret the earlier red state as a harmless flake.

## 3. Accepted package lineage through C17

- C12 — Server Runtime Automation / Generic Simulation Authoring: `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`;
- C13 — Canonical Simulation Quality: `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`;
- C14 — First-Class Operational Events: `70e311d3a359e7b00e8f0ed035478d51bb6ee001`;
- C15 — Embeddable Multi-Pen Trend HMI Object: corrected candidate `3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`;
- C16 — HMI Operational Command + Startup/Home + Popup X/Y: `6d9f971eb469d931ca56becff4d240088725f37a`;
- C17 — historical Memory authoring candidate `6db4fb33...` plus accepted bounded convergence correction `705ac0a689d6ec4b3462f85e2082410f1d8b3baa`.

Combined accepted authority through C17 is not any individual package SHA. It is:

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

## 4. C18 released

Package:

**Embeddable Alarm + Event Browser HMI Objects**

Branch:

`wave14/c18-hmi-alarm-event-browsers`

The branch was intentionally parked at historical checkpoint `1dcd80a4df448ced3a228d3f5b9057fa26ef547c` while C16/C17 convergence was unresolved.

That HOLD is now closed.

**C18 IMPLEMENTATION IS AUTHORIZED FROM EXACT PRODUCT BASE:**

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

The C18 DEV must move/start its package branch from that exact product base before product changes. Do not use the old parked SHA, `main`, or later documentation-only Coordinator commits as product authority.

Binding package handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

C18 acceptance still requires its exact candidate to pass EliteSCADA CI, Wave11 Active HMI Runtime, Preview Licensing CI, L3 Seven-Driver Lab, Interop Lab Smoke and package-specific authored Screen/Popup browser lifecycle coverage.

## 5. C11 remains locked

**KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions remain:

1. persisted authorable Popup X/Y is mandatory;
2. the living deterministic EEE Simulation must be buildable solely through normal generic EliteSCADA tools;
3. no EEE-specific simulator service, special Driver, hidden/historical DEMO runtime/package, private host hook or DEMO-only bypass is acceptable;
4. missing ordinary capability is a PRODUCT GAP to correct before C11 release;
5. Trend, Alarm Browser and Event Browser are first-class authored Screen/Popup HMI objects;
6. Trend supports persisted Multi-Pen authoring;
7. C11 is not released for implementation.

## 6. Immediate route

1. keep PR #212 OPEN/DRAFT;
2. C18 DEV starts from exact product base `568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`;
3. review C18 exact diff/architecture and package-specific regression coverage;
4. require all five exact-candidate gates green;
5. compose accepted C18 into integration without rewriting accepted package history;
6. require complete combined exact-head validation after composition;
7. execute C10 convergence cycle 2;
8. freeze a new exact product-code SHA;
9. revalidate affected C11 findings against that freeze;
10. only then consider explicit `RELEASE C11 IMPLEMENTATION`;
11. Wave13 issue #205 / PR #207 remain paused until final Wave14 acceptance.

## 7. Boundaries still in force

- PR #212 remains DRAFT and must not be merged to `main` without later Product Owner authorization.
- C11 implementation remains LOCKED.
- C18 is released only from exact base `568e93...`; release is not acceptance of future C18 code.
- Issue #208 / PR #210 are Preview infrastructure/history, not product authority.
- Wave13 issue #205 / PR #207 remain paused.
- Do not package/sign stale pre-Wave14 product bytes.
