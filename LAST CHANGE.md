# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE-07 IMPLEMENTED ON INTEGRATION / CI_DEFERRED / WAITING FOR ACTIONS RESET**  
**CI budget mode:** **CONSTRAINED until owner explicitly reports reset**

## Mandatory resume reading

Before any action read current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, plus current wave-specific documents and source/tests.

GitHub branch/PR/head/CI is operational truth.

## Wave 06 — MERGED

Wave 06 is **MERGED** through PR #83.

- final product head: `d665dc13b0922938a15252d9775ef6604e41bff4`
- final CI: #487 / run `33194041390` — fully green
- main merge: `cc79713434c1d7b5988158b843b137eaf488d923`

Automatic post-merge CI #488 did not execute product steps because no runner was allocated; it is infrastructure evidence, not a product regression.

## Wave 07 — integrated implementation, validation deferred

Wave 07 development has reached the useful limit allowed under the current Actions constraint.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- integration branch: `integration/visual-runtime-wave-07`
- current integration head: `ffde3b03a3b647acb2f0c484c11b956c602237d6`
- integration PR: **not open intentionally**
- validation state: **CI_DEFERRED**

### Worker deliveries integrated

DEV 1 head `ed8d9af027173e6d265ff382dbc9c115bcd2e284` delivered:
- typed Visual Property Registry;
- explicit `number`, `boolean`, `string`, `color`, `enum`, `assetRef` property types;
- common property definitions/defaults/constraints;
- stable AssetReference validation;
- renderer-independent Engineering visual-definition projection;
- focused contract tests.

DEV 2 head `d6c1e997178e0ce525233079effd442f59743386` delivered:
- Runtime Visual Instance identity;
- per-instance Engineering/binding/script/animation layers;
- deterministic precedence `animation > script > binding > engineering > default`;
- source diagnostics;
- invalid-layer fail-closed behavior;
- instance isolation and disposal semantics;
- focused tests.

DEV 3 head `25ebac63a957c1c0c5b8e2557caec152d9d36bfc` delivered:
- Python ↔ Visual capability boundary coverage;
- unregistered/unreadable/unwritable property denial;
- runtime-instance isolation and disposal acceptance;
- AssetReference path/URL denial;
- structured bridge value checks and renderer/DOM-private authority denial.

Coordinator integrated all three worker heads in commit `295bdabba5c25b2e4a729228130185976735d939`, then reconciled their intentionally separate interfaces by:
- adapting the typed Visual Property Registry/schema to the narrow Runtime Visual Instance consumer port;
- allowing Runtime Visual Instance construction from the public Visual Object schema;
- exposing stable `runtimeInstanceId`, `objectId`, `objectKey`, `objectType` accessors;
- exposing `readPropertyState()` while preserving DEV 2's detailed `readEffective()` API;
- exporting the integrated visual runtime public surface.

Current integrated head is `ffde3b03a3b647acb2f0c484c11b956c602237d6`.

## Scope check

The Wave 07 integration is confined to new `web/scada-web/src/visual-runtime/**` modules and Wave 07 test files.

Still **not implemented / not authorized in Wave 07**:
- graphical editor/canvas;
- Screen/Popup/Dynamo authoring UI;
- image renderer/object palette;
- asset binary importer/storage;
- production animation/tween scheduler;
- Server Python;
- new industrial protocols;
- direct Python DOM/renderer/filesystem/network authority.

## Current decision

Do not open the Wave 07 PR and do not run GitHub Actions while the owner-reported allowance remains constrained.

All workers are now `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`.

When the owner explicitly reports Actions reset:
1. reread current `main` and Wave 07 documents;
2. reconcile `integration/visual-runtime-wave-07` with current `main`;
3. open the integration PR;
4. run the required exact-head Web + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + Wave-specific visual/Python acceptance;
5. correct concrete failures without weakening tests/security;
6. merge only after full green evidence;
7. then promote Wave 08.

## Permanent rules

Workers never modify `main`, merge their own work, self-assign or broaden scope. Canonical Engineering remains authority. Research is not production implementation. CI economy changes timing only, never final quality.
