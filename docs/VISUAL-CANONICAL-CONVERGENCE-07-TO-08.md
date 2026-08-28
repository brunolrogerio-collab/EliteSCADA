# EliteSCADA — Visual Canonical Convergence 07 -> 08

Status: **COORDINATOR THIRD STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED / STOP CONDITION REACHED**  
Date: 2026-08-28  
Exact reviewed integration head: `d184fdd5b65f2ce0c0e6ca28cd092644be080555`

This document records the coordinator-only canonical visual convergence and subsequent repository-wide static-audit corrections performed while Wave 07 waits for final GitHub Actions validation and before Wave 08 graphical-editor implementation is authorized.

## Decision

Do **not** absorb the full Wave 08 graphical editor into Wave 07.

The no-Actions interval has been used only for coordinator-owned canonical convergence/readiness and newly demonstrated correctness blockers. DEV 1, DEV 2 and DEV 3 remain stopped. No Canvas/editor/product UI work is authorized or implemented.

## Why this phase existed

The original review found that the frontend Wave 07 runtime foundation and existing canonical/backend visual contracts were not yet one model:

1. canonical `VisualElementEngineeringDto.Properties` was `Dictionary<string,string>` while the Wave 07 registry requires typed number/boolean/string/color/enum/assetRef values;
2. canonical visual elements had `Key`/`Type` but no stable nested object ID while Script visual-event references already require stable object identity;
3. visual bindings did not document an unambiguous destination visual property compatible with the Wave 07 `propertyKey` model;
4. the older C# `VisualPropertyFoundation` diverged from the new Wave 07 TypeScript contract in names/defaults/source semantics;
5. frontend Engineering view types exposed Screen/Popup `elements` as `unknown[]`;
6. stable `assetRef` authority had not yet been reconciled with the future project asset model.

Starting Canvas/Property Inspector before resolving those boundaries would have forced editor-private authority, which is forbidden by `PROJECT GOAL.md`.

## Convergence outcome

### Stable visual identity — SETTLED FOR WAVE 07

The integration branch now uses transitional Engineering **Schema v11**.

`VisualElementEngineeringDto` gained an optional `Guid? Id` appended compatibly to the existing DTO. The view registry materializes missing nested IDs for legacy inputs, preserves prior IDs by unambiguous key path when a legacy update still omits them, exports the materialized IDs, and rejects empty/duplicate IDs across a visual tree.

Older schema versions remain readable. Merged `main` remains Schema v10 until Wave 07 itself is validated and merged.

Rename semantics are deliberate: once materialized/exported as v11, identity should travel explicitly by ID. A legacy package that renames an object while still omitting the new ID cannot reliably preserve identity and must not be treated as though the key itself were permanent identity.

### Binding target — SETTLED

No redundant persisted field was added.

For a binding attached to a visual element:

- `EngineeringBindingDto.Key` is the **destination visual property/slot key**;
- `EngineeringBindingDto.Target` is the TAG/property/expression **source reference**;
- `Kind` identifies source interpretation.

The frontend Engineering projection and Engineering -> Runtime adapter use this same rule.

### Default versus Engineering Base — SETTLED

C# and TypeScript now preserve the same source model:

`Animation > Script > Binding/Expression > Engineering Base > Default`.

Registry defaults are not materialized as though the engineer explicitly authored them. Compatibility APIs may expose effective design-time values, but Runtime source diagnostics distinguish explicit Engineering Base from registry Default.

### Frontend Engineering projection — SETTLED

Screen/Popup elements are represented by explicit `VisualElementEngineering` types rather than an `unknown[]` boundary.

The official frontend Engineering -> Runtime adapter:

- requires a materialized stable object ID;
- resolves properties through the declared `VisualObjectPropertySchema`;
- uses the shared transition codec rather than editor-local coercion;
- maps canonical binding semantics into the renderer-independent Runtime projection;
- preserves parent identity while flattening trees parent-before-child;
- does not introduce editor-private persisted state.

### Property registry parity — SETTLED STRUCTURALLY / EXECUTION VALIDATION DEFERRED

C# and TypeScript expose the intended common visual property family for geometry/transform, visibility/opacity, fill/background, stroke/style/corner radius, text/font/alignment and `assetRef`/image properties.

The third static audit tightened integer parity: TypeScript integer visual properties now use the same signed Int32 domain as C# `VisualIntegerValue(int)`, rejecting values outside `-2147483648..2147483647` instead of accepting JavaScript integers the backend cannot represent.

Built-in renderer-independent object schemas are established for:

- `core.group`
- `core.rectangle`
- `core.ellipse`
- `core.line`
- `core.text`
- `core.image`
- `core.valueDisplay`
- `core.button`

Backend Preview validates known `core.*` types against these schemas and fails closed for unknown built-in types, undeclared properties, and bindings targeting undeclared/unsupported properties. Non-core future Dynamo/custom/plugin types are not prematurely prohibited.

This is a contract/catalog foundation only. No Object Palette UI, Canvas or renderer was implemented.

