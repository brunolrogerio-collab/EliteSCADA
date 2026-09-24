# Wave 15 — FND-05 Main Coordinator Control Plane

> GitHub live is the sole authority. This file is PREPARED ONLY and does not authorize product mutation.

`CONTROL_BRANCH: coord/w15-fnd05-control`

`MAIN_ORDER_REV: 0004`

`STATE: PREPARED / NOT ACTIVE / HOLD_ON_FC0A_CONSOLIDATED_FINAL_ACCEPTANCE`

`PREPARED_ORDER_ID: FND05-DEV-HA-AUTHORITY-V1`

`LATEST_AUDITED_PRODUCT_CHECKPOINT: 560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

`ACTIVATION_BASE_RULE: revalidate latest wave15/corrections-integration product checkpoint before activation`

## 0A. Current hold reason

FND-06 is VERIFIED/FROZEN and the post-FND06 audit is complete with `CHANGES_REQUIRED`.

FND-05 remains **PREPARED / NOT ACTIVE** while the consolidated FC0-A correction candidate is still under Main/CODEX completion and final acceptance.

The second and third Main audit passes identified no breaking FND-05 contract risk. FND-05 remains classified additive/compatible and may activate only after Main records `FC0A_RELEASE_APPROVED` on the exact integrated checkpoint.

Executor policy changed by Product Owner/Main:
- implementation owner: **normal ChatGPT DEV chat**;
- Main owns contract/architecture review;
- scarce sequential CODEX is reserved for adversarial HA/concurrency validation, focused local tests and exact-head T1 after Main accepts the DEV candidate;
- material implementation defects return to the same FND-05 DEV;
- frozen-contract insufficiency returns `FND-05 -> BLOCKED-CONTRACT -> MAIN`.

Cross-lane coordination:
`coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`.

## 1. Purpose

FND-05 freezes the high-risk HA authority/topology/fencing contract required before EliteGO and downstream HA work may safely execute.

Sources:
- Issue #299;
- Issue #305 FND-05;
- `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`;
- `docs/WAVE14-C25-DISTRIBUTED-RUNTIME-FOUNDATION.md`;
- frozen FND-03 Runtime Session Lease/licensing contracts;
- frozen FND-04 Server Script readable-reference/identity contract.

## 2. Source audit — prepared findings

Current repository evidence shows:
- `src/Scada.Api/Runtime/RuntimeSessionLease.cs` already owns logical single-node Runtime Session Lease identity/accounting.
- `src/Scada.Api/Runtime/DistributedRuntimeFoundationApi.cs` exposes current distributed-runtime/session foundation APIs.
- `ApiAuthorizationService.RuntimeSessions` remains the current server-side session registry.
- C25 docs/tests explicitly reserve `ReadyStandby`, HA epoch/fencing, replicated lease state and automatic failover as future work.
- Searches of live production code found no current production `ReadyStandby` / HA epoch / fencing-token authority implementation.
- `.escadapkg` topology-neutrality is already a tested invariant.
- Therefore FND-05 must introduce one server-owned HA authority contract; it must not hide election/fencing inside Drivers or clients.

## 3. Frozen direction to be activated later

1. Stable `ClusterId` and `NodeId` are installation/runtime topology identities, never application-package identity.
2. Model Node A/B and Local/Remote endpoint paths in one versioned authenticated topology contract.
3. Exactly one effective industrial Active authority may own Drivers/process writes/Server Script side effects.
4. Standby mirrors state but does not duplicate Alarm/Historian/Script/Event/process effects.
5. Loss of peer communication alone never authorizes promotion.
6. Manual transfer is implemented before automatic failover and is break-before-make.
7. Fencing/epoch/reference evidence is mandatory before automatic promotion.
8. Runtime Session Lease continuity reuses frozen FND-03 logical lease identity; reconnect/failover must not double-count.
9. EliteGO consumes topology/effective-Active state but never elects/promotes a node.
10. Topology/session/fencing state remains outside `.escadapkg`.
11. Machine-bound license + redundancy entitlement stays authoritative per node; failover cannot increase entitlement.
12. Ambiguous/split-brain authority fails closed.

## 3A. Compatibility promise to FC0-A frozen contracts

FND-05 is permitted to activate only after:
- FND-06 VERIFIED/FROZEN; and
- `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01` returns `ACCEPTABLE / FC0A_RELEASE_APPROVED`.

The prepared FND-05 design is **additive**, not a license/session/Script/renderer redesign.

Must preserve frozen downstream-consumed semantics:
- FND-03 logical Runtime Session Lease identity, `ClientInstanceId`, requested/granted class, Authority ceiling, shared Web+EliteGO quota semantics, generation/authority invalidation and transactional machine-license lifecycle;
- existing `ServerNode` / `ClusterId` lease hooks may be used/extended for HA continuity but their addition must remain backward-compatible;
- FND-04 readable Script TAG binding/resolution semantics remain unchanged; HA may gate whether this node may execute industrial side effects, but must not replace the TAG resolver/source-binding contract;
- FND-06 canonical renderer/public visual model and Working-vs-Active visual authority remain unchanged.

Allowed FND-05 additions include:
- Cluster/Node/topology state;
- effective-Active/fencing/epoch authority;
- lease replication/continuity around the frozen logical lease model;
- additive authenticated topology/freshness surfaces.

If implementation requires changing the meaning of a frozen FND-03/FND-04/FND-06 contract consumed by FC0-A DEVs, stop:
`FND-05 -> BLOCKED-CONTRACT -> MAIN`.

No such breaking change is currently identified by the prepared source audit; final approval belongs to the mandatory post-FND06 audit.

### FND-03 license compatibility hard guard

The phrase `redundancy entitlement` in FND-05 does **not** authorize a breaking redesign of the frozen FND-03 signed license/session contract.

At activation, one of these must be true:

1. redundancy readiness can be derived from an already-frozen FND-03 entitlement without changing existing semantics; or
2. FND-05 introduces an **additive/backward-compatible** optional entitlement/readiness field or separate authenticated HA-readiness surface.

Required compatibility:
- existing valid FND-03 licenses remain valid under their existing meaning when HA is not requested;
- absence of a new HA-only field cannot invalidate Standalone/Single-Server operation;
- existing Interactive/ViewOnly/session-class quota semantics do not change;
- machine binding/trust chain does not change;
- install/replace/remove transaction semantics do not change;
- DEV-LICENSING-UX may continue consuming the frozen FND-03 lifecycle contract without rewrite.

If HA can only be implemented by changing the meaning of existing signed fields or frozen quota/session semantics:
`FND-05 -> BLOCKED-CONTRACT -> MAIN`
and the required Foundation/license contract delta must happen before FC0-A DEV release.

## 3B. Delivery / validation ownership

FND-05 implementation is owned by the normal-chat DEV lane after activation.

DEV deliverable:
- coherent product implementation on the exact Main-provided FC0-A base;
- compile/cheap focused sanity where practical;
- exact head/tree + changed-file map;
- topology/fencing/state-machine explanation;
- acceptance rows not executed locally marked `PENDING_FOR_CODEX`.

Main reviews the candidate before spending CODEX time.

Only after `MAIN_ACCEPTED_FOR_CODEX` does the sequential CODEX validate:
- two-node harness;
- stale epoch/fencing;
- split brain / ambiguous authority;
- peer-loss no-promotion;
- revision/license readiness negatives;
- logical lease continuity;
- no duplicate industrial side effects;
- package neutrality;
- exact-head T1 and broader HA validation required by the final diff.

Material product/design defects return to FND-05 DEV. Small validation-driven corrections may be made by CODEX if they do not redesign the contract.

## 4. Prepared first implementation slice

`ORDER_ID: FND05-DEV-HA-AUTHORITY-V1`

`ORDER_STATE: WAIT / NOT AUTHORIZED`

`EXECUTOR_MODE: NORMAL_CHAT_DEV / BOUNDED_FOUNDATION_IMPLEMENTATION`

At activation Main must write the exact product SHA/tree and create the isolated work branch.

First slice should close only:
- versioned Cluster/Node/topology public contract;
- explicit HA state model/readiness;
- effective-Active ownership authority;
- break-before-make manual transfer skeleton + Audit;
- fencing/epoch contract sufficient to deny ambiguous promotion;
- Runtime Session Lease replication/continuity contract boundary;
- deterministic two-node in-process/test harness.

Automatic failover, full replication breadth and EliteGO UI are not part of the first slice unless Main explicitly widens scope after the manual/fencing invariants are green.

## 5. Mandatory RED/acceptance matrix at activation

At minimum prove:
1. no current production HA authority exists on old base;
2. two nodes receive stable distinct NodeIds under one ClusterId;
3. topology returns both nodes and Local/Remote endpoints without entering package state;
4. only effective Active may acquire industrial ownership;
5. Standby write/command/Server Script side effects fail closed;
6. manual A->B transfer removes A authority before granting B;
7. transfer is auditable;
8. peer loss alone does not promote Standby;
9. stale epoch/fencing token cannot write after transfer;
10. ambiguous/split-brain state denies process effects;
11. incompatible revision/application state prevents ReadyStandby;
12. node license/redundancy entitlement incompatibility prevents readiness/promotion;
13. one logical Runtime Session Lease is not double-counted across A/B reconnect;
14. session class/Authority ceiling survives valid continuity;
15. expired/invalid lease does not resurrect through peer replication;
16. no duplicate Alarm/Historian/Script/Event effects in standby harness;
17. topology freshness/role state is truthful during transition;
18. `.escadapkg` remains topology/session/fencing neutral;
19. Security/Authority/Licensing frozen semantics remain unchanged;
20. natural Wave 15 T1 + required broader HA validation green on exact candidate.

## 6. Forbidden

No client-side election, Driver-owned HA, topology in `.escadapkg`, socket-count licensing, duplicate industrial effects, unsafe automatic failover, Authority/licensing weakening, or self-merge/freeze.

## 7. Activation dependency

The mandatory post-FND06 FC0-A Foundation Closure Audit returned `CHANGES_REQUIRED`; the consolidated FC0-A correction package is the active closure path. Do not activate until Main records `ACCEPTABLE / FC0A_RELEASE_APPROVED` on the exact integrated checkpoint.

After FC0-A release, FND-05 DEV may implement in parallel with the other prepared DEV lanes on its own isolated branch. There is no fixed four-DEV concurrency cap. Main controls shared-hotspot collisions and validation/integration order.

CODEX does not need to be free for FND-05 **coding**. It is required later for `CODEX_HA_ADVERSARIAL_GREEN`, focused local validation and exact-head T1 before Main integration approval.
