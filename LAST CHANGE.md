# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C11 IMPLEMENTATION LOCKED / C12+C13+C14+C15+C17 HISTORICALLY ACCEPTED+INTEGRATED / C16 ISOLATED ACCEPTED / C17 INTERMITTENT DATASOURCE RACE CORRECTION ACTIVE / C18 HOLD UNTIL C12–C17 COMBINED GREEN / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. DEV delivery is not Coordinator acceptance. Diagnose a red gate before rerun. Documentation-only commits do not redefine product-code authority.

## 1. Integration authority

Coordinator branch:

`wave14/corrections-integration`

Draft integration PR:

`#212` — **OPEN / DRAFT / DO NOT MERGE TO main WITHOUT LATER PRODUCT OWNER AUTHORIZATION**

Last full C10 converged freeze before this correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Last fully combined-green pre-DEMO correction checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That product checkpoint contains accepted C12+C13+C14+C15+C17 and passed all five gates:

- EliteSCADA CI #1337 / `33838725814` — SUCCESS;
- Wave 11 Active HMI Runtime #265 / `33838725796` — SUCCESS;
- Preview Licensing CI #287 / `33838725824` — SUCCESS;
- L3 Seven-Driver Lab #242 / `33838725850` — SUCCESS;
- Interop Lab Smoke #164 / `33838725805` — SUCCESS.

No new Wave14 product freeze has been declared. C12–C17 must first converge on one new exact combined-green product SHA after the C17 race is corrected; only then may C18 be explicitly released. After C18 convergence, C10 convergence cycle 2 must establish a new exact product-code freeze before C11 can be reconsidered.

## 2. C11 remains locked

**KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions remain:

1. persisted authorable Popup X/Y is mandatory before C11 release; centered/shell placement is not an accepted substitute;
2. the living deterministic EEE Simulation must be buildable solely through normal generic EliteSCADA tools;
3. no EEE-specific simulator service, special Driver, hidden/historical DEMO runtime/package, private host hook or DEMO-only bypass is acceptable;
4. if normal product tools cannot build the EEE Simulation, that is a PRODUCT GAP to correct before C11 release;
5. Trend, Alarm Browser and Event Browser must be first-class authored HMI objects insertable into Screen/Popup;
6. Trend must support persisted Multi-Pen authoring;
7. C11 is not released for implementation.

## 3. Accepted package history before current convergence correction

- C13 — Canonical Simulation Quality: `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`;
- C14 — First-Class Operational Events: `70e311d3a359e7b00e8f0ed035478d51bb6ee001`;
- C12 — Server Runtime Automation / Generic Simulation Authoring: `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`;
- C17 — Internal Memory Authoring UX + Full Lifecycle E2E: historical accepted candidate `6db4fb33f06159f108ca17ceca23a35ee158b228`;
- C15 — Embeddable Multi-Pen Trend HMI Object: corrected accepted candidate `3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`.

C17's earlier rerun completed successfully. The old C15 false-green candidate `2abee30a...` remains rejected historical evidence.

The fully green combined authority for those packages remains `1dcd80a4...`.

## 4. C16 — isolated accepted, composition exposed C17 race

C16 package:

**HMI Operational Command + Startup/Home Screen + Persisted Popup X/Y**

Accepted isolated candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

PR #245 is already closed/merged into integration; do not integrate C16 again.

Five isolated gates were green:

- EliteSCADA CI `33834427366`;
- Wave 11 Active HMI Runtime `33834427376`;
- Preview Licensing `33834427368`;
- L3 Seven-Driver Lab `33834427362`;
- Interop Lab Smoke `33834427361`.

Accepted C16 architecture remains:

- stable Command identity through canonical `/api/commands/{id}/execute` backend authority;
- backend authorization/execution/audit authority;
- no client TAG-write fallback;
- persisted Startup/Home Screen identity;
- persisted logical Popup X/Y.

Coordinator composition product-code SHA:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Original combined result:

