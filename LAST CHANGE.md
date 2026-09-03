# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 CONVERGED AND COMBINED GREEN / C11 PASS 2 PRODUCT-GAP AUDIT ACTIVELY WRITING EVIDENCE / C11 IMPLEMENTATION LOCKED / WAVE 13 #205/#207 PAUSED**

> Mutable coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only `[skip ci]` commits may advance branches without superseding the frozen product-code SHA.

## 1. Current Wave 14 integration state

`main` remains intentionally untouched by Wave 14 correction packages.

Coordinator integration surface:

- branch: `wave14/corrections-integration`;
- draft PR: #212;
- PR #212 remains DRAFT and is not authorized to merge to `main`;
- frozen C10 converged **product-code** SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

A later documentation-only commit may be the integration branch HEAD. Do not mistake docs-only movement for a new product baseline.

Exact post-merge validation on `97eefd8...`:

- EliteSCADA CI #1273 / run `33795337274`: **SUCCESS**;
- Wave 11 Active HMI Runtime #203 / run `33795337288`: **SUCCESS**;
- Preview Licensing CI #225 / run `33795337280`: **SUCCESS**;
- L3 Seven-Driver Lab #180 / run `33795337252`: **SUCCESS**;
- Interop Lab Smoke #102 / run `33795337270`: **SUCCESS**.

Detailed convergence evidence and the formal C11 Pass 2 release are recorded in:

`docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`

## 2. Accepted package authorities C01-C10

The converged product contains:

- C01 — Identity / secure first-run / password policy;
- C02 — backend-authoritative Driver catalog / Data Source forms;
- C03 — unrestricted OpenDNP3 production adapter / commercial dependency removal;
- C04 — TAG Source identity / address assistants / OPC UA discovery-browse;
- C05 — canonical visual properties / schema-driven Property Inspector;
- C06 — Engineering Diagnostics / TAG Monitor boundary;
- C07 — Screen Engineering / Popup / Dynamo authoring and Runtime contracts;
- C08 — Python Script Assistant / Project Object Browser / mediated TAG-write capability;
- C09 — capability-driven application shell + operator Runtime presentation;
- C10 — coordinator convergence, combined regression and multilingual corrections.

Accepted lifecycle remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Historical DEMO behavior is not product authority. Real persisted-data compatibility remains mandatory where it is part of a supported product contract.

Permanent architecture gates remain:

- backend canonical authority;
- backend-side authorization;
- host-owned fail-closed licensing;
- no Preview-only bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source contracts;
- universal CI plus impact-specific validation on exact candidate heads;
- diagnose failures before rerun;
- never weaken security, tests or product contracts merely to obtain green.

## 3. C09/C10 convergence checkpoint

Accepted C09 DEV head:

`55c23851cbed0039f8dc82b75ef833a33393f8ed`

C09 was integrated through PR #230. Post-C09 product SHA:

`ea0d05bd29404c25e97b4d857b20912ed08059f6`

Accepted C09 authority includes capability-driven shell/navigation, operator-focused Runtime, semantic Dark/Light shell themes without recoloring authored HMI content, `pt-BR`/`en`/`es`, native fullscreen, fixed logical HMI scaling with letterbox/pillarbox, no responsive HMI reflow/document-scroll composition, shared Screen/Popup transform and alarm-overlay behavior.

C10 branch:

`wave14/c10-multilingual-convergence`

Actual integration PR: #231.  
Validation-only draft PR: #232, **DO NOT MERGE TO MAIN**.

Final validated C10 branch head:

`be32dd4aac39afd6158a82a66b37d670b18c78ab`

After exact-head five-gate validation, PR #231 merged with expected-head protection. Integrated SHA `97eefd8...` then passed all five workflows again and is the frozen product authority for C11 Pass 2.

Known product concern carried into C11 audit: canonical persisted authorable Popup X/Y placement was not proven in the accepted C07/C09 contract. C11 must decide this from evidence rather than invent ad-hoc CSS persistence.

## 4. C11 Pass 2 is now actively in progress

C11 status:

**PASS 2 PRODUCT-GAP AUDIT ACTIVE / IMPLEMENTATION LOCKED.**

Audit branch:

`wave14/c11-pass2-product-gap-audit`

Canonical Pass 2 output workspace:

`docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md`

Canonical DEMO requirements authority:

`docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`

C11 is auditing only exact converged product SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

The C11 audit document is no longer an empty template. C11 has begun progressively recording evidence, revalidating Pass 1 findings and classifying requirements. This progressive matrix is **not yet the final C11 disposition** and must not be used to release implementation early.

Current evidence already recorded by C11 includes, among other items:

### Structurally supported so far

