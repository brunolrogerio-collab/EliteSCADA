# Driver Convergence Coordination v1

Date: 2026-08-30 (BRT)  
Status: **COORDINATOR LOCKED — DRIVER CONVERGENCE ACTIVE / WAVE 11 DEFERRED / NOT A MAINLINE MERGE AUTHORIZATION**

Operational handoff: `docs/COORDINATOR-HANDOFF.md`  
Live assignments: `docs/CHAT-WORK-ASSIGNMENTS.md`  
Driver/evidence status: `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`  
Shared integration issue: #174  
Shared integration PR: #175

## 1. Purpose and authority

This document locks the common convergence semantics for Driver 4 through Driver 10. Protocol workers own protocol-local implementation and evidence; the Coordinator owns shared contracts, central composition and mainline integration.

Current priority is Driver convergence before Wave 11. The common seven-peer interoperability laboratory is already merged through PR #173.

Current `main` wins implementation conflicts. `PROJECT GOAL.md` and accepted ADRs win locked future architecture. Every mutation still requires a fresh branch/PR/CI read because worker heads can move independently.

Protocol branches are **source/evidence lines, not merge trains**. Re-port/adapt narrowly against current `main`; never merge historical Driver branch baggage wholesale.

## 2. Current shared integration checkpoint

Coordinator branch: `coordination/driver-convergence-v3`  
Draft PR: #175  
Audited head: `06c7d408c76926bf5d37dfec4be20ea6044f52b1`  
Exact normal CI: #895 GREEN.

Implemented shared foundation:

- fail-closed Driver module registry keyed by stable DriverType;
- runtime planner/factory component registry;
- protocol-neutral Data Source readiness contract;
- scoped host-owned protected-material resolver/lease seam;
- focused fail-closed shared-contract tests.

A rich Communication TAG binding scaffold also exists on #175, but **schema v15 is not yet functionally complete**. At the audited head, current Engineering schema remains 14, TAG Preview does not invoke the binding validator, TAG Apply drops `CommunicationBinding`, CSV fidelity is absent and the complete public round-trip regression is missing.

The immediate Coordinator gate is therefore to finish schema v15 end-to-end before adapting MQTT. See issue #174 and `docs/COORDINATOR-HANDOFF.md` for the exact checklist.

## 3. Registry, planning and runtime composition

Shared multi-driver composition is registry-based rather than a growing central protocol switch.

Rules:

1. Registry dispatch uses stable DriverType, case-insensitively.
2. Duplicate DriverType registrations fail closed; no last-one-wins behavior.
3. Descriptor, planner and factory identities must agree on DriverType.
4. The host enumerates enabled canonical Data Sources and dispatches through registered modules/components.
5. Protocol SDK/library objects never enter `Scada.Core`, canonical Engineering, package data or module-neutral runtime plans.
6. Drivers never call sibling Drivers.
7. Drivers never bypass the TAG/cache/event architecture.
8. Workers may implement protocol-owned adapters/planners/factories, but never a competing shared registry/host.

## 4. Canonical rich Communication TAG binding

TAG identity remains stable EliteSCADA TagId. Generic integer bit identity remains `TagValueSelector`; `.NN` remains authoring/display syntax only.

The canonical rich binding direction is:

- `TagEngineeringDto.Source` identifies the owning Data Source;
- `CommunicationBinding.SchemaId` + `SchemaVersion` identify the public Driver binding schema;
- `CommunicationBinding.PortableAddress` is stable, portable protocol address/identity;
- public non-secret binding settings remain library-independent;
- compatibility `Address` must equal `CommunicationBinding.PortableAddress` while both exist during v15 migration;
- `TagValueSelector` remains outside protocol-private settings;
- protected credentials/private keys/tokens never become TAG binding data;
- SDK handles, socket/session IDs, browse display names and runtime objects are forbidden canonical identity;
- missing/incompatible modules must fail closed diagnostically without silently discarding round-trippable public binding data.

### ADR-007 physical ordering

Physical byte/word transform is binding-level and explicit only where meaningful. It is never inferred from manufacturer name.

Read path:

`raw protocol representation -> configured byte/word transform -> canonical typed decode -> TagValueSelector`

Write path applies the exact inverse/symmetric transform after canonical typed encode. Symbolic/typed protocols do not invent swap options merely for UI symmetry.

## 5. Protected material and trust

Data Source secret/certificate references remain canonical public references. Plaintext protected material is never exported in Engineering or `.escadapkg`.

The shared host resolver semantics are:

1. resolution is scoped to project/Data Source/Driver/purpose/reference;
2. Drivers cannot enumerate unrelated secrets;
3. sensitive leases/material are short-lived and disposed/zeroed where practical;
4. resolved values never enter diagnostics, exception text, package data or long-lived metadata;
5. public certificate identity/trust metadata may persist when non-secret; private key material remains protected;
6. missing/rejected credentials or trust fail closed; no insecure downgrade.

