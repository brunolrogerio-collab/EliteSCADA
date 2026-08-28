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

Canonical Engineering in merged `main` remains **Schema v10**. Client Visual Python is official merged product state. Wave 07 visual-runtime/convergence implementation currently exists only on `integration/visual-runtime-wave-07` until final validation can run.

## Completed waves

### Wave 03 — operational lifecycle

**COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; final CI #418 fully green.

### Wave 04 — portability + historian + administration

**COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; final CI #446 fully green.

### Wave 05 — canonical Script Engineering

**COMPLETE / MERGED.** Final integration head `13d3f8283275dc957d9d6168fc7fb165df992d7e`; final CI #466 fully green; PR #79 merged; main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`.

### Wave 06 — Python Editor + Client Visual sandbox

**COMPLETE / MERGED.**

- Logical WaveBaseSHA: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`
- Central ContractSHA: `01d5b3092cf9c33ffa41c12b79133157b24cd148`
- Final integration head: `d665dc13b0922938a15252d9775ef6604e41bff4`
- Final CI #487 / run `33194041390`: Web, backend Release/full tests including PostgreSQL, Runtime smoke and Chromium all SUCCESS
- PR #83: MERGED
- Main merge: `cc79713434c1d7b5988158b843b137eaf488d923`

Merged outcome includes Monaco Python editor/diagnostics, pinned/self-hosted Pyodide, isolated module Web Worker runtime behind bridge v1, compile-before-Preview/Apply/CAS, authorized TAG read, owning-client Client Memory read/write, bounded execution/cancellation/queue/disposal/failure throttling, controlled handler preview, real-Pyodide adversarial acceptance and native escape denial.

Automatic post-merge run #488 did not execute product steps because no runner was allocated; it is infrastructure evidence, not a product regression.

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
- Integration PR: intentionally not open while Actions are deferred
- Implementation contract: `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`
- Canonical convergence/readiness contract: `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`

Implemented/hardened on the integration branch:

- typed Visual Property Registry with explicit `number`, `boolean`, `string`, `color`, `enum`, `assetRef` types;
- stable common property keys/defaults/constraints and signed-Int32 parity for integer properties;
- stable project-owned AssetReference validation with path/URL-like authority rejection and null no-asset state;
- renderer-independent Engineering visual-definition projection;
- explicit separation between **Engineering Base** and **registry Default**;
- Runtime Visual Instance identity/lifecycle and per-client isolation;
- independent Engineering, Binding, Script and Animation layers;
- deterministic precedence `Animation > Script > Binding/Expression > Engineering Base > Default`;
- source diagnostics and fail-closed invalid-layer handling;
- public Runtime construction through the typed `VisualObjectPropertySchema`;
- definition/schema type matching and stable runtime/context identity validation;
- validated-value propagation into Runtime layers;
- `runtimeReadable`/`runtimeWritable` enforcement;
- renderer-independent Client Visual Python property capability bound to the exact current Runtime Visual Instance and stable target ID/key;
- explicit Python Script override **set and clear** semantics without using null as a clear sentinel;
- official Client Visual Python provider composition for TAG + Client Memory + visual-property authority;
- actual Pyodide Worker module exposure of `elite_scada.visual_property_clear` through the existing bridge-v1 `visualProperty.write` capability with operation `clear`;
- malformed visual Engineering input handled as Preview issues rather than null-dereference exceptions through generic, built-in, reference and concrete-TAG validation paths;
- Python ↔ Visual acceptance/adversarial tests covering property authority, instance isolation, disposal, AssetReference restrictions, current-instance/current-target enforcement and DOM/renderer-private authority denial;
- source/public-surface tests preventing internal registry seams from leaking as public API.

Coordinator canonical convergence additionally established:

- transitional **Engineering Schema v11** on the integration branch, adding stable nested visual object IDs while keeping older v1-v10 packages readable;
- recursive ID materialization/preservation and duplicate/empty-ID validation;
- canonical visual binding semantics: `EngineeringBindingDto.Key` is the destination visual property/slot, `Target` is the source reference;
- explicit frontend `VisualElementEngineering` instead of Screen/Popup `unknown[]`;
- C#/TypeScript common property catalog parity for geometry, appearance, stroke, typography and image/resource properties;
- renderer-independent built-in object schema catalog for `core.group`, `core.rectangle`, `core.ellipse`, `core.line`, `core.text`, `core.image`, `core.valueDisplay` and `core.button`;
- canonical `assetRef = null | { assetId }` semantics without duplicated name/MIME/path/URL authority;
- schema-guided transitional legacy property codecs in C# and TypeScript;
- official frontend Engineering -> Runtime adapter using stable IDs, declared schemas and canonical binding semantics;
- backend Preview enforcement of known `core.*` schemas/properties/binding capabilities while leaving non-core future custom/Dynamo types extensible.

Worker deliveries integrated:

- **DEV 1:** `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- **DEV 2:** `d6c1e997178e0ce525233079effd442f59743386`
- **DEV 3:** `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`
- worker integration commit: `295bdabba5c25b2e4a729228130185976735d939`

