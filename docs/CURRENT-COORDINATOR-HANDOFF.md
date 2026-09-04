# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION LOCKED / C12+C13+C14+C15+C17 ACCEPTED+INTEGRATED / C16 ACCEPTED CANDIDATE — COMPOSITION NEXT / C18 RELEASED FOR IMPLEMENTATION / WAVE13 PAUSED**

> GitHub is the official development memory. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the fastest mutable resume point. Revalidate live GitHub before acting; do not resume from conversation history alone.

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

Coordinator branch:

`wave14/corrections-integration`

Draft PR:

`#212`

Last full C10 converged freeze before the C11 correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Current accepted post-C15 product-code checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Later `[skip ci]` documentation commits may advance the branch but do not supersede that product-code validation authority.

A new product freeze is not declared until C12-C18 converge and C10 convergence cycle 2 passes.

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

## 4. Accepted correction packages

### C13 — Canonical Simulation Quality

Accepted candidate `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`, integrated at `84b12f7795b6ab5c63a4433ee7818ed418c442a1`.

Server-authoritative qualified samples preserve Source/TAG ownership. Ordinary writes remain Good, Client Visual cannot spoof physical Driver quality, and Bad/Stale/Unavailable propagate canonically.

### C14 — First-Class Operational Events

Accepted candidate `70e311d3a359e7b00e8f0ed035478d51bb6ee001`, integrated at `3d6389069015996a5a68fbb055f620560da9e839`.

Event remains separate from Alarm/Audit, Active-revision authoritative, durable and queryable through `operational.events`. Engineering schema authority is v16.

### C12 — Server Runtime Automation / Generic Simulation Authoring

Accepted candidate `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`.

C12 consumes C13 and provides Active server script lifecycle/Timer/event automation plus qualified Server Memory publication through public generic contracts. No EEE-specific simulator was introduced.

C12+C13+C14 combined checkpoint `59a338d67dc956af0aa7907a54d59aaf8e80d764` passed:

- EliteSCADA CI `33832217232`;
- Wave11 Runtime `33832217225`;
- Licensing `33832217213`;
- L3 `33832217277`;
- Interop `33832217221`.

### C17 — Internal Memory Authoring UX + Full Lifecycle E2E

Accepted candidate `6db4fb33f06159f108ca17ceca23a35ee158b228`, integrated at `74cf4713ce70d19516acce0f475561a95af2c737`.

Normal Server/Client Memory authoring plus Save/Publish/Activate/Runtime is covered. An initial combined CI failure was diagnosed as an unrelated IEC-104 timing race and passed a justified failed-job rerun; the other composition gates were green.

### C15 — Embeddable Multi-Pen Trend HMI Object

Superseded rejected candidate:

`2abee30a25577368999f16d52a0802e6eea473ca`

Corrected accepted candidate:

`3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`

Corrected exact-candidate validation:

- EliteSCADA CI `33836884357`;
- Wave11 Runtime `33836884243`;
- Licensing `33836884240`;
- L3 `33836884464`;
- Interop `33836884239`.

C15 provides first-class `core.trend`, Screen/Popup authoring, native structured Multi-Pen configuration, canonical live/history data paths, quality-aware gaps, shared visual rendering and pt-BR/en/es.

The earlier rejected intake was explicitly reverted at `c4d2ad7ddcef1c4c69108f43dbcf684746e8303c`. Corrected C15 was recomposed at `a0087901f408ae61293d9058af48adaa379b60b2` without rewriting history.

The first combined Wave11 run then exposed a Coordinator selector collision: `/active-runtime\.spec\.ts/` also matched the C15 filename and executed Trend before C17 on the shared test project. Coordinator corrected only the selector isolation.

Final accepted combined product-code checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Five exact-head combined gates are green:

- EliteSCADA CI #1337 / `33838725814`;
- Wave11 Runtime #265 / `33838725796`;
- Licensing #287 / `33838725824`;
- L3 #242 / `33838725850`;
- Interop #164 / `33838725805`.

C15 is **ACCEPTED + INTEGRATED / COMBINED GREEN**.

## 5. C18 — released for implementation

Package:

**Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 Event and C15 visual-object prerequisites are satisfied.

Exact C18 development base:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Branch created from that exact SHA:

`wave14/c18-hmi-alarm-event-browsers`

Binding handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

C18 must provide first-class Alarm Browser and Event Browser objects insertable into Screen/Popup, persisted through normal lifecycle, pt-BR/en/es, backend-authorized alarm actions, C14 `operational.events` consumption, bounded generic query behavior and independent configured instances.

C18 implementation is **AUTHORIZED**. It must target `wave14/corrections-integration`, never `main`.

## 6. C16 — accepted candidate, composition next

Package:

**HMI Operational Command + Startup/Home Screen + Persisted Popup X/Y**

Accepted exact candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

Five exact-candidate gates are green:

- EliteSCADA CI `33834427366`;
- Wave11 Runtime `33834427376`;
- Licensing `33834427368`;
- L3 `33834427362`;
- Interop `33834427361`.

Accepted architecture:

- `ExecuteCommand` sends only stable Command identity to `/api/commands/{id}/execute`;
- backend remains authority for command existence, authorization, execution and audit;
- denial/failure is propagated, with no fallback to client TAG writes;
- Screen, Dynamo and Popup command execution is validated in Active Runtime;
- Startup/Home is persisted by Screen identity and fails closed when missing/unresolved;
- Popup X/Y is persisted in canonical logical HMI coordinates and respects Runtime scaling/stacking/pointer behavior.

C16 must not be blindly overlaid from its older schema-v15 branch. Compose it as a union against current authority, preserving C14 schema v16/Operational Events plus C15/C17.

Shared conflict surfaces:

1. `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
2. `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`;
3. `web/scada-web/playwright.wave11.config.ts`.

Desired combined Wave11 dependency chain:

`C16 startup bootstrap -> generic lifecycle -> C17 Memory -> C15 Trend -> C16 operational runtime -> owner package`

Validation PR #238 remains closed / DO NOT MERGE MAIN. Intake PR #245 is only a composition/tracking surface.

## 7. Immediate coordination route

1. compose accepted C16 against current accepted product authority;
2. run all five combined exact-head gates;
3. in parallel receive/review C18 from frozen base `1dcd80a4...`;
4. accept/integrate C18 after exact-candidate gates and architecture review;
5. resolve combined C16/C18 composition and rerun exact-head regression;
6. execute C10 convergence cycle 2;
7. freeze a new exact product-code SHA;
8. revalidate affected C11 findings and remaining browser validation;
9. only then explicitly release C11 implementation;
10. finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`;
11. build the canonical living EEE Simulation through normal corrected product tools;
12. later validate conceptual mapping against physical Modbus PLC;
13. after final Wave14 acceptance resume Wave13 packaging/signing.

## 8. Preview and release boundaries

Issue #208 / PR #210 remains Preview infrastructure/history, not product authority.

Wave13 issue #205 / PR #207 remains paused. Do not sign stale pre-Wave14 product bytes.

## 9. Mandatory resume protocol

Read and revalidate in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 consolidated audit + HMI clarification on `wave14/c11-pass2-product-gap-audit`;
7. package handoff doc for the package being reviewed;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview history;
11. issue #205 / PR #207 only for paused Wave13 context.

Live GitHub wins over copied SHAs or conversation history.