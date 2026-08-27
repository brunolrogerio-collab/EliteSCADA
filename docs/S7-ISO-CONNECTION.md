# EliteSCADA Siemens S7 ISO Connection

## Status

Locked product/architecture requirement refined from product-owner direction on 2026-08-26.

Siemens S7 through **ISO Connection** is a planned external protocol family for EliteSCADA. The production driver/runtime remains gated by the mandatory sequence in `PROJECT GOAL.md` and `docs/ROADMAP.md`. An early research/design spike may run before that gate only when it does not add production S7 runtime behavior, central DI/API integration or an active external-protocol Data Source.

This document defines **S7 ISO Connection** as the classic Siemens S7 client communication path carried over **ISO-on-TCP / RFC1006 on TCP port 102**, using S7 read/write services appropriate to the target CPU. It is not generic PROFINET I/O, not OPC UA and not permission to implement proprietary S7commPlus behavior without a separate architecture/security decision.

## Product goal

The future Engineering experience should make a Siemens connection practical for an industrial engineer:

`Choose CPU/family -> enter/discover IP -> configure Rack/Slot or TSAP -> connection test/diagnostics -> import/browse Engineering symbols -> preview TAG mapping -> apply through canonical Engineering`

Manual address entry remains supported, but TIA Portal-assisted import should be a first-class workflow because large Siemens projects should not require hand-entering hundreds of DB offsets.

## Reference behavior studied

Elipse M-Prot provides useful precedents for ISO/TCP Engineering:

- ISO/TCP is exposed as a distinct Ethernet protocol mode;
- connection configuration includes Rack, Slot, Source/Destination TSAP and connection type such as PG/OP/PC;
- advanced settings include additional connections and maximum simultaneous variables for performance tuning;
- S7-1200 guidance uses ISO/TCP with Rack 0 / Slot 1 and explicit TSAP settings;
- imported TAGs may be obtained from TIA Portal through a separate importer using TIA Portal Openness;
- imported tags are presented in a tree/Tag Browser rather than forcing manual numeric parameters for every variable;
- the importer supports bulk import from supported TIA Portal versions and maps project structure into a browsable tree.

EliteSCADA should preserve the useful workflow while using its own public/versioned Engineering model rather than copying Elipse numeric N/B parameter conventions.

## Technology candidates

The research spike must compare at least:

1. **S7.NetPlus** — managed C#, MIT licensed, supports S7-200/300/400/1200/1500 and .NET Standard targets.
2. **Sharp7** — C# implementation derived from Snap7 concepts; current official line is MIT licensed and documents S7-300/400 plus basic S7-1200/1500 HMI-style access.
3. A thin EliteSCADA protocol implementation only if existing libraries fail requirements for correctness, licensing, security, cancellation, reconnect, batching or maintainability.

Do not select a dependency merely because a sample can read `DB1.DBW0`. The later production decision must evaluate maintenance activity, known issues, protocol coverage, cancellation/timeouts, async behavior, PDU negotiation, multi-variable batching, reconnect, write semantics, testability and licensing.

## Supported CPU families direction

The research must explicitly evaluate:

- S7-300;
- S7-400;
- S7-1200;
- S7-1500.

S7-200 may be documented as legacy/conditional support but must not distort the initial architecture.

Default Rack/Slot suggestions may be offered by CPU family but are never hidden constants. Engineering must allow explicit configuration and later TSAP override for non-standard topologies/CP modules.

## Connection model

A future S7 ISO Data Source should expose through a public versioned driver schema, at minimum:

- stable Data Source identity;
- host/IP;
- TCP port, default 102;
- CPU family/profile;
- connection addressing mode: Rack/Slot or explicit TSAP;
- Rack and Slot when that mode is used;
- Source TSAP / Destination TSAP in advanced mode;
- connection role/type where supported by the selected stack;
- connect timeout, request timeout and reconnect/backoff policy;
- negotiated/effective PDU size as runtime diagnostics, not mutable Engineering truth;
- polling/batching limits;
- optional parallel connection count only when the selected PLC/library safely supports it;
- write enable policy and security warnings;
- sanitized metadata useful for diagnostics.

Secrets are not expected for classic S7 PUT/GET itself, but future protected credentials/certificates or gateway credentials must follow the project secret-reference rule.

## S7-1200 / S7-1500 protection constraints

Classic HMI-style PUT/GET access to modern CPUs has important PLC-side prerequisites.

The Engineering/test workflow must be able to explain, where applicable:

- remote PUT/GET access is disabled by default on modern S7-1500 configurations and must be explicitly permitted in CPU Protection & Security / Connection mechanisms;
- CPU access-control settings still govern whether read/write is permitted;
- absolute access to S7-1200/1500 data blocks generally requires suitable non-optimized/global DB layout for classic base-protocol clients;
- optimized DB/symbolic-only access must not be silently represented as an absolute DB offset if the chosen classic ISO/S7 stack cannot address it safely;
- production diagnostics should distinguish connection failure, protection/authorization refusal, invalid rack/slot/TSAP, unsupported address/type and write rejection whenever evidence allows.

The product must not recommend weakening PLC protection beyond what is necessary for the engineered use case. A read-only SCADA application should not casually instruct users to grant Full Access if a narrower supported configuration exists.

