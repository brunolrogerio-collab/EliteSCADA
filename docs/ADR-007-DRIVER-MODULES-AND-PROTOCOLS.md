# ADR-007 — Industrial protocols and installable driver modules

## Status
Accepted and active.

## Context

EliteSCADA already has a common driver abstraction, Data Source engineering model, DriverHost boundary and a real Modbus TCP implementation. The product must grow beyond protocols compiled directly into the core repository without allowing each protocol to create its own private configuration/runtime model.

The product direction explicitly requires MQTT, OPC UA and the ability to add further communication drivers through installable modules.

## Decision

### Protocol targets

The industrial communication roadmap includes, at minimum:

- Modbus TCP — existing first real industrial driver baseline;
- MQTT — planned first-class integration;
- OPC UA — planned first-class industrial interoperability integration;
- additional first-party or third-party protocols supplied through installable driver modules.

All protocol implementations must use the common EliteSCADA Data Source, TAG, quality, runtime and Engineering boundaries. A protocol plugin must not become a separate configuration island or bypass the public engineering model.

### Installable driver modules

EliteSCADA will provide a controlled module/plugin mechanism that allows additional communication drivers to be installed without rebuilding the core product.

A driver module must declare at least:

- stable module/package identity;
- module version;
- supported EliteSCADA Driver SDK/platform contract version or compatibility range;
- driver/Data Source types provided;
- versioned public Engineering configuration schema;
- module/publisher integrity and trust metadata;
- migration information when configuration schemas change.

The lifecycle must support:

- installation;
- discovery/catalog registration;
- enable/disable;
- upgrade;
- removal;
- compatibility validation before runtime activation;
- diagnostics for missing, disabled or incompatible modules.

Disabling or removing a module must not silently destroy project Engineering configuration that references it. Such configuration must remain representable/exportable and produce explicit validation/runtime diagnostics until the required module becomes available again.

### Runtime boundary

Driver modules execute behind the DriverHost/driver contract. They do not directly own or bypass:

- TAG identity/state semantics;
- quality semantics;
- alarm engine;
- historian;
- frontend/UI;
- application authorization;
- project persistence.

The exact isolation model may evolve, but third-party driver code must not be treated as implicitly trusted merely because it is installed.

### Engineering Import/Export

Plugin-owned driver configuration must participate in the canonical Engineering model.

The public package contains the versioned configuration and module/type references required to reconstruct the project, while plaintext credentials, private keys and other secrets remain outside the package and are represented only by protected secret references.

Project preview/validation must detect:

- missing required module;
- unsupported module version;
- incompatible configuration schema;
- failed migration;
- unavailable driver/Data Source type.

### Security and audit

Module administration is security-sensitive. Installation, removal, enable/disable and upgrade must be permission-controlled and auditable when implemented.

Package integrity/publisher trust must be validated before enabling executable module code. The concrete signing/trust policy and distribution mechanism will be finalized during implementation, but silent loading of arbitrary untrusted code is not acceptable for an industrial runtime.

## Consequences

- MQTT and OPC UA are explicit product requirements rather than incidental future ideas.
- The core repository does not need to contain every future industrial protocol.
- Driver extension cannot bypass Engineering Import/Export or runtime safety boundaries.
- A module catalog/package/compatibility subsystem becomes a future product slice.
- Missing modules are an explicit diagnosable project state, not a reason to discard engineering data.

## Deferred implementation decisions

The following are intentionally deferred until the module framework implementation slice:

- physical package/archive format;
- local versus remote module catalog UX;
- exact digital-signature/trust-chain policy;
- operating-system sandbox/container/process isolation details;
- hot reload versus restart requirements;
- marketplace or private repository distribution model.

Those details may change without changing this ADR's core decision: **EliteSCADA supports MQTT, OPC UA and installable versioned driver modules through a common Driver SDK and Engineering model.**