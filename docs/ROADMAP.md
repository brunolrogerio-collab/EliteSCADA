# EliteSCADA Roadmap

**Status date:** 2026-08-28  
**Active direction:** **WAVE 08 — GRAPHICAL EDITOR FOUNDATION / CENTRAL COORDINATOR FOUNDATION IMPLEMENTED / WORKERS STOPPED / NOT CI VALIDATED**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Current coordinator checkpoint: `docs/COORDINATOR-HANDOFF.md`.  
Wave 08 execution contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.  
Wave 08 asset storage contract: `docs/VISUAL-ASSET-STORAGE-WAVE-08.md`.  
Wave 07 historical contract: `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`.  
Visual 07 -> 08 convergence: `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`.  
CI policy: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: canonical Engineering domains join versioned JSON, validation/Preview/Apply, revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

Merged `main` includes the complete Wave 07 visual-runtime foundation plus the earlier SCADA platform layers:

- TAG Engine/current cache/quality/Event Bus;
- Simulation + real Modbus TCP;
- PostgreSQL Engineering persistence;
- TimescaleDB historian;
- Working/Revision/Published/Active lifecycle;
- canonical Import/Export + Preview/Apply/CAS + `.escadapkg`;
- authentication/users/authorization/Audit;
- Client Memory / Server Memory;
- TAG Gateway;
- common Data Source diagnostics;
- Runtime/Engineering/Audit product shells;
- Engineering Data Source/TAG/Alarm workspaces;
- Runtime Operations/Alarm Center/TAG Inspector/Recent History;
- Lifecycle, Project Management/Portability, Basic Trends, Administration;
- canonical Script Engineering;
- practical Python editor and Client Visual Python sandbox;
- canonical **Engineering Schema v12** JSON-native typed visual properties;
- stable nested visual object identity and Script `VisualObject` references;
- typed Visual Property Registry and `core.*` built-in object schemas;
- deterministic Runtime Visual Instance layering/isolation/disposal;
- capability-bounded Client Visual Python visual property read/write/clear;
- canonical `assetRef = null | { assetId }` identity contract.

Wave 08 work described below exists only on the integration branch until validated and merged. Do not describe Schema v13 Visual Assets or the graphical editor foundation as merged `main` capability yet.

## Completed waves

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; CI #466 green.
- **Wave 06 — COMPLETE / MERGED.** Main merge `cc79713434c1d7b5988158b843b137eaf488d923`; CI #487 green.
- **Wave 07 — COMPLETE / MERGED / POST-MERGE GREEN.** Final integration head `6d869109af23b25d1ae95cd35610e1930a16791c`; exact-head CI #508 green; PR #89 merged as `8de706882ba20afedd666532ac41ae11115d06b3`; post-merge CI #510 green.

## Ordered path to v0.1

