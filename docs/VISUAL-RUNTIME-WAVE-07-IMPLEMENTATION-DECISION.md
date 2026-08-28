# Wave 07 — Visual Runtime Object Model Implementation Decision

Status: **LOCKED / COMPLETE / MERGED / POST-MERGE GREEN**  
Date: 2026-08-28  
Logical product base: `cc79713434c1d7b5988158b843b137eaf488d923`  
Final integration product head: `6d869109af23b25d1ae95cd35610e1930a16791c`  
Main merge: `8de706882ba20afedd666532ac41ae11115d06b3`

This document is the completed Wave 07 architecture contract. It remains normative input to Wave 08 and later visual/Python work.

## Delivered objective

Wave 07 established one deterministic, renderer-independent visual Runtime foundation with:

1. stable visual definition/object identity;
2. typed public Visual Property Registry;
3. per-client Runtime Visual Instance identity/lifecycle/isolation;
4. deterministic property-source resolution;
5. runtime override isolation from saved Engineering;
6. stable project asset-reference semantics;
7. capability-bounded Client Visual Python property access;
8. canonical JSON-native typed visual Engineering persistence via Schema v12.

Locked precedence:

`Animation > Script > Binding/Expression > Engineering Base > Default`

## Public property model

The registry supports explicit serializable property families including number/integer, boolean, string, color, enum and `assetRef`. Consumers do not infer public types from arbitrary JavaScript values.

Every public property definition carries stable key/type/default plus authority such as Engineering editability, runtime readability/writability, binding support and animation capability. Validation remains centralized through the registry.

The common catalog includes geometry/transform, visibility/appearance, stroke/fill, text/font/alignment and Image properties required by the initial built-in object schemas.

Built-in renderer-independent object type keys are:
- `core.group`
- `core.rectangle`
- `core.ellipse`
- `core.line`
- `core.text`
- `core.image`
- `core.valueDisplay`
- `core.button`

## Canonical Engineering representation

Engineering Schema v12 persists visual properties as native typed JSON values. Historical v10/v11 string properties migrate only through declared built-in schemas; the system does not guess types for arbitrary custom legacy objects.

Nested visual objects carry stable IDs. Legacy missing IDs are materialized/preserved by explicit compatibility rules and duplicate/empty IDs fail closed.

A visual binding uses:
- `Key` as the destination visual property key;
- `Target` as the canonical source reference;
- `Kind` as the source interpretation.

The graphical editor must edit this canonical Engineering representation rather than introduce private persisted canvas state.

## RuntimeVisualInstance semantics

A runtime visual instance is client-local presentation state created from one visual definition. Multiple clients/instances remain isolated.

Effective value resolution is deterministic:
1. Animation override;
2. Script override;
3. valid Binding/Expression value;
4. explicit Engineering Base;
5. registry Default.

The API supports effective reads/source diagnostics, binding apply/clear, permitted Script override set/clear, permitted Animation override set/clear and deterministic disposal. Runtime writes never implicitly save into Engineering.

Invalid/unregistered/non-readable/non-writable/non-animatable operations fail closed according to property authority.

## AssetReference

Canonical public shape:

`assetRef = null | { assetId }`

Only stable project-owned identity belongs in the reference. Filesystem paths, arbitrary URLs and duplicated metadata are not reference authority. First-class asset entity/import/storage/rendering are Wave 08/later responsibilities.

## Client Visual Python boundary

Client Visual Python can locate permitted visual instances within its current visual context, read registered runtime-readable properties and set/clear registered runtime-writable Script overrides through the trusted capability dispatcher.

Python does not receive DOM nodes, React components, renderer handles, arbitrary JavaScript objects, storage APIs, filesystem, drivers, database or unrestricted networking.

Structured bridge values are bounded and prototype-safe. Disposal, timeout/cancellation and sandbox/native-escape regressions remain required acceptance coverage.

## Engineering/Script referential integrity

Canonical Script `VisualObject` dependencies and object-scoped event references resolve against the same nested stable visual identities used by Engineering. Prospective Screen/Popup changes that would invalidate existing Script references fail during Preview rather than after Apply.

Duplicate definition-level binding writers and duplicate Script event references fail closed.

## Historical non-goals preserved

Wave 07 deliberately did **not** implement:
- graphical Canvas/editor;
- Property Inspector UI;
- Object Palette UI;
- binary asset importer/storage;
- production graphical Screen/Popup/Dynamo authoring;
- production renderer/tween scheduler;
- Wave 09 navigation/Popup/Dynamo product semantics;
- Wave 10 event editor/animation preview;
- Server Python;
- new industrial protocols.

Those boundaries remain important when reviewing Wave 08 worker scope.

## Validation and merge evidence

Final exact-head CI:
- CI #508 / run `33217787482`
- exact product head `6d869109af23b25d1ae95cd35610e1930a16791c`
- Web: SUCCESS
- backend Release/full PostgreSQL+Timescale tests: SUCCESS
- Runtime smoke: SUCCESS
- Chromium: SUCCESS
- Wave 07 visual/Python acceptance: SUCCESS
- Wave 06 sandbox/native-escape/timeout/cancellation regressions: SUCCESS

Merge:
- PR #89
- merge commit `8de706882ba20afedd666532ac41ae11115d06b3`

Post-merge main health:
- CI #510 / run `33218282760`
- conclusion: SUCCESS
- Web, backend/full tests, Runtime smoke and Chromium all SUCCESS.

The earlier temporary CI-deferral rule is **historical and no longer active**. Current CI policy is defined only by `docs/CI-USAGE-POLICY.md`.

Wave 07 is closed. New functionality belongs to the explicitly activated later wave and must not silently amend this completed contract.
