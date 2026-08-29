# PARALLEL DRIVER WORK ASSIGNMENTS — EliteSCADA

Date: 2026-08-29
Status: **AUTHORIZED — PARALLEL / PARKED FROM MAINLINE**
Coordinator: **Coordinator**
Authorization baseline: `main` at `17ebb36b393d75a6b0f8de6ae04c90d2afc260c2`

## Numbering rule

`DEV 1`, `DEV 2` and `DEV 3` are reserved for the active Wave/FOLLOW-B development workstreams and must not be reused as Driver DEV identifiers.

Parallel communication-driver work therefore uses the identifiers **DEV Driver 4 through DEV Driver 10**.

The previous names `DEV Driver 1`, `DEV Driver 2` and `DEV Driver 3` are retired aliases. Their old branches `driver1/siemens-s7-iso`, `driver2/opc-ua` and `driver3/mqtt` must not receive new work. The canonical replacements are:

- `DEV Driver 8` / `driver8/siemens-s7-iso` — Siemens S7 ISO-on-TCP;
- `DEV Driver 9` / `driver9/opc-ua` — OPC UA;
- `DEV Driver 10` / `driver10/mqtt` — MQTT.

This numbering separation is intentional to prevent chat/assignment ambiguity between Wave workers and parallel Driver workers.

## Purpose and priority

The seven Driver DEV workstreams are authorized to advance industrial communication drivers in parallel while the main EliteSCADA development sequence remains governed by the WAVEs.

The WAVEs remain the absolute priority for `main` and for product-stage progression. Driver work must not block, reorder, or redefine the current Wave sequence.

Completion of a driver does **not** authorize merge into `main`. Each driver remains parked on its isolated branch until the Coordinator reviews the completed set and an explicit integration decision is made.

No Driver DEV may depend on another Driver DEV finishing first. Shared architectural improvements that appear necessary must be reported in handoff rather than silently creating incompatible protocol-specific abstractions.

## Common architecture and contracts

Every Driver DEV must re-read the current repository before implementation and follow the public/common contracts already established by EliteSCADA, especially:

- `docs/ADR-002-*` where applicable to the common runtime/driver architecture;
- `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md`;
- `docs/ADR-009-*` where applicable to the common Engineering/driver boundary;
- `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`;
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`;
- `docs/TAG-GATEWAY.md`;
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`;
- the current Engineering Import/Export, revision and project-package contracts.

Modbus is the principal concrete reference implementation for mature integration with TAGs, Gateway, Engineering and diagnostics, but protocol-specific Modbus assumptions must not be copied blindly into unrelated protocols.

### Common mandatory behavior

Where supported by the protocol, every driver must provide:

- runtime connection lifecycle;
- deterministic reconnect behavior;
- read and write support;
- canonical TAG binding;
- quality/state propagation;
- source/device timestamps when meaningful;
- common diagnostics and communication status;
- persistent Engineering configuration through public/versioned contracts;
- validation and actionable diagnostics;
- Engineering Import/Export where applicable;
- project-package and revision fidelity where applicable;
- compatibility with the existing TAG/Gateway architecture;
- automated tests that do not require physical hardware wherever practical;
- explicit documentation of any validation that still requires hardware or a vendor simulator.

Secrets, passwords, private keys and equivalent protected credentials must never be exported as plaintext.

### Physical byte/word ordering

The binding-level byte/word ordering contract in ADR-007 is mandatory where technically applicable.

Drivers must not infer ordering from manufacturer name alone. When the underlying physical representation makes it meaningful, the public binding must be able to represent normal/native order, byte swap, word swap and combined byte+word swap subject to type-width validation.

Physical transformation occurs before canonical typed TAG publication on reads and is applied inversely on writes. Integer TAG bit selection is evaluated after this transformation against the canonical typed integer value.

### TAG identity and bit access

Do not create a driver-local TAG identity model. Stable TAG identity and integer bit selectors must reuse the canonical EliteSCADA contract. Authoring syntax such as `.NN` is display/input syntax, not an alternate persisted identity.

### CI policy

CI remains **NORMAL**. Do not run reassurance CI after every small commit. Each Driver DEV should run focused local/branch tests during development and provide exact evidence in handoff. Broader CI is coordinated when justified by integration risk or explicit acceptance needs.

