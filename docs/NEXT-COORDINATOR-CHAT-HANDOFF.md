# EliteSCADA — Next Coordinator Chat Handoff

**Prepared:** 2026-09-05 BRT  
**Purpose:** authoritative resume point because the current Coordinator chat reached its duration/context limit.

> **GitHub is the official memory. Revalidate everything live before acting.** Do not trust this file over live refs/workflows if they differ. Documentation-only handoff commits after the recorded product/test SHA do not redefine product authority.

## 1. Repository and permanent boundary

Repository: `brunolrogerio-collab/EliteSCADA`

Wave14 coordination:

- issue #211: active Product Owner validation;
- integration branch: `wave14/corrections-integration`;
- integration HEAD at handoff: `cac8b6d58a4969a4aa6369590f6bd32fc2fae3d2`;
- integration PR #212: **OPEN / DRAFT / base `main`**;
- `main` at last revalidation: `edbdf446ea657713bdc487be91bf10bfcd03c684`.

**NON-NEGOTIABLE:** PR #212 must never merge to `main` without later explicit Product Owner authorization.

Wave13 issue #205 / PR #207 remains paused until final accepted Wave14 bytes.

## 2. Current C11 branch

C11 canonical EEE DEMO branch:

`wave14/c11-canonical-eee-demo`

Implementation PR:

#263 OPEN/DRAFT -> `wave14/corrections-integration`

Validation-only CI-trigger PR:

#266 OPEN/DRAFT -> `main` — **NEVER MERGE**.

#266 exists only because several workflows are scoped to PRs targeting `main`. The real route is always #263 -> integration, then later #212 only after Product Owner authorization.

Exact C11 product/test head before this documentation handoff:

`043ee86dd4815a253978b032750a1498a6c89df9`

The branch will be ahead of that SHA because this handoff updated documentation. Those newer docs-only SHAs are not new validated product authority.

## 3. Product authority already converged

Pre-DEMO combined C12–C19 product authority:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

C20 and C21 were discovered by C11 and fixed generically before continuing.

### C20

C11 exposed a visual dynamic enum seam:

`Engineering authoring PascalCase -> persisted Active camelCase -> frontend runtime comparisons`

C20 added generic wire normalization and Active Runtime regression for Analog Fill. Do not restore any C11-specific workaround.

### C21

C11 exposed a Dynamo TagReference Active-wire problem: valid authoring omitted scalar `value`, but Active serialization materialized `value:null`, and Runtime rejected it. C21 fixed the generic normalizer and proved that one Dynamo definition supports two independent TagReference instances and subscriptions.

Accepted C21 PRODUCT SHA:

`6d0d71bc91b08114f4c3d3238b56e4ca225b76bd`

Green evidence:

- EliteSCADA CI #1387;
- Wave11 #315;
- Preview Licensing #337;
- L3 #293;
- Interop #214.

C21 integration merge:

`cac8b6d58a4969a4aa6369590f6bd32fc2fae3d2`

C11 consumed that integration with a real merge. Do not fork local C20/C21 behavior in the DEMO.

## 4. C11 canonical DEMO intent

This is not a copy of the historical EliteSCADA Demo Runtime.

Build/finish a real canonical application:

- key `eee-demo`;
- name `EliteSCADA — EEE Demo`;
- startup Screen `eee.overview`;
- logical HMI coordinate space 1920×1080.

Construction authority:

`Engineering definition -> JSON Import Preview -> Apply -> Save -> Publish -> Activate -> Active HMI Runtime -> project-package export`

No private DB/cache/runtime writes, no package-byte hacks, no EEE-specific backend/Driver/service, no legacy `SimulationDriver`/`DemoRuntimeServices` as canonical engine.

## 5. DEMO foundation already implemented

Simulation Source:

- key `eee.sim.server-memory`;
- `builtin.memory.server`;
- server-authoritative process state.

The foundation has stable conceptual EEE TAGs for process/P01/P02 and command request TAGs.

Server Script:

- server scope;
- deterministic 1 s model;
- mass-balance level behavior;
- normal/high demand;
- automatic duty/standby control;
- duty alternation;
- manual commands;
- fault injection/reset;
- server-authoritative quality scenario;
- transition-only Operational Events;
- one-shot Command request acknowledgement;
- optimized writes to stay within normal Script runtime budget.

