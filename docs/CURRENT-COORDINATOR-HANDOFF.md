# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-05 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION ACTIVE / C20+C21 INTEGRATED / C11 HMI NEAR-GREEN / POST-DEMO PRODUCT GAPS RECORDED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs and exact-SHA workflows before acting. Documentation-only commits do not redefine product authority.

## 1. Permanent gates

- backend Active revision is canonical Runtime authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- lifecycle remains `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- diagnose any red before rerun;
- never weaken tests/security/contracts/identity/licensing/lifecycle for green;
- Alarm / Operational Event / Audit remain semantically separate;
- no EEE-specific Driver/service/private runtime/hidden package/DEMO-only bypass;
- no direct history insertion;
- PR #212 remains OPEN/DRAFT and must **never merge to `main` without later explicit Product Owner authorization**;
- Wave13 issue #205 / PR #207 remain paused until final accepted Wave14 bytes.

## 2. Live repository state at handoff

Repository: `brunolrogerio-collab/EliteSCADA`

Integration:

- branch `wave14/corrections-integration`;
- exact HEAD `cac8b6d58a4969a4aa6369590f6bd32fc2fae3d2`;
- PR #212 OPEN/DRAFT -> `main`;
- `main` was `edbdf446ea657713bdc487be91bf10bfcd03c684` at last live revalidation.

C11:

- branch `wave14/c11-canonical-eee-demo`;
- implementation PR #263 OPEN/DRAFT -> `wave14/corrections-integration` only;
- validation-only PR #266 OPEN/DRAFT -> `main`, **NEVER MERGE**, used solely because repository workflows are main-scoped;
- exact C11 product/test head before this documentation handoff: `043ee86dd4815a253978b032750a1498a6c89df9`.

The branch is now ahead by documentation-only handoff commits. Do not reinterpret those docs commits as a newly validated product SHA.

## 3. Pre-DEMO product authority and C20/C21

Exact combined C12–C19 pre-DEMO product authority:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

C20 was opened because C11 exposed a generic visual dynamic wire mismatch: Engineering authoring used PascalCase enum values while persisted Active wire used camelCase. C20 fixed the frontend visual-dynamic boundary generically and proved persisted Analog Fill behavior.

C21 was later opened because C11 exposed a generic Dynamo `TagReference` wire/runtime issue. Active serialization materialized `value: null`, while runtime validation rejected the shape. C21 also proved that one Dynamo definition can serve two instances with distinct TagReferences/subscriptions/rendered state.

Accepted C21 PRODUCT SHA:

`6d0d71bc91b08114f4c3d3238b56e4ca225b76bd`

Exact C21 gates were green:

- EliteSCADA CI #1387;
- Wave11 #315;
- Preview Licensing #337;
- L3 #293;
- Interop #214.

C21 was integrated only into integration, merge commit:

`cac8b6d58a4969a4aa6369590f6bd32fc2fae3d2`

C11 then consumed the integrated C21 with a real two-parent merge. Do not reintroduce local divergent C20/C21 copies.

## 4. Canonical EEE DEMO architecture already built

The canonical application is a new real EliteSCADA project, not the historical Demo Runtime.

Stable application identity:

- project key: `eee-demo`;
- project name: `EliteSCADA — EEE Demo`;
- startup/main Screen: `eee.overview`;
- logical design: 1920×1080.

Simulation process authority:

- Source key `eee.sim.server-memory`;
- Driver `builtin.memory.server`;
- one server-authoritative deterministic Server Script;
- no Client Memory as process truth;
- no EEE-specific simulator Driver/service.

The current package includes conceptual process/P01/P02 TAGs, Commands, Alarms, Operational Events, Historian points and quality scenario.

Important deterministic behaviors already proven in foundation tests include:

- level/inflow hydraulic evolution;
- automatic duty pump start;
- high demand and both pumps;
- low-level cycle completion + duty alternation;
- manual P01/P02 Commands;
- fault injection/reset;
- server-authoritative `Unavailable` quality scenario;
- Historian samples;
- Operational Event emission;
- one-shot Command request acknowledgement.

## 5. HMI currently built

Current HMI builder/spec files:

- `web/scada-web/tests-wave11/c11-eee-demo-hmi.ts`;
- `web/scada-web/tests-wave11/c11-eee-demo-hmi.spec.ts`.

The HMI already assembles:

- `eee.overview` startup/operator overview;
- live suction-well Analog Fill;
- P01/P02 using one reusable `eee.dynamo.pump` definition with independent TagReference parameters;
- pump Popups;
- process/instrumentation/electrical/operation support Screens;
- Trend Screen with Multi-Pen `core.trend`;
- Alarm Browser;
- Event Browser;
- normal Runtime navigation/actions;
- Command execution;
- bad-quality visualization;
- logical viewport/scaling checks.

The HMI builder was corrected to use canonical visual properties (`textColor`, `strokeColor`, `cornerRadius`, etc.) and now composes on the canonical C11 foundation, not the obsolete early foundation variant.

## 6. Real EEE reference material

Product Owner supplied original FvDesigner material from an actual EEE:

- `tags(1).csv`;
- `alarm.csv`;
- 15 PNG captures representing 14 unique screens because `ESCALAS` was duplicated.

The durable repository mapping is:

`docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`

Do not ask the Product Owner to resend this material before searching repository/Library.

Critical interpretation:

- EliteSCADA canonical TAG names may be normalized and hierarchical;
- **real Modbus addresses from the supplied material are the primary authority for the later PLC-backed variant**;
- retain canonical semantic TAG -> original Modbus address/type/comment mapping;
- historical `alarm.csv` mixes true alarms and ordinary operational/status events;
- canonical application must separate Alarm from Operational Event; Audit remains separate again.

Two intended C11 deliverables:

1. canonical self-contained Simulation application/package;
2. later derived EEE package using real Modbus addresses for communication with the existing PLC logic while reusing the HMI/semantic architecture.

The second deliverable must not be built until the post-DEMO product gates below are resolved.

## 7. Exact current C11 validation

Exact product/test SHA under latest completed validation:

`043ee86dd4815a253978b032750a1498a6c89df9`

SUCCESS:

- EliteSCADA CI #1392 / `33949584309`;
- Preview Licensing #342 / `33949584221`;
- L3 Seven-Driver Lab #298 / `33949584220`;
- Interop Lab Smoke #219 / `33949584204`.

FAILURE:

- Wave11 Active HMI Runtime #320 / `33949584209`.

Wave11 #320 is fully diagnosed and is **not a product defect**.

It ran 21 tests in the serial Wave11 chain. Before the failure:

- C16 startup/lifecycle passed;
- C17 memory tests passed;
- C15 Trend passed;
- C16 operational runtime passed;
- C18 Browser passed;
- C19 operational events passed;
- C20 tests passed;
- C21 generic two-instance Dynamo tests passed;
- C11 foundation passed;
- C11 HMI progressed deep into the operator flow.

Final result:

- 16 passed;
- 1 failed;
- 4 downstream tests did not run because of dependency.

Exact failure in `c11-eee-demo-hmi.spec.ts` around line 146:

`page.getByRole('button', { name: 'TENDÊNCIAS' }).click()`

Strict mode found two legitimate controls with the same accessible name:

1. top navigation `TENDÊNCIAS`;
2. overview quick-access `TENDÊNCIAS`.

This is a test-selector ambiguity. Do not modify product code and do not rerun #320 unchanged.

## 8. FIRST TASK for next coordinator

1. revalidate C11 branch/PRs/workflows live;
2. inspect `c11-eee-demo-hmi.spec.ts` around the `TENDÊNCIAS` click;
3. replace the ambiguous selector with a deterministic locator scoped to the intended navigation control or stable object identity;
4. do not remove either real UI button merely to make the test unique;
5. commit one new exact C11 candidate SHA;
6. let validation-only PR #266 trigger the normal five gates;
7. diagnose any red before rerun;
8. if Wave11 becomes green, continue application-level C11 acceptance rather than declaring Wave14 finished.

## 9. Required work after C11 HMI becomes green

C11 acceptance still needs the final application artifact path:

1. create/prove the final canonical project with actual key `eee-demo`, not only the Wave11 harness key `e2e-wave11`;
2. `Save -> Publish -> Activate` normally;
3. export through `/api/project-package/export`;
4. inspect exported package;
5. Preview-import it through normal product package import preview (`CreateAndUpdate` where applicable);
6. record provenance and SHA-256;
7. version the accepted `.escadapkg` only after Active application acceptance.

The existing Wave11 `owner-test-artifact.spec.ts` is historical and exports `e2e-wave11` / `demo.overview`. It is not the final C11 artifact authority.

## 10. Product Owner decisions: package vs system state

Product Owner agrees that `.escadapkg` should remain application-specific.

It should **not** become a container for:

- local user passwords;
- Historian samples;
- host secrets;
- unrelated system state.

Application portability and whole-system recovery are distinct concerns.

A separate system backup/restore capability/Administration workflow must be designed so migration/disaster recovery can restore installation state without corrupting the application-package contract.

Read the binding record:

`docs/WAVE14-C11-POST-DEMO-SYSTEM-RECOVERY-SCALING-GAPS.md`

## 11. Post-DEMO PRODUCT GAPS now fixed in sequence

The Product Owner has classified the following as product/system gaps to handle **after the Simulation DEMO is assembled, but before fresh Codespace homologation and before the real Modbus/PLC variant**.

### 11.1 System backup / restore

Audit and define a protected recovery workflow covering, as applicable:

- applications/revisions;
- local identities/users/roles without plaintext password exposure;
- external Historian data;
- Alarm/Event/Audit persistent stores where outside the project package;
- host configuration;
- protected secrets/configuration;
- explicit handling/exclusion of machine-bound licensing/trust material;
- compatibility/restore validation.

### 11.2 Historian administration

Historian remains external to `.escadapkg`. Product Owner wants a normal Administration/workflow for export/backup and import/restore of historical data with identity/compatibility safety.

### 11.3 TAG engineering scaling

Confirmed generic gap, especially for the real Modbus EEE.

Example from the real PLC pattern:

- raw Holding Register = `100`;
- intended engineering value = `1.00 m`.

Required design must provide first-class TAG-level raw -> engineering scaling, preferably affine or equivalent raw-range/engineering-range semantics, and ensure normal HMI/Alarm/Historian/Trend consumers use the engineering value consistently. Write-enabled inverse scaling must be explicit/fail-closed.

Do not implement the conversion as an EEE-only Script or repeated visual expression.

### 11.4 Analog decimal-place display

Confirmed presentation gap. Engineering must allow ordinary configuration/persistence of decimal places for analog/numeric visual presentation after Save/Publish/Activate and package round-trip.

Scaling and formatting are different concepts:

- scaling changes raw transport value into engineering value;
- decimal places change only presentation.

C11 fixture currently uses formatting metadata programmatically in places; this does not satisfy the Product Owner requirement for normal human authoring.

## 12. Binding sequence from Product Owner

The sequence is now:

1. finish canonical EEE Simulation DEMO;
2. make the Simulation application-level C11 tests green;
3. export/re-preview canonical `eee-demo` package and record provenance/checksum;
4. resolve/audit system backup/restore + Historian administration requirements;
5. implement/resolve generic TAG scaling;
6. implement/resolve analog decimal-place authoring/runtime persistence;
7. run exact-SHA product validation for those generic corrections;
8. update Preview harness to consume the canonical EEE DEMO;
9. Product Owner performs fresh Codespace product/visual homologation;
10. correct and revalidate any finding;
11. only then build the second EEE package using the real Modbus addresses and test against the existing PLC logic;
12. final Wave14 acceptance;
13. resume Wave13 packaging/signing only on final accepted Wave14 bytes.

Scaling/decimal gaps are **not current blockers for finishing the Simulation DEMO** unless a generic mechanism becomes necessary to complete the present application. They **are blockers before Codespace homologation and before the Modbus variant**.

## 13. Mandatory resume order

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. `docs/WAVE14-C11-IMPLEMENTATION-RELEASE.md`;
6. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
7. `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-HANDOFF.md`;
8. `docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`;
9. `docs/WAVE14-C11-POST-DEMO-SYSTEM-RECOVERY-SCALING-GAPS.md`;
10. historical C11 Pass-2 audit/clarification docs;
11. C20 and C21 contract docs;
12. `docs/CI-VALIDATION-POLICY.md`;
13. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
14. live issue #211;
15. live PRs #212, #263, #266;
16. exact workflows on live C11 head;
17. start with the diagnosed ambiguous `TENDÊNCIAS` locator, not with product changes.

Do not ask the Product Owner to repeat decisions already recorded unambiguously in GitHub.