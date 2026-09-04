# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 LAST FULL FREEZE GREEN / C11 PASS 2 CONSOLIDATED + IMPLEMENTATION LOCKED / C12+C13+C14+C17 ACCEPTED+INTEGRATED / C16 EXACT CANDIDATE ACCEPTED — COMPOSITION DEFERRED / C15 CHANGES REQUIRED / C18 BLOCKED UNTIL C15 ACCEPTED / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. A DEV delivery is not a Coordinator acceptance. Documentation-only `[skip ci]` commits may move coordinator branches without redefining product-code validation authority.

## 1. Integration authority

Coordinator integration branch:

`wave14/corrections-integration`

Draft integration PR:

`#212`

PR #212 remains **DRAFT** and is not authorized to merge to `main`.

Last fully converged/frozen pre-correction product-code SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Accepted C12+C13+C14 combined checkpoint:

`59a338d67dc956af0aa7907a54d59aaf8e80d764`

C17 integration/composition merge:

`74cf4713ce70d19516acce0f475561a95af2c737`

A premature C15 composition was explicitly reverted without rewriting history. Product-tree checkpoint after the revert:

`c4d2ad7ddcef1c4c69108f43dbcf684746e8303c`

That product tree contains the accepted C12+C13+C14+C17 capabilities and excludes the unaccepted C15 candidate.

A new Wave14 product freeze will be declared only after C12-C18 convergence, full combined validation and C10 convergence cycle 2.

## 2. C11 gate remains locked

C11 Pass 2 remains:

**PASS 2 CONSOLIDATED / KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions:

1. persisted authorable Popup X/Y is mandatory before C11 release;
2. the living deterministic EEE Simulation must be buildable solely from normal generic EliteSCADA tools;
3. no EEE-specific simulator service, historical DEMO runtime, hidden `.escadapkg` mutation, direct DOM/React bypass, private Driver/host hook, authorization bypass or licensing bypass is acceptable;
4. Trend, Alarm Browser and Event Browser must be first-class authored HMI objects insertable into Screen/Popup; global pages alone do not satisfy the requirement;
5. Trend must support persisted Multi-Pen authoring.

Canonical correction plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

Original common DEV base:

`2607e03d5445eefe1f434495d0ee81136c6cd220`

## 3. C13 — ACCEPTED + INTEGRATED

**C13 — Canonical Simulation Quality Contract**

Accepted candidate:

`b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`

Integration SHA:

`84b12f7795b6ab5c63a4433ee7818ed418c442a1`

Accepted server-authoritative qualified samples preserve Source/TAG ownership, ordinary writes remain `Good`, Client Visual cannot spoof physical Driver quality, and `Bad` / `Stale` / `Unavailable` propagate canonically.

## 4. C14 — ACCEPTED + INTEGRATED

**C14 — First-Class Operational Events**

Accepted candidate:

`70e311d3a359e7b00e8f0ed035478d51bb6ee001`

Integration SHA:

`3d6389069015996a5a68fbb055f620560da9e839`

Accepted Event model is distinct from Alarm/Audit, Active-revision authoritative, durably persisted, queryable through `operational.events`, and advances Engineering schema authority to v16.

C14 backend Event prerequisite for C18 is satisfied.

## 5. C12 — ACCEPTED + INTEGRATED / COMBINED GREEN

**C12 — Server Runtime Automation / Generic Simulation Authoring**

Final accepted candidate:

`aa0fb1700cb805cfdbb6072ce7ce6bccda687067`

C12 consumes the accepted C13 quality contract. Server Scripts can publish qualified Server Memory samples only through declared `ServerMemoryTag` dependencies with Active revision/capability/ownership/read-only validation.

Accepted capabilities include Initialize / Timer / TagChanged / ServerRuntimeEvent / Dispose lifecycle, stateful server automation, canonical qualified Server Memory publishing, and downstream cache/realtime/alarm/historian propagation. No EEE-specific simulator/service was introduced.

Combined C12+C13+C14 checkpoint `59a338d...` passed:

- EliteSCADA CI #1325 / `33832217232`;
- Wave 11 Active HMI Runtime #253 / `33832217225`;
- Preview Licensing CI #275 / `33832217213`;
- L3 Seven-Driver Lab #230 / `33832217277`;
- Interop Lab Smoke #152 / `33832217221`.

## 6. C17 — ACCEPTED + INTEGRATED / COMBINED GREEN

**C17 — Internal Memory Authoring UX + Full Lifecycle E2E**

Accepted candidate:

`6db4fb33f06159f108ca17ceca23a35ee158b228`

Coordinator intake PR #249 merged into `wave14/corrections-integration` with merge SHA:

`74cf4713ce70d19516acce0f475561a95af2c737`

The production change is Source capability/catalog-based rather than hard-coded to private Memory provider IDs. The accepted scenario covers normal Server Memory and Client Memory authoring plus Save/Publish/Activate/Active Runtime lifecycle, Server Memory read/write/retention/historian behavior and Client Memory session isolation.

The first combined EliteSCADA CI run `33832895123` failed only in an unrelated IEC-104 T2 timing assertion. Coordinator diagnosed the failure before rerun:

- C17 does not modify IEC-104 code/tests;
- the same IEC-104 test passed on accepted checkpoint `59a338d...`;
- the test uses a 150 ms T2 and sampled transport diagnostics immediately after peer-side S-frame observation, leaving a scheduler/state-update race.

