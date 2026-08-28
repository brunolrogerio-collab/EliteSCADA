# EliteSCADA — Visual Canonical Convergence 07 -> 08

Status: **COORDINATOR REVIEW + STATIC AUDIT CORRECTIONS COMPLETE / CI_DEFERRED / STOP CONDITION REACHED**  
Date: 2026-08-28  
Exact reviewed integration head: `63878f6fe28a0a9ac101d622628f8b95658899a7`

This document records the coordinator-only canonical visual convergence and subsequent repository-wide static audit corrections performed while Wave 07 waits for final GitHub Actions validation and before Wave 08 graphical-editor implementation is authorized.

## Decision

Do **not** absorb the full Wave 08 graphical editor into Wave 07.

The no-Actions interval was used for coordinator-only canonical visual convergence/readiness work. DEV 1, DEV 2 and DEV 3 remain stopped.

After the original convergence stop point, a broader static audit found deterministic test-contract drift. Because those were concrete correctness blockers rather than speculative features, the coordinator corrected them on the integration branch. No Canvas/editor/product UI work was authorized or implemented.

## Why this phase existed

The review originally found that the frontend Wave 07 runtime foundation and existing canonical/backend visual contracts were not yet one model:

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

Registry defaults are not materialized as though the engineer explicitly authored them. Compatibility APIs may expose effective design-time values, but runtime source diagnostics distinguish explicit Engineering Base from registry Default.

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

C# and TypeScript now expose the intended common visual property family for:

- geometry/transform;
- visibility/opacity;
- fill/background;
- stroke/style/corner radius;
- text/font/alignment;
- `assetRef`, `imageFit`, and image positioning.

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

### Transitional string-property codec — CONTAINED, NOT FINAL

A schema-guided transition codec exists in both C# and TypeScript for the current string-valued visual property bag.

The declared `VisualObjectPropertySchema` determines the value type. Neither side guesses types from textual contents.

The final static review aligned canonical boolean/numeric text semantics so backend and frontend reject the same non-canonical forms rather than accepting different project representations.

This codec exists to contain the current persistence mismatch. It is **not** the desired final persisted visual model.

## Post-convergence static audit correction

A broader repository audit after the earlier reviewed head `0c00413e2dc96d770a905cf0a416833764af59e7` found deterministic test expectations that no longer matched the already-settled canonical contract:

1. a source-contract test still required an old three-field AssetReference shape containing `assetId`, `name` and `mediaType`;
2. browser acceptance simultaneously treated descriptive asset metadata as valid while another contract test correctly rejected it;
3. Engineering projection tests still called obsolete `createDefaultBaseValues()` even though the public schema API is `createDefaultValues()`;
4. the same projection tests still treated name/MIME metadata as part of a visual asset reference.

These were corrected without weakening product code:

- `cd2753f64a8191df3d2861871bb53077b74cc7a2`
- `c3f9cc15a6715bf6434b1553878ba7c6121e0783`
- `63878f6fe28a0a9ac101d622628f8b95658899a7`

Static search on the resulting Wave 07 delta found no remaining obsolete `createDefaultBaseValues` reference and no remaining three-field AssetReference validity expectation. Metadata-bearing references now appear only as deliberate invalid cases.

The Wave 07 implementation decision in `main` was also clarified to remove older permissive wording that could recreate the same drift.

All of this remains **CI_DEFERRED**. Static correction is not execution proof.

## Deliberately unresolved blocker: typed visual property persistence

`VisualElementEngineeringDto.Properties` still persists as `Dictionary<string,string>` on the current integration branch.

Therefore Schema v11 is a **transitional stable-identity/convergence schema**, not final JSON-native typed visual persistence.

Before Wave 08 becomes ACTIVE, the coordinator must deliberately settle the canonical typed visual property representation and migration/compatibility strategy. The editor must then consume that canonical representation rather than establish its own string/object persistence rules.

The target remains native typed values validated by the public Visual Property Registry, including at least:

- finite number;
- boolean;
- string;
- color string;
- enum string;
- null or stable project `assetRef` object.

Registry Default remains distinct from explicitly engineered base state.

## Repository-wide debt observed during audit

The audit also found non-functional quality/reproducibility debt outside the immediate Wave 07 test blocker:

- frontend dependencies include floating `latest` versions and no committed `package-lock.json`;
- CI uses `npm install` rather than `npm ci`;
- no `global.json` pins the .NET SDK while `LangVersion` is `latest`;
- `main` is not protected by required branch checks;
- API CORS is globally permissive;
- `tests-e2e` are not included in the normal frontend TypeScript build;
- many cheap source/contract tests run only through the expensive Playwright/WebServer path.

These findings should be addressed deliberately in the appropriate hardening/reproducibility scope. They do not authorize speculative functional expansion while Wave 07 CI is frozen.

## Wave 07 final validation boundary

Wave 07 still requires its deferred exact-head CI before merge. This review does not waive or replace:

- Web build;
- backend Release/full PostgreSQL tests;
- Runtime smoke;
- Chromium;
- Wave-specific visual/Python acceptance.

All convergence code and committed tests remain **CI_DEFERRED**. Static review is not compilation or execution evidence.

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

The exact integration head after deterministic static-audit corrections is `63878f6fe28a0a9ac101d622628f8b95658899a7`.

**Stop condition reached again.** Until the owner explicitly reports Actions reset:

- do not continue speculative Wave 07 implementation;
- do not open the Wave 07 PR;
- do not dispatch/rerun Actions;
- do not merge Wave 07;
- do not activate Wave 08 workers;
- do not implement Canvas/editor/image-import/renderer functionality.

Resume code only for another newly discovered concrete safe correctness blocker, or after the Actions reset permits deliberate typed-persistence work plus full validation.

## After Actions reset

1. Verify real `main`, integration head, PR and CI state.
2. Reconcile the integration branch with current `main` if necessary.
3. Finalize canonical typed visual property persistence/migration with compatibility tests.
4. Resolve any remaining reproducibility blocker necessary for trustworthy final validation without broadening functional scope.
5. Open one Wave 07 integration PR when ready to spend CI.
6. Validate the exact final head across Web, backend Release/full PostgreSQL tests, Runtime smoke, Chromium and Wave-specific visual/Python acceptance.
7. Fix root causes only; do not weaken sandbox/security/validation contracts.
8. Merge Wave 07 only fully green.
9. Freeze and activate Wave 08 worker assignments only after every Definition-of-Ready item is satisfied.
