# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION LOCKED / C12+C13+C14+C15+C17 ACCEPTED+INTEGRATED / C16 ISOLATED CANDIDATE ACCEPTED, COMPOSED HEAD RED IN WAVE11 / C18 RELEASED FROM LAST GREEN BASE / WAVE13 PAUSED**

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
- PR #212 stays DRAFT until final Wave14 acceptance.

## 2. Product authority and integration

Repository:

`brunolrogerio-collab/EliteSCADA`

Coordinator branch:

`wave14/corrections-integration`

Draft PR:

`#212` — open / DRAFT / DO NOT MERGE TO `main`

Last full C10 converged freeze before the C11 correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Last fully combined-green pre-DEMO correction checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

This exact checkpoint contains accepted C12+C13+C14+C15+C17 and passed:

- EliteSCADA CI #1337 / `33838725814`;
- Wave11 Runtime #265 / `33838725796`;
- Licensing #287 / `33838725824`;
- L3 #242 / `33838725850`;
- Interop #164 / `33838725805`.

Later docs-only `[skip ci]` commits do not supersede product-code validation authority.

A new Wave14 product freeze is not declared until C12-C18 converge and C10 convergence cycle 2 passes.

## 3. C11 status and binding Product Owner decisions

C11 Pass 2 is consolidated. **C11 implementation remains LOCKED.**

Read on `wave14/c11-pass2-product-gap-audit`:

- `docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md`;
- `docs/WAVE14-C11-PASS2-HMI-OBJECTS-PRODUCT-OWNER-CLARIFICATION.md`.

Binding decisions:

1. Popup X/Y is mandatory product functionality before C11 release. Centered-only Popup is not accepted mitigation.
2. The future EEE Simulation must be buildable solely through normal generic EliteSCADA Engineering/Runtime tools. No EEE-specific simulator service, hidden package manipulation, historical DEMO internals or private runtime bypass is acceptable.
3. Trend, Alarm Browser and Event Browser must be first-class visual objects insertable/configurable in authored Screens and Popups and persisted through Save/Publish/Activate/Runtime.
4. Trend must support persisted Multi-Pen configuration.
5. The later PLC variant should preserve the same conceptual HMI/TAG architecture and primarily change Source/address mapping.

Canonical pre-DEMO correction plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

## 4. Accepted correction packages before C16 composition

### C13 — Canonical Simulation Quality

Accepted candidate `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`.

Server-authoritative qualified samples preserve Source/TAG ownership. Ordinary writes remain Good; Client Visual cannot spoof physical Driver quality; Bad/Stale/Unavailable propagate canonically.

### C14 — First-Class Operational Events

Accepted candidate `70e311d3a359e7b00e8f0ed035478d51bb6ee001`.

Operational Event remains separate from Alarm/Audit, Active-revision authoritative, durable and queryable through `operational.events`. Engineering schema authority is v16.

### C12 — Server Runtime Automation / Generic Simulation Authoring

Accepted candidate `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`.

C12 consumes C13 and provides Active server script lifecycle/Timer/event automation plus qualified Server Memory publication through public generic contracts. No EEE-specific simulator was introduced.

### C17 — Internal Memory Authoring UX + Full Lifecycle E2E

Accepted candidate `6db4fb33f06159f108ca17ceca23a35ee158b228`.

Normal Server/Client Memory authoring plus Save/Publish/Activate/Runtime is covered.

### C15 — Embeddable Multi-Pen Trend HMI Object

Corrected accepted candidate:

`3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`

C15 provides first-class `core.trend`, Screen/Popup authoring, native structured Multi-Pen configuration, canonical live/history data paths, quality-aware gaps, shared visual rendering and pt-BR/en/es.

The accepted combined post-C15 product authority is `1dcd80a4...` with all five gates green as listed above.

## 5. C16 — isolated candidate accepted, combined composition currently red

Package:

**HMI Operational Command + Startup/Home Screen + Persisted Popup X/Y**

Accepted isolated candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

Five exact-candidate gates are green:

- EliteSCADA CI `33834427366`;
- Wave11 Runtime `33834427376`;
- Licensing `33834427368`;
- L3 `33834427362`;
- Interop `33834427361`.

Accepted architecture:

- `ExecuteCommand` invokes `/api/commands/{id}/execute` by stable Command identity;
- backend remains authority for command existence, authorization, execution and audit;
- denial/failure propagates with no client TAG-write fallback;
- Screen, Dynamo and Popup command execution is validated in Active Runtime;
- Startup/Home is persisted by Screen identity and fails closed when missing/unresolved;
- Popup X/Y is persisted in canonical logical HMI coordinates and respects Runtime scaling/stacking/pointer behavior.

Coordinator composed C16 against the post-C15 accepted integration authority at:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Commit:

`merge(w14): compose accepted C16 into post-C15 integration`

Combined result on that exact product-code head:

- EliteSCADA CI #1338 / `33869678384` — SUCCESS;
- Licensing #288 / `33869678597` — SUCCESS;
- L3 #243 / `33869678581` — SUCCESS;
- Interop #165 / `33869678547` — SUCCESS;
- Wave11 Runtime #266 / `33869678407` — **FAILURE**.

Failure evidence:

`tests-wave11/c17-memory-lifecycle.spec.ts`

At `createMemoryTag()` line 191, the Source selector lookup for `memory.server.c17` returned `null`:

`expect(sourceOption).toBeTruthy()` -> `Received: null`.

The C16 startup bootstrap and generic lifecycle tests passed before this failure. Six later Wave11 tests did not run.

Do **not** rerun blindly and do **not** call `607a60d0...` an accepted combined checkpoint. Diagnose whether the cause is package state mutation, Source visibility/UI refresh, or Wave11 ordering/isolation introduced by C16 composition. Preserve C12-C17 and the accepted C16 contracts while fixing the actual defect.

Last accepted combined authority therefore remains:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

until a new exact head passes all five combined gates.

## 6. C18 — RELEASED / IMPLEMENTATION AUTHORIZED

Package:

**Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 Event and C15 visual-object prerequisites are satisfied.

Exact authorized development base:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Branch already exists at that exact green base:

`wave14/c18-hmi-alarm-event-browsers`

Binding handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

C18 must provide first-class Alarm Browser and Event Browser objects insertable into Screen/Popup, persisted through normal lifecycle, pt-BR/en/es, backend-authorized alarm actions, C14 `operational.events` consumption, bounded generic query behavior and independent configured instances.

C18 implementation is **AUTHORIZED**. It targets `wave14/corrections-integration`, never `main`.

Important current rule: **do not rebase C18 onto `607a60d0...` or later docs-only coordinator commits merely because they are newer.** The authorized base is deliberately the last combined-green product authority `1dcd80a4...`. C16/C18 composition belongs to the Coordinator after C18 candidate acceptance.

## 7. Immediate next-coordinator route

1. revalidate live integration branch, PR #212, issue #211, C18 branch and all exact workflow states;
2. diagnose Wave11 #266 on composed C16 `607a60d0...` before any rerun;
3. correct only the actual composition/product/test-isolation defect;
4. require all five combined gates green on a new exact integration head;
5. in parallel receive/review C18 from frozen base `1dcd80a4...`;
6. require C18 exact-candidate five-gate green plus package-specific browser evidence;
7. accept/integrate C18 and resolve C16/C18 composition preserving C14/C15/C17/C16 contracts;
8. rerun complete combined exact-head validation;
9. execute C10 convergence cycle 2;
10. freeze a new exact product-code SHA;
11. revalidate affected C11 findings and remaining browser validation;
12. only then explicitly release C11 implementation;
13. finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`;
14. build the canonical living EEE Simulation through normal corrected product tools;
15. later validate conceptual mapping against physical Modbus PLC;
16. after final Wave14 acceptance resume Wave13 packaging/signing.

## 8. Preview and release boundaries

Issue #208 / PR #210 remain Preview infrastructure/history, not product authority.

Wave13 issue #205 / PR #207 remains paused. Do not sign stale pre-Wave14 product bytes.

PR #212 remains DRAFT.

## 9. Mandatory resume protocol

Read and revalidate in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 consolidated audit + HMI clarification on `wave14/c11-pass2-product-gap-audit`;
7. `docs/WAVE14-C18-DEV-HANDOFF.md` if handling C18;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview history;
11. issue #205 / PR #207 only for paused Wave13 context.

Live GitHub wins over copied SHAs or conversation history.
