# ADR-007 — Industrial protocols and installable driver modules

## Status
Accepted and active.

## Context

EliteSCADA already has a common driver abstraction, Data Source Engineering model, DriverHost boundary and real Modbus TCP implementation. The product must grow beyond protocols compiled directly into the core repository without allowing each protocol to create its own private configuration/runtime model.

The product direction requires MQTT, OPC UA, BACnet and the ability to add further communication drivers through installable modules. Research for MQTT, OPC UA, BACnet/IP + BACnet/SC, Siemens S7 ISO Connection and Allen-Bradley Logix EtherNet/IP/CIP is now merged as architecture input.

## Decision

### Protocol targets

The industrial communication roadmap includes, at minimum:

- Modbus TCP — existing first real industrial driver baseline;
- MQTT — planned first-class integration;
- OPC UA — planned first-class industrial interoperability integration;
- BACnet — planned communication-driver family for building automation/BMS and compatible controllers/devices;
- Siemens S7 ISO Connection — intended first installable Driver Module target;
- Allen-Bradley Logix EtherNet/IP/CIP — later installable/first-party target after licensing and hardware acceptance;
- additional first-party or third-party protocols supplied through installable Driver Modules.

All protocol implementations use common EliteSCADA Data Source, TAG, quality, runtime, diagnostics, Gateway and Engineering boundaries. A protocol plugin must not become a separate configuration island or bypass the public Engineering model.

### Research does not select production dependencies

Merged protocol research defines architecture, identity, Engineering and laboratory direction but does not by itself select final libraries/packages.

At implementation time every protocol slice must re-check:

- current dependency version and maintenance state;
- license and redistribution obligations;
- security advisories;
- .NET/platform compatibility;
- cancellation/reconnect behavior;
- representative interoperability/hardware evidence.

Library types remain private adapter details. Public Driver/Data Source/TAG-binding contracts are EliteSCADA-owned.

### First installable module target: Siemens S7 ISO Connection

The first intended proof/production target for the installable Driver Module framework remains a Siemens S7 ISO-on-TCP driver.

Merged research in `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md` narrows the intended first scope around classic HMI-style S7 communication, explicit Rack/Slot/TSAP semantics, PDU-aware bounded reads/writes and Engineering-side TIA/export import. Optimized symbolic-only DB members must not receive guessed absolute offsets.

The future production dependency decision remains gated by a dedicated lab/compatibility scorecard.

### Allen-Bradley direction after research

Allen-Bradley is no longer an undefined research target. Merged research in `docs/research/allen-bradley/ALLEN-BRADLEY-ETHERNET-IP-CIP-RESEARCH.md` establishes the initial architectural direction:

- target ControlLogix/CompactLogix first;
- use EtherNet/IP/CIP explicit messaging for Logix symbolic TAG access;
- preserve symbolic controller/program identity rather than runtime browse handles;
- treat Micro800 and legacy PCCC/data-table families separately;
- require real-hardware acceptance and explicit licensing/security review before production.

This remains **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED**.

### Installable Driver Modules

EliteSCADA will provide a controlled module/plugin mechanism that allows communication drivers to be installed without rebuilding the core product.

A Driver Module must eventually declare at least:

- stable module/package identity;
- module version;
- supported EliteSCADA Driver SDK/platform contract version or compatibility range;
- Driver/Data Source types provided;
- `CommunicationDriverTypeDescriptor` for each provided Driver type;
- versioned public Data Source and TAG-binding configuration schema;
- runtime capabilities and supported acquisition modes;
- optional Engineering capabilities such as connection test, discovery, browse, file import and reconciliation;
- module/publisher integrity and trust metadata;
- migration information when configuration schemas change.

The lifecycle must support:

- installation;
- discovery/catalog registration;
- enable/disable;
- upgrade;
- removal;
- compatibility validation before Runtime activation;
- diagnostics for missing, disabled or incompatible modules.

Disabling/removing a module must not silently destroy project Engineering configuration that references it. Such configuration remains representable/exportable and produces explicit validation/runtime diagnostics until the required module becomes available again.

### Physical value byte/word ordering

A physical `Data Source -> TAG` binding for a multi-byte scalar must be able to declare byte/word ordering explicitly when the selected driver exposes raw register/byte-oriented data and the protocol/device representation differs from EliteSCADA's canonical typed value representation.

This is a binding-level engineering property, not a global PLC-brand heuristic. Siemens, Modbus devices and other controller families may expose different layouts depending on protocol, data area, gateway, library or device model; EliteSCADA must therefore persist the selected transformation explicitly rather than infer it only from manufacturer identity.

The canonical binding model must support, where meaningful for the selected physical type and width:

- normal/native ordering;
- byte swap inside each 16-bit word;
- word swap for values spanning more than one 16-bit word;
- combined byte + word swap;
- future protocol-specific wider ordering only through a versioned public binding contract rather than private driver metadata.

Minimum required applicability includes 16-bit word/integer values and multi-register integer/floating-point values such as 32-bit and 64-bit integers and IEEE-754 floating-point values when supported by the TAG/driver type system. Invalid combinations must be rejected by Engineering validation; for example, word swap has no meaning for a single 16-bit value.

The transformation belongs to the physical representation boundary. On read, raw driver data is reordered before canonical typed decoding and publication to the TAG. On write, the inverse transformation is applied after canonical value encoding and before the physical driver write. Read/write behavior must therefore be symmetric and deterministic.

The Engineering UI should expose these options only when the selected source type makes them meaningful, using clear labels such as `Byte Swap` and `Word Swap`, with a preview/diagnostic representation of the resulting byte order where practical. Defaults must preserve the driver's normal/native ordering so existing bindings remain compatible.

These settings must participate in canonical JSON/CSV/import/export, Preview/Apply validation, immutable revisions, project packages and diagnostics. They must not be stored only in frontend state or opaque metadata.

For bit-level TAG bindings, bit selection is evaluated against the canonical integer value after the configured physical byte/word transformation has been applied. This preserves one bit-numbering contract throughout the product instead of making bit identity depend on transport byte order.

### Runtime boundary

Driver Modules execute behind DriverHost/Driver SDK contracts. They do not directly own or bypass:

- TAG identity/state semantics;
- TAG quality semantics;
- Alarm Engine;
- Historian;
- Gateway;
- frontend/UI;
- application authorization;
- project persistence/revision lifecycle.

The exact isolation model may evolve, but third-party driver code is not implicitly trusted merely because it is installed.

### Engineering boundary

Driver Engineering tooling is separate from active Runtime communication.

Capability-specific adapters may provide:

- connection test;
- discovery;
- browse/observe;
- project/file import;
- reconciliation/rescan.

Those operations return transient candidates/evidence. Canonical mutation remains:

`candidate -> validate -> preview -> choose merge mode -> apply`

A Driver that lacks a capability does not implement a fake equivalent merely for UI uniformity.

See ADR-009 and `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`.

### Engineering Import/Export

Plugin-owned Driver configuration participates in canonical Engineering.

The public package contains versioned configuration and module/type references required to reconstruct the project, while plaintext credentials, private keys and other secrets remain outside the package and are represented only by protected references.

Project preview/validation must detect:

- missing required module;
- unsupported module version;
- incompatible configuration schema;
- failed migration;
- unavailable Driver/Data Source type.

A future deliberate Engineering schema migration will introduce richer protocol-owned TAG bindings. Current Schema v9 is not changed merely to reserve speculative protocol fields.

### Security and audit

Module administration is security-sensitive. Installation, removal, enable/disable and upgrade must be permission-controlled and auditable when implemented.

Package integrity/publisher trust must be validated before enabling executable module code. Silent loading of arbitrary untrusted code is unacceptable for an industrial runtime.

Driver-specific secrets/certificates are resolved by host-owned security infrastructure. Modules receive only the minimum credential material required for their own operation and must not enumerate unrelated project secrets.

## Consequences

- MQTT, OPC UA and BACnet remain explicit product requirements.
- Siemens S7 ISO Connection remains the first intended installable Driver target.
- Allen-Bradley Logix now has a documented initial architecture instead of an undefined future research item.
- The core repository does not need to contain every industrial protocol.
- Driver extension cannot bypass Engineering Import/Export, Gateway, diagnostics or runtime safety boundaries.
- Multi-byte physical TAG bindings have an explicit, portable byte/word ordering contract rather than device-brand assumptions.
- A module catalog/package/compatibility subsystem remains a future product slice.
- Missing modules are explicit diagnosable project states, not reasons to discard Engineering data.
- Runtime and Engineering capabilities are separately declared, allowing protocols to differ honestly.

## Deferred implementation decisions

The following remain deferred until the module framework implementation slice:

- physical package/archive format;
- local versus remote module catalog UX;
- exact digital-signature/trust-chain policy;
- OS sandbox/container/process isolation details;
- hot reload versus restart requirements;
- marketplace/private-repository distribution;
- final runtime factory registration shape;
- exact secret resolver/trust-store API;
- exact rich canonical protocol-binding schema migration.

Those details may evolve without changing this ADR's core decision: EliteSCADA supports multiple industrial protocol families and installable versioned Driver Modules through a common Driver SDK and canonical Engineering model.
