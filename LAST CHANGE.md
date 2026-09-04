# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 FROZEN GREEN / C11 PASS 2 CONSOLIDATED + IMPLEMENTATION LOCKED / C13+C14 COORDINATOR-ACCEPTED AND INTEGRATED / C12 CHANGES REQUIRED / C15+C17 EXACT-HEAD VALIDATION ACTIVE / C16 WAVE11 FAILURE DIAGNOSED + DEV CORRECTION REQUIRED / C18 STILL BLOCKED / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only `[skip ci]` commits may move coordinator branches without redefining the last validated product-code SHA.

## 1. Product authority and integration surface

Wave 14 integration branch:

`wave14/corrections-integration`

Draft PR:

`#212`

PR #212 remains **DRAFT** and is not authorized to merge to `main`.

Frozen C10 product-code SHA audited by C11 Pass 2:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Exact five-gate post-C10 validation on that SHA remains the last full product freeze:

- EliteSCADA CI #1273 / `33795337274`: SUCCESS;
- Wave 11 Active HMI Runtime #203 / `33795337288`: SUCCESS;
- Preview Licensing CI #225 / `33795337280`: SUCCESS;
- L3 Seven-Driver Lab #180 / `33795337252`: SUCCESS;
- Interop Lab Smoke #102 / `33795337270`: SUCCESS.

The current integration branch now contains product changes from accepted C13 and C14, so `97eefd8...` is no longer the current integration code content. A new product freeze will be established only after the pre-DEMO correction cycle converges and exact-head validation completes.

## 2. C11 remains implementation-locked

Canonical C11 Pass 2 result lives on:

`wave14/c11-pass2-product-gap-audit`

State:

**PASS 2 CONSOLIDATED / KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions remain:

1. persisted authorable Popup X/Y is mandatory before C11 release;
2. the future living deterministic EEE Simulation must be buildable by an integrator using normal generic EliteSCADA product tools;
3. no EEE-specific simulator service, historical DEMO runtime, hidden package mutation, direct DOM/React bypass, private Driver/host hook, auth bypass or licensing bypass is acceptable.

## 3. Pre-DEMO package authority

Canonical plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

Common DEV release base for C12-C17:

`2607e03d5445eefe1f434495d0ee81136c6cd220`

Packages:

- C12 — Server Runtime Automation / Generic Simulation Authoring;
- C13 — Canonical Simulation Quality Contract;
- C14 — First-Class Operational Events;
- C15 — Embeddable Multi-Pen Trend HMI Object;
- C16 — HMI Operational Command + Startup Screen + mandatory Popup X/Y;
- C17 — Internal Memory Authoring UX + Full Lifecycle E2E;
- C18 — Embeddable Alarm + Event Browser HMI Objects + history i18n, dependent package.

No package branch merges directly to `main`.

## 4. C13 — ACCEPTED + INTEGRATED

Package branch:

`wave14/c13-simulation-quality-contract`

Accepted exact candidate:

`b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`

Coordinator review confirmed:

- qualified samples are server-authoritative only;
- publisher rejects RuntimeClient-owned/non-authoritative providers;
- a qualified Source may publish only TAGs it owns;
- ordinary Memory writes remain `Good`;
- physical Driver quality authority is not exposed to Client Visual;
- `Bad`, `Stale`, `Unavailable` use the canonical `TagValue` pipeline;
- no EEE-specific simulation logic was added.

Exact-head validation evidence on the accepted candidate includes SUCCESS for EliteSCADA CI, Wave 11 Active HMI Runtime, Preview Licensing, L3 Seven-Driver Lab and Interop Lab Smoke.

Integration PR:

`#242`

Merged to `wave14/corrections-integration` with expected-head protection.

Merge SHA:

`84b12f7795b6ab5c63a4433ee7818ed418c442a1`

Validation-only PR #239 remains **DO NOT MERGE TO MAIN**.

## 5. C14 — ACCEPTED + INTEGRATED

Package branch:

`wave14/c14-operational-events`

Accepted exact candidate:

`70e311d3a359e7b00e8f0ed035478d51bb6ee001`

Coordinator review confirmed:

- Operational Event is first-class and distinct from Alarm and Security Audit;
- Event definitions follow canonical Engineering import/export and persistence;
- only definitions from a successfully Active revision may emit;
- occurrences are immutable runtime events;
- durable history uses dedicated append-only PostgreSQL storage;
- protected Historical Query dataset is `operational.events`;
- the contract is application/protocol-neutral and is the backend authority for future C18 Event Browser.

Exact-head validation evidence on the accepted candidate includes SUCCESS for EliteSCADA CI, Wave 11 Active HMI Runtime, Preview Licensing, L3 Seven-Driver Lab and Interop Lab Smoke.

Coordinator intake PR:

`#235`

Merged to `wave14/corrections-integration` with expected-head protection.

Merge SHA:

`3d6389069015996a5a68fbb055f620560da9e839`

Validation-only PR #240 remains **DO NOT MERGE TO MAIN**.

C14 backend/Event prerequisite for C18 is now satisfied. C18 is still blocked on the accepted embeddable-object baseline from C15 and publication of an exact C18 implementation base.

## 6. C12 — CHANGES REQUIRED BEFORE ACCEPTANCE

Package branch:

`wave14/c12-server-runtime-automation`

Delivered candidate reviewed:

`739c21f97f74ae9c72894590cf818f07e9b7481a`

The Server Runtime host/scheduler/lifecycle design is accepted in principle: Active revision ownership, bounded script execution, timers, TagChanged, cancellation, failure isolation and no EEE-specific simulator are directionally correct.

