# EliteSCADA — Distributed Runtime, EliteGO and HA Product Roadmap

**Status:** BINDING PRODUCT EVOLUTION ROADMAP / IMPLEMENTATION PHASED  
**Repository:** `brunolrogerio-collab/EliteSCADA`  
**Current implementation package:** Wave 14 / C25  
**Current implementation branch:** `wave14/c25-post-demo`

> This roadmap records product direction. It does not authorize merges, bypass current Wave governance, or make every future phase part of C25. GitHub live state and the active checkpoint ledger remain the execution authority.

## 1. Product invariants

The evolution to distributed Runtime and High Availability must preserve these invariants:

- EliteSCADA Standalone remains a first-class topology.
- EliteSCADA Single Server remains valid without HA.
- EliteGO does not require redundancy.
- The same authored application must operate in Standalone, Single Server and a future Redundant Pair without being rebuilt.
- `.escadapkg` remains topology-neutral and must not carry installation/node/cluster/session/HA ownership state.
- Backend Active Revision remains the Runtime application authority.
- EliteGO is Runtime-only and has no Engineering, industrial Drivers, independent Historian, independent Authority, independent project or product license.
- Local Runtime and EliteGO reuse the same canonical Runtime/rendering implementation wherever technically possible; do not create a second HMI engine.
- Runtime clients never access industrial Drivers directly.
- The canonical process path remains `Driver -> TAG Engine / Current Cache -> Event Bus -> consumers`.
- Authority and effective capabilities remain server-side.
- Viewer sessions never receive process-write authority merely because the authenticated user normally possesses it.
- Runtime connection licensing, when introduced, counts logical Runtime Session Leases rather than HTTP/WebSocket sockets.
- Only an Active HA node may own industrial Driver execution and process writes.
- HA policy/fencing remains above Drivers; Drivers do not decide cluster authority.
- Standby realtime mirrors must not duplicate Alarm evaluation, Historian ingestion, Server Scripts, Operational Events or process effects.
- Loss of peer communication alone never authorizes automatic takeover.
- Ambiguous HA conditions fail conservatively: do not promote automatically.

## 2. Wave 14 / C25 — Distributed Runtime Foundation

C25 deliberately implements only architectural foundations that would be expensive or dangerous to retrofit later.

### C25.7 — Distributed Runtime Foundation

Required scope:

1. **Server Runtime Contract**
   - formalize the server-owned Runtime projection used by Runtime consumers;
   - project Active application/runtime content, identity, effective capabilities and supported realtime/query surfaces without exposing Drivers as client authority;
   - preserve Alarm / Operational Event / Audit as distinct domains.

2. **Transport-independent canonical Runtime boundary**
   - remove assumptions that canonical Runtime must share a machine/origin with backend services where such assumptions block remote use;
   - preserve one canonical rendering/runtime implementation for local Runtime and future EliteGO.

3. **Runtime Session Lease foundation**
   - introduce a logical session domain suitable for remote Runtime admission/renewal/expiry;
   - include stable session/client/user identity, connection class, timestamps/heartbeat/expiry, and future-compatible node/cluster slots;
   - no HA replication or commercial connection tier is required in C25.

4. **Viewer / Interactive session projection**
   - session class is an additional server-side effective-capability reduction, not a replacement for Authority;
   - Viewer is fail-closed for process-write/command capabilities;
   - a privileged user may voluntarily request View Only and receive a deliberately downscoped session;
   - modified clients cannot bypass the downscope because backend authorization remains authoritative.

5. **Topology-neutral package gates**
   - prove `.escadapkg` does not become Standalone/Server/HA-specific;
   - installation/node/cluster/session ownership remains outside authored application authority.

6. **Optional thin EliteGO proof-of-architecture**
   - allowed only if it remains a small proof of the Server Runtime Contract and reuses canonical Runtime code;
   - it must not expand C25 into production HA or a parallel HMI implementation.

### Explicit C25.7 exclusions

C25.7 does **not** implement:

- production HA pair orchestration;
- ClusterId/NodeId election semantics;
- Authority/Historian durable node replication;
- TAG Mirror between nodes;
- Runtime Session Lease replication between nodes;
- automatic takeover/failover;
- reference-device quorum;
- epoch/fencing protocol;
- seamless EliteGO failover;
- commercial Viewer/Interactive connection tiers.

### Remaining C25 order

- **C25.7 — Distributed Runtime Foundation**
- **C25.8 — Contextual multilingual Help/manual**
- **C25.9 — Integrated regression/audit**
- **C25.10 — Exact final candidate matrix / Product Owner acceptance**

Help intentionally follows C25.7 so the installed manual documents the stabilized Server Runtime/session behavior instead of becoming obsolete immediately.

## 3. Future Wave — EliteGO Single Server product

A future Wave should turn the distributed Runtime foundation into a production remote-client product without requiring HA.

Scope should include:

