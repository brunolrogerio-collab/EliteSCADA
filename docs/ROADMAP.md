# EliteSCADA Roadmap

Engineering Import/Export remains a mandatory cross-cutting capability throughout this roadmap. Every new Engineering domain must join the public versioned model, validation/preview/apply workflow, revision lifecycle and backup/restore path.

**Status date:** 2026-08-27  
**Functional development:** **ACTIVE — INTERFACE PRODUCT DEVELOPMENT / SECOND WAVE DELIVERED**

## Established `main` foundation

The current merged platform includes repository/CI foundation, TAG Engine and quality/current cache, Event Bus, Simulation and Modbus TCP drivers, common Driver SDK/DriverHost boundaries, REST/WebSocket runtime, PostgreSQL Engineering persistence, Working/Revision/Published/Active lifecycle, TimescaleDB historian foundations, canonical import/export, protected Engineering Workspace/CAS mutations, JWT/local identity and authorization, durable Audit, Engineering UI, structured TAG/Data Source/Alarm editors, local user administration, Internal Memory, Python/Visual public contract foundations, isolated Script Engineering, Engineering Schema v9 TAG Gateway, complete protocol-independent TAG Gateway, common multi-Data-Source diagnostics, the first integrated Interface Product Development checkpoint and the merged research-convergence hardening of the Driver SDK.

Important current checkpoints:

- Internal Memory: complete through PR #49;
- TAG Gateway: complete through PRs #50 and #55;
- communication diagnostics: complete through PRs #56 and #57;
- first integrated interface checkpoint: PR #58, merge `f3cc82f0d45a9f0162105b57ae6c42f643af6160`, exact-head CI #378 green;
- Driver SDK research convergence: PR #68, merge `ec82389d1f27c9929b680e8174b38ca72bcf3b54`, exact-head CI #391 green;
- canonical Engineering JSON remains **Schema v9**.

## Current development order

The product order remains:

`merged platform foundations -> INTERFACE PRODUCT DEVELOPMENT -> USER INTERFACE VALIDATION BUILD/PACKAGE -> additional external protocols`

This is a scheduling decision, not an architectural rollback. Source/provider/security/Engineering boundaries remain unchanged.

## 1. Internal Memory TAG sources

**COMPLETE / MERGED.**

Client Memory remains per-runtime-client/local and non-global. Server Memory remains shared/server-owned and retentive by stable TAG ID. Internal sources do not fabricate network diagnostics.

See `docs/INTERNAL-MEMORY-TAGS.md`.

## 2. Protocol-independent TAG Gateway

**COMPLETE / MERGED.**

Schema v9 Gateway routes, Preview/Apply/revision/package persistence, validation, Server Memory support, OnChange/Periodic execution, quality policy, conversion/scaling, transactional runtime replacement, diagnostics and Engineering UI are merged.

See `docs/TAG-GATEWAY.md`.

## 3. Common multi-driver/Data Source diagnostics

**COMPLETE / MERGED.**

Protocol-neutral per-Data-Source communication diagnostics, Modbus instrumentation, independent multi-instance failure/recovery/counters/quality behavior, protected backend snapshots and Engineering diagnostics UX are merged.

See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

## 4. Driver SDK research convergence

**COMPLETE / MERGED THROUGH PR #68 — ARCHITECTURE FOUNDATION, NOT PROTOCOL IMPLEMENTATION.**

The merged MQTT, OPC UA, BACnet/IP + BACnet/SC, Siemens S7 ISO Connection and Allen-Bradley Logix research has been reconciled into one common Driver SDK direction. Client Python and graphical-editor research were also cross-checked to preserve the same authority boundaries.

Official foundations now include:

- active `ICommunicationDriver` remains the small Runtime communication boundary;
- connection test, discovery, browse, file import and reconciliation are separate optional protected Engineering capabilities;
- one Driver type may honestly support only the capabilities its protocol provides;
- Driver type descriptors carry stable runtime/Engineering capabilities, acquisition-mode metadata and versioned public configuration-schema direction;
- public descriptor fields can expose localization resource keys for `pt-BR` / `en` / `es`;
- acquisition may be Polling, Subscription, EventDriven or Hybrid without bypassing the common TAG/cache/event path;
- `TagValue` preserves local `Timestamp` plus optional real `SourceTimestamp` and `ServerTimestamp` when a protocol provides them;
- Driver reconciliation outcomes use typed status values;
- protocol-library handles, browse/session indexes and subscription objects remain implementation details rather than canonical Engineering;
- writes remain through the owning-provider boundary and Gateway remains protocol independent;
- common communication diagnostics remain authoritative, with protocol-specific sanitized details only where meaningful.

