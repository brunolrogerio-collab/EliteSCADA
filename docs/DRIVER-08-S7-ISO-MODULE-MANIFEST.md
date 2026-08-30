# Driver 08 - Siemens S7 ISO-on-TCP module manifest

Status: **protocol-owned draft manifest / not a mainline integration authorization**

This manifest records the production-readiness and convergence evidence requested by the Driver convergence coordination work. It describes the current Driver 08 branch only. It does not define or implement the Coordinator-owned module loader, runtime registry, central runtime dispatch, shared readiness contract or licensing system.

## Proposed module identity

- Proposed stable module/package ID: `EliteSCADA.Driver.SiemensS7Iso`
- Driver type provided: `siemens.s7.iso`
- Engineering descriptor version: `1`
- Driver configuration schema ID: `siemens.s7.iso`
- Driver configuration schema version: `1`
- Public protocol-owned TAG binding schema ID: `siemens.s7.iso.binding`
- Public TAG binding schema version: `1`
- Acquisition model: polling

The final installable package/module naming convention remains Coordinator-owned. The proposed ID above is evidence for convergence, not permission to create a private module catalog or loader.

## Runtime capabilities

- RFC1006 / ISO-on-TCP over TCP, default port 102.
- Explicit Rack/Slot or explicit Source/Destination TSAP connection identity.
- S7 communication setup with negotiated PDU size.
- PDU-aware multi-variable read batching.
- Per-point configuration, protocol-return-code and communication-failure isolation where the protocol permits it.
- Process-data reads and ordinary typed writes only.
- Data Source write-enable policy is fail-closed by default.
- Common communication diagnostics plus protocol-specific sanitized evidence.
- Protocol-local readiness adapter through `IS7IsoRuntimeReadinessSource` for future Coordinator-owned readiness composition.
- Internal byte/word ordering remains supported where meaningful, with inverse read/write transforms.

Initial runtime safety scope explicitly excludes CPU RUN/STOP, program upload/download, block deletion, firmware operations and generic PLC administration.

## Engineering capabilities

- Canonical driver descriptor and versioned configuration schema.
- Non-destructive connection/session test with effective TSAP and negotiated PDU evidence.
- TIA PLC-tag file import for XLSX, XML and SDF.
- Import candidates remain visible when unsupported, malformed or inaccessible.
- Stable import identity is independent from a moved absolute address when source/path/name identity is unchanged.
- Optimized/symbolic-only members never receive fabricated absolute offsets.
- Malformed HMI boolean metadata is surfaced per candidate and fails closed.

TIA Portal Openness is not implemented in this milestone. Runtime never depends on TIA Portal.

## Canonical TAG communication-binding convergence

The Coordinator-validated Engineering v14 target keeps the Siemens binding identity stable. Driver 08 therefore continues to use:

- schema ID `siemens.s7.iso.binding`;
- schema version `1`;
- portable address prefix `s7iso:v1`.

No Siemens binding schema v2 is introduced.

`S7IsoCommunicationBindingProjection` is the branch-local convergence adapter for the future shared `CommunicationTagBinding` envelope. It:

- preserves the current Siemens schema ID and version;
- projects area, DB number, byte offset, protocol-native bit offset, value type, string length and writable intent as Siemens-owned settings;
- emits the canonical v1 portable address without a second persisted ordering authority;
- maps the internal `S7IsoValueOrder` representation to the shared physical transform semantics: Normal = no swap, ByteSwap = byte swap, WordSwap = word swap, ByteAndWordSwap = both;
- rejects canonical materialization when byte/word ordering is also persisted in Siemens settings or portable-address tokens;
- keeps legacy v1 addresses containing `order` readable through `S7IsoTagBinding` for migration only.

The final `CommunicationTagBinding`, `TagPhysicalValueTransform`, canonical `Address == PortableAddress` enforcement and generic `TagValueSelector` persistence remain shared Engineering responsibilities. Protocol-native BOOL bit addressing remains part of the physical Siemens address because it identifies the source bit itself.

## Branch-local runtime composition seam

Driver 08 now includes a branch-local Siemens runtime planner/factory core aligned with the Coordinator-owned runtime composition contract:

- `S7IsoRuntimePlanner` consumes one canonical Engineering package plus one Siemens Data Source;
- it returns a library-independent `S7IsoRuntimePlan` plus `EngineeringDriverIssue` evidence;
- the plan exposes Data Source key/name, stable DriverType and TAGs/points without retaining socket, ISO session or negotiated-PDU client objects;
- `S7IsoRuntimeFactory` receives the host-owned current-value cache and TAG registry and creates the concrete `S7IsoDriver`;
- no central registry, loader, compiler dispatch or runtime coordinator registration is performed by this branch.

