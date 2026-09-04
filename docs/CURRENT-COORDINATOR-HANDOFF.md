# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION LOCKED / LAST ACCEPTED COMBINED GREEN = C12+C13+C14+C15+C17 AT 1dcd80a4 / C16 ISOLATED ACCEPTED / C17 INTERMITTENT DATASOURCE RACE CORRECTION ACTIVE / C18 HOLD UNTIL C12–C17 COMBINED GREEN / WAVE13 PAUSED**

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
- a later green run does not erase a concrete product defect proven on the same code;
- PR #212 stays DRAFT and must not be merged to `main` without later Product Owner authorization.

## 2. Product authority and integration

Repository:

`brunolrogerio-collab/EliteSCADA`

Coordinator branch:

`wave14/corrections-integration`

Integration PR:

`#212` — **OPEN / DRAFT / DO NOT MERGE TO main WITHOUT LATER PRODUCT OWNER AUTHORIZATION**

Last full C10 converged freeze before the C11 correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Last accepted fully combined-green pre-DEMO correction checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

It contains accepted C12+C13+C14+C15+C17 and passed:

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

Canonical correction plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

## 4. Accepted package history before current convergence correction

### C13 — Canonical Simulation Quality

Accepted candidate `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`.

### C14 — First-Class Operational Events

Accepted candidate `70e311d3a359e7b00e8f0ed035478d51bb6ee001`.

Operational Event remains distinct from Alarm/Audit; Engineering schema authority is v16.

### C12 — Server Runtime Automation / Generic Simulation Authoring

Accepted candidate `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`.

C12 provides generic Active server automation/simulation mechanisms and does not introduce EEE-specific behavior.

### C17 — Internal Memory Authoring UX + Full Lifecycle E2E

Historical accepted candidate `6db4fb33f06159f108ca17ceca23a35ee158b228`.

Its earlier pending rerun completed successfully on integration composition SHA `74cf4713ce70d19516acce0f475561a95af2c737`. A later C16 composition exposed a latent C17 Data Source authoring race described below; historical evidence remains preserved, but C12–C17 convergence is not closed until the corrective candidate is accepted and combined green.

### C15 — Embeddable Multi-Pen Trend HMI Object

Corrected accepted candidate `3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`.

The old false-green C15 candidate `2abee30a...` remains rejected historical evidence.

## 5. C16 — isolated accepted; composition exposed C17 defect

Accepted isolated candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

Its branch still points to that SHA. PR #245 is already closed/merged into integration; do not integrate it a second time.

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

Coordinator composed C16 at product-code SHA:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Original combined exact product-code result:

- EliteSCADA CI #1338 / `33869678384` — SUCCESS;
- Licensing #288 / `33869678597` — SUCCESS;
- L3 #243 / `33869678581` — SUCCESS;
- Interop #165 / `33869678547` — SUCCESS;
- Wave11 Runtime #266 / `33869678407` — **FAILURE**.

The green checkpoint `1dcd80a4...` and red C16 composition `607a60d0...` were both tested through PR #212 against the exact same `main` base `edbdf446ea657713bdc487be91bf10bfcd03c684`; `main` drift is excluded.

## 6. Root cause — C17 new Data Source frontend state race

Wave11 #266 failed in `web/scada-web/tests-wave11/c17-memory-lifecycle.spec.ts` because the later TAG Source selector could not find `memory.server.c17` after canonical Working state had already lost it.

Retained Playwright trace proves:

1. Built-in Simulation exists with id `40000000-0000-0000-0000-000000000001` and `metadata.system=true`.
2. `Nova Data Source` changes `selectedIdentity` into new mode immediately.
3. Fresh `draft = newDataSourceDraft()` is installed only later by `useEffect`.
4. In that render/effect window, `isNew === true` while `draft` may still represent the previously selected persisted Source.
5. Immediate type selection invokes `switchDataSourceType(draft, type)` and preserves stale entity-owned fields through the normal spread.
6. New `Server Memory C17` inherits id `40000000-...0001` and replaces Built-in Simulation on Apply.
7. New `Client Memory C17` inherits the same id and replaces Server Memory C17.
8. TAG authoring later cannot list `memory.server.c17` because it is genuinely gone.

`newDataSourceDraft()` itself has no id. Backend `DataSourceEngineeringHandler` correctly resolves explicit stable ids and must not be weakened to compensate for a frontend race.

The same race exists on historical accepted C17 HEAD `6db4fb33...`. C16 changed sequence/timing and exposed the latent defect; C16's accepted command/startup/popup contracts are not reopened.

Ownership decision: **bounded corrective package belongs to C17 Memory/Data Source authoring.**

### Intermittency proven by later full green run

Documentation-only integration HEAD `147756ac65b452e95085a4c21bec976545d29bb7` contained no product-code fix after `607a60d0...` but automatically triggered the five PR #212 workflows against synthetic merge ref `7a1134a8a9c93b276e615541d182e8646017b27f`:

- EliteSCADA CI #1344 / `33878949767` — SUCCESS, including Chromium E2E;
- Wave11 Runtime #272 / `33878949817` — SUCCESS, with **11/11 tests actually executed** including C16 startup bootstrap, C17 Memory lifecycle, C15 Trend and C16 Operational Runtime;
- Preview Licensing #294 / `33878949850` — SUCCESS;
- L3 #249 / `33878949788` — SUCCESS;
- Interop #171 / `33878949680` — SUCCESS.

This later green result does not establish product convergence. The product code is unchanged from the run that produced a retained, concrete corrupted Working state. It confirms scheduler/timing dependence and strengthens the classification:

**REAL PRODUCT RACE / INTERMITTENT TRIGGER / NOT AN ACCEPTABLE CI FLAKE.**

Binding evidence record: issue #211 comment `5541337330`.

## 7. C17 convergence correction — bounded DEV package

Preserve historical accepted C17 branch/PR.

Correction branch:

`wave14/c17-convergence-datasource-new-race`

Exact correction product base:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Current branch HEAD at this synchronization:

`23da99aebbb93d51b84462d8568c7281642c9c39`

Its parent is exact red product SHA `607a60d0...` and it currently adds only coordinator handoff `docs/WAVE14-C17-CONVERGENCE-DATASOURCE-NEW-RACE-HANDOFF.md`.

No product-code correction has been published yet as of this synchronization.

Required invariants:

- entering New Data Source must establish a fresh draft atomically/synchronously from the user's perspective;
- a new draft must not inherit prior canonical id, system metadata, settings or secret references except explicit defaults of its selected type;
- existing Source editing must preserve intended stable identity/metadata;
- implementation must remain generic/catalog-driven, with no Memory, C17, DEMO or fixed-GUID special case;
- deterministic regression must force/immediately exercise `New Data Source -> choose type`, not rely on scheduler luck;
- existing real C17 normal-Engineering Save/Publish/Activate/Runtime lifecycle coverage remains;
- no sleeps, hidden package JSON, test weakening or relaxed backend identity semantics.

Corrected exact candidate must pass EliteSCADA CI, Wave11 Runtime, Preview Licensing, L3 and Interop. Because the correction branch starts from exact product `607a60d0...`, Wave11 candidate validation must prove the previously failing C16 -> C17 sequence passes with the deterministic race regression present.

After exact-candidate review, Coordinator composes without rewriting history and requires all five combined gates green on one new integration product-code SHA. Only then declare C12–C17 converged.

Coordination records:

- issue #211 `5541091621` — C18 HOLD + initial blocker;
- issue #211 `5541152530` — root cause/ownership;
- PR #249 `5541149386` — post-integration CHANGES REQUIRED;
- issue #211 `5541167404` — correction branch/handoff;
- issue #211 `5541337330` — later five-gate green confirms intermittency but does not clear defect.

## 8. Repository hygiene

Per `docs/CI-VALIDATION-POLICY.md`, completed validation-only PRs were closed **without merge** once integration/evidence lineage was recorded. Historical commits, runs, artifacts and comments remain preserved.

Closed validation-only Wave14 surfaces in this synchronization:

- #216 — C05;
- #228 — C08;
- #227 — C09;
- #232 — historical C10 convergence validation;
- #239 — C13;
- #240 — C14;
- #243 — C15;
- #244 — C17.

The intentionally active Wave14 PR surfaces are now:

- #212 — Coordinator integration, OPEN/DRAFT;
- #210 — temporary Test Preview infrastructure.

C10 Convergence Cycle 2 will be a new post-C18 convergence cycle, not reuse historical PR #232 as product authority.

## 9. C18 — HOLD / implementation not authorized

Product Owner decision supersedes the earlier administrative release:

**Do not release C18 for full implementation until C12–C17 are converged on one exact combined-green integration checkpoint.**

Existing branch `wave14/c18-hmi-alarm-event-browsers` remains parked at historical green SHA:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That SHA is historical parked state, not active implementation authorization.

Do not advance or rebase C18 until the Coordinator explicitly declares a new exact C18 base after C12–C17 convergence.

Binding handoff: `docs/WAVE14-C18-DEV-HANDOFF.md`.

Current marker: **HOLD / IMPLEMENTATION NOT AUTHORIZED**.

## 10. Immediate next-coordinator route

1. keep C18 HOLD and PR #212 DRAFT;
2. revalidate `wave14/c17-convergence-datasource-new-race` when DEV reports a candidate;
3. review exact diff for atomic new-draft state, generic behavior and deterministic regression coverage;
4. verify all five exact-candidate workflow runs;
5. reject timing-only/test-security-identity workarounds;
6. compose accepted correction into integration without rewriting history;
7. require all five combined gates green on one exact product-code head;
8. declare C12–C17 converged only after that combined validation;
9. explicitly release C18 with a newly declared exact base only then;
10. after C18 acceptance/integration, run complete combined exact-head validation;
11. execute C10 convergence cycle 2;
12. freeze a new exact product-code SHA;
13. revalidate affected C11 findings;
14. only then consider explicit C11 implementation release;
15. Wave13 packaging/signing remains paused until final Wave14 acceptance.

## 11. Preview and release boundaries

Issue #208 / PR #210 remain Preview infrastructure/history, not product authority.

Wave13 issue #205 / PR #207 remains paused. Do not sign stale pre-Wave14 product bytes.

PR #212 remains DRAFT and must not be merged to `main` without later Product Owner authorization.

## 12. Mandatory resume protocol

Read/revalidate in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 consolidated audit + HMI clarification on `wave14/c11-pass2-product-gap-audit`;
7. `docs/WAVE14-C17-CONVERGENCE-DATASOURCE-NEW-RACE-HANDOFF.md` while handling the current blocker;
8. `docs/WAVE14-C18-DEV-HANDOFF.md` before handling C18;
9. `docs/CI-VALIDATION-POLICY.md`;
10. live issue #211 and draft PR #212;
11. issue #208 / PR #210 only for Preview history;
12. issue #205 / PR #207 only for paused Wave13 context.

Live GitHub wins over copied SHAs or conversation history.