### AssetReference — IDENTITY/AUTHORITY SETTLED FOR WAVE 08 INPUT

Canonical visual property semantics are:

`assetRef = null | { assetId }`

The reference carries only stable project-owned identity. It does not copy asset name/MIME as competing authority and cannot be an arbitrary filesystem path or URL.

The first-class project asset entity owns future descriptive metadata such as name, original filename, MIME type, dimensions, hash and payload metadata. Those fields do not travel inside each visual `assetRef`.

The legacy C# `imageResourceId`/`ResourceReference` shape remains compatibility surface only; the new canonical visual contract uses `assetRef`.

The first-class project asset entity, binary payload/import/storage/serving, image decoding and visual renderer remain intentional Wave 08 implementation work. They were not pulled into Wave 07.

### Client Visual Python property authority — SET/CLEAR SETTLED STRUCTURALLY

The Wave 07 contract requires the current Client Visual Python execution to read declared runtime-readable visual properties and set **or clear** a Script override only when the property is runtime-writable and the target belongs to the current Runtime Visual Instance.

The integration branch now preserves bridge v1 and the existing `visualProperty.write` capability while using explicit operations:

- operation `write` carries `targetReference`, `propertyKey`, `value` and calls `RuntimeVisualInstance.setScriptOverride`;
- operation `clear` carries `targetReference`, `propertyKey` only and calls `RuntimeVisualInstance.clearScriptOverride`;
- `null` is not overloaded as a clear sentinel;
- the official provider composition forwards both operations without exposing DOM/renderer authority;
- the actual Pyodide Worker module exposes `elite_scada.visual_property_clear(target, property)` and routes it through `visualProperty.write` + `clear`;
- current visual instance, target, lifecycle, registration and runtime-writable checks remain the authority boundary.

Committed tests cover set -> clear -> fallback to Engineering Base and the real Worker source surface. Execution validation remains deferred.

### Malformed visual Engineering — FAIL-CLOSED PREVIEW SETTLED STRUCTURALLY

The third static audit found a validation-order hole: JSON can deserialize null entries/fields into nominally non-null C# DTO collections. Generic validation could correctly record an issue and later built-in/reference validation could still dereference the same malformed node/binding, throwing instead of returning an `ImportPreview`.

The integration branch now keeps the full visual Preview path null-safe:

- null visual nodes produce `VISUAL_ELEMENT_NULL`;
- null bindings produce `BINDING_NULL`;
- blank/null keys and targets remain normal required-field issues;
- placeholder checks do not dereference null targets;
- built-in visual schema validation skips malformed binding keys already owned by generic validation;
- recursive View reference validation skips diagnosed null nodes;
- concrete TAG-binding validation skips diagnosed null/malformed bindings;
- direct validation tests and a full `EngineeringExchangeService.Preview` malformed-tree test are committed.

Invalid visual Engineering should therefore remain on `parse -> validate -> preview`, not turn into a null-reference exception. Final execution proof remains deferred.

### Transitional string-property codec — CONTAINED, NOT FINAL

A schema-guided transition codec exists in both C# and TypeScript for the current string-valued visual property bag. The declared `VisualObjectPropertySchema` determines value type; neither side guesses types from textual contents.

The review aligned accepted canonical boolean/numeric text forms, but this codec must not be mistaken for a cross-runtime byte-canonical storage format. C# `double.ToString("R")` and JavaScript `String(number)` can spell some exponent values differently even when the numeric value is equivalent.

This is another reason to finish JSON-native typed persistence deliberately instead of polishing the temporary string representation forever.

## Static audit correction history

### Asset/property test drift correction

A repository audit after the earlier convergence head found deterministic test expectations that no longer matched the settled canonical contract:

1. a source-contract test still required an old three-field AssetReference shape containing `assetId`, `name` and `mediaType`;
2. browser acceptance simultaneously treated descriptive asset metadata as valid while another contract test correctly rejected it;
3. Engineering projection tests still called obsolete `createDefaultBaseValues()` even though the public schema API is `createDefaultValues()`;
4. the same projection tests still treated name/MIME metadata as part of a visual asset reference.

These were corrected without weakening product code:

- `cd2753f64a8191df3d2861871bb53077b74cc7a2`
- `c3f9cc15a6715bf6434b1553878ba7c6121e0783`
- `63878f6fe28a0a9ac101d622628f8b95658899a7`

### Third static audit hardening

A later full-project audit then found:

1. Python could set Script override but the actual Python Worker module had no public clear function;
2. malformed/null visual JSON could escape generic diagnosis and throw in later Preview validation layers;
3. TypeScript registry integer validation was wider than the C# Int32 model;
4. `python-sandbox-foundation.spec.ts` still compared the bridge policy to the exact Wave 06 object and omitted Wave 07 `maxBridgeDepth`, `maxBridgeNodes` and `maxBridgeStringLength`, making the Chromium test deterministically fail.

All four were corrected while preserving the existing security/authority contracts. The exact reviewed integration head after this hardening is:

`d184fdd5b65f2ce0c0e6ca28cd092644be080555`

The Worker change itself was verified by commit comparison as an additive four-line public bridge exposure, with no deletion/rewrite of sandbox logic. The Wave 06 capability list remains unchanged because clear is an explicit operation of the existing visual-property write capability rather than a new authority class.

All of this remains **CI_DEFERRED**. Static correction is not execution proof.

## Deliberately unresolved blocker: typed visual property persistence

`VisualElementEngineeringDto.Properties` still persists as `Dictionary<string,string>` on the current integration branch.

Therefore Schema v11 is a **transitional stable-identity/convergence schema**, not final JSON-native typed visual persistence.

Before Wave 08 becomes ACTIVE, the coordinator must deliberately settle the canonical typed visual property representation and migration/compatibility strategy. The editor must consume that canonical representation rather than establish its own string/object persistence rules.

The target remains native typed values validated by the public Visual Property Registry, including at least finite number, boolean, string, color string, enum string and null/stable project `assetRef` object. Registry Default remains distinct from explicitly engineered base state.

## Lower-priority binding-source convergence

The canonical Engineering contract distinguishes `Tag`, `Property` and `Expression` binding kinds. The current frontend Engineering -> Runtime projection intentionally reduces `Tag` and `Property` to a generic Runtime `binding` source while values are externally resolved.

That is not a current Wave 07 execution blocker, but the graphical binding engine must retain enough source-kind discrimination before Wave 08/09 depends on resolving those references itself. Resolve it deliberately with typed persistence/readiness work rather than introducing a second editor-private binding model.

## Repository-wide debt observed during audit

The audits also found non-functional quality/reproducibility debt outside the immediate Wave 07 correctness blockers:

- frontend dependencies include floating `latest` versions and no committed `package-lock.json`;
- CI uses `npm install` rather than `npm ci`;
- no `global.json` pins the .NET SDK while `LangVersion` is `latest`;
- `main` is not protected by required branch checks;
- API CORS is globally permissive;
- `tests-e2e` are not included in the normal frontend TypeScript build;
- many cheap source/contract tests run only through the expensive Playwright/WebServer path.

These findings should be addressed deliberately in the appropriate hardening/reproducibility scope. They do not authorize speculative functional expansion while Wave 07 CI is frozen.

## Main/integration reconciliation fact

The current `main` history after the Wave 06 merge contains 24 commits and changes only six documentation files. No functional code change in current `main` is missing from the Wave 07 integration branch.

The integration branch is still historically diverged and must be reconciled with current `main` before final exact-head CI, but the present divergence is documentation-only rather than a hidden product-code conflict.

## Wave 07 final validation boundary

Wave 07 still requires deferred exact-head CI before merge. Static review does not waive or replace:

- Web build;
- backend Release/full PostgreSQL tests;
- Runtime smoke;
- Chromium;
- Wave-specific visual/Python acceptance, including Wave 06 sandbox/native-escape/cancellation regressions.

If typed-persistence work changes the Wave 07 final product head after Actions reset, final CI must validate that exact later head.

## Wave 08 Definition of Ready

Wave 08 may be activated only when:

1. Wave 07 final integration is green and merged;
2. canonical typed visual property persistence/migration is settled;
3. stable visual definition/object identity is settled;
4. visual-property binding target semantics are settled;
5. frontend/backend property registry parity is settled and validated;
6. asset reference identity contract is settled sufficiently for the Image object/import slice;
7. the Engineering editor can consume canonical visual definitions without inventing a private persisted representation;
8. worker scopes and reserved central files are frozen.

At the current no-CI stop point, items 3, 4, 6 and the structural foundation for 5/7 are established. Items 1 and 2 remain gating; item 5 still requires execution validation on the final head.

## Current stop state

Exact integration head: `d184fdd5b65f2ce0c0e6ca28cd092644be080555`.

**Stop condition reached again.** Until the owner explicitly reports Actions reset:

- do not continue speculative Wave 07 implementation;
- do not open the Wave 07 PR;
- do not dispatch/rerun Actions;
- do not merge Wave 07;
- do not activate Wave 08 workers;
- do not implement Canvas/editor/image-import/renderer functionality.

Resume code only for another newly demonstrated concrete safe correctness blocker, or after the Actions reset permits deliberate typed-persistence work plus full validation.

## After Actions reset

1. Verify real `main`, integration head, PR and CI state.
2. Reconcile the integration branch with current `main`.
3. Finalize canonical typed visual property persistence/migration with compatibility tests.
4. Resolve any remaining reproducibility blocker necessary for trustworthy final validation without broadening functional scope.
5. Open one Wave 07 integration PR when ready to spend CI.
6. Validate the exact final head across Web, backend Release/full PostgreSQL tests, Runtime smoke, Chromium and Wave-specific visual/Python acceptance.
7. Fix root causes only; do not weaken sandbox/security/validation contracts.
8. Merge Wave 07 only fully green.
9. Freeze and activate Wave 08 worker assignments only after every Definition-of-Ready item is satisfied.