Blocking correction discovered by Coordinator after C13 integration:

**normal Server Script authoring must be able to publish qualified Server Memory samples through the canonical C13 contract.**

The current C12 sandbox is value-only. That is insufficient for the required generic Simulation authoring because C11 must be able to create deliberate `Bad`, `Stale` and `Unavailable` scenarios using normal product tools.

Coordinator request recorded on validation PR #241, comment `5534563805`:

- consume C13 `QualifiedSourceSample` / `IQualifiedSourceProvider` / `ServerAuthoritativeSamplePublisher` rather than fork it;
- expose only bounded Server Script capability for declared active `ServerMemoryTag` dependencies;
- preserve physical Driver quality authority;
- preserve ordinary writes as `Good`;
- prove Server Script -> qualified Server Memory -> cache -> alarm/realtime/historian/binding;
- rerun exact-head gates on the corrected candidate.

C12 is **not accepted/integrated yet**.

## 7. C15 — IMPLEMENTATION COMPLETE / EXACT-HEAD VALIDATION ACTIVE

Branch:

`wave14/c15-hmi-trend-multipen`

Current candidate observed:

`01d98d88b688008078b429d4d58e68055d3f13a2`

The implementation provides first-class `core.trend` authoring/rendering for Screen and Popup, native persisted Multi-Pen configuration, protected historian/live data paths, normal lifecycle persistence, multiple instances and pt-BR/en/es chrome.

Coordinator opened validation-only PR:

`#243`

It targets `main` **only to trigger exact-head workflows** and must never merge to `main`.

C15 is not accepted until exact-head CI/Runtime gates are green and final scope review completes.

## 8. C16 — IMPLEMENTATION COMPLETE / WAVE11 FAILURE DIAGNOSED

Branch:

`wave14/c16-hmi-command-navigation-popup`

Current exact validation head observed:

`8461ec8edfc46bc791151f21e745b7f7e340a267`

C16 implements the intended canonical contracts for:

- authored `ExecuteCommand` using backend Operational Command authority;
- explicit persisted Startup/Home Screen identity;
- persisted authorable Popup X/Y in logical HMI coordinates;
- Runtime placement/scaling and browser E2E coverage.

Validation-only PR:

`#238`

Wave 11 Active HMI Runtime run `33827272852` failed deterministically in C16 operational Runtime acceptance:

- Playwright timed out waiting for button `C16 DYNAMO START`;
- failure location: `tests-wave11/c16-operational-runtime.spec.ts:182`;
- startup/bootstrap/base lifecycle passed before the failure;
- four downstream tests did not run.

Coordinator diagnosed the failure and recorded correction instructions on PR #238, comment `5534576462`.

Do not rerun unchanged. DEV must inspect the uploaded Playwright report/trace, determine why the Dynamo control is absent/inaccessible, correct product or stale test contract without weakening requirements, publish a new candidate and revalidate.

C16 is **not accepted/integrated yet**.

## 9. C17 — IMPLEMENTATION COMPLETE / EXACT-HEAD VALIDATION ACTIVE

Branch:

`wave14/c17-memory-authoring-e2e`

Current candidate observed:

`15dda2845370d71a0b2cff3fce0303be57259b07`

C17 keeps production change narrow and adds the missing proof:

- capability-aware Address UI hides network address semantics for source providers;
- Server Memory and Client Memory are created through normal Data Source/TAG UI;
- Save -> Publish -> Activate -> Runtime lifecycle;
- Server Memory read/write/retention/historian;
- Client Memory session isolation;
- no hidden provider ID or package-edit shortcut.

Coordinator opened validation-only PR:

`#244`

It targets `main` **only to trigger exact-head workflows** and must never merge to `main`.

C17 is not accepted until exact-head CI/Runtime gates are green.

## 10. C18 release gate

C18 remains implementation-blocked.

C14 Event backend contract is now accepted/integrated.

Before publishing an exact C18 base, Coordinator still requires:

1. C15 accepted/integrated or the common embeddable visual-object baseline otherwise explicitly frozen;
2. an exact integration SHA containing those prerequisites;
3. a new `wave14/c18-hmi-alarm-event-browsers` branch created from that exact SHA;
4. C18 handoff updated to consume C14 `operational.events` and the accepted visual-object/lifecycle contracts, without parallel frontend-only schemas.

## 11. Immediate coordinator route

1. monitor C15 #243 and C17 #244 exact-head workflows;
2. monitor C16 correction after diagnosed Wave11 failure;
3. monitor C12 correction consuming integrated C13 quality contract;
4. integrate C15/C17 only after green exact-head evidence and conflict review;
5. once C15 prerequisite is accepted, publish exact C18 base and release C18 DEV;
6. integrate corrected C16 and C12 after their own exact-head gates;
7. compose overlapping Wave11 Playwright project/dependency changes rather than dropping one package's lane;
8. run combined exact-head regression on the integration branch;
9. execute C10 convergence cycle 2;
10. freeze a new exact product-code SHA;
11. revalidate affected C11 findings plus real-browser acceptance;
12. only after blockers are cleared issue explicit `RELEASE C11 IMPLEMENTATION`;
13. build the EEE Simulation exclusively through corrected normal Engineering tools;
14. later validate the same conceptual application against physical Modbus PLC;
15. only after Wave14 acceptance resume Wave13 packaging/signing.

## 12. Mandatory resume reading

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 consolidated audit + HMI Product Owner clarification on `wave14/c11-pass2-product-gap-audit`;
7. C12-C17 package handoffs for any package being reviewed;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview history;
11. issue #205 / PR #207 only for paused Wave13 context.

Then revalidate live GitHub before acting.
