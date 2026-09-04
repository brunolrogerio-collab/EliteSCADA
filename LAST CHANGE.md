# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C11 PASS 2 CONSOLIDATED + IMPLEMENTATION LOCKED / C12+C13+C14+C15+C17 ACCEPTED+INTEGRATED / C16 EXACT CANDIDATE ACCEPTED — COMPOSITION NEXT / C18 RELEASED FOR IMPLEMENTATION / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. DEV delivery is not Coordinator acceptance. Documentation-only `[skip ci]` commits may advance coordinator branches without redefining the last validated product-code authority.

## 1. Integration authority

Coordinator integration branch:

`wave14/corrections-integration`

Draft integration PR:

`#212`

PR #212 remains **DRAFT** and is not authorized to merge to `main`.

Last fully converged/frozen pre-correction product-code SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Current accepted post-C15 product-code checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

A new Wave14 product freeze is not declared yet. That requires C12-C18 convergence, full combined validation and C10 convergence cycle 2.

## 2. C11 gate remains locked

C11 Pass 2 remains:

**PASS 2 CONSOLIDATED / KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions remain:

1. persisted authorable Popup X/Y is mandatory before C11 release;
2. the living deterministic EEE Simulation must be buildable solely from normal generic EliteSCADA tools;
3. no EEE-specific simulator service, historical DEMO runtime, hidden `.escadapkg` mutation, direct DOM/React bypass, private Driver/host hook, authorization bypass or licensing bypass is acceptable;
4. Trend, Alarm Browser and Event Browser must be first-class authored HMI objects insertable into Screen/Popup;
5. Trend must support persisted Multi-Pen authoring.

Canonical correction plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

## 3. Accepted earlier correction packages

### C13 — Canonical Simulation Quality

Accepted candidate:

`b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`

Integrated at:

`84b12f7795b6ab5c63a4433ee7818ed418c442a1`

Server-authoritative qualified samples preserve Source/TAG ownership. Ordinary writes remain `Good`; Client Visual cannot spoof physical Driver quality; `Bad` / `Stale` / `Unavailable` propagate canonically.

### C14 — First-Class Operational Events

Accepted candidate:

`70e311d3a359e7b00e8f0ed035478d51bb6ee001`

Integrated at:

`3d6389069015996a5a68fbb055f620560da9e839`

Operational Event is distinct from Alarm/Audit, Active-revision authoritative, durably persisted and queryable through `operational.events`. Engineering schema authority is v16.

### C12 — Server Runtime Automation / Generic Simulation Authoring

Accepted candidate:

`aa0fb1700cb805cfdbb6072ce7ce6bccda687067`

C12 consumes C13 and provides Active server script lifecycle/Timer/event automation plus canonical qualified Server Memory publishing without introducing EEE-specific simulation code.

Combined C12+C13+C14 checkpoint `59a338d67dc956af0aa7907a54d59aaf8e80d764` passed all five gates:

- EliteSCADA CI #1325 / `33832217232`;
- Wave 11 Active HMI Runtime #253 / `33832217225`;
- Preview Licensing CI #275 / `33832217213`;
- L3 Seven-Driver Lab #230 / `33832217277`;
- Interop Lab Smoke #152 / `33832217221`.

### C17 — Internal Memory Authoring UX + Full Lifecycle E2E

Accepted candidate:

`6db4fb33f06159f108ca17ceca23a35ee158b228`

Coordinator integration merge:

`74cf4713ce70d19516acce0f475561a95af2c737`

The accepted flow covers normal Server/Client Memory authoring and Save/Publish/Activate/Runtime lifecycle. An initial combined CI failure was diagnosed as an unrelated IEC-104 T2 timing race; failed-job rerun passed and all other composition gates were green.

## 4. C15 — ACCEPTED + INTEGRATED / COMBINED GREEN

**C15 — Embeddable Multi-Pen Trend HMI Object**

Superseded rejected candidate:

`2abee30a25577368999f16d52a0802e6eea473ca`

Corrected accepted candidate:

`3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`

Corrected exact-candidate gates:

- EliteSCADA CI #1335 / `33836884357`: SUCCESS;
- Wave 11 Active HMI Runtime #263 / `33836884243`: SUCCESS;
- Preview Licensing CI #285 / `33836884240`: SUCCESS;
- L3 Seven-Driver Lab #240 / `33836884464`: SUCCESS;
- Interop Lab Smoke #162 / `33836884239`: SUCCESS.

The corrected candidate preserves first-class `core.trend`, Screen/Popup authoring, native structured Multi-Pen configuration, canonical live/history paths, non-Good quality trace gaps, shared visual rendering and pt-BR/en/es.

The earlier rejected C15 intake had been explicitly reverted at:

