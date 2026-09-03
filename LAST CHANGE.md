# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 CONVERGED GREEN / C11 PASS 2 CONSOLIDATED / C11 IMPLEMENTATION LOCKED / C12-C17 RELEASED FOR PARALLEL DEV — NO PUBLISHED DEV COMMITS OBSERVED YET / C18 DEFINED BUT IMPLEMENTATION-BLOCKED / WAVE 13 #205/#207 PAUSED**

> Mutable coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only `[skip ci]` commits may advance coordinator branches without superseding the last validated product-code SHA.

## 1. Current Wave 14 product authority

Coordinator integration surface:

- branch `wave14/corrections-integration`;
- draft PR #212;
- PR #212 remains DRAFT, open and not authorized to merge to `main`.

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

Current C11 state:

**PASS 2 CONSOLIDATED / IMPLEMENTATION LOCKED**

C11 recommendation:

`KEEP C11 IMPLEMENTATION LOCKED`

The product is substantially closer to the intended canonical EEE than Pass 1 suggested. Confirmed supported foundations include Internal Memory, Analog Fill, reusable Dynamos, Popup open/context, alarms, historian, alarm history, operator shell, project assets and logical Source/TAG separation suitable for later Modbus remapping.

Confirmed gaps remain and must be corrected as product capabilities before the canonical DEMO is allowed to depend on them.

## 4. Binding Product Owner decisions after Pass 2

### 4.1 Popup X/Y is mandatory

Persisted authorable Popup X/Y is a required product correction before C11 implementation release.

Required path:

`Engineering authoring -> persisted canonical position -> Save -> Publish -> Activate -> Active Runtime logical coordinate transform`

Centered/shell-defined placement is not an accepted substitute. DEMO-specific CSS/DOM positioning, hidden package editing or private `left/top` state is forbidden.

### 4.2 The EEE Simulation must be buildable from normal product tools

EliteSCADA must provide generic product mechanisms sufficient for an integrator to build the future living deterministic EEE Simulation normally.

The product must not receive an EEE-specific simulator service merely to satisfy the DEMO.

The future C11 project must be able to author, through supported public mechanisms:

- inflow raising wet-well level;
- pumps lowering level;
- thresholds and sequencing;
- duty/standby alternation;
- two-pump demand;
- faults/trips;
- bad/stale/unavailable quality scenarios;
- coherent flow/pressure/current/frequency values;
- alarms/events/history/trends driven by the same evolving canonical state.

If normal Server Memory, Server Runtime automation, quality or event contracts cannot express that class of model, that insufficiency is a product gap and must be corrected before C11.

Forbidden substitutes include `EeeSimulatorService`, historical Demo/Simulation internals, hard-coded DEMO pages, direct DOM/React mutation, hidden `.escadapkg` JSON and auth/licensing bypasses.

## 5. Pre-DEMO correction package authority

Canonical package plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

Immediate packages released for parallel development:

- **C12 — Server Runtime Automation / Generic Simulation Authoring**;
- **C13 — Canonical Simulation Quality Contract**;
- **C14 — First-Class Operational Events**;
- **C15 — Embeddable Multi-Pen Trend HMI Object**;
- **C16 — HMI Operational Command + Startup Screen + mandatory Popup X/Y**;
- **C17 — Internal Memory Authoring UX + Full Lifecycle E2E**.

Dependent package:

- **C18 — Embeddable Alarm + Event Browser HMI Objects + related history i18n**.

C18 remains implementation-blocked until Coordinator accepts the upstream C14 Event contract and the needed common embeddable-object contracts from C15/common visual baseline, then publishes an exact C18 implementation base.

## 6. Common DEV release base

The coordinator released C12-C17 from one common administrative checkpoint:

`2607e03d5445eefe1f434495d0ee81136c6cd220`

That commit contains the pre-DEMO correction coordination state over the unchanged frozen C10 product code. It does not supersede `97eefd8...` as the C11-audited product-code SHA.

Released branches:

- `wave14/c12-server-runtime-automation`;
- `wave14/c13-simulation-quality-contract`;
- `wave14/c14-operational-events`;
- `wave14/c15-hmi-trend-multipen`;
- `wave14/c16-hmi-command-navigation-popup`;
- `wave14/c17-memory-authoring-e2e`.

No package branch merges directly to `main`.

## 7. Parallel DEV publication status — latest coordinator recheck

Coordinator compared every immediate DEV branch against exact release base `2607e03d...` after the handoffs were issued.

Observed GitHub state:

| Package | Branch | Ahead of release base | Behind | Published state |
|---|---|---:|---:|---|
| C12 | `wave14/c12-server-runtime-automation` | 0 | 0 | no published commit yet |
| C13 | `wave14/c13-simulation-quality-contract` | 0 | 0 | no published commit yet |
| C14 | `wave14/c14-operational-events` | 0 | 0 | no published commit yet |
| C15 | `wave14/c15-hmi-trend-multipen` | 0 | 0 | no published commit yet |
| C16 | `wave14/c16-hmi-command-navigation-popup` | 0 | 0 | no published commit yet |
| C17 | `wave14/c17-memory-authoring-e2e` | 0 | 0 | no published commit yet |

The open-PR inventory also contains no C12-C17 package PR at this checkpoint.

This means **GitHub does not yet contain observable DEV implementation progress for C12-C17**. It does not prove that a DEV is idle: a chat may still be reading/auditing or may have unpushed local changes. Officially, however, only published repository state counts for coordination.

Coordinator must not infer architecture or progress from chat activity that has not been committed/pushed.

## 8. Intended dependency/integration route

Development C12-C17 may run in parallel. Initial intended integration order remains:

1. C13 quality contract;
2. C17 Memory authoring/E2E;
3. C14 Operational Events;
4. C16 Command/Home/Popup positioning;
5. C15 Trend Multi-Pen;
6. C12 Server Runtime Automation aligned to accepted C13 quality contract;
7. publish exact C18 base;
8. C18 Alarm/Event Browser objects;
9. C10 convergence cycle 2.

Coordinator may adjust this ordering when real diffs show a safer dependency route.

## 9. Acceptance expectations for DEV candidates

A package is not ready merely because it has commits.

For every C12-C17 candidate, Coordinator must verify:

- exact ancestry from the authorized base or explicitly approved rebased base;
- no scope theft from another package;
- architecture/security boundaries preserved;
- package-specific tests;
- universal EliteSCADA CI where required;
- impacted specialized workflows;
- exact candidate HEAD associated with every claimed validation;
- diagnosis of any failure before rerun;
- handoff documentation with known limitations and dependency changes.

C12 must not hide EEE-specific simulation logic in product code. C13 must not grant Client Visual arbitrary authority over physical Driver quality. C14 must keep Event distinct from Alarm/Audit. C15 must provide canonical persisted embeddable Multi-Pen Trend, not a DEMO React component. C16 must implement real command/startup/Popup contracts. C17 must prove normal Internal Memory authoring without private provider IDs/package editing.

## 10. C11 remains implementation-locked

C12-C18 correct the product. They do not authorize building the canonical EEE DEMO.

After all approved corrections:

1. integrate accepted candidates into `wave14/corrections-integration`;
2. run universal and impacted specialized workflows;
3. execute C10 convergence cycle 2;
4. freeze a new exact product-code SHA;
5. revalidate every affected C11 finding against that SHA;
6. execute remaining real-browser acceptance checks;
7. only after blocking gaps are cleared issue explicit `RELEASE C11 IMPLEMENTATION`.

At release create/finalize:

`docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`

Then implement the canonical EEE DEMO Simulation exclusively through normal corrected product contracts.

## 11. Future C11 implementation target

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

## 12. Preview and Wave 13 boundaries

Issue #208 / PR #210 remains Preview infrastructure/history, not product authority. Preview should only be updated for the new canonical DEMO after C11 implementation exists through normal product flow.

Wave 13 issue #205 / PR #207 remains paused. Future Windows packaging/signing must consume the final accepted Wave 14 product bytes, not a stale pre-Wave14 snapshot.

## 13. Mandatory resume reading

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

## 14. Exact next coordinator actions

1. monitor C12-C17 branch heads for first published DEV candidates;
2. when a branch advances, inspect diff/scope immediately rather than waiting for all packages;
3. keep C18 implementation blocked pending upstream contracts;
4. review every DEV candidate against exact ancestry, scope and CI;
5. integrate accepted corrections with expected-head protection;
6. run affected exact-head workflows after every product integration step as required by policy;
7. establish the C18 base only after accepted prerequisite contracts exist;
8. integrate C18;
9. run C10 convergence cycle 2 and freeze the new exact product SHA;
10. re-audit affected C11 rows and execute remaining browser acceptance;
11. if all blocking product gaps are cleared, explicitly release C11 implementation;
12. build the EEE Simulation only through corrected normal product tools;
13. later validate the same conceptual application against the physical Modbus PLC;
14. complete full CI, Preview/Codespaces and Product Owner homologation;
15. only after Wave 14 acceptance resume Wave 13 packaging/signing.