Authoritative reading:

- `docs/ADR-009-DRIVER-SDK-ENGINEERING-BOUNDARIES.md`;
- `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`;
- `docs/RESEARCH-CONVERGENCE-READINESS.md`;
- `docs/ADR-002-DRIVER-SDK-AND-REALTIME.md`;
- `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md`.

### Deliberately deferred

PR #68 does not implement:

- any new external protocol runtime;
- final protocol package/library selection;
- Driver Module loader/runtime registration;
- the final rich canonical protocol TAG-binding DTO/schema migration;
- host secret resolver/trust-store infrastructure;
- Python runtime/editor;
- graphical Screen/Popup/Dynamo editor.

Current Schema v9 remains authoritative until a dedicated schema migration can include validation, Preview/Apply, revision persistence, package round-trip, module-missing behavior and frontend editor generation together.

## 5. Interface product development

**ACTIVE PRODUCT BLOCK — FIRST CHECKPOINT MERGED / SECOND WAVE DELIVERED IN OPEN PRS.**

Authoritative task direction: `docs/INTERFACE-DEVELOPMENT.md`.

The first checkpoint through PR #58 established:

- persistent coherent Runtime / Engineering / Audit navigation;
- localized shell identity in `pt-BR` / `en` / `es`;
- authenticated user/session affordance;
- Runtime operational overview using protected facts;
- Engineering Data Source and TAG browser/search surfaces over canonical Engineering;
- existing Preview/Apply/CAS flows preserved.

### Second interface wave

The three worker slices are now delivered but remain **IMPLEMENTED IN PR / NOT MERGED** pending coordinator reconciliation with the post-PR-#68 `main`:

1. **DEV 1 / PR #65 — Engineering Alarm Workspace ergonomics**: scalable searchable/filterable Alarm definition navigation/master-detail over canonical Engineering; exact worker head `071b56b532cf14039d1c0cab9891fc06a27f9873`; CI #388 green.
2. **DEV 2 / PR #67 — Runtime Alarm Center + protected acknowledgement UX**: existing protected active-alarm and ACK APIs, backend-authoritative refresh and authorization/Audit; exact worker head `0c2baae9f3eabe501fa4b1790f4f1607bd04771b`; CI #387 green after retrying an unrelated transient existing Modbus diagnostics failure on the same head.
3. **DEV 3 / PR #66 — Audit workspace ergonomics**: compact filters, scalable results/master-detail and quieter diagnostics while preserving keyset pagination and `SystemAdmin` backend enforcement; exact worker head `fdb622df821fefd14e5af9244ad0df4cd9eb1302`; CI #386 green.
4. **COORDINATOR**: reconcile each worker branch with current `main`, review semantics, integrate Runtime Alarm Center placement, normalize cross-product UX and require exact-head green CI before merge.

Detailed scope is authoritative in `docs/CHAT-WORK-ASSIGNMENTS.md`.

### Guardrails

This UI block does not authorize private frontend Engineering, frontend-only security, direct frontend-to-driver access, fake diagnostics, production Python, the graphical Screen/Popup/Dynamo editor or new production external drivers.

## 6. User interface validation build/package

**DEFERRED / STILL REQUIRED.**

`docs/INTERFACE-VALIDATION-MILESTONE.md` remains valid. Windows x64 packaging/launcher is postponed until the interface has materially matured. The parked `integration/interface-validation-preview` branch remains unmerged.

After the second interface wave, the coordinator must explicitly reassess whether the interface is mature enough to resume this validation package.

## 7. Research consolidation

The architecture research backlog is incorporated into official `main` as **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED**:

- PR #53 — graphical Screen/Popup/Dynamo editor architecture, CI #383 green, merge `491ee337bf2723d13d2759bc677300edd34e1fca`;
- PR #54 — Client Visual Python editor/browser sandbox, CI #384 green, merge `80d06ea467c7c844807c0548940308ccf74a7510`;
- PR #62 — BACnet/IP + BACnet/SC architecture, CI #380 green, merge `c60c611465bd82a898ee30d5f67fe79234381b8c`;
- PR #63 — MQTT industrial Data Source architecture, CI #381 green, merge `05df6bc63893cb025f87899d27a5988b2e1cf896`;
- PR #64 — Allen-Bradley EtherNet/IP/CIP Logix architecture, CI #382 green, merge `a71ce2d962d6b122714b61b5851465d9c284e7b6`;
- merged OPC UA and Siemens S7 research remain additional architecture inputs;
- PR #68 converts the common cross-research conclusions into official platform contracts without implementing the protocols themselves.