Alarm/Event semantics are distinct. Do not copy the old FvDesigner misuse of normal pump status as alarm.

Foundation browser tests already proved:

- lifecycle;
- hydraulic evolution;
- one/two pump operation;
- cycle/duty behavior;
- Commands;
- fault Alarm;
- quality `Unavailable`;
- Historian;
- Operational Events;
- Script diagnostics.

## 6. HMI already implemented

Files:

- `web/scada-web/tests-wave11/c11-eee-demo-hmi.ts`;
- `web/scada-web/tests-wave11/c11-eee-demo-hmi.spec.ts`.

Current HMI includes:

- main operator overview;
- live well Analog Fill;
- numeric level/flow/pressure values;
- one reusable pump Dynamo with P01/P02 independent instances;
- P01/P02 contextual Popups;
- Commands;
- Instrumentation/Process screen;
- Electrical screen;
- Operation/scenario screen;
- Trends screen with Multi-Pen Trend;
- Alarm/Event screen with Alarm Browser + Event Browser;
- startup `eee.overview`;
- persisted Popup X/Y;
- logical viewport/scaling tests;
- visible quality-unavailable behavior.

The builder has already been aligned with canonical visual property names and composes from the canonical C11 foundation.

## 7. Real EEE source material

The Product Owner originally supplied real FvDesigner material from an operating EEE:

- `tags(1).csv`;
- `alarm.csv`;
- 15 PNG captures representing 14 unique Screens because the `ESCALAS` image was duplicated.

Repository authority:

`docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`

The material was also recovered from the user's file Library during the previous chat. Do not ask the Product Owner to resend before searching repository/Library.

Important rules:

- canonical EliteSCADA TAG names may be cleaner/normalized;
- supplied **Modbus addresses are primary authority** for the later PLC-backed variant;
- preserve a mapping canonical semantic TAG <-> real address/type/comment;
- original alarm CSV mixes actual alarms and normal operational/status events;
- canonical Alarm, Operational Event and Audit remain separate.

## 8. Two intended EEE deliverables

A. Canonical self-contained Simulation project/package using generic Server Memory + Server Script.

B. Later derived project/package using the real Modbus addresses so Product Owner can communicate with an existing PLC running the real EEE logic.

Do **not** build B yet. Product Owner has inserted generic product gates between A and B; see section 11.

## 9. EXACT CURRENT VALIDATION / IMMEDIATE BLOCKER

Exact C11 product/test SHA:

`043ee86dd4815a253978b032750a1498a6c89df9`

Green:

- EliteSCADA CI #1392 / `33949584309`;
- Preview Licensing #342 / `33949584221`;
- L3 Seven-Driver Lab #298 / `33949584220`;
- Interop Lab Smoke #219 / `33949584204`.

Red:

- Wave11 Active HMI Runtime #320 / `33949584209`.

**This red is diagnosed. Do not rerun unchanged. Do not open a product gap.**

Wave11 #320 reached C11 HMI after all preceding product tests passed:

- 16 tests passed;
- 1 C11 HMI test failed;
- 4 downstream tests did not run because the dependency chain stops on failure.

Exact failure:

`getByRole('button', { name: 'TENDÊNCIAS' })` is ambiguous because the overview intentionally contains two legitimate buttons named `TENDÊNCIAS`:

1. the top navigation button;
2. the quick-access status-panel button.

The failure is at approximately line 146 of:

`web/scada-web/tests-wave11/c11-eee-demo-hmi.spec.ts`

## 10. FIRST ACTION — DO THIS BEFORE ANYTHING ELSE

After mandatory live revalidation:

1. inspect the failing locator around line 146;
2. choose a deterministic selector for the intended Trends control, preferably scoped to the correct navigation context or stable object identity;
3. **do not remove/rename a legitimate UI control merely to satisfy Playwright**;
4. do not change product Runtime/backend for this failure;
5. commit one new exact C11 candidate SHA;
6. let #266 trigger the five normal gates;
7. diagnose any red before rerun.

If that Wave11 run moves deeper and exposes a new problem, diagnose that new point normally. Do not assume the DEMO is accepted merely because this selector is trivial.

## 11. Product Owner decisions made immediately before handoff

Binding record:

`docs/WAVE14-C11-POST-DEMO-SYSTEM-RECOVERY-SCALING-GAPS.md`

### `.escadapkg`

