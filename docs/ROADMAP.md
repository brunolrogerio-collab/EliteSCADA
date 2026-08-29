# EliteSCADA Roadmap

**Status date:** 2026-08-28  
**Active direction:** **WAVE 08 — GRAPHICAL EDITOR FOUNDATION / CENTRAL FOUNDATION CI GREEN / DEV 1-2-3 ACTIVE / FULL PRODUCT PATH TO v0.1 OWNER VALIDATION**

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

Official `main` product state includes the complete Wave 07 foundation and all earlier SCADA layers:

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
- Python editor and Client Visual sandbox;
- Engineering Schema v12 JSON-native typed visual properties;
- stable nested visual object identity and Script `VisualObject` references;
- typed Visual Property Registry and `core.*` built-ins;
- deterministic Runtime Visual Instance layering/isolation/disposal;
- bounded Client Visual Python visual property read/write/clear;
- canonical `assetRef = null | { assetId }` identity contract.

Wave 08 Schema v13 assets/editor work exists in Draft PR #90 and is **not merged product state yet**.

## Completed waves

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; CI #466 green.
- **Wave 06 — COMPLETE / MERGED.** Main merge `cc79713434c1d7b5988158b843b137eaf488d923`; CI #487 green.
- **Wave 07 — COMPLETE / MERGED / POST-MERGE GREEN.** Final integration head `6d869109af23b25d1ae95cd35610e1930a16791c`; CI #508 green; PR #89 merged as `8de706882ba20afedd666532ac41ae11115d06b3`; post-merge CI #510 green.

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

## Wave 08 — Graphical Editor Foundation

**ACTIVE — CENTRAL FOUNDATION VALIDATED IN DRAFT PR #90 / WORKER INTERACTION SLICES ACTIVE.**

- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- ContractSHA / logical worker base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration: `integration/graphical-editor-wave-08`
- Draft integration PR: **#90**
- exact validated central-foundation product head: **`b48b489660ae953029fd2416aa18b149eaa18258`**
- CI #515 / run `33225051402`: **SUCCESS**
- Actions mode: NORMAL, conservative usage

CI #515 validated Web build/typecheck, backend Release/full tests including PostgreSQL/Timescale, Runtime smoke, Chromium E2E, the new Screen editor save/reopen test and prior Wave 06/07 visual/Python browser regressions.

### Validated central foundation in PR #90

Implemented in PR, not merged:

- Engineering Schema v13 first-class Visual Asset metadata;
- stable Visual Asset ID + SHA-256 content identity;
- bounded PNG/JPEG/BMP validation;
- canonical Working asset authority;
- PostgreSQL revision asset blobs/links;
- `.escadapkg` v2 asset sidecars with v1 compatibility;
- protected Visual Asset API with Engineering CAS/authorization/Audit;
- frontend asset catalog/import/content seams;
- `Engineering -> Telas` central `VisualEditorWorkspace`;
- canonical Screen Preview/Apply/CAS and reload;
- shared `core.*` preview renderer + stable `assetRef`;
- legacy visual compatibility placeholders without silent migration;
- shared `visualEditorContracts.ts` separating transient UI state from canonical mutation intents;
- Screen Preview -> Apply -> canonical export -> reopen E2E.

The editor remains a projection/editor of canonical Engineering. Private Canvas persistence is forbidden.

### Parallel worker phase — ACTIVE

The coordinator deliberately activated all three slices only after CI #515 and after seeding the shared intent contract into each worker branch.

- **DEV 1 — Canvas / Selection**  
  Branch seed head `57521312914e21e303976a81bc81c84ad5aa9cbb`.  
  Owns viewport, zoom/pan/grid/snap, selection/multiselect, move/resize/rotate, duplicate/delete/z-order interaction intents and UI-only adornment state.

- **DEV 2 — Property Inspector**  
  Branch seed head `3e942ed641d96afe848966f123fb10eaeaa99ed7`.  
  Owns shared-schema-driven typed property controls, explicit Default vs Engineering behavior, validation/mixed-value behavior and property set/remove intents.

- **DEV 3 — Object Palette / Binding Foundation**  
  Branch seed head `df3cd6c332a19bb3011373c95d010f33754c0c12`.  
  Owns registered `core.*` palette, object-add intents, canonical binding authoring foundation, source-catalog boundary and Image palette entry using existing `assetRef`.

All three are parallel-safe because central route/workspace/schema/API/persistence remain coordinator-reserved and the worker modules communicate through the single seeded `visualEditorContracts.ts` seam.

### Coordinator work during worker phase

Coordinator owns:

- canonical application of worker mutation intents into Screen drafts;
- central selection/viewport orchestration boundaries;
- source-catalog composition without direct driver authority;
- cross-slice integration and acceptance tests;
- worker review/integration;
- PR #90 final CI/merge.

Coordinator must not duplicate Canvas gesture implementation, Property Inspector controls or Palette/Binding UI while those worker slices are active.

### Wave 08 gate

Integrated product must prove:

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient Canvas selection/viewport/hover/adornment/drag-preview state must not become persisted Engineering authority.

Final integrated CI must preserve all Wave 07 visual/Python and Wave 06 sandbox regressions. PR #90 remains Draft until this full gate is green.

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

## Development quality

- use Development Waves with frozen logical bases and coordinator integration trains;
- exactly one ACTIVE assignment per worker;
- workers edit only owned scopes and stop after delivery;
- never merge known-failing work;
- fix root causes instead of weakening tests/security/concurrency;
- preserve canonical Engineering/backend authority;
- use Actions to buy evidence, not ceremony;
- require final integrated CI and healthy post-merge `main` for every functional wave;
- keep assignment board/handoff synchronized because `siga` depends on them.