- backend-authoritative typed Data Source configuration;
- protocol-aware TAG address assistance;
- canonical Server Memory / Client Memory concepts;
- numeric `% Full` process presentation;
- canonical Analog Fill capable of driving the suction-well liquid level from an analog process value;
- reusable Dynamo definitions with typed public properties/TAG references;
- canonical Popup open/close/context action model;
- project PNG/background asset import/use through Engineering;
- Active Engineering Runtime remains the accepted authority rather than a legacy/fallback DEMO page.

### Confirmed product gap already recorded

C11 has confirmed an Engineering UX inconsistency for Internal Memory TAG authoring: the memory-specific model states that no network address exists, while the generic TAG authoring path still exposes network-style `Address` semantics.

C11 currently recommends this as a bounded **fix before C11 implementation** rather than a DEMO workaround.

This finding still requires Coordinator/Development Lead disposition after the consolidated Pass 2 result. C11 does not own release authority.

### Partial support currently recorded

- pump/Dynamo semantic running/fault/bad-quality state vocabulary exists, but final authorability/operator clarity and non-color-only bad-quality representation still require proof;
- fixed logical HMI composition/scaling exists, but required real-browser multi-resolution behavior remains to validate.

### High-priority open validation items

C11 is still closing important questions including:

- normal UI creation of both Server Memory and Client Memory Sources;
- complete end-to-end Internal Memory Source + TAG authoring;
- actual Server Script/Timer Runtime host, executor, scheduler and activation lifecycle for a continuously living Simulation;
- official Simulation path for deliberate bad/stale/unavailable quality generation;
- canonical persisted authorable Popup X/Y placement;
- explicit authorable startup/home Runtime Screen;
- real-browser fullscreen/no-scroll behavior across target resolutions;
- preservation of conceptual Screens/Dynamos/Popups/TAG identity when changing Simulation mapping to real Modbus PLC mapping.

Server Script/Timer runtime execution and canonical simulated bad-quality generation are potentially blocking requirements for the intended DEMO Simulation and must be closed before implementation release.

## 5. C11 classification and release rule

Every canonical C11 requirement must finish as exactly one of:

- `SUPPORTED`;
- `PARTIALLY SUPPORTED`;
- `PRODUCT GAP`;
- `NEEDS VALIDATION`.

Every non-SUPPORTED item must record:

1. exact requirement;
2. product evidence/code/API/test/Runtime behavior;
3. limitation;
4. impact on the canonical EEE DEMO;
5. Simulation impact;
6. PLC/Modbus impact;
7. recommended owner/action;
8. whether bounded product correction is recommended before DEMO implementation;
9. any safe mitigation, explicitly separated from a real fix.

Pass 1 findings solved by convergence must be marked `RESOLVED/SUPERSEDED` with evidence. They must not silently disappear.

C11 does **not** release implementation. At the end of Pass 2 it returns one consolidated matrix and one recommendation to Coordinator/Development Lead:

- `RELEASE C11 IMPLEMENTATION`; or
- `KEEP C11 IMPLEMENTATION LOCKED`.

The Coordinator/Development Lead then decides each confirmed gap.

## 6. Canonical EEE DEMO intent remains unchanged

The future canonical application is a realistic **Estação Elevatória de Esgoto (EEE)** built through normal EliteSCADA Engineering/Runtime workflows, not a hand-built DEV webpage and not a resurrection of the historical DEMO.

It must serve simultaneously as:

- commercial/product demonstration;
- Engineering authoring example;
- operator Runtime demonstration;
- regression/acceptance application;
- Product Owner homologation application;
- canonical EliteSCADA project example;
- later proof against a physical PLC using Modbus.

The planned Runtime experience includes:

- `Visão Geral / EEE Principal`;
- `Instrumentação`;
- `Sistema Elétrico`;
- `Operação`;
- two pumps/motors with stopped/running/fault/trip/unavailable/bad-quality states;
- suction-well analog level;
- numeric `% Full`;
- visibly animated liquid rising/falling with process state;
- coherent level, flow, pressure, frequency, current and related values;
- contextual equipment Popups;
- reusable Dynamos/public properties/TAG bindings;
- alarms, events, trends and history;
- project PNG/background/assets through canonical project contracts;
- proper operator versus Engineering/Diagnostics separation;
- `pt-BR`, `en`, `es` user experience;
- scripts/animations only through canonical EliteSCADA APIs and boundaries.

Critical states must not rely only on color.

## 7. DEMO Simulation target

The first executable canonical application will be **DEMO Simulation**.

It must be visibly alive rather than a static fixture. Intended behavior includes:

- process inflow raises well level;
- running pump(s) lower level;
- automatic start/stop thresholds;
- duty/standby alternation between pump 1 and pump 2;
- second-pump demand under higher process load;
- pump fault/trip injection;
- unavailable/bad-quality scenario;
- coherent current/frequency/flow/pressure response;
- alarms triggered by simulated process conditions;
- evolving historian/trend data;
- deterministic/reproducible enough behavior for browser homologation and regression.

