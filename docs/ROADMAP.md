# EliteSCADA Roadmap

**Status date:** 2026-08-27  
**Active direction:** **FULL PRODUCT PATH TO v0.1 OWNER VALIDATION**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.

Engineering Import/Export remains cross-cutting: every new canonical Engineering domain joins versioned JSON, validation/Preview/Apply, revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

The platform already includes, in `main`, substantial foundations across:

- TAG Engine/current cache/quality and Event Bus;
- Simulation and real Modbus TCP;
- PostgreSQL Engineering persistence;
- TimescaleDB historian foundation;
- Working/Revision/Published/Active lifecycle and transactional activation/recovery foundations;
- canonical Engineering import/export and Preview/Apply/CAS;
- authentication, local users, authorization and Audit;
- Client Memory and retentive Server Memory;
- protocol-independent TAG Gateway;
- common multi-Data-Source communication diagnostics;
- Runtime/Engineering/Audit product shell;
- Engineering Data Source/TAG/Alarm workspace ergonomics;
- Runtime Operations Overview and Alarm Center/ACK UX;
- Audit workspace ergonomics;
- public Python/visual contract foundations and isolated Script Engineering foundation;
- merged Python/editor/protocol research;
- Driver SDK research convergence through PR #68.

Canonical Engineering remains **Schema v9** until a deliberate migration wave changes it with compatibility tests.

## Wave 00 — current interface closeout

**FUNCTIONAL INTEGRATION COMPLETE.**

PRs #65, #66 and #67 are merged. Coordinator PR #69 integrated the Runtime Alarm Center into the actual Runtime composition, removed the legacy duplicate alarm path and preserved backend-authoritative ACK identity.

PR #69:

- final head `c493709a221614a093717b6e6a16bf8821226e91`;
- exact-head CI #403: Web PASS, backend/tests/smoke PASS, Chromium PASS;
- merge `ee65ab51a39cd74ef6f14395d27b0ee16b8c6970`.

Before freezing Wave 03 base, coordinator must address or deliberately isolate the PostgreSQL concurrent schema-initialization race observed during CI. It is infrastructure hardening, not a new product feature.

## Ordered path to v0.1

The approved sequence is:

```text
Wave 03  Operational lifecycle + Runtime TAG Inspector + acceptance foundation
Wave 04  Project portability + basic Trends + Administration
Wave 05  Canonical Script Engineering
Wave 06  Python Editor + Client Visual sandbox
Wave 07  Visual Runtime Object Model
Wave 08  Graphical Editor Foundation
Wave 09  Screens + Popups + Dynamos
Wave 10  Python visual events + animation + preview
Wave 11  Complete HMI Runtime demo vertical slice
Wave 12  Hardening
Wave 13  Windows x64 product package
Wave 14  Product-owner validation
Wave 15  Feedback/corrections
FINAL    EliteSCADA v0.1 — Full Product Validation Preview
```

Do not skip dependencies simply because later research already exists.

## Wave 03 — complete operational lifecycle

Goal: prove `edit -> save -> publish -> activate -> operate` as a coherent user workflow.

Preferred parallel slices:

- DEV 1: Engineering Lifecycle Workspace;
- DEV 2: read-only Runtime TAG Inspector + recent history;
- DEV 3: cross-product Interface Validation Readiness Harness;
- Coordinator: integration branch, central hooks, final CI and gate decision.

Wave details/assignments are created only after Definition of Ready and a frozen `WaveBaseSHA`.

## Wave 04 — portability + historian + administration

- Project Management surface for canonical JSON Import/Export, Preview/Apply and `.escadapkg` backup/restore;
- basic live + historical Trend Viewer;
- Administration workspace ergonomics.

Gate: practically usable non-graphical SCADA/platform workflow before canonical scripting/visual product integration.

## Wave 05 — canonical Script Engineering

**ARCHITECTURE-FIRST WAVE.**

Coordinator first stabilizes shared canonical Script contracts in the wave integration branch: Engineering schema/model, Scripts collection, stable references, import/export/migration, Preview/Apply, revision/PostgreSQL and `.escadapkg`.

Workers then build isolated Script workspace/reference/compatibility slices against that contract.

Gate: Script source/scope/events/dependencies/references/enabled state survive canonical round-trip, revision and package flows.

## Wave 06 — Python Editor + Client Visual sandbox

Production Client Visual Python begins only after canonical Script integration.

