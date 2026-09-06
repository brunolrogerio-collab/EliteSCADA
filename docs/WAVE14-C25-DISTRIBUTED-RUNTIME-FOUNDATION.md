# Wave 14 C25.7 — Distributed Runtime Foundation

**Status:** BINDING C25.7 ARCHITECTURE / EXACT-SHA GREEN / IMPLEMENTATION NOT YET STARTED  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`  
**Target:** `wave14/corrections-integration`

Exact clean architecture-handoff SHA:

`c84882d578cb891797a80faf6073d422c4fe55ed`

Exact-SHA validation:

- Wave 14 C25 Post-Demo #292 / `34055739682` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #290 / `34055739696` — **SUCCESS**.

This SHA contains architecture/coordination mutations only. C25.7 product implementation has not started. Revalidate the live PR #283 head before using this as implementation authority.

> C25.7 exists to establish architectural foundations for future EliteGO/Server Runtime without turning C25 into a production distributed/HA implementation.

## 1. Why this checkpoint precedes Help

C25.6 reusable libraries is complete. Before writing the contextual product manual, the Runtime/server boundary must be stabilized so Help documents the product that will actually ship after C25.

C25.7 therefore precedes contextual Help and is intentionally smaller than the complete EliteGO/HA roadmap.

Binding product roadmap:

`docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`

## 2. Architectural invariants

C25.7 must preserve:

- Standalone as a first-class topology;
- Single Server as a valid topology without redundancy;
- one authored application across Standalone/Server/future HA;
- `.escadapkg` topology neutrality;
- backend Active Revision as Runtime application authority;
- no direct Runtime/EliteGO access to industrial Drivers;
- canonical `Driver -> TAG Engine / Current Cache -> Event Bus -> consumers` authority;
- one canonical HMI/runtime rendering implementation wherever possible;
- backend Authority/effective-capability enforcement;
- Engineering Lock, licensing, package lifecycle, recovery and audit boundaries already accepted in C25;
- Alarm, Operational Event and Audit as distinct domains.

## 3. C25.7 required implementation scope

### 3.1 Server Runtime Contract

Perform a live code audit first, then formalize a server-owned contract sufficient for a Runtime consumer to obtain only supported product information/services.

The contract should cover the applicable equivalents of:

- current authenticated identity;
- effective capabilities;
- canonical Active Runtime application identity/revision;
- Runtime visual definitions/assets required by the canonical renderer;
- TAG values, Quality and timestamps through canonical TAG/realtime authority;
- Alarm and Operational Event access through their existing domains;
- Historian/Trend query authority where already supported;
- command/process-write requests through existing backend authorization paths.

The contract must not expose Driver SDK/instances as client-facing Runtime authority.

### 3.2 Transport-independent canonical Runtime boundary

Audit the local Runtime implementation for assumptions that backend and renderer necessarily share the same host/origin/process context.

Where necessary, introduce boundaries/adapters so the same canonical Runtime implementation can consume either:

- the supported local/server transport path; or
- a future remote EliteGO transport path.

Do not create a second rendering engine.

### 3.3 Runtime Session Lease foundation

Introduce a logical Runtime Session Lease domain suitable for future remote Runtime admission and licensing.

The initial model should be capable of representing:

- SessionId;
- UserId;
- ClientInstanceId;
- ConnectionClass;
- CreatedAt;
- LastHeartbeat;
- ExpiresAt;
- ServerNode (future-compatible; local/default acceptable now);
- ClusterId (future-compatible/nullable now).

C25.7 must define clear authority for create/admit, renew/heartbeat and terminate/expire.

A logical Runtime session remains one lease even if the client uses multiple HTTP requests, WebSockets or reconnects.

No commercial connection-count enforcement is required in C25.7 unless needed to prove the domain safely.

### 3.4 Viewer / Interactive effective-capability reduction

Support at least two conceptual Runtime session classes:

- Viewer;
- Interactive.

These classes do not replace Authority roles/capabilities. They constrain the effective authority of the issued Runtime session.

Viewer must fail closed for process-affecting capabilities such as the applicable equivalents of:

- CommandExecute;
- ProcessValueWrite;
- any other backend capability whose exercise can alter the industrial process.

A user with broader Authority privileges may voluntarily request Viewer / View Only. The server must then issue a deliberately downscoped session.

Frontend hiding is not security. Direct calls from a modified client must remain denied by backend authority.

### 3.5 Topology-neutral application/package proof

Add structural/regression proof that authored application/package authority does not absorb deployment authority.

The application/package must not gain canonical fields such as:

- Standalone/Server/HA topology mode;
- ClusterId;
- NodeId;
- Active/Standby ownership;
- EliteGO endpoint lists;
- Runtime Session Lease state;
- HA epoch/fencing/health state.

Those belong to installation/runtime topology authorities outside `.escadapkg`.

### 3.6 Optional EliteGO proof-of-architecture

A thin proof may be added only if it remains small and validates the architectural separation above.

Permitted proof characteristics:

- Runtime-only;
- authenticates against one EliteSCADA Single Server;
- consumes the new Server Runtime Contract;
- reuses canonical Runtime/rendering code;
- proves Viewer/Interactive session projection;
- carries no Engineering, Driver, independent project, Authority, Historian or license authority.

If this requires a parallel renderer or substantial new product shell, defer it to the future EliteGO Wave instead of expanding C25.

## 4. Explicit exclusions

Do not implement in C25.7:

- redundant pair orchestration;
- automatic or manual HA election/takeover;
- ClusterId/NodeId production semantics beyond future-compatible nullable contract slots;
- HA Authority replication;
- Historian replication;
- realtime TAG Mirror between nodes;
- replicated session accounting between nodes;
- ReadyStandby;
- reference-device quorum;
- operational ownership heartbeat in PLCs/devices;
- epoch/fencing protocol;
- self-demotion;
- automatic failover;
- seamless EliteGO A->B failover;
- advanced commercial Viewer/Interactive tiers.

Those belong to the future Waves recorded in the product roadmap.

## 5. Required first implementation action

Before creating or editing product code, the coordinator must perform a live audit of the current implementation and record the concrete authorities to reuse. At minimum inspect:

- local Runtime application projection and Active Revision resolution;
- Runtime visual payload/asset serving;
- realtime TAG current-value/quality/timestamp transport;
- authentication, session identity and capability projection;
- process command and process-value-write backend authorization paths;
- existing WebSocket/reconnect/session assumptions;
- Historian/Trend, Alarm and Operational Event public boundaries already used by Runtime;
- `.escadapkg` schema and deployment/topology separation.

The audit exists to prevent a parallel Runtime stack. Reuse existing canonical authorities; introduce only the smallest boundary/adapters necessary for the C25.7 contract.

## 6. Validation requirements

Before C25.7 can close:

- exact product SHA must be known;
- C25 and C03 exact-SHA workflows must be green on the same SHA;
- Server Runtime/lease/downscope tests must exercise backend authority, not frontend-only behavior;
- Viewer direct command/write denial must be proven;
- voluntary View Only must prove capability reduction from a more privileged identity;
- topology-neutral package tests must prove no deployment state entered `.escadapkg`;
- existing Engineering Lock, Restore-first, Runtime session UX and reusable-library regressions must remain green;
- any browser proof added for EliteGO/remote Runtime must use the canonical product paths rather than fabricated process truth;
- no red CI may be rerun without diagnosis.

## 7. Checkpoint sequence after C25.7

- C25.8 — Contextual multilingual Help/manual;
- C25.9 — Integrated regression/audit;
- C25.10 — Exact final candidate matrix and explicit Product Owner acceptance.

After C25 acceptance, follow the post-C25 sequence in `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`: integration revalidation -> C11/EEE compatibility -> canonical EEE package -> Preview Codespace kept active -> authorized main transition -> Wave 13 Windows installable from the new main.

## 8. Post-C25 release authority clarification

The Windows-installable product must not be produced from the preserved pre-C25 Wave 13 snapshot once this sequence is complete.

Required order is:

1. accepted C25 -> integration -> exact-SHA validation;
2. C11/EEE compatibility and canonical package freeze;
3. Preview Codespace with the canonical EEE package, kept active through visual homologation and the approved `main` transition;
4. explicit Product Owner authorization before #212 merges to `main`;
5. validation of the resulting new `main` containing accepted Wave 14/C25;
6. only then resume issue #205 / PR #207 and adapt/revalidate the preserved Windows release/signing work against that new mainline;
7. produce/sign/validate the Windows installable from the new mainline authority.

## 9. Governance

- PR #283 remains OPEN/DRAFT and targets only `wave14/corrections-integration`.
- PR #212 remains OPEN/DRAFT and is not authorized to merge to `main`.
- Never modify `main` directly.
- C11 remains frozen until C25 is accepted, integrated only into integration and post-merge exact-SHA revalidated.
- PR #266 remains validation-only and MUST NEVER MERGE.
- Wave 13 issue #205 / PR #207 remain paused until the post-C25/main sequence explicitly reaches them.
- no force push, destructive rebase, branch deletion or unrelated cleanup.
