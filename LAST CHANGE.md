# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 CONVERGED GREEN / C11 PASS 2 CONSOLIDATED / C11 IMPLEMENTATION LOCKED / C12-C14 DEV DELIVERED — COORDINATOR REVIEW/INTEGRATION PENDING / C15-C17 ACTIVELY PUBLISHING IMPLEMENTATION / C18 DEFINED BUT IMPLEMENTATION-BLOCKED / WAVE 13 #205/#207 PAUSED**

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

C12-C17 product changes are not yet part of this frozen authority. A DEV delivery is not equivalent to Coordinator acceptance or integration.

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

## 3. C11 Pass 2 state

C11 audit branch:

`wave14/c11-pass2-product-gap-audit`

Canonical result:

`docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md`

State:

**PASS 2 CONSOLIDATED / IMPLEMENTATION LOCKED**

Recommendation:

`KEEP C11 IMPLEMENTATION LOCKED`

C11 implementation remains locked while the confirmed product gaps are corrected, integrated and revalidated.

Binding Product Owner decisions remain:

1. persisted authorable Popup X/Y is mandatory before C11 implementation release;
2. the living deterministic EEE Simulation must be buildable by an integrator from normal generic EliteSCADA product tools;
3. no EEE-specific simulator service, historical DEMO runtime, hidden package editing, direct DOM/React mutation, private host/Driver hook or auth/licensing bypass is acceptable.

## 4. Pre-DEMO correction package authority

Canonical package plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

Common DEV release base:

`2607e03d5445eefe1f434495d0ee81136c6cd220`

Immediate package branches:

- C12 — `wave14/c12-server-runtime-automation`;
- C13 — `wave14/c13-simulation-quality-contract`;
- C14 — `wave14/c14-operational-events`;
- C15 — `wave14/c15-hmi-trend-multipen`;
- C16 — `wave14/c16-hmi-command-navigation-popup`;
- C17 — `wave14/c17-memory-authoring-e2e`.

Dependent package:

- C18 — `wave14/c18-hmi-alarm-event-browsers` — implementation base not yet released.

No package branch merges directly to `main`.

## 5. Latest parallel DEV status

Coordinator rechecked all immediate package branches against exact release base `2607e03d...`.

### C12 — DEV DELIVERED / COORDINATOR REVIEW PENDING

Branch:

`wave14/c12-server-runtime-automation`

Observed ancestry at recheck:

- merge-base: exactly `2607e03d...`;
- 10 commits ahead;
- 0 behind.

Published scope includes:

- `ServerScriptRuntimeManager`;
- isolated Python Server Script executor/runner;
- activation and persisted-runtime recovery lifecycle integration;
- Runtime facade integration;
- coordinator changes needed for real Server Script execution;
- dedicated integration tests;
- `docs/WAVE14-C12-SERVER-RUNTIME-AUTOMATION.md`.

DEV delivery is acknowledged. Coordinator still must review architecture, exact candidate HEAD, claimed CI evidence, interaction with C13 quality contracts and absence of EEE-specific simulation logic before acceptance/integration.

### C13 — DEV DELIVERED / COORDINATOR REVIEW PENDING

Branch:

`wave14/c13-simulation-quality-contract`

Observed ancestry at recheck:

- merge-base: exactly `2607e03d...`;
- 4 commits ahead;
- 0 behind.

Published scope includes:

- server-authoritative sample publication contract;
- Internal Server Memory quality-aware publication path;
- Source-provider contract evolution;
- canonical TAG quality changes;
- backend/driver tests plus browser propagation coverage;
- `docs/WAVE14-C13-SIMULATION-QUALITY-CONTRACT.md`.

DEV delivery is acknowledged. Coordinator still must verify authority boundaries, normal physical-Driver quality isolation, exact candidate HEAD and CI evidence before acceptance/integration.

### C14 — DEV DELIVERED / COORDINATOR REVIEW PENDING

Branch:

`wave14/c14-operational-events`

Observed ancestry at recheck:

- merge-base: exactly `2607e03d...`;
- 27 commits ahead;
- 0 behind.

Published scope includes:

- first-class Operational Event domain models;
- Engineering registry/import-export contracts;
- Runtime Event integration;
- PostgreSQL operational-event history store;
- Historical Query integration;
- core/runtime/persistence test coverage;
- `docs/WAVE14-C14-OPERATIONAL-EVENTS.md`.

