# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION LOCKED / C12+C13+C14+C15+C17 ACCEPTED+INTEGRATED / C16 ISOLATED ACCEPTED BUT COMBINED COMPOSITION RED / C18 HOLD UNTIL C12–C17 CONVERGENCE / WAVE13 PAUSED**

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
- PR #212 stays DRAFT until final Wave14 acceptance and must not be merged to `main` without later authorization.

## 2. Product authority and integration

Repository:

`brunolrogerio-collab/EliteSCADA`

Coordinator branch:

`wave14/corrections-integration`

Draft integration PR:

`#212` — DRAFT / DO NOT MERGE TO `main`

Last full C10 converged freeze before the C11 correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Last fully combined-green pre-DEMO correction checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

This exact product checkpoint contains accepted C12+C13+C14+C15+C17 and passed:

- EliteSCADA CI #1337 / `33838725814`;
- Wave11 Runtime #265 / `33838725796`;
- Licensing #287 / `33838725824`;
- L3 #242 / `33838725850`;
- Interop #164 / `33838725805`.

Later coordinator/documentation commits do not supersede product-code validation authority.

A new Wave14 product freeze is not declared until C12-C18 converge and C10 convergence cycle 2 passes.

## 3. C11 status and binding Product Owner decisions

C11 Pass 2 is consolidated. **C11 implementation remains LOCKED.**

Binding decisions:

1. Popup X/Y is mandatory persisted authorable product functionality. Centered/shell-defined placement is not accepted mitigation.
2. The future living EEE Simulation must be buildable solely through normal generic EliteSCADA Engineering/Runtime tools.
3. No EEE-specific simulator service, special Driver, hidden DEMO runtime/package, private host logic or DEMO-only bypass is acceptable.
4. If normal generic tools are insufficient to construct the EEE Simulation, that is a PRODUCT GAP and must be corrected before C11 release.
5. Trend, Alarm Browser and Event Browser must be first-class visual objects insertable/configurable in authored Screens and Popups and persisted through Save/Publish/Activate/Runtime.
6. Trend must support persisted Multi-Pen configuration.
7. C11 is not released for implementation.

Canonical pre-DEMO correction plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

## 4. Accepted correction packages before C16 composition

### C13 — Canonical Simulation Quality

Accepted candidate `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`.

### C14 — First-Class Operational Events

Accepted candidate `70e311d3a359e7b00e8f0ed035478d51bb6ee001`.

Operational Event remains distinct from Alarm/Audit; Engineering schema authority is v16.

### C12 — Server Runtime Automation / Generic Simulation Authoring

Accepted candidate `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`.

C12 provides generic Active server automation/simulation mechanisms and does not introduce EEE-specific behavior.

### C17 — Internal Memory Authoring UX + Full Lifecycle E2E

Accepted candidate `6db4fb33f06159f108ca17ceca23a35ee158b228`.

The previously pending rerun completed successfully on integration composition SHA `74cf4713ce70d19516acce0f475561a95af2c737`; C17 itself is accepted.

### C15 — Embeddable Multi-Pen Trend HMI Object

Corrected accepted candidate `3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`.

The old false-green C15 candidate `2abee30a...` remains rejected historical evidence. The corrected candidate is included in the fully green `1dcd80a4...` checkpoint.

## 5. C16 — isolated accepted, combined composition blocked

Package:

**HMI Operational Command + Startup/Home Screen + Persisted Popup X/Y**

Accepted isolated candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

Its branch still points to that exact SHA. PR #245 is closed/merged into the integration branch; do not integrate it a second time.

Five isolated gates were green:

- EliteSCADA CI `33834427366`;
- Wave11 Runtime `33834427376`;
- Licensing `33834427368`;
- L3 `33834427362`;
- Interop `33834427361`.

Accepted architecture remains:

- `ExecuteCommand` invokes canonical `POST /api/commands/{id}/execute` by stable Command identity;
- backend remains authority for command existence, authorization, execution and audit;
- denial/failure propagates with no client TAG-write fallback;
- Screen, Dynamo and Popup command execution are exercised through Active Runtime;
- Startup/Home is persisted by Screen identity and fails closed when missing/unresolved;
- Popup X/Y is persisted in canonical logical HMI coordinates and respects Runtime scaling/stacking/pointer behavior.

