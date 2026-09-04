# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 LAST FULL FREEZE GREEN / C11 PASS 2 CONSOLIDATED + IMPLEMENTATION LOCKED / C12+C13+C14 ACCEPTED+INTEGRATED AND COMBINED GREEN / C17 COMPOSED — COMBINED CI RERUN AFTER DIAGNOSED IEC104 TIMING FLAKE / C15 CHANGES REQUIRED — EXACT CANDIDATE RED / C16 CHANGES REQUIRED — 4/5 GREEN / C18 BLOCKED UNTIL C15 ACCEPTED / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. A DEV delivery is not a Coordinator acceptance. Documentation-only `[skip ci]` commits may move coordinator branches without redefining product-code validation authority.

## 1. Integration authority

Coordinator integration branch:

`wave14/corrections-integration`

Draft integration PR:

`#212`

PR #212 remains **DRAFT** and is not authorized to merge to `main`.

Last fully converged/frozen pre-correction product-code SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Accepted pre-DEMO correction checkpoint with C12+C13+C14:

`59a338d67dc956af0aa7907a54d59aaf8e80d764`

Five combined gates were green on that checkpoint:

- EliteSCADA CI #1325 / `33832217232`;
- Wave 11 Active HMI Runtime #253 / `33832217225`;
- Preview Licensing CI #275 / `33832217213`;
- L3 Seven-Driver Lab #230 / `33832217277`;
- Interop Lab Smoke #152 / `33832217221`.

C17 was subsequently composed. A premature C15 composition was explicitly reverted after live exact-candidate revalidation proved C15 red.

Product-tree checkpoint immediately after that C15 revert:

`c4d2ad7ddcef1c4c69108f43dbcf684746e8303c`

The revert did not rewrite history. It restores the functional product tree to C12+C13+C14+C17 while preserving the audit trail.

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

Accepted server-authoritative qualified-sample contract includes Source/TAG ownership enforcement, ordinary writes remaining `Good`, no Client Visual physical-Driver quality spoofing, and canonical `Bad` / `Stale` / `Unavailable` propagation.

Integration PR #242 merged. Integration SHA:

`84b12f7795b6ab5c63a4433ee7818ed418c442a1`

Validation PR #239 is DO NOT MERGE TO MAIN.

## 4. C14 — ACCEPTED + INTEGRATED

**C14 — First-Class Operational Events**

Accepted candidate:

`70e311d3a359e7b00e8f0ed035478d51bb6ee001`

Accepted first-class Event model remains distinct from Alarm/Audit, is Active-revision authoritative, persists durably in PostgreSQL, exposes protected Historical Query dataset `operational.events`, and advances Engineering schema authority to v16.

Integration PR #235 merged. Integration SHA:

`3d6389069015996a5a68fbb055f620560da9e839`

C14 backend Event prerequisite for C18 is satisfied.

## 5. C12 — ACCEPTED + INTEGRATED / COMBINED GREEN

**C12 — Server Runtime Automation / Generic Simulation Authoring**

Final accepted candidate:

`aa0fb1700cb805cfdbb6072ce7ce6bccda687067`

The corrected C12 consumes C13 rather than creating a parallel quality model. Server Scripts can publish qualified Server Memory samples only through declared `ServerMemoryTag` dependencies and .NET-side Active revision/capability/ownership/read-only validation.

Accepted capability includes:

- Initialize / Timer / TagChanged / ServerRuntimeEvent / Dispose lifecycle;
- stateful Active Runtime server automation;
- `publish_server_memory_sample(stable_tag_id, value, quality)`;
- canonical `TagQuality`;
- `Bad` / `Stale` / `Unavailable` propagation through cache/realtime, Communication alarm semantics and historian/query;
- no Client Visual quality-authoring authority;
- no physical Driver quality spoofing;
- no EEE-specific simulator/service.

C12+C13+C14 combined checkpoint `59a338d...` passed all five required gates listed in section 1.

## 6. C17 — CANDIDATE DELIVERED + COMPOSED / COMBINED CI RERUN UNDERWAY

**C17 — Internal Memory Authoring UX + Full Lifecycle E2E**

Corrected DEV candidate:

`6db4fb33f06159f108ca17ceca23a35ee158b228`

C17 isolated candidate evidence is green and the Coordinator review found the production change capability/catalog-based rather than a hidden Memory provider-ID workaround.

Coordinator composition commit:

`74cf4713ce70d19516acce0f475561a95af2c737`

The combined EliteSCADA CI run `33832895123` initially failed only in:

`Scada.Drivers.Tests.Iec104TcpFaultInjectionTests.Adapter_T2FlushesPendingReceiveAcknowledgementWithoutFaultingSession`

Observed assertion:

- expected `PendingReceiveAcknowledgementCount = 0`;
- observed `1` immediately after the peer had already received the S-frame.

Coordinator diagnosis before rerun:

- C17 does not modify IEC-104 product/test code;
- the same IEC-104 test passed on accepted checkpoint `59a338d...` in EliteSCADA CI run `33832217232`;
- the test uses a 150 ms T2 and samples transport diagnostics immediately after a peer-side `TaskCompletionSource` reports the S-frame, leaving a plausible scheduler/state-update race;
- there is no evidence connecting the failure to Memory authoring/lifecycle behavior.

Therefore a **failed-jobs-only rerun of `33832895123` was authorized after diagnosis**. This is not an unchanged blind rerun.

C17 must not be promoted to combined-accepted until the rerun closes green and dependent Chromium executes successfully.

## 7. C15 — CHANGES REQUIRED / EXACT CANDIDATE RED

