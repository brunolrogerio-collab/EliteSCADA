# EliteSCADA Roadmap

**Status date:** 2026-08-27  
**Active direction:** **SCRIPT-WAVE-05 / FULL PRODUCT PATH TO v0.1 OWNER VALIDATION**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.

Engineering Import/Export remains cross-cutting: every new canonical Engineering domain joins versioned JSON, validation/Preview/Apply, revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

The platform already includes, in `main`, substantial foundations across TAG Engine/current cache/quality/Event Bus, Simulation and real Modbus TCP, PostgreSQL Engineering persistence, TimescaleDB historian foundations, Working/Revision/Published/Active lifecycle, canonical Import/Export + Preview/Apply/CAS, authentication/users/authorization/Audit, Client/Server Memory, TAG Gateway, common Data Source diagnostics, Runtime/Engineering/Audit shell, Engineering Data Source/TAG/Alarm workspaces, Runtime Operations/Alarm Center, Runtime TAG Inspector + Recent History, Engineering Lifecycle Workspace, Project Management/Portability, Basic Trend Viewer, Administration workspace, cross-product Chromium acceptance foundations, Python/visual public foundations, isolated Script Engineering and merged Driver SDK/research convergence.

Canonical Engineering in merged `main` remains **Schema v9**. Wave 05 schema v10 is **IMPLEMENTED IN PR #79 / NOT YET MERGED** and has a frozen green central ContractSHA.

## Wave 00 — interface closeout

**COMPLETE / MERGED.**

PRs #65, #66 and #67 are merged. Coordinator PR #69 integrated Runtime Alarm Center into actual Runtime composition, removed the legacy duplicate alarm path and preserved backend-authoritative ACK identity. PR #69 exact-head CI #403 green; merge `ee65ab51a39cd74ef6f14395d27b0ee16b8c6970`.

PostgreSQL concurrent schema initialization is also **FIXED / MERGED** through PR #70, exact-head CI #405 green; merge `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`.

## Wave 03 — complete operational lifecycle

**COMPLETE / MERGED.**

Worker PRs #71, #72 and #73 are merged into coordinator PR #74.

Final Wave 03 evidence:

- frozen WaveBaseSHA `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`;
- integration head `41d44d513d337e8ef6d3cc0e04ef0cf07a697b41`;
- CI #418: Web, backend/full tests, Runtime smoke and Chromium all SUCCESS;
- merge commit `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`.

Merged product outcome includes Engineering Lifecycle Workspace, Runtime TAG Inspector + Recent History, cross-product acceptance, central product composition and durable Preview/Apply -> Save -> Publish -> Activate -> Runtime consistency.

## Wave 04 — portability + historian + administration

**COMPLETE / MERGED.**

**WaveBaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**Final integration head:** `f0762d12814496a223abe740c57eb995ca472e97`  
**Final CI:** #446 fully green: Web, backend/full tests, Runtime smoke and Chromium  
**Main merge:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`

Merged product outcome:

- Project Management / Portability Workspace using canonical JSON Import/Export, Preview/Apply/CAS, `.escadapkg` and backup/restore paths;
- Basic Trend Viewer over existing historian contracts;
- Administration Workspace over backend-authoritative identity/authorization/session contracts;
- integrated composition with no duplicate product surfaces.

This merge is the logical WaveBaseSHA for Wave 05.

## Wave 05 — canonical Script Engineering

**ACTIVE — PARALLEL WORKER PHASE.**

**Logical WaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**Integration branch:** `integration/interface-wave-05`  
**Integration PR:** #79 `Canonicalize Script Engineering for Wave 05` — Draft integration train  
**Central ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**Contract CI:** #458 fully green: Web, backend Release/full tests including PostgreSQL, Runtime smoke and Chromium.

Central architecture-first contract is **IMPLEMENTED IN PR #79**:

- canonical `scada.engineering` v10 with first-class `Scripts` and `ScriptVisualEventReferences`;
- stable Script registry/path ownership under the Engineering Workspace;
- normal dirty/changeVersion behavior;
- canonical JSON Export/Import and Preview/Apply;
- deterministic reuse of existing Script Engineering validation and Python preflight;
- TAG, Client Memory, Server Memory and visual-definition reference boundaries;
- protected Script read endpoints;
- dependency-safe Script delete through normal CAS/security/Audit authority;
- v9 backward compatibility;
- `.escadapkg` fidelity;
- PostgreSQL immutable revision fidelity.

Parallel worker slices now execute from the frozen ContractSHA:

- DEV 1: Script Engineering Workspace foundation on `feature/script-wave-05-engineering-workspace`;
- DEV 2: Script Reference Runtime / validation adapter on `feature/script-wave-05-reference-validation`;
- DEV 3: Script compatibility validation on `test/script-wave-05-compatibility`;
- Coordinator: event-based reviews, integration into PR #79, central `EngineeringApp.tsx` placement/composition and final Wave 05 validation.

Wave 05 gate: Script source, scope, enabled state, events, dependencies and stable references survive canonical round-trip, revision and package flows, and a practical Engineering workspace edits them without bypassing Preview/Apply/CAS.

## Ordered path to v0.1

```text
Wave 03  Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04  Project portability + basic Trends + Administration                        COMPLETE
Wave 05  Canonical Script Engineering                                                ACTIVE
Wave 06  Python Editor + Client Visual sandbox                                       QUEUED
Wave 07  Visual Runtime Object Model + visual Asset contract                         architecture-first
Wave 08  Graphical Editor Foundation + image import/object
Wave 09  Screens + Popups + Dynamos + asset dependencies
Wave 10  Python visual events + animation + preview
Wave 11  Complete HMI Runtime demo vertical slice
Wave 12  Hardening
Wave 13  Windows x64 product package
Wave 14  Product-owner validation
Wave 15  Feedback/corrections
FINAL    EliteSCADA v0.1 — Full Product Validation Preview
```

Do not skip dependencies simply because later research already exists.

## Wave 06 — Python Editor + Client Visual sandbox

Production Client Visual Python begins only after canonical Script integration. Required result includes practical code editor, safe browser sandbox, narrow versioned EliteSCADA API, cancellation/time budgets/bounded queues/isolation and meaningful diagnostics. No direct driver/database/filesystem/shell/arbitrary-network/credential access.

## Wave 07 — Visual Runtime Object Model

**ARCHITECTURE-FIRST WAVE.** Coordinator stabilizes canonical visual identity/runtime-instance/property-resolution and Asset/Resource reference rules before parallel implementation.

Locked property precedence: `Animation > Script > Binding/Expression > Engineering Base`.

Visual Assets are project-authoritative stable resources, not arbitrary filesystem paths. JPG/JPEG, BMP and PNG import is required for v0.1; PNG alpha transparency must be preserved. Full asset rules: `docs/VISUAL-ASSETS-AND-IMAGES.md`.

Gate: canonical visual definitions can render, bind, be changed by Python/animation, use stable project assets and dispose deterministically without mutating saved Engineering.

## Wave 08 — Graphical Editor Foundation

Canvas/selection + shared Property Inspector + initial Object Palette/bindings + project image import/Image object. The editor consumes canonical Engineering and common Visual Property/Asset contracts. Renderer/canvas state never becomes project truth.

Gate: create Screen, place/manipulate objects including imported images, edit properties, save/reopen and export/import while preserving asset references/transparency.

## Wave 09 — Screens + Popups + Dynamos

Multiple Screen navigation, Popup runtime/definition behavior, first reusable Dynamo definition/instance/public-parameter model and deterministic asset dependencies.

Gate: Screen + objects/assets + Dynamo + button -> Popup works through normal Engineering publication/activation and Runtime.

## Wave 10 — Python visual events + animation

Event association, renderer-native animation/tween and Engineering Python Preview/Test.

Gate: an engineered button can execute controlled Python that reads a TAG, changes Client Memory, changes another object's Runtime presentation, opens a Popup and requests animation.

## Wave 11 — complete HMI vertical slice

Build `Estação Elevatória EliteSCADA Demo` only through normal product APIs/tools. It proves graphical Runtime, imported visual resources, bindings, Python, alarms/ACK, historian, diagnostics, Gateway, memory and optional laboratory Modbus connection as one SCADA application. Hidden DB/private JSON/manual developer intervention is a product gap, not acceptance.

## Wave 12 — hardening

No major feature family. Stress Engineering lifecycle/portability, visual model/assets, Python failure/isolation, Runtime communication/reconnect/quality/alarm/historian/Gateway, persistence/restart/Active recovery and browser/session/localization behavior.

## Wave 13 — Windows x64 package

Create the first owner-facing product package only after graphical Engineering + Client Visual Python + complete HMI Runtime path exist. Assets must package with the project so no source workstation file paths are required. Internal packaging spikes may happen earlier but are development evidence only.

## Wave 14 — owner validation

Owner tests Demo, project creation from scratch, real Modbus, Dynamo reuse, Popup + Python + Client Memory, imported images/assets and restart/Active recovery.

Core acceptance:

`create project -> source/TAG/alarm -> Screen/object/image/binding -> Python behavior -> save -> publish -> activate -> Runtime -> restart -> recover Active application`.

## Wave 15 — feedback/corrections

Classify P0 BLOCKER, P1 MAJOR, P2 WORKFLOW, P3 UX, P4 COSMETIC. v0.1 requires P0=0, P1=0, required CI/package smoke green and restart/Active recovery green.

## v0.1 protocol boundary

Required real industrial protocol: **Modbus TCP**. Also available/required for product validation: Simulation, Client Memory, Server Memory and Gateway.

Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module framework remain **POST-v0.1 OWNER VALIDATION**. Research readiness does not equal an open production gate.

Preferred post-v0.1 progression: `MQTT reference implementation -> OPC UA -> BACnet -> Driver Module framework -> Siemens S7 -> Allen-Bradley`.

## Python/visual dependency chain remains locked

`canonical Script Engineering -> Python editor/sandbox -> visual Runtime object/property/asset model -> graphical editor -> advanced visual libraries`.

Detailed architecture remains in `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/VISUAL-ASSETS-AND-IMAGES.md` and merged research documents.

## Development quality

- use Development Waves with a frozen logical base and coordinator integration train;
- workers that depend on an architecture-first central contract start from its frozen green ContractSHA;
- never merge known-failing work;
- fix root causes rather than weaken tests/security/concurrency;
- preserve canonical Engineering and backend authority;
- require final integrated CI for every wave;
- do not move product-code `main` with unrelated work during an active wave unless an allowed exception applies;
- keep the assignment board synchronized because `siga` depends on it.
