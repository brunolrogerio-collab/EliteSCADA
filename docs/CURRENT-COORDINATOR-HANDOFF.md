# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION LOCKED / C12+C13+C14+C17 ACCEPTED+INTEGRATED / C16 ACCEPTED CANDIDATE WITH COMPOSITION DEFERRED / C15 CHANGES REQUIRED / C18 BLOCKED UNTIL C15 ACCEPTED / WAVE13 PAUSED**

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

Accepted C12+C13+C14 combined checkpoint:

`59a338d67dc956af0aa7907a54d59aaf8e80d764`

C17 integration merge:

`74cf4713ce70d19516acce0f475561a95af2c737`

A premature C15 composition was explicitly reverted, without history rewrite, at:

`c4d2ad7ddcef1c4c69108f43dbcf684746e8303c`

The product tree after that revert contains accepted C12+C13+C14+C17 and excludes unaccepted C15 code. Documentation-only `[skip ci]` commits may advance the branch without changing that product-tree authority.

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

## 4. Accepted pre-DEMO corrections

### C13 — Canonical Simulation Quality

Accepted candidate:

`b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`

Integrated at:

`84b12f7795b6ab5c63a4433ee7818ed418c442a1`

Server-authoritative qualified samples, Source/TAG ownership enforcement, ordinary writes remain Good, no Client Visual Driver-quality spoofing, canonical Bad/Stale/Unavailable propagation.

### C14 — First-Class Operational Events

Accepted candidate:

`70e311d3a359e7b00e8f0ed035478d51bb6ee001`

Integrated at:

`3d6389069015996a5a68fbb055f620560da9e839`

First-class Event remains separate from Alarm/Audit, is Active-revision authoritative, durable and queryable through `operational.events`. Engineering schema authority is v16. C14 backend prerequisite for C18 is satisfied.

### C12 — Server Runtime Automation / Generic Simulation Authoring

Accepted candidate:

`aa0fb1700cb805cfdbb6072ce7ce6bccda687067`

C12 consumes C13. Server Scripts can execute Active lifecycle/Timer/event automation and publish qualified Server Memory samples through public, server-authoritative contracts. No EEE-specific simulator was introduced.

C12+C13+C14 combined checkpoint `59a338d...` passed all five gates:

- EliteSCADA CI `33832217232`;
- Wave11 Runtime `33832217225`;
- Licensing `33832217213`;
- L3 `33832217277`;
- Interop `33832217221`.

### C17 — Internal Memory Authoring UX + Full Lifecycle E2E

Accepted candidate:

`6db4fb33f06159f108ca17ceca23a35ee158b228`

Merged by coordinator intake PR #249 at:

`74cf4713ce70d19516acce0f475561a95af2c737`

A first combined CI attempt exposed only an unrelated IEC-104 T2 timing race. After diagnosis, a failed-jobs-only rerun was authorized. Backend, Web and Chromium all passed on the rerun. Other exact composition gates were already green:

- Wave11 Runtime `33832895152`;
- Licensing `33832895127`;
- L3 `33832895193`;
- Interop `33832895118`.

C17 is ACCEPTED + INTEGRATED. Validation-only PR #244 is closed without merge to `main`.

## 5. C16 — accepted candidate, integration intentionally deferred

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

- `ExecuteCommand` sends only stable Command identity to canonical `/api/commands/{id}/execute`;
- backend remains authority for command existence, authorization, execution and audit;
- denial/failure is propagated, with no fallback to client TAG writes;
- Screen, Dynamo and Popup command execution is validated in Active Runtime;
- Startup/Home is persisted by Screen identity and fails closed when missing/unresolved;
- Popup X/Y is persisted in canonical logical HMI coordinates and respects Runtime scaling/stacking/pointer behavior.

Validation PR #238 is closed / DO NOT MERGE TO MAIN.

Coordinator intake PR #245 remains open only as a composition/tracking surface. It must not be automatically merged.

C16 will be composed once against the accepted integration authority after C15 is corrected/accepted so schema v16, Events, Server Runtime, quality, Memory and final visual-object contracts are preserved together.

## 6. C15 — CHANGES REQUIRED

Package:

**Embeddable Multi-Pen Trend HMI Object**

Current candidate at this synchronization:

`2abee30a25577368999f16d52a0802e6eea473ca`

This candidate is RED in EliteSCADA CI run `33832058712` Chromium.

Confirmed defects:

1. frontend Trend scalar property registry conflicts with canonical common-property authority;
2. frontend/backend cross-stack property parity is inconsistent for seven Trend scalar properties;
3. mounted Engineering Property Inspector does not expose persisted `trendMode`;
4. runtime Trend SVG may have a path element but no visibly drawable segment around quality/no-data discontinuity.

Coordinator CHANGES REQUIRED comment on PR #243:

`5535439463`

Do not weaken parity or visibility assertions. Correct the product/authoring contract and publish a new exact candidate.

No corrected C15 head had been published at this synchronization.

## 7. C18 — not released yet

Package:

**Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 backend Event prerequisite is ready. C18 remains blocked until C15 is corrected, accepted and integrated, because C18 must consume the accepted embeddable visual-object authoring/persistence/runtime pattern rather than invent a parallel frontend contract.

When released, C18 must provide first-class Alarm Browser and Event Browser objects insertable into Screen/Popup, persisted through normal lifecycle, multilingual pt-BR/en/es, with backend-authorized interactive alarm actions and C14 `operational.events` consumption.

## 8. Immediate coordination route

1. receive/revalidate corrected C15 head;
2. require full exact-head gates and architecture acceptance for C15;
3. integrate corrected C15 and run combined gates;
4. publish exact C18 base and release C18 DEV;
5. compose already accepted C16 once against the accepted post-C15 integration authority and rerun combined gates;
6. implement/accept/integrate C18;
7. run complete combined exact-head regression;
8. execute C10 convergence cycle 2;
9. freeze new exact product-code SHA;
10. revalidate affected C11 findings plus remaining real-browser Memory/visual/resolution items;
11. only then issue explicit `RELEASE C11 IMPLEMENTATION`;
12. finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`;
13. build canonical living EEE Simulation through corrected normal Engineering tools;
14. later validate conceptual application against physical Modbus PLC;
15. after final Wave14 acceptance resume Wave13 packaging/signing.

## 9. Preview and release boundaries

Issue #208 / PR #210 remains Preview infrastructure/history, not product authority.

Wave13 issue #205 / PR #207 remains paused. Do not sign stale pre-Wave14 product bytes.

## 10. Mandatory resume protocol

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
