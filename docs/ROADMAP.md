# EliteSCADA Roadmap

Engineering Import/Export remains a mandatory cross-cutting capability throughout this roadmap. Every new Engineering domain must join the public versioned model, validation/preview/apply workflow, revision lifecycle and backup/restore path.

**Status date:** 2026-08-27  
**Functional development:** **ACTIVE — INTERFACE PRODUCT DEVELOPMENT**

## Established `main` foundation

The current merged platform includes:

1. repository architecture and CI/CD foundation;
2. TAG Engine, quality model, current-value cache and Event Bus;
3. Simulation driver and common Driver SDK/DriverHost boundary;
4. REST API + WebSocket realtime runtime;
5. React runtime client baseline;
6. PostgreSQL Engineering persistence;
7. Working / immutable Revision / Published / Active lifecycle;
8. transactional activation and fail-closed persisted-runtime recovery;
9. TimescaleDB historian baseline plus retention/downsampling foundation;
10. real Modbus TCP runtime with grouped polling, writes, reconnect and communication quality;
11. Engineering Data Source compilation into runtime plans;
12. isolated Engineering Workspace and CAS-protected mutation flows;
13. capability-based authorization, trusted JWT/local identity and durable Audit foundations;
14. protected runtime read, historian, alarm, Engineering, diagnostics and realtime surfaces;
15. Engineering UI at `/engineering` plus Runtime/Engineering/Audit navigation and localization baseline;
16. structured TAG, Data Source and Alarm editors;
17. local user administration;
18. complete Client/Server Internal Memory product integration;
19. Python Scripting + Visual Property public contract foundation;
20. isolated Public Script Engineering domain;
21. Engineering Schema v9 with first-class TAG Gateway routes;
22. complete protocol-independent TAG Gateway runtime/product integration;
23. complete protocol-neutral multi-driver/Data Source diagnostics with Modbus instrumentation and multi-instance acceptance;
24. elaborated communication diagnostics UX in Engineering;
25. first integrated Interface Product Development checkpoint with persistent localized product shell, authenticated user/session affordance, Runtime operational overview, and Engineering Data Source/TAG entity-browser navigation.

Important recent merged checkpoints:

- PR #35 Commands: `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- PR #37 Engineering UI foundation: `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- PR #38 Local identity/browser login: `2a581d279a428cb605429d5939c333ff7ad8d1b4`;
- PR #40 Internal Memory foundation: `bb38617c9c27cb5c379973a6f65d66006f24eadc`;
- PR #41 Python/Visual foundation: `fc0731309d5b92d302f019d06d3511d3a247b607`;
- PR #42 secured Engineering mutations: `6d49b99181fce6dabce838822ce972332e2f77f0`;
- PR #43 Historian retention/downsampling: `0c5f2aefdd5a7286c0c9367569067e2d12091c81`;
- PRs #44-#46 Audit durability/runtime/UI;
- PR #47 isolated Script Engineering foundation: `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`;
- PRs #48-#49 complete Internal Memory product integration;
- PR #50 TAG Gateway Engineering / Schema v9: `7a039c0eda8802a8ed2851fe9223fd831859fc61`;
- PR #55 complete TAG Gateway runtime/product integration: `41bc437ba64f60fba26754794a9dc5a4e9a034f7`;
- PRs #56-#57 complete communication diagnostics product;
- PR #57 merge SHA: `c8190cc119a2e288834d619084396107103b2f56`;
- PR #58 first integrated interface product checkpoint: `f3cc82f0d45a9f0162105b57ae6c42f643af6160`, exact-head CI #378 fully green;
- worker interface slices PR #59 / #60 / #61 were integrated through PR #58.

Canonical Engineering JSON on official `main` remains **Schema v9**.

## Current development order

The product owner reprioritized work on 2026-08-27 to gain more value from the interface before additional drivers or a provisional presentation package.

The active order is now:

`merged platform foundations -> INTERFACE PRODUCT DEVELOPMENT -> USER INTERFACE VALIDATION BUILD/PACKAGE -> additional external protocols`

This is a scheduling refinement, not an architectural rollback. The stable source/provider/security/Engineering boundaries remain unchanged.

## 1. Internal Memory TAG sources

**COMPLETE / MERGED.**

Client Memory remains per-runtime-client/local and non-global. Server Memory remains shared/server-owned and retentive by stable TAG ID. Internal sources do not fabricate network diagnostics.

See `docs/INTERNAL-MEMORY-TAGS.md`.

## 2. Protocol-independent TAG Gateway

**COMPLETE / MERGED.**

Canonical Schema v9 Gateway routes, Preview/Apply/revision/package persistence, cycle/multiple-writer validation, fan-out, Server Memory support, OnChange/Periodic execution, quality policy, deadband/rate/coalescing/startup synchronization, checked conversion/scaling, transactional active-runtime replacement, diagnostics and Engineering UI are merged.

See `docs/TAG-GATEWAY.md`.

## 3. Common multi-driver/Data Source diagnostics

**COMPLETE / MERGED.**

Current product supports protocol-neutral per-Data-Source communication diagnostics, Modbus instrumentation, independent multi-instance failure/recovery/counters/quality behavior, protected backend snapshots and Engineering diagnostics UX.

See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

## 4. Interface product development

**ACTIVE PRODUCT BLOCK — FIRST INTEGRATED CHECKPOINT MERGED.**

Authoritative task direction: `docs/INTERFACE-DEVELOPMENT.md`.

The first acceptance checkpoint is now merged through PR #58. It establishes the common product shell plus central integration of the first worker primitives. Interface development remains active; this merge is not the end of the block and does not by itself reopen the external-protocol or Windows-package gates.

