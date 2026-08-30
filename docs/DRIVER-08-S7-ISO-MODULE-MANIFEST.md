# Driver 08 - Siemens S7 ISO-on-TCP module manifest

Status: **protocol-owned draft manifest / not a mainline integration authorization**

This manifest records the production-readiness evidence requested by `docs/DRIVER-CONVERGENCE-COORDINATION-V1.md`. It describes the current Driver 08 branch only. It does not define or implement the Coordinator-owned module loader, runtime registry, shared readiness contract or licensing system.

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
- Binding-level byte/word ordering where meaningful, with inverse read/write transforms.

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

## Production distribution status

**Blocked on evidence / integration.**

The protocol implementation has no third-party runtime-license blocker, but production distribution is not yet authorized because all of the following remain required:

1. exact-head .NET build/test CI evidence;
2. Coordinator reconciliation into the shared registry/planner/factory/module composition seam;
3. Coordinator-owned rich canonical Driver binding projection/reconciliation;
4. Coordinator-owned common readiness adapter integration;
5. representative hardware or vendor-simulator interoperability evidence;
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

Driver 08 intentionally does not modify these central seams:

- DriverHost registry/runtime composition;
- common runtime planner/factory interfaces;
- installable module loader/catalog;
- canonical rich TAG Driver-binding DTO;
- common readiness/activation contract;
- common licensing/module policy;
- Gateway dispatch.

The branch provides protocol-owned evidence intended to plug into those seams rather than competing replacements.
