# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 CONVERGED GREEN / C11 PASS 2 CONSOLIDATED / C11 IMPLEMENTATION LOCKED / C12-C17 PRE-DEMO PRODUCT CORRECTIONS RELEASED FOR PARALLEL DEV / C18 DEFINED BUT IMPLEMENTATION-BLOCKED / WAVE 13 #205/#207 PAUSED**

> Mutable coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only `[skip ci]` commits may advance branches without superseding the last validated product-code SHA.

## 1. Current Wave 14 product authority

Coordinator integration surface:

- branch `wave14/corrections-integration`;
- draft PR #212;
- #212 remains DRAFT, open and not authorized to merge to `main`.

Frozen C10 converged product-code SHA audited by C11 Pass 2:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Exact post-C10 validation on that SHA:

- EliteSCADA CI #1273 / `33795337274`: SUCCESS;
- Wave 11 Active HMI Runtime #203 / `33795337288`: SUCCESS;
- Preview Licensing CI #225 / `33795337280`: SUCCESS;
- L3 Seven-Driver Lab #180 / `33795337252`: SUCCESS;
- Interop Lab Smoke #102 / `33795337270`: SUCCESS.

Documentation-only coordinator commits after `97eefd8...` do not redefine the product audited by C11.

## 2. Accepted C01-C10 authority

The converged product contains:

- C01 — Identity / secure first-run / password policy;
- C02 — backend-authoritative Driver catalog / Data Source forms;
- C03 — unrestricted OpenDNP3 production adapter;
- C04 — TAG Source identity / address assistants / OPC UA discovery-browse;
- C05 — canonical visual properties / schema-driven Property Inspector;
- C06 — Engineering Diagnostics / TAG Monitor boundary;
- C07 — Screen Engineering / Popup / Dynamo authoring/runtime contracts;
- C08 — Python Script Assistant / Project Object Browser / mediated TAG writes;
- C09 — capability-driven application shell + operator Runtime presentation;
- C10 — combined regression, multilingual convergence and exact-SHA freeze.

Permanent gates remain:

- backend canonical authority;
- backend-side authorization;
- host-owned fail-closed licensing;
- no Preview-only product bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identity;
- lifecycle `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- exact-head universal CI plus impact-specific validation;
- diagnose failures before rerun;
- never weaken meaningful tests/security/contracts merely to obtain green.

## 3. C11 Pass 2 is consolidated

C11 audit branch:

`wave14/c11-pass2-product-gap-audit`

Canonical result:

`docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md`

Current state on the C11 branch:

**PASS 2 CONSOLIDATED / IMPLEMENTATION LOCKED**

C11 recommendation:

`KEEP C11 IMPLEMENTATION LOCKED`

The product is substantially closer to the intended canonical EEE than Pass 1 suggested. Confirmed supported foundations include Internal Memory, Analog Fill, reusable Dynamos, Popup open/context, alarms, historian, alarm history, operator shell, project assets and logical Source/TAG separation suitable for later Modbus remapping.

However, confirmed product gaps remain and must be corrected before the DEMO may conceal them with authoring workarounds.

## 4. Confirmed pre-DEMO correction areas

Blocking/required correction areas include:

- active Server Runtime automation / Server Script host, scheduler and timer lifecycle;
- generic normal-product capability sufficient to author a living deterministic Simulation;
- canonical deliberate bad/stale/unavailable quality origin for Simulation/internal sources;
- first-class operational Event model/history/query distinct from Alarm/Audit;
- operator/authored Trend exposure;
- embeddable configurable Multi-Pen Trend HMI object;
- canonical authored HMI invocation of Operational Command entities;
- embeddable Alarm Browser and Event Browser HMI objects;
- Internal Memory authoring UX cleanup and complete authoring lifecycle proof;
- explicit authorable Startup/Home Runtime Screen;
- Historical Browser multilingual convergence;
- persisted authorable Popup X/Y placement.

## 5. Binding Product Owner decisions after Pass 2

### Popup X/Y

Popup X/Y is **not** an accepted limitation or mitigation. It must be implemented as a normal canonical product capability before C11 implementation release.

Required path:

`Engineering authoring -> persisted canonical position -> Save -> Publish -> Activate -> Active Runtime logical coordinate transform`

No DEMO-specific CSS/DOM positioning or hidden package editing is acceptable.

### EEE Simulation must be buildable from normal product tools

EliteSCADA must provide generic product mechanisms sufficient for an integrator to build the future EEE Simulation normally.

The product must **not** receive an EEE-specific simulator service merely to satisfy the DEMO.

The future C11 project itself must be able to author, using supported public mechanisms, a process with:

- inflow raising level;
- pumps lowering level;
- thresholds and sequencing;
- duty/standby alternation;
- two-pump demand;
- faults/trips;
- bad/stale/unavailable quality scenarios;
- coherent process analogs;
- alarms/events/history/trends driven by the same evolving canonical state.

If normal Server Memory/Server Runtime automation/quality/event contracts cannot express that class of model, that is a product gap and must be corrected before C11.

Forbidden substitutes include a new `EeeSimulatorService`, historical Demo/Simulation internals, hard-coded DEMO pages, direct DOM/React mutation, hidden `.escadapkg` JSON and security/licensing bypasses.

## 6. Pre-DEMO correction package authority

Canonical package plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

Immediate parallel packages:

- **C12 — Server Runtime Automation / Generic Simulation Authoring**;
- **C13 — Canonical Simulation Quality Contract**;
- **C14 — First-Class Operational Events**;
- **C15 — Embeddable Multi-Pen Trend HMI Object**;
- **C16 — HMI Operational Command + Startup Screen + mandatory Popup X/Y**;
- **C17 — Internal Memory Authoring UX + Full Lifecycle E2E**.

Dependent package:

- **C18 — Embeddable Alarm + Event Browser HMI Objects + related history i18n**.

C18 is defined but **implementation-blocked** until Coordinator accepts the upstream C14 Event contract and the needed common embeddable-object contracts from C15/common visual baseline, then publishes an exact implementation base.

## 7. Package branches

Coordinator creates/releases these immediate branches from one common documentation checkpoint over the unchanged frozen C10 product code:

- `wave14/c12-server-runtime-automation`;
- `wave14/c13-simulation-quality-contract`;
- `wave14/c14-operational-events`;
- `wave14/c15-hmi-trend-multipen`;
- `wave14/c16-hmi-command-navigation-popup`;
- `wave14/c17-memory-authoring-e2e`.

Planned later branch after prerequisite integration:

- `wave14/c18-hmi-alarm-event-browsers`.

No package branch merges directly to `main`.

## 8. Intended dependency/integration route

Development C12-C17 may run in parallel. Initial intended integration order is:

1. C13 quality contract;
2. C17 Memory authoring/E2E;
3. C14 Operational Events;
4. C16 Command/Home/Popup positioning;
5. C15 Trend Multi-Pen;
6. C12 Server Runtime Automation aligned to accepted C13 quality contract;
7. publish exact C18 base;
8. C18 Alarm/Event Browser objects;
9. C10 convergence cycle 2.

Coordinator may adjust ordering when actual diffs show a safer dependency route.

## 9. C11 remains implementation-locked

C12-C18 correct the product. They do not authorize building the canonical EEE DEMO.

After all approved corrections:

1. integrate into `wave14/corrections-integration`;
2. run universal and impacted specialized workflows;
3. execute C10 convergence cycle 2;
4. freeze a new exact product-code SHA;
5. revalidate every affected C11 finding against that SHA;
6. execute remaining real-browser acceptance checks;
7. only after blocking gaps are cleared issue explicit `RELEASE C11 IMPLEMENTATION`.

At release create/finalize:

`docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`

Then implement the canonical EEE DEMO Simulation through normal product contracts.

## 10. Future C11 implementation target

The canonical EEE application remains:

- realistic commercial/product DEMO;
- Engineering example;
- operator Runtime demonstration;
- regression/acceptance application;
- Product Owner browser homologation application;
- later physical PLC/Modbus proof.

Expected Screens include at least:

- `Visão Geral / EEE Principal`;
- `Instrumentação`;
- `Sistema Elétrico`;
- `Operação`.

Required experience includes two pumps, full state projection including bad quality, animated wet-well level and `% Full`, coherent process values, reusable Dynamos, contextual positioned Popups, commands, alarms, events, Multi-Pen Trends, history, project assets and `pt-BR`/`en`/`es`.

Simulation and later PLC/Modbus variants should preserve the same conceptual Screens/Dynamos/Popups/TAG identities as far as product contracts permit, with the later transition primarily changing Source/address mapping.

## 11. Preview and Wave 13 boundaries

Issue #208 / PR #210 remains Preview infrastructure/history, not product authority. Preview should only be updated for the new canonical DEMO after C11 implementation exists through normal product flow.

Wave 13 issue #205 / PR #207 remains paused. Future Windows packaging/signing must consume the final accepted Wave 14 product bytes, not a stale pre-Wave14 snapshot.

## 12. Mandatory resume reading

Read/revalidate in order:

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`;
6. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
7. C11 consolidated audit on `wave14/c11-pass2-product-gap-audit`;
8. C11 HMI-object Product Owner clarification on that branch;
9. `docs/WAVE14-CORRECTION-PACKAGES.md` for historical C01-C10 intake;
10. `docs/CI-VALIDATION-POLICY.md`;
11. live issue #211 and draft PR #212;
12. issue #208 / PR #210 only for Preview history;
13. issue #205 / PR #207 only for paused Wave13 context.

Then revalidate live GitHub before acting.

## 13. Exact next coordinator actions

1. release C12-C17 DEV chats against the common published base;
2. keep C18 implementation blocked pending its prerequisites;
3. review each DEV candidate against exact branch ancestry, scope and CI;
4. integrate accepted corrections with expected-head protection;
5. run affected exact-head workflows after every product integration step as required by policy;
6. establish the C18 base only after accepted prerequisite contracts exist;
7. integrate C18;
8. run C10 convergence cycle 2 and freeze the new exact product SHA;
9. re-audit affected C11 rows and execute remaining browser acceptance;
10. if all blocking product gaps are cleared, explicitly release C11 implementation;
11. build the EEE Simulation only through the corrected normal product tools;
12. later validate the same conceptual application against the physical Modbus PLC;
13. complete full CI, Preview/Codespaces and Product Owner homologation;
14. only after Wave 14 acceptance resume Wave 13 packaging/signing.
