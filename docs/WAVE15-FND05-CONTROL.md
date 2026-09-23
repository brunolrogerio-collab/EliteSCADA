# Wave 15 — FND-05 Main Coordinator Control Plane

> GitHub live is the sole authority. This file is PREPARED ONLY and does not authorize product mutation.

`CONTROL_BRANCH: coord/w15-fnd05-control`

`MAIN_ORDER_REV: 0001`

`STATE: PREPARED / NOT ACTIVE`

`PREPARED_ORDER_ID: FND05-CODEX-HA-AUTHORITY-V1`

`PROVISIONAL_PRODUCT_CHECKPOINT: 6c810647c9773a19b212d9c33694780141786ac7`

`ACTIVATION_BASE_RULE: revalidate latest wave15/corrections-integration product checkpoint before activation`

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

## 4. Prepared first implementation slice

`ORDER_ID: FND05-CODEX-HA-AUTHORITY-V1`

`ORDER_STATE: WAIT / NOT AUTHORIZED`

`EXECUTOR_MODE: BOUNDED_FOUNDATION_IMPLEMENTATION`

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

Do not activate while another sequential high-risk Foundation CODEX order is active unless Main explicitly assigns a separate isolated executor.

Current intended sequencing:
- FND-06 active now;
- after FC0-A release, Main may run FND-05 while downstream FC0-A feature lanes execute.