## TAG address model

The research must define a typed, protocol-specific binding model that can later live inside the public Data Source/TAG Engineering schema without leaking Elipse N1/N2/N3/N4 conventions.

Candidate address areas include, as supported by CPU family/library:

- Inputs (I/E);
- Outputs (Q/A);
- Merkers/flags (M);
- Data Blocks (DB);
- counters/timers for CPU families where classic addressing remains valid.

The binding must explicitly carry area, DB number where applicable, byte/bit offset and EliteSCADA data type/length instead of relying only on an opaque address string. A canonical text form may also be provided for import/export and engineer convenience.

Required type research includes at least Bool, byte/word/dword integers, signed integers, Real, LReal where supported, String/WString, DATE/TIME/date-time families and arrays/structures. Unsupported or lossy mappings must be explicit.

## TIA Portal import

EliteSCADA should investigate a TIA-assisted import workflow inspired by the Elipse M-Prot importer but better integrated with canonical Engineering.

Primary path to evaluate: **TIA Portal Openness** on the Engineering workstation.

The import research must determine how to obtain and reconcile:

- PLC/device identity;
- PLC tag tables and tags;
- logical addresses;
- data types;
- DBs and DB members;
- UDTs where practical;
- accessibility/writability metadata;
- optimized/non-optimized information where available;
- comments/descriptions;
- hierarchy/grouping useful for proposed EliteSCADA TAG paths.

The production EliteSCADA runtime must not depend on TIA Portal being installed. TIA Portal/Openness is an Engineering-side optional importer.

Also evaluate a file-based workflow using TIA Portal exported XML or other Siemens-supported exports so users can import Engineering data without granting a running EliteSCADA service access to TIA Portal automation APIs.

## Import-preview workflow

Import must use the canonical EliteSCADA flow:

`TIA project/export -> build candidates -> validate -> preview -> choose merge semantics -> apply`

The preview should show:

- source PLC/table/DB hierarchy;
- proposed EliteSCADA TAG path/name;
- S7 area/address;
- data type and length;
- read/write capability;
- conflicts with existing paths/IDs;
- unsupported optimized/symbolic-only variables;
- unsupported data types;
- warnings about PUT/GET/protection prerequisites;
- selected Data Source and polling profile.

The engineer may select all, selected tables/DBs, individual tags or a subtree and may rename/repath candidates before Apply.

No TIA importer may bypass the public/versioned Engineering model.

## Discovery / connection test

Classic S7 ISO does not provide an OPC-UA-like universal address-space discovery service. EliteSCADA must therefore distinguish **network assistance** from authoritative device discovery.

A future optional `Scan for S7 ISO devices` tool may:

- require explicit user initiation and subnet/CIDR/interface scope;
- be cancellable and rate/concurrency limited;
- probe TCP/102 only within explicit bounds;
- perform only non-destructive connection/identity checks supported safely by the selected library;
- never write, change CPU mode or enumerate private project content during scan;
- return candidates for engineer inspection, never auto-create Data Sources.

Manual IP/host entry remains the primary deterministic path.

## Diagnostics direction

The S7 ISO driver must eventually feed the common Data Source diagnostics model, including where meaningful:

- connecting/healthy/degraded/reconnecting/faulted state;
- last success/failure;
- request/success/failure/timeout counters;
- reconnect count;
- request latency;
- scan/data age;
- negotiated PDU and batching information where useful;
- sanitized last protocol error;
- associated TAG quality counts.

S7-specific detail can enrich diagnostics but must not create a parallel health system.

## Performance and batching

Research must explicitly evaluate efficient multi-variable operation rather than one TCP request per TAG.

The later driver should support deterministic grouping/batching subject to:

- negotiated PDU size;
- address contiguity and type/area compatibility;
- PLC limits;
- bounded request size;
- scan classes;
- independent failure handling;
- no write coalescing that changes operator command semantics.

Elipse's Extra Connections / Max Simult Req concepts are useful references, but EliteSCADA should expose safe engineering-level performance profiles and runtime diagnostics rather than vendor-copy settings.

## Safety / forbidden operations

The initial SCADA driver is for process data read/write only. The production driver must not expose generic PLC-control operations such as CPU RUN/STOP, program upload/download, block deletion or firmware operations through the normal TAG path.

If such administrative capabilities are ever added, they require separate explicit product design, strong authorization, confirmation and Audit.

## Required research outcomes

The early spike must produce:

1. library comparison and recommended stack;
2. connection matrix for S7-300/400/1200/1500 including Rack/Slot/TSAP behavior;
3. protection/PUT-GET/optimized-DB compatibility matrix;
4. typed address and data-type mapping proposal;
5. batching/reconnect/performance design;
6. TIA Portal Openness/file-export import strategy;
7. import-preview UX/data-contract proposal;
8. bounded optional network-scan approach;
9. representative real/simulated PLC test strategy, including PLCSIM limitations;
10. a later production implementation breakdown that fits common Driver/Data Source/Gateway/diagnostics architecture.

## Implementation gate

Research may proceed now in an isolated documentation-only branch.

Production implementation remains blocked until the current sequence reaches the external-protocol gate:

`TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols/modules`

When that gate opens, this research becomes input to the production S7 ISO Connection assignment rather than permission to bypass it.