- separate EliteGO application;
- Runtime-only authentication/session lifecycle;
- HTTPS/WSS transport and reconnect behavior;
- canonical Active Runtime acquisition;
- Screens/Popups/Dynamos/assets;
- TAG value/quality/timestamps;
- Alarm and Operational Event viewing/actions according to Authority;
- Trends/Historian queries;
- Interactive commands/writes according to effective capabilities;
- Viewer/Interactive and voluntary View Only;
- Runtime Connection Manager and logical Session Lease admission;
- Single Server discovery/configuration;
- license authority remaining exclusively on the EliteSCADA Server;
- no independent EliteGO product license.

No HA is required for this Wave.

## 4. Future Wave — HA Foundation / Manual Hot Standby

Only after Single Server remote Runtime is stable should HA node semantics become product behavior.

Scope should include:

- ClusterId and NodeId;
- paired-node configuration and health;
- explicit HA states, including Synchronizing, Standby, ReadyStandby, Promoting, Active, Demoting, Isolated, Maintenance and Faulted;
- license compatibility/readiness checks for each machine-bound node license;
- application/revision integrity compatibility;
- shared logical Authority contract;
- minimum durable synchronization required for safe readiness;
- strict exclusivity of industrial Drivers;
- manual switchover and maintenance transfer;
- `break before make` as an invariant;
- audited administrative Promote/Demote/Transfer/Maintenance operations.

Automatic failover is explicitly out of scope until manual transfer is proven deterministic and safe.

## 5. Future Wave — HA Runtime Replication

After manual HA is stable:

- Active -> Standby realtime TAG replication;
- Standby Tag Mirror;
- value, Quality, source/server timestamps, source identity, sequence, epoch/revision evidence;
- takeover Quality transition such as stale/initializing until a new real acquisition is obtained;
- replication of Runtime Session Lease state required for connection accounting/continuity;
- Alarm acknowledgement/shelving and other durable state needed for continuity;
- retained Server Memory continuity;
- Historian continuity architecture;
- explicit proof that mirrored TAGs do not re-trigger local Alarm/Historian/Server Script/Operational Event/process effects.

## 6. Future Wave — HA Protection / Automatic Failover

Only after manual HA and replication are mature:

- HA Reference Devices / configurable quorum;
- Active operational ownership heartbeat where technically appropriate;
- epoch/generation authority;
- fencing before Driver write/command;
- Active self-demotion when isolated from both peer/process evidence as defined by policy;
- coordinated takeover while peers still communicate;
- conservative automatic promotion after real Active failure;
- split-brain protections;
- optional HA Fenced mode without making it mandatory for HA Standard.

Primary rule: **when authority evidence is ambiguous, the Standby does not automatically promote itself.**

## 7. Future Wave — Seamless EliteGO failover and commercial HA

After HA is proven:

- server topology discovery by EliteGO;
- Active endpoint discovery;
- transparent A -> B reconnect on failover;
- Session Lease resume/revalidation where safe;
- Runtime snapshot/realtime resubscription;
- reauthentication fallback where required;
- commercial Viewer/Interactive connection limits;
- HA licensing features and compatible-pair policy;
- future cluster/pair logical licensing identity if adopted.

## 8. Required post-C25 product sequence

Closing C25 does not jump directly to release packaging. The required sequence is:

1. finish C25.7, C25.8, C25.9 and C25.10;
2. obtain explicit Product Owner acceptance of one exact C25 candidate SHA;
3. merge accepted C25 **only** into `wave14/corrections-integration`;
4. run post-merge exact-SHA validation on integration;
5. only then synchronize/adapt the preserved C11 canonical EEE Demo branch to the accepted C25 contracts;
6. revalidate the EEE application and package portability using normal generic product mechanisms;
7. export/version/freeze the canonical `EliteSCADA-EEE-Demo.escadapkg` plus checksum/provenance;
8. launch the canonical EEE package in Preview Codespace for Product Owner visual homologation;
9. **keep that Codespace active** through homologation and the subsequent approved main transition so the visual environment remains available for comparison/verification;
10. only after later explicit Product Owner authorization may PR #212 merge into `main`;
11. verify the new `main` after merge while keeping the Preview Codespace available as requested;
12. resume the paused Wave 13 release/signing/installable work from the **new `main` that already contains accepted Wave 14/C25 behavior**;
13. produce and validate the Windows-installable EliteSCADA from that new mainline authority.

Do not use an older pre-C25 `main` as the final Windows release authority after this sequence.

## 9. Current frozen boundaries

Until the above gates are reached:

- PR #212 remains OPEN/DRAFT and is not authorized to merge to `main`;
- C11 remains frozen on `wave14/c11-canonical-eee-demo` at its preserved exact head until C25 acceptance/integration/post-merge validation;
- PR #266 remains validation-only and MUST NEVER MERGE;
- Wave 13 #205/#207 remains paused;
- no direct `main` mutation is authorized.