```text
Wave 03  Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04  Project portability + basic Trends + Administration                        COMPLETE
Wave 05  Canonical Script Engineering                                                COMPLETE
Wave 06  Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07  Visual Runtime Object Model + typed visual Engineering                      COMPLETE
Wave 08  Graphical Editor Foundation + image import/object                           ACTIVE
Wave 09  Screens + Popups + Dynamos + asset dependencies/navigation                 WAITING
Wave 10  Python visual events + animation + preview                                  WAITING
Wave 11  Complete HMI Runtime demo vertical slice                                    WAITING
Wave 12  Hardening                                                                   WAITING
Wave 13  Windows x64 product package                                                 WAITING
Wave 14  Product-owner validation                                                    WAITING
Wave 15  Feedback/corrections                                                        WAITING
FINAL    EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 07 closure

Wave 07 delivered and validated:
- typed common Visual Property Registry and built-in schema parity;
- stable nested visual definition/object IDs;
- native typed visual Engineering persistence in Schema v12 with v10/v11 migration;
- canonical binding destination/source semantics;
- deterministic `Animation > Script > Binding/Expression > Engineering Base > Default` source resolution;
- per-client Runtime Visual Instance isolation/disposal;
- canonical `assetRef` stable identity and path/URL rejection;
- Client Visual Python property read/write/explicit-clear without DOM/renderer authority;
- canonical Script -> nested `VisualObject` reference integrity;
- fail-closed malformed visual/Script Preview paths;
- reproducible .NET/Node/npm validation baseline;
- cross-subsystem PostgreSQL/Timescale schema-DDL locking.

Final evidence is recorded in `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`.

## Wave 08 — Graphical Editor Foundation

**ACTIVE — CENTRAL COORDINATOR FOUNDATION IMPLEMENTED / WORKERS STOPPED / NOT CI VALIDATED.**

- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- ContractSHA / branch base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration: `integration/graphical-editor-wave-08`
- last coordinator code checkpoint before documentation sync: `a2cdc39a9d7a8dcf425aab6a253d6bb0694faab1`
- main reconciliation merge on integration: `011d3c649bb868bfcf8d13bfafe896bb0863a153`
- integration verified 0 behind `main` at that checkpoint
- integration Actions validation: none yet
- integration PR: none yet
- DEV 1: `feature/graphical-editor-wave-08-canvas` @ contract base at last verification, STOPPED
- DEV 2: `feature/graphical-editor-wave-08-property-inspector` @ contract base at last verification, STOPPED
- DEV 3: `feature/graphical-editor-wave-08-palette-bindings` @ contract base at last verification, STOPPED

The editor remains a projection/editor of canonical Engineering. Private Canvas persistence is forbidden.

### Coordinator foundation already on integration

Implemented but not yet exact-head CI validated:

**Visual Asset / persistence foundation**
- Engineering Schema v13 first-class Visual Asset metadata;
- stable Visual Asset project/reference `Id` separated from developer `Key` and content SHA-256 identity;
- Working content-addressed raster registry shared by Engineering Exchange, persistence, package, validation and API;
- bounded PNG/JPEG/BMP structural validation;
- v13 `core.image.assetRef` prospective stable-ID integrity and identity-conflict rejection;
- PostgreSQL immutable asset blob + revision link persistence;
- asset-aware revision save/load/checkout/rollback;
- `.escadapkg` v2 sidecars with v1 asset-free compatibility;
- protected Visual Asset API and frontend metadata/import/content seams;
- focused Core/PostgreSQL source tests and v11/v12 compatibility updates.

**Central Screen editor foundation**
- `Engineering -> Telas` now opens `VisualEditorWorkspace`;
- canonical Screen name/key/route draft editing;
- candidate generation through the public Engineering package, not a private editor document;
- Preview/Apply reuses canonical Engineering Preview/Apply plus Workspace `changeVersion` CAS;
- central Object Palette / Canvas / Property Inspector composition boundaries are present without absorbing worker scope;
- canonical preview renderer consumes the shared `core.*` Visual Property Registry and explicit Engineering values;
- `core.image` resolves project content by stable `assetRef`;
- legacy seeded object types render as compatibility placeholders and are not silently promoted into a second property/type model;
- PT-BR/EN/ES central editor text;
- focused Playwright test covers Screen route edit -> Preview -> Apply -> canonical export -> reopen -> restore.

Detailed state, review defects, commits and resume sequence are in `docs/COORDINATOR-HANDOFF.md`.

### Parallel slices when explicitly restarted

Owner currently has all three DEV chats stopped. Their assignments are preserved but are not authorization to execute.

- **DEV 1:** Canvas / selection, zoom/pan/grid/snap, move/resize/rotate, duplicate/delete/z-order interaction intents.
- **DEV 2:** Property Inspector consuming the shared Visual Property Registry only.
- **DEV 3:** Object Palette + canonical binding authoring foundation.
- **Coordinator:** canonical Screen mutation/save/reopen authority, project image asset authority, central Engineering workspace/renderer/localization, cross-slice integration.

### Wave 08 still pending

- exact-head TypeScript/Vite and .NET build/test/Runtime-smoke evidence;
- execution of focused and full browser regression tests;
- Canvas/Property Inspector/Object Palette worker implementations after deliberate restart;
- integration of object add, move/resize/rotate, registered property editing and canonical binding into the central Screen draft;
- image object end-to-end flow through stable project `assetRef`;
- complete create/save/reopen/export/import product gate;
- final PR/CI/merge and post-merge health confirmation.

Actions should not be deliberately consumed merely to manufacture a checkpoint while owner access/quota is unavailable. Lack of CI means lack of validation, not permission to weaken the gate.

### Wave 08 gate

Integrated product must prove:

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient Canvas selection/viewport state must not become persisted Engineering authority.

Final integrated CI must preserve all Wave 07 visual/Python and Wave 06 sandbox regressions.

## Remaining v0.1 sequence

- **Wave 09:** multiple Screens, Popups, reusable Dynamos, navigation and deterministic asset dependencies.
- **Wave 10:** Python visual events, renderer-native animation/tween and Engineering visual Preview/Test.
- **Wave 11:** build `Estação Elevatória EliteSCADA Demo` only through normal product APIs/tools.
- **Wave 12:** hardening.
- **Wave 13:** Windows x64 product package.
- **Wave 14:** owner validation.
- **Wave 15:** feedback/corrections; v0.1 requires P0=0, P1=0 and required validation green.

## v0.1 protocol boundary

Required real industrial protocol: **Modbus TCP**. Simulation, Client Memory, Server Memory and Gateway remain part of product validation.

Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module framework remain post-v0.1 owner validation.

## Python/visual dependency chain

`canonical Script Engineering -> Python editor/sandbox -> visual Runtime object/property/asset model -> graphical editor -> advanced visual libraries`.

Wave 08 is the active graphical-editor stage. Wave 09/10 functionality must not leak backward merely because adjacent code looks convenient to edit.

## Development quality

- use Development Waves with frozen bases and coordinator integration trains;
- exactly one ACTIVE assignment per worker when workers are authorized to execute;
- stopped workers are not authorized merely because their preserved task text exists;
- workers edit only owned scopes and stop after delivery;
- never merge known-failing work;
- fix root causes instead of weakening tests/security/concurrency;
- preserve canonical Engineering/backend authority;
- require final integrated CI and healthy post-merge `main` for every functional wave;
- keep assignment board/handoff synchronized because `siga` depends on them.
