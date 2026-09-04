# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 LAST FULL FREEZE GREEN / C11 PASS 2 CONSOLIDATED + IMPLEMENTATION LOCKED / C13+C14 ACCEPTED+INTEGRATED / C12 CHANGES REQUIRED / C15 BUILD FAILURE DIAGNOSED / C17 WAVE11 TEST FAILURE DIAGNOSED / C16 CORRECTED HEAD WITH 3 OF 5 GATES GREEN + INTEGRATION CONFLICT PENDING COMPOSITION / C18 BLOCKED / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only `[skip ci]` commits may move coordinator branches without redefining a product-code freeze.

## 1. Integration authority

Coordinator integration branch:

`wave14/corrections-integration`

Draft integration PR:

`#212`

PR #212 remains **DRAFT** and is not authorized to merge to `main`.

Last fully converged/frozen product-code SHA before pre-DEMO corrections:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Its five post-C10 gates were green:

- EliteSCADA CI #1273 / `33795337274`;
- Wave 11 Active HMI Runtime #203 / `33795337288`;
- Preview Licensing CI #225 / `33795337280`;
- L3 Seven-Driver Lab #180 / `33795337252`;
- Interop Lab Smoke #102 / `33795337270`.

The integration branch now contains accepted C13 and C14 product changes. Therefore `97eefd8...` remains historical C10/C11-audit authority, not the current integration code content. A new exact product freeze will only be declared after C12-C18 convergence and combined validation.

## 2. C11 gate remains locked

C11 Pass 2 branch:

`wave14/c11-pass2-product-gap-audit`

Canonical result:

**PASS 2 CONSOLIDATED / KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions:

1. persisted authorable Popup X/Y is mandatory before C11 release;
2. the living deterministic EEE Simulation must be buildable from normal generic EliteSCADA tools;
3. no EEE-specific simulator service, historical DEMO runtime, hidden `.escadapkg` mutation, direct DOM/React bypass, private Driver/host hook, authorization bypass or licensing bypass is acceptable.

Canonical correction plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

Common original DEV base:

`2607e03d5445eefe1f434495d0ee81136c6cd220`

## 3. C13 — ACCEPTED + INTEGRATED

Package:

**C13 — Canonical Simulation Quality Contract**

Accepted exact candidate:

`b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`

Coordinator review confirmed server-authoritative qualified samples, Source/TAG ownership enforcement, ordinary writes remaining `Good`, no Client Visual physical-Driver quality spoofing, and canonical propagation of `Bad` / `Stale` / `Unavailable`.

Five exact-head gates were green.

Integration PR #242 merged with expected-head protection.

C13 integration merge SHA:

`84b12f7795b6ab5c63a4433ee7818ed418c442a1`

Validation-only PR #239 remains **DO NOT MERGE TO MAIN**.

## 4. C14 — ACCEPTED + INTEGRATED

Package:

**C14 — First-Class Operational Events**

Accepted exact candidate:

`70e311d3a359e7b00e8f0ed035478d51bb6ee001`

Coordinator review confirmed:

- Event is first-class and distinct from Alarm/Audit;
- canonical Engineering persistence/import-export;
- Active-revision authority;
- immutable runtime occurrence;
- dedicated durable PostgreSQL history;
- protected Historical Query dataset `operational.events`;
- application/protocol-neutral contract suitable for C18.

Five exact-head gates were green.

Coordinator intake PR #235 merged with expected-head protection.

C14 integration merge SHA:

`3d6389069015996a5a68fbb055f620560da9e839`

Validation-only PR #240 remains **DO NOT MERGE TO MAIN**.

C14 backend Event prerequisite for C18 is satisfied.

## 5. C12 — CHANGES REQUIRED

Package branch:

`wave14/c12-server-runtime-automation`

Reviewed delivered candidate:

`739c21f97f74ae9c72894590cf818f07e9b7481a`

The Server Runtime host/scheduler/lifecycle architecture is accepted in principle, including Active revision ownership, timers, TagChanged, bounded execution, cancellation and failure isolation.

Blocking correction:

**normal Server Scripts must be able to publish qualified Server Memory samples through the accepted C13 contract.**

C11 must be able to author `Bad`, `Stale` and `Unavailable` process scenarios using normal tools. Value-only Server Script writes are insufficient.

Required DEV correction is recorded on validation PR #241, comment `5534563805`:

- consume C13 `QualifiedSourceSample` / `IQualifiedSourceProvider` / `ServerAuthoritativeSamplePublisher`;
- restrict the capability to declared active Server Memory dependencies;
- validate canonical `TagQuality` values;
- preserve physical Driver quality authority;
- preserve ordinary writes as `Good`;
- prove qualified Server Script -> Server Memory -> cache -> alarm/realtime/historian/binding;
- no private quality fork and no EEE-specific simulator logic;
- rerun exact-head gates on the corrected candidate.

C12 is not accepted or integrated.

## 6. C15 — BUILD FAILURE DIAGNOSED / DEV CORRECTION REQUIRED

Package branch:

`wave14/c15-hmi-trend-multipen`

Candidate under validation:

`01d98d88b688008078b429d4d58e68055d3f13a2`

Validation-only PR:

`#243`

EliteSCADA CI #1295 / `33827646239`: **FAILURE** in Web build.

Backend build/test/runtime smoke passed. Chromium E2E was skipped because Web build failed.

Deterministic compile failures:

- `trendAuthoringModel.ts`: six TS7006 implicit-`any` errors for `explicitCount` / `selectionCount`;
- `trendVisualModel.ts`: self-referenced `color` initializer, TS7022 + TS2448.

Coordinator diagnosis/correction request:

PR #243 comment `5534591484`.

Do not rerun unchanged. DEV must correct types/initializer without weakening TypeScript strictness or the canonical Trend contract, publish a new exact head and revalidate.

C15 is the remaining visual prerequisite for publishing C18 base.

## 7. C17 — WAVE11 FAILURE DIAGNOSED / TEST CORRECTION REQUIRED

Package branch:

`wave14/c17-memory-authoring-e2e`

Candidate under validation:

`15dda2845370d71a0b2cff3fce0303be57259b07`

Validation-only PR:

`#244`

Wave 11 Active HMI Runtime #225 / `33827655527`: **FAILURE** in the new C17 Memory lane.

The failure is currently classified as deterministic test-selector ambiguity, not product Memory failure:

`page.getByLabel('Somente leitura')`

matched both the intended checkbox and an unrelated select. The test failed before the C17 Save/Publish/Activate/Runtime assertions could execute.

Playwright report artifact:

`9920620171`

Coordinator correction request:

PR #244 comment `5534613449`.

DEV must use an unambiguous public UI locator, preserve the acceptance scope, publish a new exact candidate and rerun. Do not modify product labels merely to satisfy the test.

C17 is not accepted/integrated.

## 8. C16 — PRODUCT FAILURE FIXED / VALIDATION CONTINUES / INTEGRATION COMPOSITION REQUIRED

Package branch:

`wave14/c16-hmi-command-navigation-popup`

The earlier Wave11 Runtime failure on `8461ec8...` was diagnosed at the Dynamo command interaction. DEV corrected it and published:

`2a4377cfa93c6020bc89882f6d2b3088cefd030b`

On this corrected exact head, current confirmed gates are:

- Wave 11 Active HMI Runtime #223 / `33827619319`: **SUCCESS**;
- Preview Licensing #245 / `33827619337`: **SUCCESS**;
- Interop Lab Smoke #122 / `33827619361`: **SUCCESS**;
- EliteSCADA CI #1294 / `33827619323`: still running at this checkpoint;
- L3 Seven-Driver Lab #200 / `33827619326`: still running at this checkpoint.

Coordinator opened intake PR #245 against `wave14/corrections-integration` to test composition. GitHub reports it non-mergeable against the current C13+C14 integration.

This is a real integration task, not permission to discard upstream changes.

When C16 is composed, preserve:

- C14 Engineering schema **v16**;
- C14 Operational Event contracts;
- C16 `ExecuteCommand` / StartupScreenId / Popup X/Y;
- all Wave11 lanes from accepted packages;
- backend Operational Command authorization/audit authority.

Coordinator strategy: accept/integrate corrected C17 and C15 first, then synchronize/compose C16 once against the resulting integration authority rather than repeatedly rebasing it after each frontend package.

C16 is not integrated yet.

## 9. C18 — BLOCKED

Package:

**C18 — Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 Event prerequisite is satisfied.

C18 remains implementation-blocked until C15/common embeddable visual-object contract is accepted/integrated and Coordinator publishes an exact C18 implementation base.

C18 must consume:

- C14 `operational.events` backend/query contract;
- accepted canonical visual-object authoring/persistence/runtime lifecycle;
- normal Screen/Popup placement and Property Inspector contracts;
- pt-BR/en/es chrome;
- backend authority for ACK/shelve/interactive operations.

No frontend-only parallel Event schema is permitted.

## 10. Current coordination order

1. C15 DEV fixes deterministic TypeScript build errors and revalidates;
2. C17 DEV fixes the ambiguous Playwright locator and revalidates;
3. C12 DEV consumes the accepted C13 qualified-quality contract and revalidates;
4. C16 completes its remaining isolated exact-head gates;
5. accept/integrate C17 and C15 after green evidence;
6. freeze/publish exact C18 base and release C18 DEV;
7. compose C16 once against the then-current integration authority and revalidate the composed head;
8. integrate corrected C12 after C13-aligned validation;
9. implement/accept/integrate C18;
10. run combined exact-head integration regression;
11. execute C10 convergence cycle 2;
12. freeze a new exact product-code SHA;
13. revalidate affected C11 findings and real-browser acceptance;
14. only then issue explicit `RELEASE C11 IMPLEMENTATION`;
15. build the canonical EEE Simulation solely through corrected normal Engineering tools;
16. later validate the same conceptual application against the physical Modbus PLC;
17. only after Wave14 acceptance resume Wave13 packaging/signing.

## 11. Official tracker references

- issue #211 is the Wave14 owner/coordinator tracker;
- latest coordinator integration checkpoint comment: `5534599811`;
- latest CI/failure-classification follow-up: `5534636604`;
- draft integration PR #212 remains the sole Wave14 integration surface toward `main`;
- Preview issue #208 / PR #210 remains infrastructure/history, not product authority;
- Wave13 issue #205 / PR #207 remains paused.

## 12. Mandatory resume reading

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 consolidated audit + HMI clarification on `wave14/c11-pass2-product-gap-audit`;
7. package handoff docs for the package being reviewed;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview history;
11. issue #205 / PR #207 only for paused Wave13 context.

Then revalidate live GitHub before acting.