Product Owner agrees it should remain an **application package**. It should not silently contain local login/password data, Historian history or host secrets.

### Whole-system backup/restore

Application portability is not disaster recovery. EliteSCADA needs a separate, protected system backup/restore concept and Administration/workflow covering the required system stores without violating security/licensing boundaries.

This should audit at least project/revision state, local identity/roles, Historian, Alarm/Event/Audit persistence where external, host config/secrets and explicit machine-bound licensing/trust exclusions.

### Historian

Historian remains external to `.escadapkg`. Product Owner wants an Administration/operational workflow to export/backup and import/restore historical data safely, with project/TAG identity compatibility rather than blind database copying.

### TAG scaling — confirmed PRODUCT GAP

Real EEE Modbus case demonstrates why this is needed.

Example:

- Holding Register raw value `100`;
- intended HMI engineering value `1.00 m`.

Need first-class generic TAG-level raw -> engineering scaling, not repeated Screen expressions or EEE Script conversion. HMI, Alarm, Historian and Trend should consume the engineering value consistently. Writes require explicit inverse/fail-closed semantics.

### Decimal places — confirmed PRODUCT GAP

Operator/Engineering must be able to choose numeric display precision, e.g. level `1.00 m`, current/frequency one decimal, counters zero decimals.

Formatting is distinct from scaling. Decimal places affect only presentation; scaling defines engineering value.

The fixture already uses decimal formatting metadata programmatically in places, but Product Owner requires normal human Engineering authoring/persistence through lifecycle/package round-trip.

## 12. Binding new sequence

Product Owner explicitly fixed this order:

1. finish the canonical Simulation DEMO;
2. close C11 HMI/application repository validation;
3. produce canonical `eee-demo` project artifact through normal export;
4. inspect and Preview-import exported package; record SHA-256/provenance;
5. then resolve/audit whole-system backup/restore + Historian administration;
6. implement/resolve generic TAG scaling;
7. implement/resolve decimal-place human authoring/runtime persistence;
8. exact-SHA validation of those generic corrections;
9. update Preview harness to consume the canonical EEE DEMO;
10. Product Owner performs fresh Codespace visual/product homologation;
11. fix/revalidate findings;
12. only after that build the real Modbus/PLC EEE variant;
13. final Wave14 acceptance;
14. resume Wave13 packaging/signing only on final accepted Wave14 bytes.

Scaling and decimal places are **not required to stop finishing the current Simulation DEMO**, unless the current application itself cannot proceed without them. They are mandatory **before Codespace homologation and before the Modbus/PLC variant**.

## 13. Canonical package/export detail

The product already exposes the intended package path:

- `GET /api/project-package/export`;
- package inspect;
- package import preview, including `CreateAndUpdate` mode where applicable.

Do not treat the historical Wave11 `owner-test-artifact.spec.ts` as final C11 package authority. It exports the `e2e-wave11` harness / historical `demo.overview` identity.

The final C11 package must be generated under actual project key `eee-demo` so its manifest/application identity is correct.

## 14. Hard boundaries

- never merge #212 to main without Product Owner authorization;
- never merge #266;
- #263 targets integration only;
- backend Active revision remains authority;
- no EEE-specific simulator product code;
- no direct history insert;
- no security/licensing/auth bypass;
- no Alarm/Event/Audit conflation;
- no hidden fixture-only workaround for a generic missing capability;
- if a normal required capability is absent, stop and classify PRODUCT GAP;
- diagnose every red before rerun.

## 15. Mandatory resume order

Read in this order, revalidating live versions:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. this file;
5. `docs/WAVE14-C11-IMPLEMENTATION-RELEASE.md`;
6. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
7. `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-HANDOFF.md`;
8. `docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`;
9. `docs/WAVE14-C11-POST-DEMO-SYSTEM-RECOVERY-SCALING-GAPS.md`;
10. C11 historical Pass-2 audit + HMI clarification docs;
11. C20 visual dynamic contract doc;
12. C21 Dynamo TagReference contract doc;
13. `docs/CI-VALIDATION-POLICY.md`;
14. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
15. live issue #211;
16. live PRs #212, #263, #266;
17. current workflow history for live C11 head.

Then fix only the diagnosed ambiguous `TENDÊNCIAS` test locator and continue from evidence.

Do not ask the Product Owner to reconstruct decisions already committed to GitHub. The previous chat already spent enough time excavating its own history.