DEV delivery is acknowledged. Coordinator still must verify that Event remains distinct from Alarm/Audit, validate persistence/query semantics, exact candidate HEAD and CI evidence, then freeze the accepted Event contract needed by C18.

### C15 — ACTIVE DEVELOPMENT / IMPLEMENTATION PUBLISHED

Branch:

`wave14/c15-hmi-trend-multipen`

Observed ancestry at recheck:

- merge-base: exactly `2607e03d...`;
- at least 4 commits ahead;
- 0 behind.

Published work already includes canonical Trend authoring/runtime models, Multi-Pen editor/rendering, visual schema/Property Inspector integration and substantial browser/Runtime tests. Do not classify C15 as delivered until its DEV explicitly hands off a stable candidate and Coordinator revalidates the final branch head.

### C16 — ACTIVE DEVELOPMENT / IMPLEMENTATION PUBLISHED

Branch:

`wave14/c16-hmi-command-navigation-popup`

Observed ancestry at recheck:

- merge-base: exactly `2607e03d...`;
- at least 44 commits ahead;
- 0 behind.

Published work already touches Operational Command invocation, Startup/Home Screen contracts, Popup X/Y persistence/runtime positioning, Engineering configuration and browser/Runtime tests. Do not classify C16 as accepted or delivered merely from branch activity; final DEV handoff and Coordinator review remain required.

### C17 — ACTIVE DEVELOPMENT / IMPLEMENTATION PUBLISHED

Branch:

`wave14/c17-memory-authoring-e2e`

Observed ancestry at recheck:

- merge-base: exactly `2607e03d...`;
- at least 11 commits ahead;
- 0 behind.

Published work includes Internal Memory address-policy/Engineering corrections and lifecycle/browser coverage. Final DEV handoff and Coordinator review remain required.

## 6. Coordinator integration priority from this checkpoint

Do not wait for every parallel DEV before beginning candidate review.

Immediate Coordinator sequence:

1. review **C13** first because its canonical quality contract is consumed by/related to C12;
2. review **C14** next and freeze the Event contract required by C18;
3. review **C12** against the accepted C13 contract and generic Simulation-authoring requirement;
4. continue monitoring C15-C17 while their DEVs finish;
5. integrate only accepted exact candidates into `wave14/corrections-integration` with expected-head protection;
6. run required universal and impact-specific exact-head workflows after each product integration step;
7. once C14 and the needed common embeddable-object baseline are accepted/integrated, publish the exact C18 implementation base;
8. implement/integrate C18;
9. execute C10 convergence cycle 2;
10. freeze a new exact product-code SHA;
11. revalidate affected C11 findings and remaining browser acceptance;
12. only then consider explicit `RELEASE C11 IMPLEMENTATION`.

The earlier tentative integration order is subordinate to actual diffs and dependency review. C13 must not be bypassed when accepting C12 quality-related behavior.

## 7. C11 future implementation gate

After all approved product corrections are integrated:

`C12-C18 accepted`
`-> exact-head full/impacted CI`
`-> C10 convergence cycle 2`
`-> new exact product freeze`
`-> C11 affected-finding revalidation`
`-> real-browser acceptance`
`-> explicit C11 implementation release`
`-> finalize docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`
`-> build canonical EEE DEMO Simulation through normal Engineering`
`-> Save -> Publish -> Activate -> HMI Runtime`
`-> later physical PLC/Modbus validation`
`-> Preview/Codespaces + Product Owner homologation`
`-> accepted Wave 14 baseline`
`-> resume Wave 13 packaging/signing on accepted Wave 14 bytes`

The C11 DEMO must not be used as a workaround lane for any remaining product deficiency.

## 8. Preview and Wave 13 boundaries

Issue #208 / PR #210 remains Preview infrastructure/history, not product authority.

Wave 13 issue #205 / PR #207 remains paused. Future Windows packaging/signing must consume the final accepted Wave 14 product bytes, not a stale pre-Wave14 snapshot.

## 9. Mandatory resume reading

Read/revalidate in order:

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 consolidated audit on `wave14/c11-pass2-product-gap-audit`;
7. C11 HMI-object Product Owner clarification on that branch;
8. package handoff/documentation for each candidate being reviewed;
9. `docs/CI-VALIDATION-POLICY.md`;
10. live issue #211 and draft PR #212;
11. issue #208 / PR #210 only for Preview history;
12. issue #205 / PR #207 only for paused Wave13 context.

Then revalidate live GitHub before acting.
