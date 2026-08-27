# EliteSCADA Roadmap

**Status date:** 2026-08-27  
**Active direction:** **INTERFACE-WAVE-03 / FULL PRODUCT PATH TO v0.1 OWNER VALIDATION**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.

Engineering Import/Export remains cross-cutting: every new canonical Engineering domain joins versioned JSON, validation/Preview/Apply, revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

The platform already includes, in `main`, substantial foundations across TAG Engine/current cache/quality/Event Bus, Simulation and real Modbus TCP, PostgreSQL Engineering persistence, TimescaleDB historian foundations, Working/Revision/Published/Active lifecycle, canonical Import/Export + Preview/Apply/CAS, authentication/users/authorization/Audit, Client/Server Memory, TAG Gateway, common Data Source diagnostics, Runtime/Engineering/Audit shell, Engineering Data Source/TAG/Alarm workspaces, Runtime Operations/Alarm Center, Audit workspace, Python/visual public foundations, isolated Script Engineering and merged Driver SDK/research convergence.

Canonical Engineering remains **Schema v9** until a deliberate migration wave changes it with compatibility tests.

## Wave 00 — interface closeout

**COMPLETE / MERGED.**

PRs #65, #66 and #67 are merged. Coordinator PR #69 integrated Runtime Alarm Center into actual Runtime composition, removed the legacy duplicate alarm path and preserved backend-authoritative ACK identity.

PR #69: final head `c493709a221614a093717b6e6a16bf8821226e91`; exact-head CI #403 green; merge `ee65ab51a39cd74ef6f14395d27b0ee16b8c6970`.

The PostgreSQL concurrent schema-initialization defect discovered during that integration is also **FIXED / MERGED** through PR #70. Engineering, Audit and Local Identity initialization now hold the shared transaction-scoped advisory lock through the complete DDL batch; Server Memory uses the same shared schema lock. A concurrent multi-store regression was added. PR #70 exact-head CI #405 passed Web, backend/tests/runtime smoke and Chromium; merge `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`.

## Wave 03 — complete operational lifecycle

**ACTIVE.**

**WaveBaseSHA:** `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`  
**Integration branch:** `integration/interface-wave-03`

Goal: prove `edit -> save revision -> publish -> activate -> operate` as a coherent user workflow while adding honest Runtime TAG inspection and cross-product acceptance evidence.

Active parallel slices:

- DEV 1: Engineering Lifecycle Workspace on `feature/interface-wave-03-lifecycle-workspace`;
- DEV 2: read-only Runtime TAG Inspector + Recent History on `feature/interface-wave-03-runtime-tag-inspector`;
- DEV 3: Interface Validation Readiness Harness on `test/interface-wave-03-acceptance-harness`;
- Coordinator: integration branch, central `EngineeringApp.tsx`/Runtime composition hooks, review, final CI and wave gate.

The existing backend already supplies protected lifecycle status/revisions/save/checkout/publish/activate/runtime-consistency endpoints and protected TAG/current/by-path/history/WebSocket surfaces. Wave 03 UI workers consume those contracts rather than inventing parallel authority.

Wave 03 Definition of Done requires accepted worker slices integrated, no duplicate product paths, lifecycle gate operational, TAG Inspector read-only and useful, acceptance findings classified, and final integrated Web/backend/tests/runtime-smoke/Chromium CI green.

Documentation-only coordination commits after the WaveBaseSHA do not invalidate worker branches. Unrelated product-code changes should not enter `main` until this wave closes.

## Ordered path to v0.1

```text
Wave 03  Operational lifecycle + Runtime TAG Inspector + acceptance foundation
Wave 04  Project portability + basic Trends + Administration
Wave 05  Canonical Script Engineering
Wave 06  Python Editor + Client Visual sandbox
Wave 07  Visual Runtime Object Model + visual Asset contract
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

## Wave 04 — portability + historian + administration

Planned after Wave 03 gate: Project Management surface for canonical JSON Import/Export, Preview/Apply and `.escadapkg` backup/restore; basic live + historical Trend Viewer; Administration workspace ergonomics.

Gate: practically usable non-graphical SCADA/platform workflow before canonical scripting/visual product integration.

## Wave 05 — canonical Script Engineering

**ARCHITECTURE-FIRST WAVE.** Coordinator first stabilizes shared canonical Script contracts in the wave integration branch: Engineering schema/model, Scripts collection, stable references, import/export/migration, Preview/Apply, revision/PostgreSQL and `.escadapkg`. Workers then build isolated Script workspace/reference/compatibility slices.

Gate: Script source/scope/events/dependencies/references/enabled state survive canonical round-trip, revision and package flows.

## Wave 06 — Python Editor + Client Visual sandbox

Production Client Visual Python begins only after canonical Script integration. Required result includes practical code editor, safe browser sandbox, narrow versioned EliteSCADA API, cancellation/time budgets/bounded queues/isolation and meaningful diagnostics. No direct driver/database/filesystem/shell/arbitrary-network/credential access.

## Wave 07 — Visual Runtime Object Model

**ARCHITECTURE-FIRST WAVE.** Coordinator stabilizes canonical visual identity/runtime-instance/property-resolution and Asset/Resource reference rules before parallel implementation.

Locked property precedence: `Animation > Script > Binding/Expression > Engineering Base`.

Visual Assets are project-authoritative stable resources, not arbitrary filesystem paths. JPG/JPEG, BMP and PNG import is required for v0.1; PNG alpha transparency must be preserved. Full asset rules: `docs/VISUAL-ASSETS-AND-IMAGES.md`.

Gate: canonical visual definitions can render, bind, be changed by Python/animation, use stable project assets and dispose deterministically without mutating saved Engineering.

## Wave 08 — Graphical Editor Foundation

Canvas/selection + shared Property Inspector + initial Object Palette/bindings + project image import/Image object. The editor consumes canonical Engineering and the common Visual Property/Asset contracts. Renderer/canvas state never becomes project truth.

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
- never merge known-failing work;
- fix root causes rather than weaken tests/security/concurrency;
- preserve canonical Engineering and backend authority;
- require final integrated CI for every wave;
- do not move product-code `main` with unrelated work during an active wave unless an allowed exception applies;
- keep the assignment board synchronized because `siga` depends on it.