Shared/authoritative process state should use product-supported shared mechanisms. Client-local memory is reserved for client UI state where appropriate. The exact simulation implementation mechanism remains subject to Pass 2 and the later approved implementation premise.

No DEMO-only Driver, direct DOM/React manipulation, hidden package JSON, security bypass or private host memory shortcut is acceptable.

## 8. DEMO PLC / Modbus target

After Simulation is accepted, a second stage will validate the canonical application against a physical PLC using Product Owner supplied Modbus addresses.

The architectural goal is to preserve as much as technically possible:

- Screens;
- Dynamos;
- Popups;
- conceptual TAG identities;
- alarms;
- trends/history;
- visual logic;
- operator experience.

The intended change is primarily Source/address mapping:

`Simulation/internal source -> real Modbus Source -> Product Owner supplied PLC addresses`

Do not design a Simulation-only visual/TAG architecture that must be rebuilt for PLC validation.

Simulation success does not prove PLC/Modbus integration. PLC connectivity does not excuse a non-reproducible Simulation project. They are separate acceptance proofs of one conceptual HMI architecture.

## 9. Binding future route

The official sequence from the current checkpoint is:

`97eefd8... C10 converged product freeze`
`-> C11 Pass 2 progressive audit`
`-> consolidated evidence-backed C11 gap matrix`
`-> Coordinator/Development Lead disposition of every gap`
`-> bounded pre-DEMO product correction lane(s), if required`
`-> integrate approved corrections into wave14/corrections-integration`
`-> rerun C10 convergence and freeze a new exact product SHA if product code changed`
`-> revalidate affected C11 findings`
`-> explicit C11 implementation release`
`-> create/finalize docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`
`-> record full DEMO premise + functional architecture + Product Owner decisions in GitHub`
`-> implement canonical EEE DEMO Simulation through normal Engineering contracts`
`-> Save -> Publish -> Activate -> HMI Runtime validation`
`-> validate living process, alarms, events, trends/history, Dynamos, Popups, assets and multilingual behavior`
`-> establish PLC/Modbus-compatible mapping model`
`-> validate against physical PLC when hardware is available`
`-> run full exact-head EliteSCADA CI + impact-specific workflows`
`-> update Preview harness to load the new canonical DEMO through normal product flow`
`-> validate from a clean Codespace`
`-> real-browser Product Owner homologation`
`-> accept final Wave 14 product baseline`
`-> only then resume Wave 13 Windows release/signing on the accepted Wave 14 bytes`

Do not reorder the critical gates by building the DEMO before gap disposition or by signing a stale pre-Wave14 product.

## 10. Preview and Wave 13 boundaries

Issue #208 / PR #210 remains Preview infrastructure/history, not product authority. Operational procedure remains in:

`docs/CODESPACES-PREVIEW-RUNBOOK.md`

The historical Preview launcher that reconstructs the old Wave11 DEMO is not the final Wave14 acceptance fixture. Preview should be updated only after the canonical C11 DEMO exists and is accepted for that stage.

Wave 13 issue #205 / PR #207 remains paused.

Do not resume release/signing against stale product bytes. Future Windows packaging/signing must consume the accepted Wave14 product baseline and preserve the already-proven Wave13 release machinery, recording exact product and packaging/signing SHAs.

## 11. Mandatory resume reading

Read in order:

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 active output: `docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md` on `wave14/c11-pass2-product-gap-audit`;
7. `docs/WAVE14-CORRECTION-PACKAGES.md`;
8. `docs/CI-VALIDATION-POLICY.md`;
9. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
10. live issue #211 and draft PR #212;
11. issue #208 / PR #210 only for Preview history;
12. issue #205 / PR #207 only for paused Wave13 context.

Then revalidate live GitHub before acting.

## 12. Exact next actions

1. C11 continues Pass 2 only against frozen product SHA `97eefd8...` and writes evidence into `docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md` on `wave14/c11-pass2-product-gap-audit`;
2. C11 closes the high-priority Runtime/Simulation/quality/authoring questions and re-tests all Pass 1 findings;
3. C11 returns the consolidated matrix, blocking/non-blocking gap list and final recommendation;
4. Coordinator/Development Lead dispositions every confirmed gap;
5. approved product fixes, if any, are dispatched as bounded correction lanes rather than implemented inside C11;
6. any product-code change is reintegrated and causes a new C10 exact-SHA convergence freeze plus affected C11 revalidation;
7. only after blocking gaps are cleared or explicitly accepted does the Coordinator release C11 implementation;
8. at implementation release, write the full canonical DEMO implementation premise/architecture into the repository before or alongside implementation;
9. build and validate DEMO Simulation first while preserving PLC/Modbus reuse by design;
10. later validate the same conceptual application against the physical Modbus PLC;
11. finish Wave14 with exact-head CI, updated Preview, clean Codespace and real-browser Product Owner homologation;
12. only after Wave14 acceptance resume Wave13 Windows packaging/signing against the accepted Wave14 product bytes.
