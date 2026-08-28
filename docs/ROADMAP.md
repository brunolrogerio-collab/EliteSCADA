# EliteSCADA Roadmap

**Status date:** 2026-08-28  
**Active direction:** **PYTHON-WAVE-06 DEFINITION OF READY / FULL PRODUCT PATH TO v0.1 OWNER VALIDATION**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
CI usage mode: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: every canonical Engineering domain joins versioned JSON, validation/Preview/Apply, revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

The merged product now includes TAG Engine/current cache/quality/Event Bus, Simulation and real Modbus TCP, PostgreSQL Engineering persistence, TimescaleDB historian foundations, Working/Revision/Published/Active lifecycle, canonical Import/Export + Preview/Apply/CAS, authentication/users/authorization/Audit, Client/Server Memory, TAG Gateway, common Data Source diagnostics, Runtime/Engineering/Audit shell, Engineering Data Source/TAG/Alarm workspaces, Runtime Operations/Alarm Center, Runtime TAG Inspector + Recent History, Lifecycle, Project Management/Portability, Basic Trend Viewer, Administration, Driver SDK/research convergence and **canonical Script Engineering schema v10 with a practical Script Engineering Workspace**.

Canonical Engineering in merged `main` is now **Schema v10**.

## Wave 03 — complete operational lifecycle

**COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; final CI #418 fully green.

## Wave 04 — portability + historian + administration

**COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; final CI #446 fully green.

## Wave 05 — canonical Script Engineering

**COMPLETE / MERGED.**

- Logical WaveBaseSHA: `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`
- Central ContractSHA: `b08b45201bf25a6d4d403b07c511cc34444177db`
- Final integration head: `13d3f8283275dc957d9d6168fc7fb165df992d7e`
- Final CI #466 / run `33139334379`: Web, backend Release/full tests including PostgreSQL, Runtime smoke and Chromium all SUCCESS
- Coordinator PR #79: MERGED
- Main merge: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`

Merged outcome:

- `scada.engineering` v10 with first-class `Scripts` and visual-event references;
- stable Script identity/path ownership and Workspace dirty/changeVersion semantics;
- canonical JSON/v9 compatibility/Preview/Apply/revisions/PostgreSQL/`.escadapkg` fidelity;
- dependency-safe CAS/security/Audit delete;
- deterministic stable-reference resolver for TAG, Client Memory, Server Memory and visual definitions;
- practical Engineering Script workspace with source/metadata/entry-point/dependency editing over canonical Preview/Apply/CAS;
- no Python execution yet, by design.

## Ordered path to v0.1

```text
Wave 03  Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04  Project portability + basic Trends + Administration                        COMPLETE
Wave 05  Canonical Script Engineering                                                COMPLETE
Wave 06  Python Editor + Client Visual sandbox                                       PREPARING / Definition of Ready
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

**PREPARING — COORDINATOR DEFINITION OF READY. Workers are not active yet.**

Base candidate: Wave 05 merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`.

Production Client Visual Python begins only after the coordinator pins:

- browser sandbox implementation/isolation boundary;
- narrow versioned EliteSCADA API exposed to Python;
- source validation and line/column diagnostics;
- cancellation/time budget/bounded queues/isolation and deterministic disposal;
- TAG read/write permissions and Client Memory authority;
- event/timer lifecycle;
- editor versus executor responsibility;
- final acceptance gate and parallel dependency map.

Locked security boundary: Python never receives direct drivers, PostgreSQL/database handles, filesystem, shell/process execution, arbitrary network access or credentials. Client Visual Python acts only through public versioned APIs/capabilities.

Likely worker domains remain, pending explicit board promotion:

- DEV 1: practical Python Editor UX over canonical Scripts;
- DEV 2: browser Client Visual sandbox/runtime adapter and narrow API;
- DEV 3: sandbox execution safety/acceptance/failure isolation.

## Wave 07 — Visual Runtime Object Model

**ARCHITECTURE-FIRST WAVE.** Stabilize canonical visual identity/runtime-instance/property-resolution and Asset/Resource rules. Locked property precedence: `Animation > Script > Binding/Expression > Engineering Base`.

Visual Assets are project-authoritative stable resources, never arbitrary filesystem paths. JPG/JPEG, BMP and PNG with alpha are required for v0.1. Full rules: `docs/VISUAL-ASSETS-AND-IMAGES.md`.

## Wave 08 — Graphical Editor Foundation

Canvas/selection + shared Property Inspector + initial Object Palette/bindings + project image import/Image object.

## Wave 09 — Screens + Popups + Dynamos

Multiple Screen navigation, Popup behavior, reusable Dynamo definitions/instances and deterministic asset dependencies.

## Wave 10 — Python visual events + animation

Event association, renderer-native animation/tween and Engineering Python Preview/Test.

## Wave 11 — complete HMI vertical slice

Build `Estação Elevatória EliteSCADA Demo` only through normal product APIs/tools, proving graphical Runtime, visual resources, bindings, Python, alarms/ACK, historian, diagnostics, Gateway, memory and optional laboratory Modbus as one application.

## Wave 12 — hardening

Stress lifecycle/portability, visual/assets, Python failure/isolation, Runtime communication/reconnect/quality/alarm/historian/Gateway, persistence/restart/Active recovery and browser/session/localization.

## Wave 13 — Windows x64 package

First owner-facing product package only after graphical Engineering + Client Visual Python + complete HMI Runtime path exist.

## Wave 14 — owner validation

Core acceptance:

`create project -> source/TAG/alarm -> Screen/object/image/binding -> Python behavior -> save -> publish -> activate -> Runtime -> restart -> recover Active application`.

## Wave 15 — feedback/corrections

v0.1 requires P0=0, P1=0, required CI/package smoke green and restart/Active recovery green.

## v0.1 protocol boundary

Required real industrial protocol: **Modbus TCP**. Simulation, Client Memory, Server Memory and Gateway are also part of product validation.

Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module framework remain **POST-v0.1 OWNER VALIDATION**.

Preferred post-v0.1 progression: `MQTT -> OPC UA -> BACnet -> Driver Module framework -> Siemens S7 -> Allen-Bradley`.

## Python/visual dependency chain remains locked

`canonical Script Engineering -> Python editor/sandbox -> visual Runtime object/property/asset model -> graphical editor -> advanced visual libraries`.

## Development quality

- use Development Waves with frozen logical bases and coordinator integration trains;
- workers start only explicit ACTIVE assignments;
- never merge known-failing work;
- fix root causes rather than weaken tests/security/concurrency;
- preserve canonical Engineering and backend authority;
- require final integrated CI for every functional wave;
- while `docs/CI-USAGE-POLICY.md` is `CONSTRAINED`, economize CI frequency, never final quality;
- keep assignment board/handoff synchronized because `siga` depends on them.