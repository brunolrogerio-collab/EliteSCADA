# Driver Convergence Coordination v1

Date: 2026-08-30  
Status: **COORDINATOR LOCKED — ACTIVE FOR DRIVER 4..10 / NOT A MAINLINE MERGE AUTHORIZATION**  
Coordinator baseline: `main` at `e8c020c8d7239b76747c3749fb0e23ad5211b52c`

## 1. Purpose and authority

The parallel Driver workstreams have reached the point where protocol-local implementation can continue independently, but shared product concerns must converge before each branch invents a different solution.

This document locks the common direction for Driver 4 through Driver 10 while preserving the existing rule that WAVEs have priority on `main` and Driver branches remain parked until explicit Coordinator integration.

Where this document is more specific than an older Driver handoff or assignment note, this document controls the convergence direction. It does not authorize a Driver worker to edit shared central contracts, merge another Driver, merge to `main`, or create a private replacement for a Coordinator-owned seam.

Drivers may implement protocol-owned adapters and evidence that conform to these semantics. The Coordinator owns the eventual shared implementation and mainline integration.

Observed Driver heads at this checkpoint:

| Driver | Protocol | Branch | Observed head |
| --- | --- | --- | --- |
| 4 | BACnet/IP | `driver4/bacnet` | `ad950ec25fdd010b729b4e41cddeccd5c2140136` |
| 5 | Allen-Bradley Logix EtherNet/IP/CIP | `driver5/allen-bradley-cip` | `0209958f24fd3895b6c56c4d11fbfe8c6e98e374` |
| 6 | IEC 60870-5-104 | `driver6/iec-60870-5-104` | `a85a5b8460411637d5113e707aa92e40cc939f7e` |
| 7 | DNP3 | `driver7/dnp3` | `83807afc394e5422c2f708b8d89393b02af250bd` |
| 8 | Siemens S7 ISO-on-TCP | `driver8/siemens-s7-iso` | `bcef5c038326bdaa2d3608cb04be33f266b71ee2` |
| 9 | OPC UA | `driver9/opc-ua` | `8ba5870d7dbe119a2999d8a73394289e2349f401` |
| 10 | MQTT Industrial | `driver10/mqtt` | `fdbc296a4aaf9366c3579922c4c349780dde4816` |

These SHAs are a coordination snapshot, not frozen worker heads. Every mutation still requires a fresh branch read.

---

## 2. Coordinator-owned runtime composition

The current Modbus integration is intentionally not the template for adding seven more hard-coded branches to a central switch. The future DriverHost composition is registry-based.

The Coordinator reserves the following semantic seams for the shared integration slice; exact implementation details may be refined without changing their responsibilities:

- `IDriverRuntimePlan`: library-independent compiled plan for one Data Source;
- `ICommunicationDriverRuntimePlanner`: validates/compiles canonical Engineering for one supported `DriverType` into a runtime plan plus actionable issues;
- `ICommunicationDriverRuntimeFactory`: creates an `ICommunicationDriver` from the matching runtime plan and host-owned runtime services;
- a Driver registration/module entry that contributes exactly one stable `CommunicationDriverTypeDescriptor` per Driver type plus its optional Engineering capabilities, planner and runtime factory.

Rules:

1. Registry dispatch is keyed by stable `DriverType`, case-insensitively.
2. Duplicate registrations for the same `DriverType` fail closed. There is no last-one-wins behavior.
3. Descriptor, planner and factory identities must agree on `DriverType`.
4. The host enumerates enabled canonical Data Sources and dispatches through the registry. New protocols do not add `if/switch` branches to the central compiler/runtime coordinator.
5. Protocol SDK/library objects never appear in `Scada.Core`, canonical Engineering, package JSON/CSV or module-neutral runtime plans.
6. Driver workers may build protocol-owned planner/factory classes now, but must not create a competing shared registry.

---

## 3. Canonical rich TAG binding direction

TAG identity remains the existing stable EliteSCADA TAG ID. Integer bit identity remains the existing `TagValueSelector`; authoring syntax such as `.NN` is never a second persisted identity.

The shared future rich Driver binding has these semantics:

- `TagEngineeringDto.Source` continues to identify the owning Data Source;
- `Address` remains portable/friendly authoring text and backward-compatible projection;
- a versioned Driver binding identifies `DriverType`, public binding schema identity/version, a library-independent `PortableAddress`, and schema-validated public properties;
- `AddressSelector` remains outside protocol-private fields and is evaluated after any applicable physical byte/word transformation;
- protected credentials are references on the Data Source, never TAG binding values;
- runtime handles, browse IDs, SDK objects, socket/session IDs and display labels are forbidden as canonical identity;
- unknown/missing modules must not cause project binding data to be discarded. The public binding stays round-trippable and validation reports the missing/incompatible module.