- EliteSCADA CI #1338 / `33869678384` — SUCCESS;
- Preview Licensing CI #288 / `33869678597` — SUCCESS;
- L3 Seven-Driver Lab #243 / `33869678581` — SUCCESS;
- Interop Lab Smoke #165 / `33869678547` — SUCCESS;
- Wave 11 Active HMI Runtime #266 / `33869678407` — **FAILURE**.

The green Wave11 #265 and red Wave11 #266 both used the same PR #212 `main` base:

`edbdf446ea657713bdc487be91bf10bfcd03c684`

Therefore `main` drift is excluded.

## 5. Root cause isolated — latent C17 Data Source new-mode race

Wave11 #266 failed in `tests-wave11/c17-memory-lifecycle.spec.ts` because the later TAG editor could not find `memory.server.c17`.

Retained Playwright trace proves canonical Working state genuinely lost that Source before the lookup:

1. Built-in Simulation exists with id `40000000-0000-0000-0000-000000000001` and `metadata.system=true`.
2. `Nova Data Source` changes `selectedIdentity` into new mode immediately.
3. Fresh `draft = newDataSourceDraft()` is installed only later by `useEffect`.
4. During that render/effect window, `isNew === true` while `draft` can still be the previously selected persisted Source.
5. Immediate type selection calls `switchDataSourceType(draft, type)`, whose normal spread preserves the stale Source `id`/metadata.
6. `Server Memory C17` inherits `40000000-...0001` and replaces Built-in Simulation on Apply.
7. `Client Memory C17` then inherits the same id and replaces Server Memory C17.
8. TAG authoring later cannot list `memory.server.c17` because it no longer exists.

`newDataSourceDraft()` itself does not assign an id. Backend stable-identity resolution is behaving correctly for an incoming explicit id and must not be weakened.

The same frontend race exists on historical accepted C17 HEAD `6db4fb33...`. C16 changed the Wave11 sequence/timing and exposed the latent C17 authoring defect; C16 contracts are not being reopened.

### Intermittency confirmed, not excused

A later documentation-only integration HEAD:

`147756ac65b452e95085a4c21bec976545d29bb7`

contained no product-code correction after `607a60d0...`, yet automatically triggered all five PR #212 workflows against synthetic merge ref `7a1134a8a9c93b276e615541d182e8646017b27f` and all completed successfully:

- EliteSCADA CI #1344 / `33878949767` — SUCCESS, including Chromium E2E;
- Wave 11 Active HMI Runtime #272 / `33878949817` — SUCCESS, **11/11 tests actually executed** including C16 bootstrap, C17 Memory, C15 Trend and C16 Operational Runtime;
- Preview Licensing CI #294 / `33878949850` — SUCCESS;
- L3 Seven-Driver Lab #249 / `33878949788` — SUCCESS;
- Interop Lab Smoke #171 / `33878949680` — SUCCESS.

Because product code was unchanged, this later green run confirms the race is timing-dependent. It does **not** erase the concrete corrupted Working state captured in Wave11 #266 and does **not** establish a new accepted product checkpoint. Classification is binding:

**REAL PRODUCT RACE / INTERMITTENT TRIGGER / NOT AN ACCEPTABLE CI FLAKE.**

Issue #211 evidence record: comment `5541337330`.

## 6. C17 convergence correction — DEV active on bounded branch

Ownership decision: **return bounded post-integration correction to C17 Memory/Data Source authoring**.

Historical accepted C17 branch/PR remain preserved.

Correction branch:

`wave14/c17-convergence-datasource-new-race`

Exact correction product base:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Current correction-branch HEAD:

`23da99aebbb93d51b84462d8568c7281642c9c39`

That commit adds only:

`docs/WAVE14-C17-CONVERGENCE-DATASOURCE-NEW-RACE-HANDOFF.md`

No product-code fix has yet been published on that branch as of this synchronization.

Required correction invariants:

- entering New Data Source must establish a fresh draft atomically/synchronously from the user's perspective;
- no prior stable id/system metadata/settings/secrets may leak into a new entity except explicit defaults of the selected type;
- existing Source editing must still preserve its intended identity/metadata;
- solution remains generic/catalog-driven, with no Memory/DEMO/fixed-GUID special case;
- deterministic regression must cover immediate `New Data Source -> choose type` interaction so scheduler timing cannot hide the defect;
- existing real C17 normal-Engineering lifecycle coverage remains intact;
- no sleeps, hidden package JSON or weakened backend identity semantics.

Exact corrected candidate must pass all five gates. Coordinator then composes it preserving history and requires all five combined gates green before declaring C12–C17 converged.

Coordination records:

- issue #211 C18 HOLD / initial blocker: comment `5541091621`;
- issue #211 root-cause ownership: comment `5541152530`;
- historical C17 PR #249 CHANGES REQUIRED: comment `5541149386`;
- issue #211 correction branch record: comment `5541167404`;
- issue #211 intermittent-race five-gate evidence: comment `5541337330`.

## 7. Repository hygiene completed for historical Wave14 validation surfaces

Per `docs/CI-VALIDATION-POLICY.md`, completed validation-only PRs were closed **without merge** after their integration/evidence lineage was recorded. Commits, workflow runs, artifacts and comments remain historical evidence.

Closed validation-only surfaces in this synchronization:

- #216 — C05;
- #228 — C08;
- #227 — C09;
- #232 — C10 historical convergence validation;
- #239 — C13;
- #240 — C14;
- #243 — C15;
- #244 — C17.

For Wave14, the intentionally active PR surfaces now remain:

- #212 — Coordinator correction integration, OPEN/DRAFT;
- #210 — temporary Test Preview infrastructure.

C10 Convergence Cycle 2 is a future **new cycle** after C18 convergence and must not reuse the historical C10 validation PR as product authority.

## 8. C18 — HOLD / NOT RELEASED

Product Owner decision:

**C18 must not be released for full implementation until C12–C17 are converged on one exact combined-green integration checkpoint.**

Parallel C18 DEV context may exist, but implementation authorization is inactive.

Existing branch:

`wave14/c18-hmi-alarm-event-browsers`

Live branch remains parked at:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That SHA is historical parked state, **not active implementation authorization**.

Do not advance/rebase C18 until the Coordinator explicitly declares a new exact C18 base after C12–C17 convergence.

Binding handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

Current marker: **HOLD / IMPLEMENTATION NOT AUTHORIZED**.

## 9. Immediate route

1. keep C18 HOLD and PR #212 DRAFT;
2. wait for C17 DEV to publish a product-code candidate on `wave14/c17-convergence-datasource-new-race`;
3. revalidate the exact candidate diff/architecture and five workflow runs;
4. require deterministic coverage of the previously timing-dependent transition and reject scheduler-luck acceptance;
5. reject any test/security/identity weakening or timing-only workaround;
6. compose accepted C17 correction into integration without rewriting history;
7. require all five combined gates green on one exact product-code head;
8. declare C12–C17 converged only then;
9. explicitly release C18 with a newly declared exact base only after convergence;
10. after C18 acceptance/integration, run full combined validation;
11. execute C10 convergence cycle 2 and freeze a new exact product SHA;
12. revalidate affected C11 findings;
13. only then consider explicit `RELEASE C11 IMPLEMENTATION`;
14. Wave13 issue #205 / PR #207 remain paused until final Wave14 acceptance.

## 10. Boundaries still in force

- PR #212 remains DRAFT and must not be merged to `main` without later Product Owner authorization.
- C11 implementation remains LOCKED.
- C18 remains HOLD.
- Issue #208 / PR #210 are Preview infrastructure/history, not product authority.
- Wave13 issue #205 / PR #207 remain paused.
- Do not package/sign stale pre-Wave14 product bytes.
