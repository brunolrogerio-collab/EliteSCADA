# EliteSCADA Roadmap

**Status date:** 2026-08-28  
**Active direction:** **VISUAL-RUNTIME-WAVE-07 FOURTH STATIC AUDIT COMPLETE / CI AVAILABLE / PRE-FINALIZATION / FULL PRODUCT PATH TO v0.1 OWNER VALIDATION**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Live worker ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Wave 07 implementation boundary: `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`.  
Wave 07 -> 08 readiness boundary: `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`.  
CI usage mode: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: every canonical Engineering domain joins versioned JSON, validation/Preview/Apply, revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current merged foundation

The merged product includes TAG Engine/current cache/quality/Event Bus, Simulation and real Modbus TCP, PostgreSQL Engineering persistence, TimescaleDB historian foundations, Working/Revision/Published/Active lifecycle, canonical Import/Export + Preview/Apply/CAS, authentication/users/authorization/Audit, Client/Server Memory, TAG Gateway, common Data Source diagnostics, Runtime/Engineering/Audit shell, Engineering Data Source/TAG/Alarm workspaces, Runtime Operations/Alarm Center, Runtime TAG Inspector + Recent History, Lifecycle, Project Management/Portability, Basic Trend Viewer, Administration, Driver SDK/research convergence, canonical Script Engineering schema v10, practical Script Engineering Workspace and the merged Wave 06 Client Visual Python foundation.

Canonical Engineering in merged `main` remains **Schema v10**. Wave 07 visual-runtime/convergence implementation exists only on `integration/visual-runtime-wave-07` until final validation and merge.

## Completed waves

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; final CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; final CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Final CI #466 green; PR #79 merged; main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`.
- **Wave 06 — COMPLETE / MERGED.** Final integration head `d665dc13b0922938a15252d9775ef6604e41bff4`; final CI #487 green; PR #83 merged; main merge `cc79713434c1d7b5988158b843b137eaf488d923`.

## GitHub Actions availability

The previous CI-budget freeze is lifted.

On 2026-08-28 the owner reported an account/repository configuration change expected to provide roughly 1000 additional Actions minutes. Repository tooling cannot verify billing balance directly, but a controlled probe proved execution availability:

- historical run #488, attempt 2;
- reran only Web build job `98990802140`;
- hosted runner allocated;
- checkout/setup/install/build all SUCCESS.

This probe validates Actions availability only, not Wave 07 product correctness.

## Ordered path to v0.1

```text
Wave 03  Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04  Project portability + basic Trends + Administration                        COMPLETE
Wave 05  Canonical Script Engineering                                                COMPLETE
Wave 06  Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07  Visual Runtime Object Model + visual Asset contract                         PRE-FINALIZATION / CI AVAILABLE
Wave 08  Graphical Editor Foundation + image import/object                           WAITING
Wave 09  Screens + Popups + Dynamos + asset dependencies/navigation
Wave 10  Python visual events + animation + preview
Wave 11  Complete HMI Runtime demo vertical slice
Wave 12  Hardening
Wave 13  Windows x64 product package
Wave 14  Product-owner validation
Wave 15  Feedback/corrections
FINAL    EliteSCADA v0.1 — Full Product Validation Preview
```

Do not skip dependencies simply because CI capacity or later research exists.

## Wave 07 — Visual Runtime Object Model