A failed-jobs-only rerun was therefore authorized. The rerun passed:

- Backend build/test/smoke: SUCCESS;
- Web build: SUCCESS;
- Chromium end-to-end: SUCCESS.

Other exact composition workflows were already green:

- Wave 11 Active HMI Runtime `33832895152`: SUCCESS;
- Preview Licensing `33832895127`: SUCCESS;
- L3 Seven-Driver Lab `33832895193`: SUCCESS;
- Interop Lab Smoke `33832895118`: SUCCESS.

C17 is **ACCEPTED + INTEGRATED / COMBINED GREEN**. Validation-only PR #244 is closed without merge to `main`.

## 7. C15 — CHANGES REQUIRED / EXACT CANDIDATE RED

**C15 — Embeddable Multi-Pen Trend HMI Object**

Current branch candidate at this checkpoint:

`2abee30a25577368999f16d52a0802e6eea473ca`

EliteSCADA CI run `33832058712` failed Chromium end-to-end. Four deterministic issues were identified:

1. frontend common Trend scalar property registry conflicts with the canonical common-registry contract;
2. frontend/backend cross-stack visual property authority is inconsistent for the seven Trend scalar properties;
3. mounted Engineering Property Inspector does not expose `trendMode` as required;
4. Trend SVG series may exist without a visibly drawable segment around quality/no-data discontinuity.

Do not weaken property parity or visibility tests.

Coordinator `CHANGES REQUIRED` comment on PR #243:

`5535439463`

The premature C15 integration was reverted explicitly at `c4d2ad7ddcef1c4c69108f43dbcf684746e8303c`. History was preserved.

C18 remains blocked until a corrected C15 candidate is accepted and integrated.

## 8. C16 — EXACT CANDIDATE ACCEPTED / COMPOSITION DEFERRED

**C16 — HMI Operational Command + Startup Screen + Persisted Popup X/Y**

Accepted exact candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

The three previously diagnosed Chromium failures were corrected without weakening C16 contracts.

Five exact-candidate gates are green:

- EliteSCADA CI `33834427366`: SUCCESS;
- Wave 11 Active HMI Runtime `33834427376`: SUCCESS;
- Preview Licensing `33834427368`: SUCCESS;
- L3 Seven-Driver Lab `33834427362`: SUCCESS;
- Interop Lab Smoke `33834427361`: SUCCESS.

Coordinator architecture review confirmed:

- `ExecuteCommand` sends only canonical stable Command identity to `/api/commands/{id}/execute`;
- backend denial/failure is propagated and no fallback to client TAG writes exists;
- backend Operational Command authorization/audit remains the canonical authority already accepted by C11;
- Screen, Dynamo and Popup command execution is exercised through Active Runtime;
- Startup/Home Screen is persisted by canonical Screen identity and Runtime fails closed when missing/unresolved;
- Popup X/Y is persisted and projected in the C09 logical HMI coordinate system with stacking/pointer behavior preserved;
- no DEMO-specific workaround was introduced.

Coordinator acceptance comment on validation PR #238:

`5535491120`

PR #238 is closed / validation only / DO NOT MERGE TO MAIN.

Coordinator intake PR #245 remains open as a composition/tracking surface and is intentionally not mergeable automatically. Its body has been updated to the accepted candidate.

**C16 is accepted as a candidate but intentionally not integrated yet.** Compose it once against the accepted integration authority after C15 is corrected/accepted, preserving C14 schema v16/events, C12+C13 runtime/quality, C17, final C15 visual contracts and all Wave11 lanes.

## 9. C18 — BLOCKED UNTIL C15 ACCEPTANCE/INTEGRATION

**C18 — Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 Event backend prerequisite is satisfied.

No C18 implementation branch has been released yet.

C18 must consume:

- C14 `operational.events` backend/query contract;
- accepted canonical visual-object authoring/persistence/runtime lifecycle from corrected C15;
- normal Screen/Popup placement and Property Inspector contracts;
- pt-BR/en/es chrome;
- backend authority for ACK/shelve/interactive operations.

No frontend-only parallel Event schema and no process-event-as-alarm workaround are permitted.

## 10. Current coordination order

1. receive corrected C15 candidate;
2. validate C15 exact head and integrate only after required gates are green;
3. after C15 acceptance publish exact C18 base and release C18 implementation;
4. compose already accepted C16 once against the then-current accepted integration authority and rerun combined gates;
5. implement/accept/integrate C18;
6. run complete combined exact-head integration regression;
7. execute C10 convergence cycle 2;
8. freeze a new exact product-code SHA;
9. revalidate affected C11 findings and remaining real-browser Memory/visual/resolution acceptance;
10. only then issue explicit `RELEASE C11 IMPLEMENTATION`;
11. finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`;
12. build the canonical living EEE Simulation solely through corrected normal Engineering tools;
13. later validate the same conceptual application against the physical Modbus PLC;
14. only after Wave14 acceptance resume Wave13 packaging/signing.

## 11. Official tracker references

- issue #211 is the Wave14 owner/coordinator tracker;
- latest C17/C16 coordinator checkpoint comment: `5535505898`;
- draft integration PR #212 remains the sole Wave14 integration surface toward `main` and stays DRAFT;
- C15 validation PR #243 has Coordinator CHANGES REQUIRED comment `5535439463`;
- C16 intake/composition PR #245 tracks the accepted candidate but must not be auto-merged;
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