**C15 — Embeddable Multi-Pen Trend HMI Object**

Current candidate:

`2abee30a25577368999f16d52a0802e6eea473ca`

Live exact-candidate revalidation superseded an earlier incorrect coordinator note that described this head as 5/5 green.

EliteSCADA CI run:

`33832058712` — **FAILURE in Chromium end-to-end**.

Four deterministic defects were identified from the Playwright artifact:

1. `visual-property-registry-contract.spec.ts`: frontend common registry contains seven new Trend scalar keys while the canonical common-registry contract still expects the prior set;
2. `visual-property-cross-stack-parity.spec.ts`: backend canonical `VisualPropertyFoundation` does not contain those seven frontend Trend keys, while C15 declares Trend definitions privately in `BuiltinVisualObjectSchemas.TrendDefinitions`; cross-stack property authority is inconsistent;
3. `wave-14-c15-trend-i18n-mounted.spec.ts`: mounted Engineering Property Inspector does not expose `[data-property-key="trendMode"]` as required;
4. `wave-14-c15-trend-multipen-mounted.spec.ts`: Trend series SVG path exists but is not visibly rendered; captured path data around the quality/no-data discontinuity contains move operations without a drawable segment.

Do not weaken parity tests or visibility assertions to obtain green.

Coordinator `CHANGES REQUIRED` comment on PR #243:

`5535439463`

The premature C15 composition was reverted explicitly at:

`c4d2ad7ddcef1c4c69108f43dbcf684746e8303c`

C18 remains blocked until a corrected C15 candidate is accepted and integrated.

## 8. C16 — CHANGES REQUIRED / 4 OF 5 GREEN

**C16 — HMI Operational Command + Startup Screen + Persisted Popup X/Y**

Current candidate:

`2a4377cfa93c6020bc89882f6d2b3088cefd030b`

Current results:

- EliteSCADA CI #1294 / `33827619323`: FAILURE in Chromium end-to-end;
- Wave 11 Active HMI Runtime #223 / `33827619319`: SUCCESS;
- Preview Licensing #245 / `33827619337`: SUCCESS;
- L3 Seven-Driver Lab #200 / `33827619326`: SUCCESS;
- Interop Lab Smoke #122 / `33827619361`: SUCCESS.

The Chromium artifact produced three deterministic failures:

1. `wave-03-integrated-composition.spec.ts`: historical fixture enters Runtime without the new canonical Startup/Home Screen and correctly receives `HMI_RUNTIME_STARTUP_SCREEN_REQUIRED`; update the historical E2E fixture, do not weaken fail-closed Startup/Home behavior;
2. `wave-09-popup-dynamo-runtime-web.spec.ts`: brittle exact source-format assertion no longer matches a multiline/cast form of the same `executeVisualNavigationAction` call; update the test semantically, not production formatting;
3. `wave-14-c16-engineering-contract.spec.ts`: acceptance test assumes default workspace already contains an enabled Operational Command; it must create/provide a canonical Command through normal setup before proving Screen/Popup/Dynamo `ExecuteCommand` stable-ID resolution.

Coordinator `CHANGES REQUIRED` comment on PR #238:

`5535440996`

C16 must preserve:

- C14 schema v16 and Operational Events;
- C12 Server Runtime + C13 quality;
- accepted C17 and later accepted C15;
- `ExecuteCommand` with backend authorization/audit authority;
- persisted Startup/Home Screen;
- persisted Popup X/Y in canonical logical HMI coordinates;
- Runtime scaling and hit-target correctness.

Coordinator intake PR #245 remains a composition surface only and is not authorized to merge while candidate/integration requirements are unresolved.

## 9. C18 — BLOCKED UNTIL C15 ACCEPTANCE/INTEGRATION

**C18 — Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 Event backend prerequisite is satisfied.

No C18 implementation branch has been released yet.

C18 must consume:

- C14 `operational.events` backend/query contract;
- accepted canonical visual-object authoring/persistence/runtime lifecycle from the corrected C15 pattern;
- normal Screen/Popup placement and Property Inspector contracts;
- pt-BR/en/es chrome;
- backend authority for ACK/shelve/interactive operations.

No frontend-only parallel Event schema and no process-event-as-alarm workaround are permitted.

## 10. Current coordination order

1. close the diagnosed C17 combined CI rerun and accept C17 only if the composed checkpoint is green;
2. receive corrected C15 candidate, validate exact head, integrate only after all required gates are green;
3. only after C15 acceptance publish the exact C18 base and release C18 implementation;
4. receive corrected C16 candidate, validate exact head and compose once against the then-current accepted integration authority;
5. implement/accept/integrate C18;
6. run complete combined exact-head integration regression;
7. execute C10 convergence cycle 2;
8. freeze a new exact product-code SHA;
9. revalidate affected C11 findings plus remaining real-browser Memory/visual/resolution acceptance;
10. only then issue explicit `RELEASE C11 IMPLEMENTATION`;
11. finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`;
12. build the canonical living EEE Simulation solely through corrected normal Engineering tools;
13. later validate the same conceptual application against the physical Modbus PLC;
14. only after Wave14 acceptance resume Wave13 packaging/signing.

## 11. Official tracker references

- issue #211 is the Wave14 owner/coordinator tracker;
- draft integration PR #212 remains the sole Wave14 integration surface toward `main` and stays DRAFT;
- C15 validation PR #243 has Coordinator CHANGES REQUIRED comment `5535439463`;
- C16 validation PR #238 has Coordinator CHANGES REQUIRED comment `5535440996`;
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