Required result includes practical code editor, safe browser sandbox, narrow versioned EliteSCADA API, cancellation/time budgets/bounded queues/isolation and meaningful diagnostics.

No direct driver/database/filesystem/shell/arbitrary-network/credential access.

## Wave 07 — Visual Runtime Object Model

**ARCHITECTURE-FIRST WAVE.**

Coordinator stabilizes canonical visual identity/runtime-instance/property-resolution rules before parallel implementation.

Locked precedence:

`Animation > Script > Binding/Expression > Engineering Base`

Gate: a canonical visual definition can render, bind, be changed by Python/animation and dispose deterministically without mutating saved Engineering.

## Wave 08 — Graphical Editor Foundation

Canvas/selection + shared Property Inspector + initial Object Palette/bindings.

The editor consumes canonical Engineering and the common Visual Property Registry. Renderer/canvas private state never becomes project truth.

Gate: create Screen, place/manipulate objects, edit properties, save/reopen and export/import.

## Wave 09 — Screens + Popups + Dynamos

Multiple Screen navigation, Popup runtime/definition behavior and first reusable Dynamo definition/instance/public-parameter model.

Gate: Screen + objects + Dynamo + button -> Popup works through normal Engineering publication/activation and Runtime.

## Wave 10 — Python visual events + animation

Event association, renderer-native animation/tween and Engineering Python Preview/Test.

Gate: an engineered button can execute controlled Python that reads a TAG, changes Client Memory, changes another object's Runtime presentation, opens a Popup and requests animation.

## Wave 11 — complete HMI vertical slice

Build `Estação Elevatória EliteSCADA Demo` only through normal product APIs/tools.

It proves graphical Runtime, bindings, Python, alarms/ACK, historian, diagnostics, Gateway, memory and optional laboratory Modbus connection as one SCADA application.

Hidden DB/private JSON/manual developer intervention is a product gap, not acceptance.

## Wave 12 — hardening

No major feature family. Stress Engineering lifecycle/portability, visual model, Python failure/isolation, Runtime communication/reconnect/quality/alarm/historian/Gateway, persistence/restart/Active recovery and browser/session/localization behavior.

## Wave 13 — Windows x64 package

Create the first owner-facing product package only after graphical Engineering + Client Visual Python + complete HMI runtime path exist.

Internal packaging spikes may happen earlier, but are development evidence only.

Owner package must not require `dotnet run`, npm/Vite, Git, solution knowledge or manual migrations.

## Wave 14 — owner validation

Owner tests Demo, project creation from scratch, real Modbus, Dynamo reuse, Popup + Python + Client Memory and restart/Active recovery.

The core acceptance is:

`create project -> source/TAG/alarm -> Screen/object/binding -> Python behavior -> save -> publish -> activate -> Runtime -> restart -> recover Active application`.

## Wave 15 — feedback/corrections

Classify P0 BLOCKER, P1 MAJOR, P2 WORKFLOW, P3 UX, P4 COSMETIC.

v0.1 requires P0=0, P1=0, required CI/package smoke green and restart/Active recovery green.

## v0.1 protocol boundary

Required real industrial protocol: **Modbus TCP**.

Also available/required for product validation: Simulation, Client Memory, Server Memory and Gateway.

Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module framework remain **POST-v0.1 OWNER VALIDATION**.

Research for those protocols is already merged and the Driver SDK is architecturally converged. Research readiness does not equal an open production gate.

## Post-v0.1 protocol direction

Preferred progression:

`MQTT reference implementation -> OPC UA -> BACnet -> Driver Module framework -> Siemens S7 -> Allen-Bradley`.

MQTT should first prove the common Driver SDK/Engineering/runtime/diagnostics model as one coordinated protocol wave. Do not initially assign three different protocols to three workers while the production SDK pattern remains unproved.

## Python/visual dependency chain remains locked

`canonical Script Engineering -> Python editor/sandbox -> visual Runtime object/property model -> graphical editor -> advanced visual libraries`.

The detailed architecture remains in `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md` and merged research documents.

## Development quality

- use Development Waves with a frozen logical base and coordinator integration train;
- never merge known-failing work;
- fix root causes rather than weaken tests/security/concurrency;
- preserve canonical Engineering and backend authority;
- require final integrated CI for every wave;
- do not move `main` with unrelated product work during an active wave unless an allowed exception applies;
- keep the assignment board synchronized because `siga` depends on it.