# Wave 15 — FC0-A Foundation Closure Audit Evidence Matrix

> PREPARATORY ONLY. This file is not an audit PASS and does not release FC0-A.
> Final authority is the exact post-FND06 checkpoint plus independent AUD review.

`AUDIT_ID: FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

`MATRIX_REV: 0002`

`STATE: PREPARED / PENDING_INFRA_CI_01B_AND_FND06_FREEZE_AND_INDEPENDENT_AUDIT`

## 1. Exact checkpoint currently under post-merge validation

- FND-06 candidate: `2257f8f99b5e6deac80d64ed2cc0c43aa8dab1cc`
- candidate tree: `2ebb839a788bb4fad249877689c25ac1b18f6d74`
- FND-06 merge: `624f2eca456310a2c6156538b3616a06e3be075f`
- merge tree: `fb864fb954b0123e69db379cd6b3120349b43600`
- candidate T1: `35939646387` — SUCCESS
- post-merge broad CI: `35940661531` — FAILURE due generic PostgreSQL schema initialization race; FND-06 visual causality not established.

No row below may become final `CLOSED_FOUNDATION` or `RELEASE` solely from this preparatory matrix.

## 1A. Infrastructure blocker classification

The exact FND-06 merge checkpoint cannot yet become the FC0-A audit base because its broad CI is red for a generic shared-schema PostgreSQL race.

Required correction:
`INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`

This blocker does not alter the provisional FND-06 contract-risk conclusions, but it blocks the exact checkpoint/freeze requirement.

The final matrix must use the integration SHA **after** INFRA-CI-01B and its exact green broad CI, not `624f2eca...` alone.

## 2. Wave 14 -> Wave 15 closure matrix — provisional

| Source item | Wave 15 owner | Current exact evidence | Provisional status | Residual to independently verify | FC0-A impact |
| --- | --- | --- | --- | --- | --- |
| W15-P0-01 Working identity/bootstrap | FND-01 | Frozen lifecycle contract in Main handoff; Working/Published/Active separation retained by FND-06 | PENDING_FINAL_AUDIT / expected CLOSED_FOUNDATION | Recheck no FND-06 editor compatibility path mutates Active authority | BLOCK if lifecycle weakened |
| W15-P1-01 Server Script recovery/observability | prior Wave15 Script/runtime correction | Frozen runtime behavior referenced by Wave15 backlog/control history | PENDING_FINAL_AUDIT | Correlate exact implementation PR/tests and ensure no unresolved throttle/recovery P1 remains | BLOCK if unresolved P1 |
| W15-P1-02 known legacy visual compatibility | FND-06 | PR #337 final candidate + mounted Screen/Popup compatibility evidence; exact merge `624f2eca...` | PENDING_POST_MERGE_GREEN / expected CLOSED_FOUNDATION | Verify broad post-merge CI and exact frozen contract | BLOCK until green/frozen |
| W15-P1-03 / Wave14 A7 mounted Screen/Popup selection crash | FND-06 | `fnd06-mounted-legacy-selection.spec.ts`; Screen tank/value/dynamo/status + Popup value/status; T1 `35939646387` green | PENDING_POST_MERGE_GREEN / expected CLOSED_FOUNDATION | Independent AUD checks mounted evidence and post-merge CI | BLOCK until green/frozen |
| W15-P1-04 Runtime projection/navigation persistence | FND-06 | retryable Popup persistence + Active-identity reset tests in PR #337 | PENDING_POST_MERGE_GREEN / expected CLOSED_FOUNDATION | Recheck same-identity retain vs genuine Active reset semantics | BLOCK if inconsistent |
| W15-P1-05 readable TAG Foundation portion | FND-04 | FND-04 frozen at `6c810647...`; exact post-merge CI `35913456486` SUCCESS | PENDING_FINAL_AUDIT / expected CLOSED_FOUNDATION | Recheck no FND-06/FND-05 future contract redefines it | No current blocker |
| W15-P1-05 A8 cursor-safe insertion / API discovery / event-aware authoring | DEV-SCRIPT-ENGINEERING | Explicit downstream FC0-A order prepared | PENDING_FINAL_AUDIT / expected READY_FOR_DOWNSTREAM_DEV | Confirm all remaining A8 items are owned by DEV lane, not missing Foundation | BLOCK if Foundation prerequisite still missing |
| W15-P1-06 truthful Engineering/SPA error/fallback UX | mixed / downstream | Existing backlog owner must be re-correlated | PENDING_FINAL_AUDIT | Identify exact owner/evidence; ensure no fictitious Demo/HTTP state remains as P1 blocker | BLOCK if unowned P1 |
| W15-P2-01 Trends bounded follow-up | downstream bounded | Wave15 backlog | PENDING_FINAL_AUDIT / expected DEFERRED_BOUNDED_WITH_EVIDENCE | Verify not a hidden P1 | non-blocking if bounded |
| W15-P2-02 Popup live value/freshness | downstream bounded | Wave15 backlog | PENDING_FINAL_AUDIT / expected DEFERRED_BOUNDED_WITH_EVIDENCE | Verify not a hidden P1 | non-blocking if bounded |
| W15-P2-03..07 shared UI/accessibility/navigation/templates | downstream | Wave15 backlog | PENDING_FINAL_AUDIT / expected READY/DEFERRED | Map each owner | non-blocking only if bounded |
| W15-U-01 transport/forwarding latency | evidence-bounded uncertain | Wave14 chronology | PENDING_FINAL_AUDIT / expected DEFERRED_BOUNDED_WITH_EVIDENCE | No speculative product fix without correlated repro | non-blocking if still uncertain |
| W15-U-02 Alarm timestamp semantics | evidence-bounded uncertain | Wave14 chronology | PENDING_FINAL_AUDIT / expected DEFERRED_BOUNDED_WITH_EVIDENCE | Recheck no newer confirmed evidence | non-blocking if still uncertain |
| RECHECK-SIM-PUMP-LEVEL | evidence-bounded uncertain | Wave14 transfer comments | PENDING_FINAL_AUDIT | Must not be promoted to confirmed Script defect without correlated repro | non-blocking absent repro |
| protected whole-system backup/restore | future requirement | FND-07/System Recovery future work | NOT_A_WAVE14_DEFECT / PENDING_SCOPE_CONFIRMATION | Preserve as future requirement | no FC0-A block unless prerequisite discovered |
| Historian Administration | future requirement | future roadmap | NOT_A_WAVE14_DEFECT / PENDING_SCOPE_CONFIRMATION | keep separate | no FC0-A block |
| raw->engineering scaling / decimal authoring | future requirement | future roadmap | NOT_A_WAVE14_DEFECT / PENDING_SCOPE_CONFIRMATION | keep separate from current defects | no FC0-A block |
| real EEE Modbus/PLC variant | future requirement | future roadmap | NOT_A_WAVE14_DEFECT / PENDING_SCOPE_CONFIRMATION | keep separate | no FC0-A block |
| Wave 13 signed-release work | future requirement | future roadmap | NOT_A_WAVE14_DEFECT / PENDING_SCOPE_CONFIRMATION | keep separate | no FC0-A block |

## 3. Frozen-contract compatibility — provisional

### DEV-EDITOR

Expected contracts:
- FND-06 canonical renderer/public visual model;
- centralized known-legacy compatibility boundary;
- Working-design != Active Runtime authority.

Current risk: `LOW / GUARDED`.

Final AUD must verify DEV-EDITOR can implement single-primary-canvas UX without:
- bypassing `getVisualSchemaForEngineering` for known persisted legacy types;
- creating a second renderer/schema registry;
- weakening arbitrary-unknown fail-closed behavior;
- coupling design preview to Runtime Active authority.

### DEV-SCRIPT-ENGINEERING

Expected contracts:
- FND-04 readable source token -> persisted binding -> stable TagId proof;
- existing Script sandbox/Authority boundaries.

Current risk: `NONE IDENTIFIED / GUARDED`.

Final AUD must verify prepared FND-05 only adds node execution fencing around Script runtime and does not redefine FND-04 source/binding semantics.

### DEV-AUTHORITY-UX

Expected contracts:
- FND-02/AUTH-04 capability/scope/effective-permission semantics;
- Runtime Session Class remains a ceiling, not a role.

Current risk: `NONE IDENTIFIED / GUARDED`.

Final AUD must verify FND-07 detach composes Authority/session invalidation without redefining evaluator semantics.

### DEV-LICENSING-UX

Expected contracts:
- FND-03 machine-license v2 trust;
- transactional install/replace/remove;
- one logical Runtime Session Lease;
- shared Interactive/ViewOnly quota semantics.

Current risk: `LOW BUT MATERIAL RESIDUAL`.

Hard guard already added to FND-05 rev 0003:
- HA redundancy entitlement/readiness must be additive/backward-compatible;
- existing FND-03 Standalone/Single-Server license meaning cannot change;
- no reinterpretation of Interactive/ViewOnly/session class/quota, machine binding or transactional lifecycle.

If HA cannot satisfy this, result is `BLOCKED-CONTRACT` before FC0-A release.

## 4. Future Foundation compatibility — provisional

| Foundation | Current hypothesis | Required final proof |
| --- | --- | --- |
| FND-05 | ADDITIVE / EXPECTED COMPATIBLE | Preserve FND-03 lease/license semantics, FND-04 Script identity contract, FND-06 renderer authority; topology/fencing/replication only additive |
| FND-07 | COMPOSITIONAL / EXPECTED COMPATIBLE | Compose FND-01 lifecycle + FND-02 Authority + FND-03 licensing; no semantic redefinition |

## 5. Finalization procedure

After post-merge CI `35940661531` is fully green:

1. Main marks FND-06 VERIFIED/FROZEN on exact merge SHA/tree.
2. Audit control moves to ACTIVE.
3. Main replaces provisional statuses with exact evidence-backed statuses.
4. Independent AUD reads the mandatory Wave14 comments and all frozen contracts, then reviews this matrix.
5. Any unresolved confirmed P0/P1 or breaking future Foundation contract yields `CHANGES_REQUIRED` or `BLOCKED-CONTRACT`.
6. Only final `ACCEPTABLE / FC0A_RELEASE_APPROVED` permits the four DEV lanes plus FND-05/FND-07 to activate.