Merged first-checkpoint behavior includes:

- persistent coherent Runtime / Engineering / Audit navigation;
- localized shell identity in `pt-BR` / `en` / `es`;
- authenticated user/session affordance;
- Runtime operational overview using existing protected facts while preserving the process demo;
- Engineering Data Source and TAG browser/search surfaces over the canonical model;
- existing Preview/Apply/CAS protected mutation flows preserved;
- full exact-head Web/backend/smoke/Chromium acceptance on PR #58.

Primary continuing implementation areas:

### Product shell

- continue consistent route/page identity and cross-product context;
- consistent loading/error/empty/status patterns;
- desktop-first responsive application structure;
- shared restrained industrial visual language.

### Engineering workspace

- continue improved information architecture;
- extend scalable entity-browser/master-detail patterns where useful;
- compact large-project tables;
- clear Working/Revision/Published/Active and dirty-state context;
- consistent structured editors and actionable validation feedback;
- keyboard/focus/accessibility improvements;
- localized touched surfaces.

### Runtime operations

- preserve the process demo;
- continue useful platform-level operational overview;
- communication/Data Source health;
- alarms and TAG-quality visibility;
- Gateway/diagnostic context where useful;
- clear online/offline/degraded semantics;
- future trend entry points based on existing historian capabilities.

### Session/admin/Audit consistency

- maintain visible authenticated identity and roles;
- understandable logout/session behavior;
- administration and Audit aligned with the same product shell.

### Guardrails

This active UI block does not authorize:

- a private frontend Engineering model;
- frontend-only security;
- direct frontend-to-driver access;
- fake diagnostics;
- the full graphical Screen/Popup/Dynamo editor ahead of Script/visual prerequisites;
- new production external drivers.

## 5. User interface validation build/package

**DEFERRED / STILL REQUIRED.**

The milestone in `docs/INTERFACE-VALIDATION-MILESTONE.md` remains valid, but its Windows x64 packaging/launcher deliverable is postponed until the interface has materially matured.

The parked branch `integration/interface-validation-preview` preserves two preparatory unmerged commits but has no PR and is not an active integration candidate.

When resumed, the validation deliverable still requires:

- practical Windows x64 startup;
- built React + backend/runtime entry path;
- reliable PostgreSQL/TimescaleDB service startup/check;
- controlled local login/bootstrap;
- canonical demo/readiness path;
- visible exact build identity;
- validation checklist;
- package/startup smoke separate from repository-only execution.

The package should contain the improved product resulting from the active interface block, not freeze the older demo-oriented UI merely to satisfy a checkpoint.

## 6. External protocol/module wave

**PRODUCTION POSTPONED UNTIL AFTER INTERFACE DEVELOPMENT + VALIDATION FEEDBACK. RESEARCH MAY PROCEED.**

Planned order remains broadly:

1. MQTT through the common Data Source/Source Provider/TAG/Gateway architecture;
2. OPC UA through the same model;
3. BACnet through the same model;
4. installable/versioned Driver Module framework and public Driver SDK compatibility boundary;
5. Siemens S7 ISO Connection as an intended installable-module target;
6. later Allen-Bradley based on public documentation, licensing and testability.

OPC UA and Siemens S7 research already exist as architecture inputs. MQTT, BACnet/IP + BACnet/SC, and Allen-Bradley EtherNet/IP/CIP research were assigned in parallel on 2026-08-27. Research documents, branches and research PRs do not authorize production runtime, Data Source registration, dependency selection or bypass of the product gate.

## Historian and trends

Storage foundations exist. Still required:

- canonical Engineering representation for retention/downsampling policy;
- raw vs aggregate query/resolution selection;
- engineered, ad-hoc and saved multi-Pen trends;
- historical + live trend integration;
- expressions where appropriate.

The active interface block may create useful trend entry points only where current APIs support them honestly; it must not invent unimplemented historian semantics.

## Audit evolution

Current merged Audit includes append-only PostgreSQL storage, bounded query/filtering, runtime integration, retention and `/audit` UI.

Still future:

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

PR #54 remains **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**.

### Visual runtime object instances/property API

**SPECIFIED / NOT IMPLEMENTED AS PRODUCT INTEGRATION.**

Runtime precedence remains:

`Animation > Script > BindingOrExpression > EngineeringBase`

### Graphical Screen/Popup/Dynamo editor

PR #53 remains **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**.

Production graphical-editor work must not begin merely because general interface development is active.

## Additional future Engineering/product slices

Still planned according to dependencies and ownership safety:

- Engineering XLSX import/export;
- Engineering Fragments and dependency-aware cross-project copy/paste;
- configurable application shell regions;
- visual asset/resource package management;
- reusable Equipment/Template/Dynamo class-instance libraries;
- runtime-HMI multilingual content separate from Engineering UI localization;
- public SDK/module lifecycle, trust/integrity and diagnostics;
- later sandboxed Server Python for shared calculations/automation.

## Development quality rules

- Prefer small coherent slices with automated validation.
- Never merge a known-failing change into `main`.
- Use GitHub CI as the external .NET validation environment when local .NET execution is unavailable.
- Validate affected backend behavior, Web build and Chromium E2E.
- Fix root causes rather than weakening tests/security/concurrency.
- Preserve Engineering schema compatibility and current lifecycle boundaries.
- Keep runtime safety ahead of UI convenience.
- Interface changes must not bypass Engineering, TAG quality, security, Audit or source-provider architecture.
- Worker branches never self-merge and coordinator-owned central files remain centrally controlled unless a narrow exception is explicitly assigned.
