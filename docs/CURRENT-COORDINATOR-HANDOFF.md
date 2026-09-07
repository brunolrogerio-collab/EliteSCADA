# CURRENT COORDINATOR HANDOFF — Wave 14

**Date:** 2026-09-07 BRT  
**State:** **C25 PRODUCT OWNER ACCEPTED / INTEGRATION EXACT-SHA GREEN / C11 HISTORY-PRESERVING SYNCHRONIZATION ACTIVE / PRE-SYNC C11 PACKAGE PORTABILITY RED DIAGNOSED / #212 OPEN-DRAFT NOT AUTHORIZED / WAVE13 PAUSED**

## 1. Rule zero

GitHub live state is the official and sole project authority. Before every decision, diagnosis or mutation, revalidate branch heads, PR states, relevant issue comments and exact-SHA CI. If this handoff differs from live GitHub, GitHub wins.

Do not treat documentation-only changes as new product authority.

## 2. Repository and protected route

Repository: `brunolrogerio-collab/EliteSCADA`

Integration branch:

`wave14/corrections-integration`

Integration PR:

#212 -> `main`

**#212 MUST remain OPEN/DRAFT and MUST NOT merge to `main` without a later, separate and explicit Product Owner authorization.**

Neither `siga`, green CI, mergeability, C25 acceptance, C11 acceptance nor Product Owner Preview success is authorization for #212 -> `main`.

Never mutate `main` directly. No force push, destructive rebase, branch deletion or unrelated cleanup.

## 3. C25 accepted and integrated

C25 PR: #283  
C25 issue: #282

Explicit Product Owner acceptance applies to exact product/test candidate:

`5193b81220f499cb2039dc1146c6dd1a7f7b3dbd`

Retained exact evidence:

- Wave 14 C25 Post-Demo #314 / run `34072225644` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #301 / run `34072225635` — SUCCESS.

Validated C25.10 coordination head:

`a781294ed2996888a8147c6f5fcaeb4eaddcf3c1`

#283 merged only into integration with merge commit:

`f282758d47c0419f3534f948658701467807758b`

C25 acceptance/integration does not authorize any merge to `main`.

## 4. Integration regression diagnosed, corrected and exact-SHA green

Post-C25 integration initially exposed six Chromium failures. They were diagnosed rather than blindly rerun.

Five failures shared one UI cause: when Engineering was unlocked, the C25 Engineering Lock management surface mounted its own locale picker while normal `EngineeringApp` also mounted the canonical Engineering locale picker. Tests correctly using `getByLabel('Idioma')` therefore found two controls.

The sixth failure was independent: `report-designer-workspace.spec.ts` used a broad `**/api/** -> 404` fallback and had not declared the newly required `/api/engineering/lock/status`; the Engineering Lock gate therefore failed before `EngineeringApp` mounted.

Generic correction commit:

`ff185ffd67fe4abc597af9184c21f86376ba6e17`

Message:

`fix(w14): restore Engineering E2E after lock integration`

Correction scope:

- remove only the redundant locale picker from **unlocked** Engineering Lock management;
- preserve the Lock-specific locale picker on the **locked** restricted surface;
- provide `{ configured: false, locked: false }` for `/api/engineering/lock/status` in the isolated Report Designer harness;
- no assertion, authorization, Authority or Engineering Lock contract weakened.

Exact-SHA validation on `ff185ffd...`:

- EliteSCADA CI #1425 / `34079800458` — SUCCESS, including Backend/Web/Chromium;
- Preview Licensing CI #373 — SUCCESS;
- Interop Lab Smoke #250 — SUCCESS;
- L3 Seven-Driver Lab #329 — SUCCESS;
- Wave 11 Active HMI Runtime #351 — SUCCESS.

This exact green integration parent is the authority consumed by the C11 synchronization.

## 5. C11 canonical branch and synchronization

Canonical EEE Demo branch:

`wave14/c11-canonical-eee-demo`

Implementation PR:

#263 -> `wave14/corrections-integration`, OPEN/DRAFT.

Validation-only PR:

#266 -> `main`, OPEN/DRAFT, **MUST NEVER MERGE**.

Pre-sync C11 head:

`41d24d89c3b9d2b881215255e44023fabde262f3`

Pre-sync C11 tree:

`967e982090a075760f57e8035c7a557d2c5f5bdd`

Green integration parent:

`ff185ffd67fe4abc597af9184c21f86376ba6e17`

Green integration tree:

`34a3036b8cc1746c016fc815736884095272f35f`

The two branches diverged from merge-base:

`5962bee401fadd700041e7c61cd430d4b4f28e27`

C11 was 38 commits ahead and 207 behind integration. Only 16 files differed on the C11 side; only two overlapped with integration changes:

- `LAST CHANGE.md`;
- `docs/CURRENT-COORDINATOR-HANDOFF.md`.

Therefore the synchronization uses a normal two-parent merge preserving both histories. The 14 non-conflicting C11 blobs are retained byte-for-byte; only the two documentation conflicts are reconciled to this current state.

Parents of the synchronization merge:

1. C11 first parent `41d24d89c3b9d2b881215255e44023fabde262f3`;
2. integration second parent `ff185ffd67fe4abc597af9184c21f86376ba6e17`.

Revalidate the live C11 branch after this commit to obtain the resulting merge SHA. This document intentionally does not guess its own commit SHA.

Temporary PR #284 was opened solely as a GitHub conflict probe (`integration -> C11`). It is not an integration path. Once this manual history-preserving merge is confirmed on the C11 branch, close #284 **without merge**.

## 6. C11 application architecture preserved

The canonical application is a real EliteSCADA project, not the historical Demo Runtime.

Identity:

- project key `eee-demo`;
- project name `EliteSCADA — EEE Demo`;
- startup Screen `eee.overview`;
- logical HMI coordinate space 1920×1080.

Construction/lifecycle authority:

`Engineering definition -> Import Preview -> Apply -> Save -> Publish -> Activate -> Active HMI Runtime -> project-package export`

Simulation authority uses generic product surfaces:

- Source `eee.sim.server-memory`;
- Driver `builtin.memory.server`;
- deterministic server-side process Script;
- no EEE-specific backend, Driver or private Runtime engine.

The C11 project includes conceptual EEE process/P01/P02 TAGs, Commands, Alarms, Operational Events, Historian/Trend, quality scenario, one reusable pump Dynamo with independent P01/P02 instances, Screens, Popups and operator navigation.

Alarm, Operational Event and Audit remain distinct concepts.

Real EEE reference material and canonical-to-Modbus mapping remain governed by:

`docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`

The later real Modbus/PLC variant must reuse generic product mechanisms and the supplied real addresses. Do not build it before the binding post-DEMO generic gates are resolved.

## 7. Pre-sync C11 exact validation and diagnosed blocker

Exact pre-sync C11 SHA:

`41d24d89c3b9d2b881215255e44023fabde262f3`

Normal gates:

- EliteSCADA CI #1410 / `33977314325` — SUCCESS;
- Preview Licensing CI #360 / `33977314302` — SUCCESS;
- Interop Lab Smoke #237 / `33977314294` — SUCCESS;
- L3 Seven-Driver Lab #316 / `33977314306` — SUCCESS;
- Wave 11 Active HMI Runtime #338 / `33977314297` — FAILURE.

Wave11 #338 is diagnosed. The normal Wave11 Active Runtime browser sequence passed **22/22**. The only failure occurred in the separate C11 canonical package portability gate using an isolated fresh `eee-demo` database/project.

Failure point:

`tests-wave11/c11-eee-demo-package.spec.ts`

During `Save -> Publish -> Activate`, Publish returned HTTP 400 because validation reported:

- code `DYNAMO_TEMPLATE_NOT_FOUND`;
- dynamo `dynamo.pump.standard`;
- referenced template `pump.standard` was not present in the fresh project.

Consequently the package could not reach Export -> Inspect -> Import Preview in that run.

Do not blind-rerun the old head. After synchronization, let the new exact C11 head validate against accepted integration bytes. If the same issue remains, determine why a fresh canonical project contains or receives `dynamo.pump.standard` without its template and correct the generic package/project/fixture source without deleting legitimate validation or adding EEE-only exceptions.

## 8. C11 package authority

The final canonical artifact must come from the actual `eee-demo` application through normal product endpoints and lifecycle.

Required proof:

1. create/use actual project key `eee-demo`;
2. Save;
3. Publish;
4. Activate;
5. `GET /api/project-package/export`;
6. inspect exported package;
7. Preview-import the package through normal product import preview (`CreateAndUpdate` where applicable);
8. verify portability/self-containment;
9. version/freeze `EliteSCADA-EEE-Demo.escadapkg`;
10. record SHA-256 and provenance.