### Static audit hardening history

First deterministic test-contract correction sequence:

- `cd2753f64a8191df3d2861871bb53077b74cc7a2` — identity-only AssetReference source contract;
- `c3f9cc15a6715bf6434b1553878ba7c6121e0783` — browser acceptance aligned to `assetRef = { assetId }`;
- `63878f6fe28a0a9ac101d622628f8b95658899a7` — projection tests aligned to actual registry API and canonical asset shape.

Third static audit then corrected:

- missing Python clear path from dispatcher/provider/composition through the real Worker module;
- null/malformed visual Engineering paths that could throw during Preview instead of returning `ImportIssue` diagnostics;
- TypeScript integer validation outside the C# signed-Int32 domain;
- stale Wave 06 exact policy expectation that omitted Wave 07 structured-value limits and would deterministically fail Chromium.

The exact resulting integration head is `d184fdd5b65f2ce0c0e6ca28cd092644be080555`. These corrections remain **CI_DEFERRED** and are not execution evidence.

### Transitional persistence caveat

`VisualElementEngineeringDto.Properties` remains `Dictionary<string,string>` on the current Wave 07 integration head. Schema v11 is therefore a transitional stable-identity/convergence schema, **not final typed visual property persistence**. The shared codecs contain this mismatch but do not make string persistence the desired editor model.

Canonical JSON-native typed visual property persistence and migration must be deliberately settled before Wave 08 becomes ACTIVE.

Two lower-priority convergence items remain deliberately attached to that work rather than extending the current no-CI coding interval indefinitely:

- C# `double.ToString("R")` and JavaScript `String(number)` need not produce byte-identical exponent spellings in the transitional string codec;
- canonical `Tag` and `Property` binding kinds are currently projected into the same generic Runtime binding source kind and should retain enough source discrimination before the graphical binding engine depends on it.

### Current no-CI stop state

After the third static audit hardening, the no-Actions stop condition is reached again. Do not continue speculative Wave 07 code merely to make progress visible.

The owner most recently reported approximately 19 included GitHub Actions minutes remaining. Until the owner explicitly reports reset:
- do not open the Wave 07 PR;
- do not run Actions;
- do not merge to `main`;
- do not activate Wave 08 workers;
- do not implement Wave 08 graphical functionality.

The 24 commits on `main` since the Wave 06 merge modify documentation only. Reconciliation with current `main` is still required before final exact-head CI, but no functional main delta is currently missing from the Wave 07 branch.

After reset, reconcile the integration branch with current `main`, settle/finalize typed visual persistence with migration coverage, then require exact-final-head Web + backend Release/full tests + Runtime smoke + Chromium + Wave-specific visual/Python acceptance green before merge.

## Wave 08 — Graphical Editor Foundation

**WAITING FOR WAVE 07 FINAL VALIDATION/MERGE AND WAVE-08 DEFINITION OF READY.**

Planned scope remains Canvas/selection + shared Property Inspector + initial Object Palette/bindings + project image import/Image object. Stable `assetRef` identity is already defined; first-class asset entity/import/storage/payload/renderer remains intentional Wave 08 functionality.

No Wave 08 worker is active.

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
- while CI is deferred, economize Actions timing, never final quality;
- stop speculative product changes when meaningful validation is unavailable;
- keep assignment board/handoff synchronized because `siga` depends on them.