MQTT, OPC UA and future secure protocol integrations must use this host seam, never private secret stores.

## 6. Runtime readiness is not point quality

Common readiness states are equivalent to:

`NotStarted -> Starting -> Ready | Faulted -> Stopped`

`Ready` means mandatory transport/protocol initialization completed sufficiently for safe Runtime activation. It does **not** require every point to be Good or to have a current sample.

Protocol expectations:

- BACnet/IP: target/device resolution/reachability and configured acquisition path active;
- CIP: EtherNet/IP/CIP session/route established and bounded acquisition can execute;
- IEC-104: TCP + STARTDT + configured startup/GI complete or validly disabled;
- DNP3: association online + startup integrity/initialization complete;
- S7: ISO/S7 session + negotiated PDU + bounded acquisition can execute;
- OPC UA: selected endpoint/session + configured monitored items/subscriptions or polling active;
- MQTT: broker authentication/connection + configured subscriptions accepted.

A bad/unsupported individual point remains point-local and must not redefine Data Source readiness unless it proves a required source-level failure.

## 7. Ordinary writes versus rich operations

`ICommunicationDriver.WriteAsync(tagId, value)` remains the normal boundary when complete operation semantics are fixed by canonical binding plus typed value.

Do not encode distinct operations through `null`, magic strings or ad hoc metadata.

Invocation-time semantics beyond a normal TAG value require the future shared namespaced operation surface with stable operation key, versioned/validated arguments, explicit target and explicit outcome evidence. Examples include BACnet relinquish or invocation-specific DNP3 CROB pulse timing.

Workers may keep protocol-owned executors behind adapters, but must not independently expand central command enums/contracts.

## 8. Timestamps, late events and current-value authority

Canonical meanings remain:

- `Timestamp`: local EliteSCADA observation/publication time;
- `SourceTimestamp`: originating device/application timestamp when supplied;
- `ServerTimestamp`: intermediary/server timestamp when independently supplied.

Rules until the shared late-event policy is implemented:

1. preserve real source/server timestamps;
2. never fabricate source time from receipt time;
3. do not add branch-private current-cache monotonic filters that destroy late protocol events;
4. historical/event value and current HMI authority are separate concerns;
5. DNP3/IEC-104 preserve synchronization/quality evidence;
6. OPC UA preserves SourceTimestamp and ServerTimestamp separately;
7. MQTT freshness uses local receive/admission silence, not source timestamp age.

The Coordinator owns the final current-value versus historical-event ingress policy.

## 9. Installable modules and licensing

Every accepted Driver integration must document:

- stable module/package ID and DriverType(s);
- public contract/schema versions;
- runtime and Engineering capabilities;
- external runtime dependencies/versions;
- license/redistribution classification;
- whether production distribution is allowed, evidence-blocked or commercially license-gated;
- remaining L3/L4/vendor/hardware validation.

Locks:

- no worker builds its own module loader/marketplace/trust store;
- DNP3 Step Function production distribution remains blocked until applicable commercial licensing evidence is recorded;
- Allen-Bradley dependency selection remains behind protocol-neutral adapters;
- test-only lab peer dependencies never silently become production dependencies.

## 10. Evidence and current order

Evidence levels stay distinct:

- L0 unit/codec/contracts;
- L1 same-stack/in-process/loopback;
- L2 independent software peer over real wire;
- L3 representative vendor simulator/device;
- L4 representative hardware/site.

Normal CI, L2/L3/L4, licensing and conformance are independent gates. Never weaken an assertion merely to obtain green status.

Current evidence-driven integration order after the schema-v15 gate:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

Current worker classification:

- MQTT: READY FOR COORDINATOR CONVERGENCE;
- IEC-104: READY FOR COORDINATOR CONVERGENCE;
- CIP: READY FOR COORDINATOR CONVERGENCE;
- OPC UA: ACTIVE product-path L2;
- DNP3: ACTIVE canonical Int32 publication fix + rerun #167;
- Siemens S7: ACTIVE product-path L2;
- BACnet/IP: ACTIVE product-path L2.

Exact live heads/evidence belong in `docs/CHAT-WORK-ASSIGNMENTS.md` and `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`, not frozen indefinitely here.

## 11. Mainline acceptance

A Driver is not accepted because its isolated worker CI is green. Shared convergence closes only when the accepted Driver set:

- registers through common host composition;
- uses canonical rich Engineering binding and round-trips through required persistence/export paths;
- uses common readiness and protected-material seams;
- has appropriate product-path interoperability evidence;
- preserves required security/license boundaries;
- passes exact integration-head CI;
- transitions to `main` under controlled review;
- receives required exact post-main evidence before the stage closes.

Wave 11 remains deferred until this Driver convergence stage closes or product priority is explicitly reprioritized.