# EliteSCADA Roadmap

**Status date:** 2026-08-28  
**Active direction:** **VISUAL-RUNTIME-WAVE-07 THIRD STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED / FULL PRODUCT PATH TO v0.1 OWNER VALIDATION**

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
- **Wave 06 — COMPLETE / MERGED.** Final integration head `d665dc13b0922938a15252d9775ef6604e41bff4`; final CI #487 fully green; PR #83 merged; main merge `cc79713434c1d7b5988158b843b137eaf488d923`.

Wave 06 delivered Monaco Python editing/diagnostics, pinned/self-hosted Pyodide, isolated Worker runtime behind bridge v1, compile-before-Preview/Apply/CAS, authorized TAG read, owning-client Client Memory read/write, bounded execution/cancellation/queue/disposal/failure throttling, controlled handler preview, real-Pyodide adversarial acceptance and native escape denial.

Automatic run #488 after the Wave 06 merge allocated no runner/product steps and is infrastructure evidence, not a product regression.

## Ordered path to v0.1

```text
Wave 03  Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04  Project portability + basic Trends + Administration                        COMPLETE
Wave 05  Canonical Script Engineering                                                COMPLETE
Wave 06  Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07  Visual Runtime Object Model + visual Asset contract                         THIRD STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED
Wave 08  Graphical Editor Foundation + image import/object                           WAITING
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

## Wave 07 — Visual Runtime Object Model

**THIRD STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED / NOT MERGE-READY.**

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- Integration branch: `integration/visual-runtime-wave-07`
- Exact current integration head: `d184fdd5b65f2ce0c0e6ca28cd092644be080555`
- Integration PR: intentionally not open

Wave 07 integration currently contains:

- typed common Visual Property Registry and renderer-independent built-in `core.*` schemas;
- signed-Int32 parity for integer visual properties;
- stable `assetRef = null | { assetId }` authority;
- Runtime Visual Instance identity/lifecycle/isolation and deterministic `Animation > Script > Binding/Expression > Engineering Base > Default` resolution;
- stable nested visual IDs through transitional Engineering Schema v11 with v1-v10 compatibility;
- canonical visual binding semantics and typed frontend visual Engineering projection;
- schema-guided transitional legacy property codecs in C# and TypeScript;
- official Engineering -> Runtime adapter;
- backend Preview enforcement of `core.*` schema/property/binding authority;
- Client Visual Python property read/write/explicit-clear bound to the exact current Runtime Visual Instance;
- real Worker exposure of `elite_scada.visual_property_clear` through existing bridge-v1 `visualProperty.write` capability + operation `clear`;
- fail-closed malformed/null visual Engineering Preview handling;
- adversarial structured-value/sandbox/property authority coverage committed but not yet executed on the final head.

Integrated worker heads remain:

- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`
- worker integration commit `295bdabba5c25b2e4a729228130185976735d939`

### Static audit history

Earlier deterministic contract-test drift around AssetReference and `createDefaultValues()` was corrected through `63878f6fe28a0a9ac101d622628f8b95658899a7`.

The third audit then corrected:

1. missing actual-Python clear path for Script overrides;
2. malformed visual JSON paths that could throw after generic validation instead of staying as `ImportIssue` diagnostics;
3. TypeScript integer values outside the backend C# Int32 domain;
4. a stale Wave 06 exact policy snapshot missing Wave 07 structured-value bounds, which would deterministically fail Chromium.

Exact resulting integration head: `d184fdd5b65f2ce0c0e6ca28cd092644be080555`.

These are **CI_DEFERRED** corrections, not execution evidence.

### Transitional persistence caveat

`VisualElementEngineeringDto.Properties` remains `Dictionary<string,string>`. Schema v11 is therefore transitional stable-identity/convergence, not final typed visual persistence.

Before Wave 08 becomes ACTIVE, settle and validate canonical JSON-native typed property persistence/migration. The graphical editor must consume canonical typed Engineering rather than create an editor-private string/object model.

Lower-priority items to resolve with that readiness work:

- transitional C# and JavaScript numeric serializers can spell some exponent values differently;
- canonical `Tag` versus `Property` binding source discrimination should be retained before the graphical binding engine resolves sources itself.

### Current stop state

The no-Actions stop condition is reached again. Until explicit owner report of Actions reset:

- do not open Wave 07 PR;
- do not run/rerun Actions;
- do not merge Wave 07;
- do not activate Wave 08 workers;
- do not implement graphical Wave 08 functionality.

Every current `main` change after the Wave 06 merge is documentation-only. No functional `main` code delta is hidden from the Wave 07 branch, though historical reconciliation with current `main` remains mandatory before final exact-head CI.

After reset: reconcile integration with `main`, finalize typed persistence/migration, resolve any reproducibility blocker needed for trustworthy validation, then require exact-final-head Web + backend Release/full PostgreSQL + Runtime smoke + Chromium + Wave-specific visual/Python acceptance before merge.

## Wave 08 — Graphical Editor Foundation

**WAITING FOR WAVE 07 FINAL VALIDATION/MERGE AND WAVE-08 DEFINITION OF READY.**

Planned scope remains Canvas/selection + shared Property Inspector + initial Object Palette/bindings + project image import/Image object. Stable `assetRef` identity is already defined; the first-class asset entity/import/storage/payload/renderer remains intentional Wave 08 functionality.

No Wave 08 worker is active.

## Remaining v0.1 sequence

- **Wave 09:** multiple Screens, Popups, reusable Dynamos and deterministic asset dependencies.
- **Wave 10:** Python visual events, renderer-native animation/tween and Engineering visual Preview/Test.
- **Wave 11:** build `Estação Elevatória EliteSCADA Demo` only through normal product APIs/tools.
- **Wave 12:** hardening of lifecycle, portability, visual/assets, Python failure/isolation, communication, alarms/historian/Gateway, persistence/restart/Active recovery and browser/session/localization.
- **Wave 13:** Windows x64 product package.
- **Wave 14:** owner validation of the complete Engineering -> publish/activate -> Runtime -> restart/recover path.
- **Wave 15:** feedback/corrections; v0.1 requires P0=0, P1=0 and all required validation green.

## v0.1 protocol boundary

Required real industrial protocol: **Modbus TCP**. Simulation, Client Memory, Server Memory and Gateway are also part of product validation.

Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module framework remain **POST-v0.1 OWNER VALIDATION**. Preferred later progression: `MQTT -> OPC UA -> BACnet -> Driver Module framework -> Siemens S7 -> Allen-Bradley`.

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
- stop speculative product changes when meaningful validation is unavailable;
- keep assignment board/handoff synchronized because `siga` depends on them.