Protocol branches should preserve enough public evidence to populate this model later, but must not add their own competing central DTO.

### Physical ordering

ADR-007 remains authoritative. Byte/word swap is a common optional physical-representation concern only where meaningful. It is not inferred from manufacturer name.

- raw/register/byte-oriented bindings may expose normal, byte swap, word swap and combined byte+word where the width permits;
- symbolic/typed protocols do not invent swap options merely for UI symmetry;
- bit selection occurs after physical transformation and typed decode;
- read and write transforms are exact inverses.

---

## 4. Host-owned credentials, certificates and trust

`DataSourceEngineeringDto.SecretReferences` and public certificate/reference fields remain canonical. Plaintext secrets/private keys are never exported.

The Coordinator owns one common credential resolver boundary. Driver implementations may currently accept a narrow injected resolver/lease adapter, but must obey these semantics:

1. Resolution is scoped to the requesting project/Data Source/Driver/purpose/reference.
2. A Driver cannot enumerate unrelated project secrets.
3. Returned sensitive material is short-lived and explicitly disposed; buffers owned by the Driver are zeroed when practical.
4. Drivers do not persist resolved values or copy them into diagnostics, exception text, package data or long-lived metadata.
5. Public certificate identity/trust metadata may be persisted when non-secret; private key material remains protected.
6. Missing/rejected credentials or trust material fail closed. No insecure downgrade.

MQTT, OPC UA and future secure protocol slices must converge on this host seam rather than creating protocol-specific secret stores.

---

## 5. Runtime readiness is not TAG quality

The current mainline activation rule that waits for every runtime TAG to become `Good` is a legacy assumption and is not the future multi-driver contract.

The Coordinator reserves a common readiness source/snapshot semantic with states equivalent to:

- `NotStarted`;
- `Starting`;
- `Ready`;
- `Faulted`;
- `Stopped`.

`Ready` means the Data Source completed the mandatory transport/protocol initialization required to expose that runtime safely. It does **not** mean every point is `Good`, and it may coexist with degraded communication diagnostics or individual point-level `BadDevice`, `BadConfiguration`, `Stale` or no-current-sample states.

Activation will eventually require every required runtime source to report `Ready`, not every TAG to report `Good`. Until the shared host seam is implemented, existing mainline behavior remains unchanged and Driver branches must not patch the central coordinator independently.

Protocol readiness expectations:

- **BACnet/IP:** required Device Instance/target reachability established and configured acquisition path active; optional companion-property failures do not block readiness.
- **Allen-Bradley Logix:** EtherNet/IP/CIP session/route established and a bounded initial acquisition attempt can execute; one unsupported/bad symbol does not make the whole Data Source unready.
- **IEC-104:** TCP connected, data transfer started (`STARTDT`) and configured startup/GI policy completed or explicitly disabled by valid configuration.
- **DNP3:** association online and configured startup integrity/initialization completed.
- **Siemens S7 ISO:** ISO/S7 session and negotiated PDU established and an initial bounded acquisition attempt can execute; one malformed or device-rejected point remains point-local.
- **OPC UA:** selected secure endpoint/session established and configured subscriptions/monitored items (or explicit polling mode) activated; first monitored value is not required for readiness.
- **MQTT:** broker connection/authentication complete and configured subscriptions accepted; first telemetry sample is not required.

Workers should expose protocol-local readiness evidence behind a narrow adapter/interface if useful, but must not create a private host activation framework.

---

## 6. Ordinary writes versus rich operational commands

`ICommunicationDriver.WriteAsync(tagId, value)` remains the normal boundary for writes whose complete semantics are determined by canonical binding configuration plus the typed value.

Do not overload `WriteAsync(null)`, magic metadata, special strings or guessed values to represent a different operational command.

Operations that require invocation-time semantics beyond a normal TAG value will use a future Coordinator-owned command-operation capability with:

- stable namespaced operation key;
- versioned/validated argument schema;
- explicit target identity;
- explicit result/outcome evidence;
- normal authorization/audit at the host boundary.

Examples:

- BACnet priority-array **relinquish** is a rich operation and must not become `WriteAsync(null)`;
- DNP3 CROB pulse/count/on-time/off-time that varies per invocation is a rich operation;
- DNP3 or BACnet writes whose command mode/priority is fixed canonically in the binding may remain ordinary typed writes, provided protocol success/status is still checked;
- Allen-Bradley, S7, OPC UA and MQTT ordinary scalar writes remain `WriteAsync` unless a future protocol-specific operation genuinely needs additional invocation semantics.

Driver workers may define protocol-owned operation descriptors/executors behind adapters, but must not add protocol names to the shared `CommandKind` enum independently.

---

## 7. Timestamps, late events and current-value authority

The existing common `TagValue` distinction remains authoritative:

- `Timestamp`: local EliteSCADA observation/publication time;
- `SourceTimestamp`: optional originating device/application timestamp;
- `ServerTimestamp`: optional intermediary/server timestamp where the protocol exposes one separately.

A timestamp being present does not prove it is synchronized or trustworthy for ordering. The future common ingress policy will carry source-time state/evidence equivalent to `unknown`, `synchronized` and `unsynchronized` rather than forcing each protocol to guess.

Until that common ingress policy is implemented:

1. Driver branches must preserve real source/server timestamps when provided.
2. Drivers must not fabricate source time from receipt time.
3. Drivers must not implement private current-cache monotonic filters that discard late protocol events.
4. Event/historian evidence and current-value authority are separate concerns. A late/backlogged event may remain historically meaningful even when it must not move the HMI current value backward.
5. DNP3 and IEC-104 must retain synchronization/quality evidence needed for the Coordinator to implement this policy later.
6. OPC UA preserves both SourceTimestamp and ServerTimestamp when available.
7. MQTT freshness remains a local receive/admission concept and is not inferred from process/source timestamp age.

The Coordinator will reconcile the central current-value versus historical-event ingress policy. Workers should surface evidence, not decide globally.

---

## 8. Installable module and licensing convergence

ADR-007 remains authoritative. Every Driver handoff must now include a small module/production-readiness manifest section containing:

- proposed stable module/package ID;
- Driver type(s) provided;
- public contract/schema version(s);
- runtime and Engineering capabilities;
- external runtime dependencies and versions;
- license/redistribution classification;
- whether production distribution is currently allowed, blocked on evidence, or requires commercial licensing;
- hardware/vendor-simulator validation still required.

Specific lock:

- Siemens S7 remains the first intended proof target for installable Driver Module composition.
- DNP3 Step Function remains an optional adapter/module and **production distribution is blocked until applicable commercial licensing evidence is recorded**.
- Allen-Bradley production dependency selection remains open behind its protocol-neutral adapter; current public Engineering identity must not depend on that choice.
- No Driver worker builds its own module loader, trust store, marketplace or plugin catalog.

---

## 9. Per-Driver autonomous next milestone

### Driver 4 — BACnet/IP

Continue without waiting for Coordinator code.

Next milestone:

1. Keep `BacnetEngineeringRuntimePlanner`/provider output library-independent and ready to plug into the future registry.
2. Document/expose protocol-local readiness evidence using the BACnet readiness rule above.
3. Maintain strict separation between ordinary WriteProperty and explicit relinquish operation; no `null` overload.
4. Keep COV/FDR/reachability hardening bounded; do not expand into BACnet/SC or alarm/event services in this milestone.
5. Add the module/license/hardware manifest section to the handoff.
6. Remain Draft/parked. No mainline merge.

### Driver 5 — Allen-Bradley Logix EtherNet/IP/CIP

Continue without waiting for Coordinator code.

Next milestone:

1. Update the PR handoff to the actual branch head when the next reviewable checkpoint is reached.
2. Keep stable Controller/Program/symbol identity and native type/access evidence portable and library-independent for future rich binding.
3. Expose protocol-local readiness evidence using session/route + bounded acquisition semantics.
4. Keep array/fragmented-read groundwork internal until a common canonical array value model is explicitly approved; do not invent a Driver 5-only array TAG type.
5. Preserve fail-closed direct BOOL/security/fragmented-write behavior.
6. Add the module/dependency/licensing/hardware manifest section and remain parked.

### Driver 6 — IEC 60870-5-104

The branch has moved beyond research and now needs a bounded delivery target rather than unlimited protocol expansion.

Next milestone:

1. Preserve canonical point identity as **Common Address + IOA**. Type/semantic family, command profile and acquisition representation remain binding/configuration concerns, not address identity.
2. Expose readiness evidence for TCP + STARTDT + configured startup/GI completion.
3. Preserve Cause of Transmission, quality and CP56Time2a/synchronization evidence needed for later current-vs-history policy.
4. Keep spontaneous/backlogged events available to the common ingress; do not add a branch-local late-event current-cache filter.
5. Keep command transaction outcome explicit; no “bytes sent = success”.
6. Finish bounded point-list import/export/reconcile and protocol tests already in flight, then produce a Draft handoff PR with exact head and justified exact-head CI. Do not expand scope before that milestone.

### Driver 7 — DNP3

The branch is already at a strong validated milestone. Prefer convergence evidence over broad new features.

Next milestone:

1. Expose/retain readiness evidence for association-online + startup integrity completion.
2. Preserve synchronized versus unsynchronized DNP3 time evidence; do not solve global late-event ordering privately.
3. Keep stable point kind + index identity and group/variation/command profile as binding concerns.
4. Classify ordinary typed output writes versus invocation-time CROB rich operations according to section 6.
5. Add the module/licensing manifest with Step Function explicitly production-blocked pending commercial license evidence.
6. Do not expand to serial, SAv5, file transfer or product Outstation role in this milestone. Remain parked.

### Driver 8 — Siemens S7 ISO-on-TCP

Continue current hardening, but close toward a reviewable module milestone.

Next milestone:

1. Preserve PDU negotiation, batching and per-point failure isolation. A malformed STRING/DATE or rejected point must not redefine Data Source readiness.
2. Expose readiness evidence for negotiated ISO/S7 session + bounded initial acquisition attempt.
3. Preserve explicit Rack/Slot/TSAP and absolute area/DB identity. Never invent offsets for optimized symbolic-only members.
4. Keep ADR-007 byte/word ordering symmetric and binding-level where meaningful.
5. Finish the current scalar/STRING/date-time and TIA import fidelity hardening, then stop feature expansion and open a Draft handoff PR with exact-head CI.
6. Include a first installable-module manifest. Do not build the central module loader in this branch.

### Driver 9 — OPC UA

Continue secure runtime/Engineering convergence without creating an OPC-UA-specific security island.

Next milestone:

1. Preserve stable OPC UA NodeId identity; display/browse names are not identity.
2. Keep unknown/unsupported DataTypes explicit and fail closed rather than guessing a canonical type.
3. Expose readiness evidence for secure session + subscription/monitored-item activation; no first-value requirement.
4. Preserve SourceTimestamp and ServerTimestamp separately.
5. Route username/password/certificate/private-key needs through injected reference/resolver seams only; no private secret/trust store.
6. Finish the current secure discovery/browse/reconcile/runtime-provider slice, then produce a Draft handoff PR and exact-head CI before expanding scope.

### Driver 10 — MQTT Industrial

The branch has reached substantial runtime/backpressure/reconnect hardening. Close the milestone instead of adding unrelated MQTT features.

Next milestone:

1. Expose readiness evidence for authenticated broker connection + accepted subscriptions; `NoCurrentSample` is valid after activation.
2. Keep freshness based on local monotonic receive/admission time and separate from SourceTimestamp age.
3. Preserve exact-topic canonical identity; no wildcard-created TAG identity.
4. Keep bounded inbound queue, deferred QoS acknowledgement and oversized-payload fail-closed behavior.
5. Keep the narrow injected secret-resolver seam and do not implement host security locally.
6. Complete the current bounded-burst/reconnect-jitter validation evidence, then open a Draft handoff PR with exact-head CI. Sparkplug B, WebSockets and dynamic topic discovery remain out of scope.

---

## 10. Handoff and CI gate from this checkpoint

A Driver is `REVIEWABLE / PARKED` only when its handoff states:

1. exact branch and exact head;
2. exact changed-file list/delta;
3. delivered runtime and Engineering scope;
4. common-contract alignment against this document;
5. exact test evidence;
6. exact-head CI when a reviewable milestone justifies the matrix;
7. known protocol/library limitations;
8. module/dependency/license manifest;
9. hardware/vendor-simulator validation still required;
10. remaining Coordinator-owned integration items;
11. confirmation that no `main`, Wave branch or sibling Driver branch was modified.

Do not run full CI merely because this coordination document exists. CI remains NORMAL.

No Driver enters `main` solely because it is reviewable. Final convergence still requires Coordinator-owned shared-contract implementation, reconciliation against then-current `main`, integration-risk CI and explicit merge authorization.