---

## DEV Driver 8 — Siemens S7 ISO-on-TCP

Branch: `driver8/siemens-s7-iso`
Status: **AUTHORIZED / PARALLEL**

Primary references:

- `docs/S7-ISO-CONNECTION.md`
- `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md`

Ownership:

- Siemens S7 communication over ISO-on-TCP;
- connection parameters including Rack/Slot/TSAP as supported by the chosen contract;
- read/write operations;
- DB, M, I and Q areas where supported;
- supported Siemens scalar/data types;
- connection state, quality, timestamps where meaningful, reconnect and diagnostics;
- safe batching and PDU-aware request handling;
- Engineering configuration and persistence;
- TIA/project import only where already specified by canonical research/contract;
- binding-level byte/word ordering where technically meaningful.

Constraints:

- do not encode `Siemens = swap` as a manufacturer heuristic;
- do not create a second TAG addressing/identity system;
- do not bypass common Driver SDK/Engineering/diagnostics contracts.

---

## DEV Driver 9 — OPC UA

Branch: `driver9/opc-ua`
Status: **AUTHORIZED / PARALLEL**

Primary references:

- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`

Ownership:

- OPC UA client runtime;
- endpoint discovery and endpoint selection;
- secure sessions and reconnect;
- certificate handling through public/protected Engineering contracts;
- subscriptions, monitored values and read/write;
- stable NodeId identity;
- StatusCode/quality and source/server timestamps;
- browse and node selection/import into TAG Engineering;
- diagnostics and communication state;
- persistence, validation, Import/Export and package fidelity.

Constraints:

- preserve OPC UA identity rather than reducing nodes to display names;
- do not export private credentials/certificates as plaintext;
- do not create an OPC-UA-only persistence island.

---

## DEV Driver 10 — MQTT

Branch: `driver10/mqtt`
Status: **AUTHORIZED / PARALLEL**

Primary reference:

- `docs/research/mqtt/MQTT-INDUSTRIAL-DRIVER-RESEARCH.md`

Ownership:

- industrial MQTT client behavior;
- broker connection over TCP/TLS;
- protected authentication/secrets;
- QoS handling;
- subscriptions and deterministic reconnect/resubscribe;
- retained-message behavior;
- Topic -> TAG mapping through canonical TAG bindings;
- typed payloads and JSON payload extraction where specified;
- publication for writable TAGs where configured;
- communication state, quality and diagnostics;
- Engineering persistence, validation and Import/Export.

Constraints:

- do not build a separate MQTT-only Engineering/configuration subsystem;
- do not persist plaintext secrets;
- define deterministic behavior for malformed/unsupported payloads rather than silently coercing values.

---

## DEV Driver 4 — BACnet

Branch: `driver4/bacnet`
Status: **AUTHORIZED / PARALLEL**

Primary reference:

- `docs/research/bacnet/BACNET-IP-SC-RESEARCH.md`

Ownership:

- initial BACnet/IP implementation;
- architecture that does not prevent later BACnet/SC support;
- discovery;
- canonical device/object/property identity;
- read/write;
- COV where applicable plus polling where required;
- supported BACnet data types and quality/state mapping;
- reconnect and diagnostics;
- browse/import into Engineering;
- persistence, validation and Import/Export.

Constraints:

- do not expose BACnet/SC as if implemented until the secure transport/session layer is genuinely functional;
- do not collapse object/property identity into display labels;
- do not create protocol-private diagnostics or persistence mechanisms when common ones exist.

---

## DEV Driver 5 — Allen-Bradley EtherNet/IP / CIP

Branch: `driver5/allen-bradley-cip`
Status: **AUTHORIZED / PARALLEL**

Primary reference:

- `docs/research/allen-bradley/ALLEN-BRADLEY-ETHERNET-IP-CIP-RESEARCH.md`

Ownership:

- first-cut support focused on ControlLogix/CompactLogix;
- symbolic Logix access over EtherNet/IP/CIP;
- preservation of Controller/Program TAG symbolic identity;
- supported Logix scalar/data types;
- read/write;
- safe request batching;
- reconnect, quality and diagnostics;
- browse/import of supported symbols into Engineering;
- persistence, validation and Import/Export.

Constraints:

- Micro800, PCCC and legacy families remain outside the first cut unless current canonical documentation explicitly expands scope;
- do not flatten symbolic identity into unstable display strings;
- do not bypass shared TAG/Gateway/Engineering contracts.

---

## DEV Driver 6 — IEC 60870-5-104

Branch: `driver6/iec-60870-5-104`
Status: **AUTHORIZED / PARALLEL — RESEARCH FIRST**

The repository does not yet have an equally mature canonical IEC-104 research contract. The first deliverable on this branch is therefore a focused research/contract document before substantial runtime implementation.

Ownership target:

- EliteSCADA as IEC 60870-5-104 client/master in the initial release;
- TCP connection lifecycle and reconnect;
- STARTDT, STOPDT and TESTFR handling;
- I/S/U frame sequencing, counters and transmission windows;
- General Interrogation;
- supported indication ASDUs;
- spontaneous/event reporting;
- Cause of Transmission;
- Common Address and Information Object Address identity;
- CP56Time2a timestamps where applicable;
- quality descriptors mapped into canonical EliteSCADA quality/state;
- supported single/double commands and setpoints where appropriate;
- explicit command outcome/error handling;
- diagnostics;
- point browse/import or bulk Engineering workflows where technically appropriate;
- persistent Engineering configuration, validation and Import/Export.

Research-first requirements:

- select/document implementation/library strategy and license implications;
- define supported ASDUs/type identifications for the first release;
- define addressing and stable point identity;
- define quality/timestamp/event semantics;
- define command semantics and safety behavior;
- define automated test strategy and simulator/hardware validation still required;
- record limitations before broad implementation begins.

Constraints:

- no hidden IEC-104-only TAG identity;
- no silent command success assumptions;
- no protocol behavior guessed from vendor branding.

---

## DEV Driver 7 — DNP3

Branch: `driver7/dnp3`
Status: **AUTHORIZED / PARALLEL — RESEARCH FIRST**

The first deliverable on this branch is a focused DNP3 research/contract document before substantial runtime implementation.

Ownership target:

- EliteSCADA as DNP3 Master/Client in the initial release;
- initial TCP transport, with architecture that can accommodate serial transport later without corrupting the common driver contract;
- Binary Input;
- Double-Bit Binary Input;
- Analog Input;
- Counter and Frozen Counter where selected for the first release;
- object groups/variations explicitly defined by the contract;
- Class 0/1/2/3 processing;
- integrity polling;
- unsolicited responses;
- DNP3 time/timestamp handling;
- point flags/quality mapping;
- retries, timeout, reconnect and communication diagnostics;
- Binary Output / CROB and Analog Output where selected for the first release;
- explicit Select-Before-Operate vs Direct Operate behavior where applicable;
- command result/error visibility;
- persistent Engineering configuration including station addressing, polling/event configuration, group/variation/index identity, write permissions and diagnostics;
- validation and Import/Export.

Research-first requirements:

- select/document library or implementation strategy and licensing;
- define first-release object groups/variations;
- define stable point addressing/identity;
- define quality/flags, timestamps, event classes and unsolicited behavior;
- define command modes and safety semantics;
- define automated testing and simulator/hardware validation requirements;
- record limitations before broad implementation begins.

Constraints:

- do not reduce DNP3 commands to a blind generic `set value` operation;
- do not create a DNP3-specific quality or TAG model disconnected from the common runtime;
- serial transport is not required for the initial cut unless later explicitly authorized.

---

## Required handoff from every Driver DEV

When the assigned scope is complete, the Driver DEV must report:

1. exact branch and exact head SHA;
2. concise delivered scope;
3. exact changed-file list;
4. tests executed and exact results;
5. supported protocol features/types/operations;
6. known limitations and risks;
7. hardware/vendor-simulator validation still required;
8. confirmation that no unassigned branch or `main` was modified;
9. any shared Driver SDK, TAG, Gateway, Engineering, diagnostics or Import/Export contract decision requiring Coordinator reconciliation.

## Integration gate

Driver branches are intentionally independent from the Wave integration branch.

When all seven Driver DEV efforts are complete or parked at a reviewable milestone, the Coordinator will assess them together against the then-current `main`, identify shared-contract conflicts and integration cost, and report whether incorporation is advisable at that point.

No driver enters `main` solely because its branch is complete.