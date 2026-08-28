# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE-07 INTEGRATED + INTERFACE-HARDENED / CI_DEFERRED / WAITING FOR ACTIONS RESET**  
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

## Wave 07 — integrated implementation, second-pass interface hardening complete

Wave 07 remains **NOT MERGED** and **CI_DEFERRED**.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- integration branch: `integration/visual-runtime-wave-07`
- current hardened integration head: `590a51b24b79e1c43417a03492b1a5712b9ab584`
- integration PR: **not open intentionally**
- validation state: **CI_DEFERRED**
- exact current head has no pull-request workflow run associated with it

### Worker deliveries already integrated

- DEV 1 head `ed8d9af027173e6d265ff382dbc9c115bcd2e284`: typed Visual Property Registry, validation, AssetReference semantics, Engineering visual-definition projection and focused tests.
- DEV 2 head `d6c1e997178e0ce525233079effd442f59743386`: Runtime Visual Instance identity/state/layers/resolution/disposal and focused isolation tests.
- DEV 3 head `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`: Python ↔ Visual capability/acceptance/adversarial coverage.
- Coordinator integration commit `295bdabba5c25b2e4a729228130185976735d939` preserved all three worker heads as parents.

## Second-pass review findings and corrections

A fresh coordinator review found several interface weaknesses before formal CI. They were corrected on the integration branch rather than deferred into the graphical-editor waves.

### 1. Engineering Base versus registry Default was semantically blurred

Previous projection logic materialized every registered default into `baseProperties`. That made a missing Engineering value appear to Runtime diagnostics as source `engineering`, making source `default` effectively unreachable for normal projected definitions.

Corrected behavior:
- Engineering projection preserves **only explicitly engineered base values**;
- registry defaults remain registry-owned;
- Runtime resolution can now truthfully distinguish `engineering` from `default`.

### 2. Runtime exposed a second property-authority path

The initial integration allowed `RuntimeVisualInstance` to be constructed from either a schema or an arbitrary narrow registry port. That seam was useful during parallel implementation but should not become a second public property authority.

Corrected behavior:
- public Runtime construction now requires `VisualObjectPropertySchema`;
- typed schema remains the common authority shared with Engineering;
- registry-port/adapter files remain internal and are no longer exported by `visual-runtime/index.ts`;
- Runtime rejects definition/schema `objectType` mismatch.

### 3. Validation had a check/use gap

The runtime previously validated a candidate value and then stored the original candidate rather than the validator-returned value.

Corrected behavior:
- validation success now carries the accepted/normalized value;
- Engineering, binding, Script, animation and default layers store/use that validated value.

### 4. Convenience reads could bypass `runtimeReadable`

`readPropertyState()` previously delegated to the unrestricted effective-value read.

Corrected behavior:
- `readPropertyState()` now enforces `runtimeReadable`;
- unrestricted `readEffective()` is explicitly an engine/diagnostic surface and is not the Python capability path.

### 5. Runtime identity validation was too tolerant

Explicit runtime/context identifiers could previously be silently trimmed or collapse whitespace-only optional values to undefined.

Corrected behavior:
- explicit runtime/context/parent identifiers must be stable non-empty tokens;
- malformed supplied identities fail closed instead of being normalized away.

### 6. AssetReference and stacking semantics were tightened

Corrected behavior:
- asset IDs reject filesystem separators and non-`asset:` colon namespaces that could masquerade as URI schemes;
- bare opaque IDs such as UUIDs remain supported;
- optional asset media type must be image-shaped metadata;
- optional display name is bounded/trimmed/control-character safe;
- `zIndex` is integer-only rather than accepting fractional stacking positions.

### 7. Python ↔ Visual boundary now has a concrete product adapter

Added a renderer-independent `createVisualPythonPropertyCapabilityProvider()` that:
- binds capability access to the exact `visualRuntimeInstanceId` owned by the current script execution;
- accepts only the current visual object's stable ID/key;
- delegates reads through runtime-readable policy;
- delegates writes through Script-override/runtime-writable policy;
- rejects disposed instances and outside-context targets;
- returns a write acknowledgement rather than leaking a value through write authority.

The existing official `createClientVisualPythonCapabilityProvider()` now accepts an optional visual-property provider, allowing TAG + Client Memory + Visual authority to be composed deliberately without changing the sandbox worker or adding DOM/renderer authority.

## Added/updated acceptance coverage

The integration now includes focused tests for:
- explicit Engineering-versus-default source resolution;
- real typed schema consumption by Runtime Visual Instance rather than a parallel mock registry;
- schema/definition mismatch and malformed runtime identities;
- `runtimeReadable` enforcement on public property state;
- integer `zIndex`;
- disguised URL/path and invalid metadata AssetReferences;
- integrated Python visual property provider current-instance/current-target enforcement;
- official Python capability-provider composition;
- public visual runtime surface not exporting internal registry-port seams.

These tests are **written but not executed through GitHub Actions yet** under the owner's temporary no-Actions rule.

## Current diff boundary

Wave 07 product changes remain narrowly scoped to:
- new `web/scada-web/src/visual-runtime/**` foundation modules;
- Wave 07 focused/acceptance tests;
- one minimal existing composition file: `web/scada-web/src/python-runtime/createClientVisualPythonCapabilityProvider.ts`.

The Python sandbox Worker, Pyodide execution engine, backend Engineering schema, shell/routing, workflows and graphical editor were not modified by this hardening pass.

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

Do not open the Wave 07 PR, run GitHub Actions or merge Wave 07 while the owner-reported allowance remains constrained.

All workers remain `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`.

When the owner explicitly reports Actions reset:
1. reread current `main` and Wave 07 documents;
2. reconcile `integration/visual-runtime-wave-07` with current `main` without losing the hardened interfaces;
3. open the integration PR;
4. run the required exact-head Web + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + Wave-specific visual/Python acceptance;
5. correct concrete failures without weakening tests/security;
6. merge only after full green evidence;
7. then promote Wave 08.

## Permanent rules

Workers never modify `main`, merge their own work, self-assign or broaden scope. Canonical Engineering remains authority. Research is not production implementation. CI economy changes timing only, never final quality.