The current branch base predates the validated v14 `CommunicationBinding` field, so the planner still consumes the compatibility `TagEngineeringDto.Address` alias and treats generic `AddressSelector` support as a later shared-contract reconciliation point. This is a transitional adapter boundary, not a second canonical binding model.

## Current scalar and text/date coverage

Implemented protocol-owned bindings/codecs include:

- Boolean
- Byte / USInt representation
- SInt
- UInt16 / Word
- Int16 / Int
- UInt32 / DWord
- Int32 / DInt
- Float32 / Real
- Int64 / LInt
- Float64 / LReal
- classic STRING
- first-cut WSTRING
- classic DATE
- classic DATE_AND_TIME

Arrays, structures/UDTs, optimized symbolic access, TIME/TOD families and newer long date/time families are not promoted into private Driver 08-only canonical value models.

## External runtime dependencies and redistribution

- External S7 runtime package/library: **none**.
- Runtime implementation: .NET Base Class Library plus EliteSCADA project references.
- Native binaries: none introduced by Driver 08.
- S7.NetPlus, Sharp7 and Snap7 were research/reference candidates only and are not runtime dependencies in this branch.
- Third-party S7 library redistribution obligations introduced by this implementation: none.

The repository currently does not expose a formal project/module license declaration that this branch can authoritatively assign. Therefore the overall EliteSCADA/module distribution license remains a project-owner/Coordinator decision. Driver 08 must not invent that legal classification.

## CI evidence

The protocol implementation, binding projection and branch-local runtime composition core are exercised through the existing pull-request CI. Review evidence must always be tied to the exact current branch HEAD before integration. The branch has already demonstrated successful Release build/test/runtime-smoke checkpoints with zero build warnings/errors, including focused Siemens binding-projection and runtime-planning tests.

A documentation-only manifest update still creates a new Git commit, so the final handoff must cite the CI run for that exact final HEAD rather than inheriting green status from an earlier commit by optimism.

## Production distribution status

**Blocked on shared integration and external interoperability evidence.**

The protocol implementation has no third-party runtime-license blocker. Branch-local readiness, canonical Siemens binding projection and planner/factory core now exist, but production distribution is not yet authorized because the following remain required:

1. green CI evidence on the exact final review HEAD;
2. Coordinator-owned adapter/registration into the shared runtime planner/factory registry and central dispatch;
3. Coordinator-owned Engineering v14 `CommunicationTagBinding` envelope integration, including `Address == PortableAddress`, shared physical transform and generic `TagValueSelector` semantics;
4. Coordinator-owned common readiness/activation integration;
5. representative Siemens hardware or vendor-simulator interoperability evidence;
6. final project/module license declaration and packaging policy.

## Hardware and vendor-simulator validation still required

The review/production matrix must include representative evidence for:

- S7-300 normal CPU topology, including common Rack 0 / Slot 2 behavior;
- S7-400 topology where CPU slot and communication processor placement can vary;
- S7-1200 classic absolute PUT/GET-compatible access with protection constraints recorded;
- S7-1500 classic absolute PUT/GET-compatible access with protection constraints recorded;
- explicit TSAP topology;
- negotiated PDU behavior;
- multi-variable reads and per-item failure isolation;
- scalar, STRING, WSTRING, DATE and DATE_AND_TIME byte layouts;
- write acceptance/rejection and protection-denied classification;
- cable/session loss and reconnect behavior;
- non-optimized/global DB access versus unsupported optimized/symbolic-only members.

A loopback peer is useful deterministic protocol evidence but is not a substitute for Siemens hardware/vendor interoperability evidence.

## Shared Coordinator reconciliation still required

Driver 08 intentionally does not implement or register these central seams:

- common DriverHost runtime registry/dispatch;
- installable module loader/catalog;
- central compiler/runtime coordinator migration;
- canonical Engineering v14 communication-binding envelope ownership;
- common readiness/activation orchestration;
- common licensing/module policy;
- Gateway dispatch.

The branch now supplies the Siemens-specific pieces intended to plug into those seams: protocol/runtime implementation, readiness evidence, stable binding-v1 projection, and branch-local planner/factory core. They are integration evidence, not competing shared frameworks.