Do not use the historical `owner-test-artifact.spec.ts` (`e2e-wave11` / `demo.overview`) as final C11 package authority.

## 9. Binding post-DEMO Product Owner decisions

Read:

`docs/WAVE14-C11-POST-DEMO-SYSTEM-RECOVERY-SCALING-GAPS.md`

### Application package boundary

`.escadapkg` remains an application/project package. It must not silently contain local user passwords, Historian samples, host secrets or unrelated host/system state.

Runtime/Active must not depend on external `.escadalib`; final `.escadapkg` remains self-contained according to accepted C25 Reusable Libraries contracts.

### Whole-system backup / restore

Application portability is distinct from disaster recovery. A separate protected system backup/restore capability and Administration workflow must cover appropriate application/revision state, local identity/roles, Historian and other persistent stores, host configuration/secrets and explicit machine-bound licensing/trust exclusions.

### Historian administration

Historian remains outside `.escadapkg`. Provide/audit a safe Administration workflow for backup/export and import/restore with project/TAG identity compatibility rather than blind database copying.

### TAG raw-to-engineering scaling

Confirmed generic product gap. Example: raw Modbus register `100` may represent `1.00 m`. Engineering needs first-class TAG-level raw -> engineering scaling so HMI, Alarm, Historian and Trend consume one canonical engineering value. Write inverse semantics must be explicit and fail-closed.

Do not implement this as an EEE-only Script or repeated visual expression.

### Decimal-place authoring

Confirmed generic presentation gap. Human Engineering must allow configured numeric display precision to persist through Save/Publish/Activate and package round-trip. Formatting is separate from scaling.

These generic gaps do not block finishing the current Simulation application unless needed by it, but they **do block fresh Product Owner Codespace homologation and the real Modbus/PLC variant**.

## 10. Permanent technical boundaries

Never obtain green by weakening:

- tests or validation;
- authentication/authorization/identity;
- backend Authority;
- Engineering Lock;
- licensing;
- lifecycle and Save/Publish/Activate;
- package or System Recovery semantics;
- Runtime authority or Active Revision;
- Drivers or security/distribution contracts.

Backend Active Revision remains canonical Runtime application authority.

Reusable Libraries retain accepted C25 semantics: Engineering-only; Engineering Lock; Association != Import; selective incorporation; incorporated content becomes project-owned canonical; association/disassociation does not destroy incorporated content; Runtime/Active does not depend on `.escadalib`; final `.escadapkg` is self-contained.

Installed Help keeps pt-BR/en/es and stable Topic IDs.

OpenDNP3/commercial dependency boundary remains valid.

No generic product defect may be hidden behind an EEE-specific workaround.

## 11. Immediate sequence after synchronization

1. revalidate live C11 branch, #263, #266, #284 and all workflows on the exact new C11 SHA;
2. close temporary #284 without merge after confirming the history-preserving synchronization;
3. diagnose every red before any rerun/correction;
4. finish canonical Simulation C11 application validation;
5. prove `Save -> Publish -> Activate`;
6. prove `Export -> Inspect -> Import Preview` and package portability;
7. freeze/version `EliteSCADA-EEE-Demo.escadapkg`, SHA-256 and provenance;
8. run exact C11 gates, using #266 only as a validation trigger and never as an integration route;
9. then resolve/audit whole-system backup/restore + Historian administration;
10. implement/resolve generic TAG scaling;
11. implement/resolve normal decimal-place human authoring/runtime/package persistence;
12. exact-SHA validate those generic corrections;
13. update Preview harness to consume canonical EEE DEMO;
14. perform fresh Product Owner Codespace/product/visual homologation;
15. correct/revalidate findings;
16. only then build and validate the real Modbus/PLC EEE variant;
17. obtain final Wave14 acceptance;
18. only after a later separate explicit Product Owner authorization may #212 merge to `main`;
19. validate the resulting exact new `main`;
20. only then resume Wave13 release/signing.

## 12. Wave13 pause

Wave13 issue #205 and PR #207 remain paused. PR #207 stays OPEN/DRAFT with preserved pre-Wave14-validation head:

`fda87ba4445127c174f6ea533a6bcabaabc7bb20`

Do not resume signing/release from that stale snapshot. Resume only from the approved and validated new mainline after the Wave14 sequence above.
