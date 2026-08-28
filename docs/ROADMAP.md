# EliteSCADA Roadmap

**Status date:** 2026-08-28  
**Active direction:** **PYTHON-WAVE-06 ACTIVE / FULL PRODUCT PATH TO v0.1 OWNER VALIDATION**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Wave 06 implementation boundary: `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`.  
CI usage mode: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: every canonical Engineering domain joins versioned JSON, validation/Preview/Apply, revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

The merged product includes TAG Engine/current cache/quality/Event Bus, Simulation and real Modbus TCP, PostgreSQL Engineering persistence, TimescaleDB historian foundations, Working/Revision/Published/Active lifecycle, canonical Import/Export + Preview/Apply/CAS, authentication/users/authorization/Audit, Client/Server Memory, TAG Gateway, common Data Source diagnostics, Runtime/Engineering/Audit shell, Engineering Data Source/TAG/Alarm workspaces, Runtime Operations/Alarm Center, Runtime TAG Inspector + Recent History, Lifecycle, Project Management/Portability, Basic Trend Viewer, Administration, Driver SDK/research convergence and **canonical Script Engineering schema v10 with a practical Script Engineering Workspace**.

Canonical Engineering in merged `main` is **Schema v10**. Production Client Visual Python is now under active Wave 06 implementation and is **not yet merged product state**.

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

Merged outcome includes first-class canonical Scripts/visual-event references, stable identity and dependencies, canonical round-trip/revision/package fidelity, dependency-safe CAS/security/Audit mutation and a practical Script Engineering Workspace. Python execution remained intentionally deferred to Wave 06.

## Ordered path to v0.1

```text
Wave 03  Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04  Project portability + basic Trends + Administration                        COMPLETE
Wave 05  Canonical Script Engineering                                                COMPLETE
Wave 06  Python Editor + Client Visual sandbox                                       ACTIVE
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

**ACTIVE — PARALLEL WORKER PHASE.**

**Logical WaveBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**Central ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`  
**Integration branch:** `integration/python-wave-06`  
**Integration PR:** #83 `Establish Wave 06 Client Visual Python foundation` — Draft integration train  
**Contract CI:** #468 / run `33140329634` fully green: Web, backend Release/full tests including PostgreSQL, Runtime smoke and Chromium.  
**Implementation decision:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`.

Architecture-first central foundation is **IMPLEMENTED IN PR #83 / NOT MERGED TO MAIN**:

- pinned Pyodide `314.0.6` as the first Client Visual browser engine adapter;
- pinned Monaco Editor `0.56.0` as the first Engineering Python editor;
- dedicated module Web Worker / Script Runtime Instance isolation direction;
- bridge v1 request/response and runtime-identity contracts;
- existing safe execution policy preserved: 250 ms handler budget, queue 128, timer minimum 50 ms, five failures before throttle, coalescing and per-instance isolation;
- initial 50 ms hard-stop grace after soft interrupt, then Worker termination and interpreter discard;
- explicit Client Visual capability and denied-boundary model;
- COOP/COEP development/acceptance host configuration for SharedArrayBuffer interruption;
- canonical Engineering v10 remains Script-definition authority.

Active worker slices, all created from ContractSHA `01d5b3092cf9c33ffa41c12b79133157b24cd148`:

- **DEV 1:** `feature/python-wave-06-editor` — Monaco-backed Python Editor UX over canonical Scripts;
- **DEV 2:** `feature/python-wave-06-client-runtime` — Pyodide/Web Worker Client Visual runtime adapter behind bridge v1;
- **DEV 3:** `test/python-wave-06-sandbox-safety` — adversarial sandbox execution safety and acceptance.

Coordinator owns shared dependencies/bridge policy, central Engineering/Runtime hooks, actual authorized TAG/Client Memory/backend capability wiring, integration into PR #83 and final Wave CI.

### Wave 06 gate

The integrated product must prove:

`canonical ClientVisual Script -> edit/compile diagnostics -> isolated execution -> permitted TAG read -> owning-client Client Memory read/write -> controlled event -> bounded timeout/failure -> understandable diagnostics`

One faulty Script must not destabilize unrelated Runtime/backend state. Client Visual Python must not receive direct driver, database, filesystem, shell/process, arbitrary-network, credential, browser-DOM/storage, Server Memory write or direct shared/process TAG write authority.

Under `CONSTRAINED` CI mode, workers use focused evidence during iteration. The final exact-head integration must still pass Web + backend Release/full tests + Runtime smoke + Chromium plus Wave-specific sandbox acceptance before PR #83 can merge. If the budget cannot support the final matrix, the wave becomes `BLOCKED_BY_CI_BUDGET`, not partially accepted.

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