`c4d2ad7ddcef1c4c69108f43dbcf684746e8303c`

Corrected C15 was recomposed without rewriting history at:

`a0087901f408ae61293d9058af48adaa379b60b2`

That first combined Wave11 run exposed a Coordinator test-project selector collision: `/active-runtime\.spec\.ts/` also matched `c15-trend-active-runtime.spec.ts`, causing the Trend lifecycle to run before C17 against the shared Wave11 project. This was not a C15 or C17 product regression.

Coordinator fixed only the test-project isolation so the generic lifecycle matches the exact `active-runtime.spec.ts` filename:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Final exact-head combined validation on `1dcd80a4...`:

- EliteSCADA CI #1337 / `33838725814`: SUCCESS;
- Wave 11 Active HMI Runtime #265 / `33838725796`: SUCCESS;
- Preview Licensing CI #287 / `33838725824`: SUCCESS;
- L3 Seven-Driver Lab #242 / `33838725850`: SUCCESS;
- Interop Lab Smoke #164 / `33838725805`: SUCCESS.

C15 is **ACCEPTED + INTEGRATED / COMBINED GREEN**.

Coordinator acceptance comment on PR #243:

`5535952767`

## 5. C18 — RELEASED / IMPLEMENTATION AUTHORIZED

**C18 — Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 Event and C15 visual-object prerequisites are satisfied.

Exact authorized development base:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Branch created from that exact product-code SHA before later docs-only commits:

`wave14/c18-hmi-alarm-event-browsers`

Binding handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

C18 owns:

- first-class Alarm Browser HMI object for Screen/Popup;
- first-class Event Browser HMI object for Screen/Popup consuming C14 `operational.events`;
- persisted filters/presentation;
- backend-authorized alarm interactions;
- independent instances;
- Save/Publish/Activate/Active Runtime lifecycle;
- related pt-BR/en/es Historical/Browser chrome.

C18 does not own C16, Trend redesign, EEE physics, PLC mapping, Preview infrastructure or Wave13 packaging.

Issue #211 release checkpoint comment:

`5535956255`

## 6. C16 — EXACT CANDIDATE ACCEPTED / COMPOSITION NEXT

**C16 — HMI Operational Command + Startup Screen + Persisted Popup X/Y**

Accepted exact candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

Five exact-candidate gates are green:

- EliteSCADA CI `33834427366`;
- Wave 11 Active HMI Runtime `33834427376`;
- Preview Licensing `33834427368`;
- L3 Seven-Driver Lab `33834427362`;
- Interop Lab Smoke `33834427361`.

Accepted contracts:

- `ExecuteCommand` invokes canonical `/api/commands/{id}/execute` by stable Command identity;
- backend authorization/audit remains authoritative and denial/failure has no client TAG-write fallback;
- Screen/Dynamo/Popup command execution is exercised through Active Runtime;
- Startup/Home Screen is persisted by canonical Screen identity and fails closed if unresolved;
- Popup X/Y is persisted in logical HMI coordinates and rendered through the C09 transform.

Validation PR #238 is closed / DO NOT MERGE TO MAIN.

Coordinator intake PR #245 remains a composition/tracking surface. C16 must be manually composed against the post-C15 integration authority, preserving C14 schema v16/Operational Events plus C15/C17 contracts. Do not blindly overlay its older schema-v15 branch.

Known shared files requiring union rather than ours/theirs:

1. `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
2. `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`;
3. `web/scada-web/playwright.wave11.config.ts`.

Desired combined Wave11 order:

`C16 startup bootstrap -> generic lifecycle -> C17 Memory -> C15 Trend -> C16 operational runtime -> owner package`

## 7. Immediate coordination route

1. compose accepted C16 against current accepted product authority without losing C14/C15/C17;
2. run all five exact-head combined gates;
3. receive/review C18 candidate independently from its frozen base `1dcd80a4...`;
4. integrate C18 after exact-candidate acceptance and resolve any composition conflicts;
5. run complete combined exact-head regression;
6. execute C10 convergence cycle 2;
7. freeze new exact product-code SHA;
8. revalidate affected C11 findings plus remaining real-browser Memory/visual/resolution items;
9. only then issue explicit `RELEASE C11 IMPLEMENTATION`;
10. finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`;
11. build canonical living EEE Simulation through corrected normal Engineering tools;
12. later validate conceptual application against physical Modbus PLC;
13. after final Wave14 acceptance resume Wave13 packaging/signing.

## 8. Boundaries that remain in force

- PR #212 stays DRAFT.
- C11 implementation stays LOCKED.
- Issue #208 / PR #210 remain Preview infrastructure/history, not product authority.
- Wave13 issue #205 / PR #207 remain paused.
- Do not sign stale pre-Wave14 product bytes.