Research merge means the evidence is official. It does **not** select final production dependencies, register Data Sources, change runtime composition or bypass any prerequisite/product gate.

## 8. External protocol/module wave

**PRODUCTION POSTPONED UNTIL AFTER INTERFACE DEVELOPMENT + VALIDATION FEEDBACK.**

Planned order remains broadly:

1. MQTT through the common Data Source/Source Provider/TAG/Gateway architecture;
2. OPC UA through the same model;
3. BACnet through the same model;
4. installable/versioned Driver Module framework and public Driver SDK compatibility boundary;
5. Siemens S7 ISO Connection as an intended installable-module target;
6. later Allen-Bradley production work based on the merged research, licensing/security review and real-hardware acceptance.

When this wave opens, every protocol task must read ADR-009, the Driver SDK convergence/readiness documents and its protocol-specific research together. New protocol work must extend the common descriptor/Engineering-capability/TAG/diagnostics/Gateway model rather than create a protocol-private architecture.

No production work in this wave is authorized by the current assignment board.

## Historian and trends

Storage foundations exist. Still required:

- canonical Engineering retention/downsampling policy;
- raw vs aggregate query/resolution selection;
- engineered, ad-hoc and saved multi-Pen trends;
- historical + live trend integration;
- expressions where appropriate.

Interface work may expose honest entry points using current APIs, but must not invent historian semantics.

## Audit evolution

Current merged Audit includes append-only PostgreSQL storage, bounded query/filtering, runtime integration, retention and `/audit` UI.

The active DEV 3 delivery is interface ergonomics only until its PR is coordinator-merged. Future backend items remain:

- persistent crash-surviving outbox while events are buffered;
- manual purge-all endpoint;
- weaker/general AuditRead capability separate from `SystemAdmin`.

## Python scripting and visual-runtime prerequisite chain

The locked order remains:

`canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical Screen/Popup/Dynamo editor -> advanced visual libraries`

### Canonical Script integration

**PARTIALLY MERGED — ISOLATED DOMAIN COMPLETE / CANONICAL PACKAGE INTEGRATION PENDING.**

Still required before production Python editor/runtime work:

- first-class Scripts collection/entity kind in canonical Engineering;
- stable visual Script references;
- schema migration and JSON round-trip;
- Preview/Apply integration;
- Revision/PostgreSQL persistence and `.escadapkg` backup/restore;
- authoritative TAG/Client Memory/Server Memory/resource-reference catalogs.

### Python editor/sandbox

Research is **MERGED through PR #54 / PRODUCTION NOT IMPLEMENTED**. Pyodide/Monaco remain research recommendations only. Production work remains blocked by canonical Script integration and the active product schedule.

### Visual runtime object instances/property API

**SPECIFIED / NOT IMPLEMENTED AS PRODUCT INTEGRATION.**

Runtime precedence remains:

`Animation > Script > BindingOrExpression > EngineeringBase`

### Graphical Screen/Popup/Dynamo editor

Research is **MERGED through PR #53 / PRODUCTION NOT IMPLEMENTED**. Production graphical-editor work remains behind the locked prerequisite chain.

## Additional future Engineering/product slices

Still planned according to dependencies and ownership safety:

- Engineering XLSX import/export;
- Engineering Fragments and dependency-aware cross-project copy/paste;
- configurable application shell regions;
- visual asset/resource package management;
- reusable Equipment/Template/Dynamo class-instance libraries;
- runtime-HMI multilingual content separate from Engineering UI localization;
- public SDK/module lifecycle, trust/integrity and diagnostics;
- later sandboxed Server Python.

## Development quality rules

- Prefer small coherent slices with automated validation.
- Never merge a known-failing change into `main`.
- Use GitHub CI as the external .NET validation environment when local .NET execution is unavailable.
- Validate affected backend behavior, Web build and Chromium E2E.
- Fix root causes rather than weakening tests/security/concurrency.
- Preserve Engineering schema compatibility and lifecycle boundaries.
- Keep runtime safety ahead of UI convenience.
- Interface changes must not bypass Engineering, TAG quality, security, Audit or source-provider architecture.
- Worker branches never self-merge and coordinator-owned central files remain centrally controlled unless a narrow exception is explicitly assigned.