Coordinator composed C16 against the post-C15 accepted integration authority at:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Combined exact product-code result:

- EliteSCADA CI #1338 / `33869678384` — SUCCESS;
- Licensing #288 / `33869678597` — SUCCESS;
- L3 #243 / `33869678581` — SUCCESS;
- Interop #165 / `33869678547` — SUCCESS;
- Wave11 Runtime #266 / `33869678407` — **FAILURE**.

### C16 composition failure diagnosis

The failure occurs in `tests-wave11/c17-memory-lifecycle.spec.ts`, while creating the C17 Server Memory TAG: the Source selector cannot find `memory.server.c17`.

This is not the earlier IEC-104 timing flake and is not currently eligible for blind rerun.

The retained Wave11 artifact/trace proves a deeper state/identity problem:

1. Working initially contains Built-in Simulation with id `40000000-0000-0000-0000-000000000001` and Wave11 Server Memory with its own stable id.
2. Normal Engineering creation of `Server Memory C17` sends a package where that new Source reuses `40000000-...0001`; Apply therefore replaces Built-in Simulation.
3. The next normal creation of `Client Memory C17` again reuses `40000000-...0001`; Apply then replaces `Server Memory C17`.
4. The later TAG Source selector cannot find `memory.server.c17` because canonical Working state genuinely no longer contains it.

The green checkpoint `1dcd80a4...` and red composition `607a60d0...` were both tested through PR #212 against the exact same `main` base:

`edbdf446ea657713bdc487be91bf10bfcd03c684`

Therefore `main` drift is excluded as the green-to-red cause.

C16 does not directly modify the generic Data Source editor, so final code ownership is still a root-cause question. Treat this as a **C16×C17 composition blocker exposing a generic authoring-state/identity defect**. Do not weaken C17's normal-UI lifecycle test, canonical Source identity, or backend import semantics merely to obtain green.

Last accepted combined product authority therefore remains `1dcd80a4...` until a new exact integration product head passes all five gates.

Issue #211 binding diagnostic record: comment `5541091621`.

## 6. C18 — HOLD / implementation not authorized

Package:

**Embeddable Alarm + Event Browser HMI Objects + related history i18n**

Product Owner / Coordinator decision superseding the earlier release:

**Do not release C18 for full implementation until C12–C17 are converged on one exact combined-green integration checkpoint.**

The C18 branch `wave14/c18-hmi-alarm-event-browsers` may remain parked at historical green SHA:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That SHA is no longer an active implementation authorization.

Do not advance/rebase C18 onto `607a60d0...` or later docs-only coordinator commits. The Coordinator will explicitly declare a new exact C18 base only after C12–C17 convergence.

Binding handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

Its current marker must read **HOLD / IMPLEMENTATION NOT AUTHORIZED**.

## 7. Immediate next-coordinator route

1. preserve C18 on HOLD;
2. keep PR #212 DRAFT;
3. isolate root cause/ownership of the C16×C17 duplicate Data Source identity defect;
4. require a bounded fix preserving normal Engineering UI, canonical identities and existing acceptance contracts;
5. validate any proposed fix on its exact candidate SHA under the owning package policy;
6. compose the fix into `wave14/corrections-integration` without rewriting history;
7. require all five combined gates green on one new exact product-code head;
8. declare C12–C17 converged only after that combined validation;
9. only then explicitly release C18 with a newly declared exact base;
10. after C18 acceptance/integration, run complete combined exact-head validation;
11. execute C10 convergence cycle 2;
12. freeze a new exact product-code SHA;
13. revalidate affected C11 findings;
14. only then consider explicit C11 implementation release;
15. Wave13 packaging/signing remains paused until final Wave14 acceptance.

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
7. `docs/WAVE14-C18-DEV-HANDOFF.md` before handling C18;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview history;
11. issue #205 / PR #207 only for paused Wave13 context.

Live GitHub wins over copied SHAs or conversation history.