**FOURTH STATIC AUDIT COMPLETE / CI AVAILABLE / PRE-FINALIZATION / NOT MERGE-READY.**

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- Integration branch: `integration/visual-runtime-wave-07`
- Exact current integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`
- Integration PR: not open yet

Current integration includes:

- typed common Visual Property Registry and `core.*` built-in schemas;
- signed-Int32 parity for integer properties;
- stable `assetRef = null | { assetId }` authority;
- Runtime Visual Instance identity/lifecycle/isolation and deterministic precedence;
- stable nested visual IDs through transitional Engineering Schema v11 with v1-v10 compatibility;
- canonical visual binding semantics and typed frontend Engineering projection;
- schema-guided transitional property codecs in C# and TypeScript;
- official Engineering -> Runtime adapter;
- backend Preview enforcement for built-in schema/property/binding authority;
- Client Visual Python property read/write/explicit-clear bound to the exact current instance;
- real Worker exposure of `elite_scada.visual_property_clear`;
- fail-closed malformed/null visual and Script Engineering Preview handling;
- canonical nested `VisualObject` Script dependency/event resolution;
- prospective validation of already-saved Scripts against incoming Engineering changes;
- legacy v10 visual identity projection aligned with Apply semantics.

Integrated worker heads remain:

- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

### Static audit history

- AssetReference/API test drift corrected through `63878f6fe28a0a9ac101d622628f8b95658899a7`.
- Third audit corrected Python clear, malformed nested visual Preview, Int32 parity and stale Wave 06 policy snapshot through `d184fdd5b65f2ce0c0e6ca28cd092644be080555`.
- Fourth audit corrected top-level malformed views, stale current-schema Script tests, nested visual Script references, malformed Script arrays, prospective existing-Script integrity and v10 visual identity projection through `e376b37d2772906dd667afa199a6d8882abd43ae`.

These corrections still require execution proof on the final candidate head.

### Remaining Wave 07 readiness blockers

1. `VisualElementEngineeringDto.Properties` still persists as `Dictionary<string,string>`; canonical JSON-native typed visual property persistence/migration must be settled before Wave 08.
2. Integration must be reconciled with current documentation-only `main` history.
3. Minimum reproducibility blockers needed for trustworthy final validation should be settled deliberately, notably frontend lockfile/version pinning and .NET SDK/compiler pinning if chosen.
4. Final exact-head Web + backend Release/full PostgreSQL + Runtime smoke + Chromium + Wave-specific visual/Python acceptance must be green before merge.

Lower-priority items remain numeric textual canonicalization, `Tag` versus `Property` binding-source discrimination, broader legacy import null/empty-ID hardening, production CORS and branch protection.

## Wave 08 — Graphical Editor Foundation

**WAITING FOR WAVE 07 FINAL VALIDATION/MERGE AND WAVE-08 DEFINITION OF READY.**

Planned scope remains Canvas/selection + shared Property Inspector + initial Object Palette/bindings + project image import/Image object. Stable `assetRef` identity is defined; first-class asset entity/import/storage/payload/renderer remains Wave 08 functionality.

No Wave 08 worker is active.

## Remaining v0.1 sequence

- **Wave 09:** multiple Screens, Popups, reusable Dynamos and deterministic asset dependencies.
- **Wave 10:** Python visual events, renderer-native animation/tween and Engineering visual Preview/Test.
- **Wave 11:** build `Estação Elevatória EliteSCADA Demo` only through normal product APIs/tools.
- **Wave 12:** hardening.
- **Wave 13:** Windows x64 product package.
- **Wave 14:** owner validation.
- **Wave 15:** feedback/corrections; v0.1 requires P0=0, P1=0 and all required validation green.

## v0.1 protocol boundary

Required real industrial protocol: **Modbus TCP**. Simulation, Client Memory, Server Memory and Gateway are also part of product validation.

Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module framework remain **POST-v0.1 OWNER VALIDATION**.

## Python/visual dependency chain remains locked

`canonical Script Engineering -> Python editor/sandbox -> visual Runtime object/property/asset model -> graphical editor -> advanced visual libraries`.

## Development quality

- use Development Waves with frozen logical bases and coordinator integration trains;
- workers start only explicit ACTIVE assignments;
- never merge known-failing work;
- fix root causes rather than weaken tests/security/concurrency;
- preserve canonical Engineering and backend authority;
- require final integrated CI for every functional wave;
- economize Actions timing, never final quality;
- keep assignment board/handoff synchronized because `siga` depends on them.
