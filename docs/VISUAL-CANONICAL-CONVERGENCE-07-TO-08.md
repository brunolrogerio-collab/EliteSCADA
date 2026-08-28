# EliteSCADA — Visual Canonical Convergence 07 -> 08

Status: **COORDINATOR FOURTH STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED / STOP CONDITION REACHED**  
Date: 2026-08-28  
Exact reviewed integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`

This document records coordinator-only visual convergence and correctness hardening performed while Wave 07 waits for final GitHub Actions validation and before Wave 08 graphical-editor implementation is authorized.

## Decision

Do **not** absorb the full Wave 08 graphical editor into Wave 07.

The no-Actions interval is restricted to canonical convergence/readiness and newly demonstrated correctness blockers. DEV 1, DEV 2 and DEV 3 remain stopped. No Canvas, Property Inspector, Object Palette, renderer or image-import functionality is authorized yet.

## Canonical convergence settled structurally

### Stable visual identity

The integration branch uses transitional Engineering **Schema v11**. `VisualElementEngineeringDto` has an optional stable `Guid? Id`. Missing IDs from legacy inputs are materialized/preserved by the view registry; empty/duplicate IDs are rejected across a visual definition.

Older schema versions remain readable. Merged `main` remains Schema v10 until Wave 07 is validated and merged.

The fourth audit also aligned prospective Script-reference validation with the same Screen/Popup identity rules used by Apply: existing resolved definition identity is preserved, legacy v10 child elements inherit existing IDs by unique sibling key when applicable, and removed referenced children disappear from the prospective catalog before Apply.

### Visual binding target semantics

For bindings attached to a visual element:

- `EngineeringBindingDto.Key` = destination visual property/slot;
- `EngineeringBindingDto.Target` = source TAG/property/expression reference;
- `Kind` identifies source interpretation.

No redundant editor-only target field was introduced.

### Runtime source semantics

C# and TypeScript preserve:

`Animation > Script > Binding/Expression > Engineering Base > Default`.

Registry defaults remain distinct from explicitly engineered values.

### Frontend Engineering -> Runtime seam

Screen/Popup visual elements are typed, not `unknown[]`. The official adapter requires stable ID, declared object schema, shared transition codec and canonical bindings, preserves parent identity and does not create editor-private persisted state.

### Property registry and built-in object schemas

C# and TypeScript share the intended common visual property family and renderer-independent built-in types:

- `core.group`
- `core.rectangle`
- `core.ellipse`
- `core.line`
- `core.text`
- `core.image`
- `core.valueDisplay`
- `core.button`

TypeScript integer visual properties use the same signed Int32 domain as C# `VisualIntegerValue(int)`.

Backend Preview validates known `core.*` properties/binding capabilities and fails closed for unknown built-in types. Non-core future custom/Dynamo/plugin types remain extensible.

### AssetReference

Canonical visual property semantics are:

`assetRef = null | { assetId }`

The reference carries only project-owned stable identity. Asset name, original filename, MIME, dimensions, hash and payload metadata belong to the future first-class asset entity. Paths/URLs are not valid visual asset references.

The legacy `imageResourceId`/`ResourceReference` C# surface remains compatibility only.

### Client Visual Python property authority

Wave 07 structurally supports read plus explicit Script-override set/clear within the exact current Runtime Visual Instance.

Bridge v1 retains the existing `visualProperty.write` capability with operations:

- `write`: target + property + value -> `setScriptOverride`;
- `clear`: target + property -> `clearScriptOverride`.

`null` is not a clear sentinel. The actual Worker module exposes `elite_scada.visual_property_clear(target, property)` and routes it through the trusted capability dispatcher. Target, current-instance, lifecycle, registration and runtime-writable checks remain authoritative.

### Malformed Engineering fail-closed Preview

The third audit found that JSON-deserialized null visual nodes/bindings/fields could be diagnosed generically and then dereferenced by later validation stages. The full nested visual Preview path was made null-safe.

The fourth audit extended fail-closed handling to the remaining top-level view and Script paths:

- null Screen/Popup entries no longer reach duplicate/reference/existing-resolution code;
- null/blank top-level view keys stay on the `ImportIssue` path;
- null Script definitions produce `SCRIPT_NULL`;
- null Script visual-event references produce `SCRIPT_VISUAL_REFERENCE_NULL`;
- malformed Script collections cannot Preview green and then fail during Apply.

Direct and full `EngineeringExchangeService.Preview` tests are committed. Execution proof remains deferred.

### Canonical Script -> VisualObject reference convergence

Wave 05 Script Engineering already defined stable visual-object references as `definitionId/objectId`. Wave 07 supplied the missing stable nested object identities, but the real Engineering reference catalog initially registered only visual definitions.

The fourth audit corrected that cross-wave gap:

- `ScriptEngineeringDependencyKind.VisualObject` can resolve canonical Screen/Popup nested object IDs;
- object-scoped `ScriptVisualEventReference.VisualObjectId` is checked against the same catalog;
- `ScriptEngineeringReferenceResolver.FromEngineeringPackage` recursively derives nested visual object references from canonical package data;
- prospective validation now surfaces failures attached to already-saved Scripts when incoming TAG/Data Source/visual changes invalidate their dependencies;
- v10 Screen/Popup updates that preserve a child by unique key retain its stable ID for validation, while removing the child invalidates the Script before Apply.

No renderer or DOM authority was introduced by this reference convergence.

## Transitional string-property codec

A schema-driven codec exists in C# and TypeScript because canonical visual Engineering still stores `Dictionary<string,string>`.

The codec is containment, not the desired final model. Accepted boolean/numeric forms are aligned, but C# `double.ToString("R")` and JavaScript `String(number)` can still spell some exponent values differently. Do not treat this transitional format as cross-runtime byte-canonical.

## Static audit correction history

### Asset/property test drift

An earlier audit corrected deterministic stale expectations around three-field AssetReferences and obsolete `createDefaultBaseValues()` calls. That correction sequence ended at:

`63878f6fe28a0a9ac101d622628f8b95658899a7`

### Third audit hardening

The third audit found four additional blockers:

1. actual Python Worker had no visual-property clear function even though Runtime could clear Script overrides;
2. malformed/null nested visual JSON could throw in later Preview layers;
3. TypeScript integer validation exceeded the C# Int32 domain;
4. `python-sandbox-foundation.spec.ts` still expected the old exact Wave 06 policy and omitted Wave 07 `maxBridgeDepth`, `maxBridgeNodes` and `maxBridgeStringLength`, guaranteeing a Chromium failure.

All four were corrected without weakening sandbox/security authority. Exact integration head after that hardening:

`d184fdd5b65f2ce0c0e6ca28cd092644be080555`

### Fourth audit hardening

The fourth audit found additional deterministic and referential-integrity blockers:

1. top-level malformed Screen/Popup entries could still escape fail-closed Preview;
2. three current-export Script tests still hard-coded schema v10 after current Engineering moved to transitional v11;
3. stable nested `VisualObject` IDs were absent from the actual Script Engineering reference catalog;
4. the package-derived public reference resolver had the same nested-object gap;
5. null Script/reference collections could survive Preview handling and fail later in Apply;
6. validation already computed errors on existing Scripts invalidated by incoming Engineering changes but the handler discarded them;
7. legacy v10 Screen/Popup object identity used by prospective Script validation did not fully mirror registry/Apply preservation semantics;
8. prospective Screen/Popup/Dynamo definition identity precedence did not initially match the real import handlers.

All were corrected on the integration branch with focused source-level tests. Exact integration head after this hardening:

`e376b37d2772906dd667afa199a6d8882abd43ae`

All corrections remain **CI_DEFERRED**.

## Reviewed hypotheses deliberately not changed

- Bare historical visual `Type` strings such as `group`/`text` were not automatically aliased to `core.group`/`core.text`, because the legacy type surface was extensible and could represent custom types.
- Script-only mutation packages remain schema v10 intentionally. They do not carry the v11 visual-ID surface and continue to exercise a supported compatibility boundary.
- C# Runtime visual `Clear*` methods do not re-check every corresponding write/binding/animation flag as strictly as the TypeScript path. No current public capability can create a forbidden layer and then use clear as stronger authority, so this remains semantic-convergence debt rather than a current security blocker.

## Deliberately unresolved blocker: typed visual property persistence

`VisualElementEngineeringDto.Properties` still persists as `Dictionary<string,string>`. Schema v11 is therefore a transitional identity/convergence schema, not final typed persistence.

Before Wave 08 becomes ACTIVE, settle and validate canonical JSON-native typed visual property representation and migration/compatibility. The editor must consume that canonical model rather than establish a second private persistence format.

## Lower-priority binding/source and import hardening

Canonical Engineering distinguishes `Tag`, `Property` and `Expression`. The current frontend projection reduces `Tag` and `Property` to generic Runtime `binding` while values are externally resolved.

That is not the present Wave 07 execution blocker, but sufficient source-kind discrimination must be retained before the graphical binding engine resolves references itself.

Older Engineering collections also retain broader null/empty-ID robustness debt. The fourth audit corrected the concrete visual/Script paths it demonstrated, but did not expand the frozen Wave 07 scope into a repository-wide rewrite of every historical import handler.

## Repository-wide debt observed during audit

Tracked separately from current Wave 07 scope:

- floating frontend `latest` dependencies and no committed `package-lock.json`;
- CI using `npm install` rather than `npm ci`;
- no `global.json` while .NET language version is `latest`;
- unprotected `main`;
- globally permissive API CORS;
- `tests-e2e` outside normal frontend typecheck;
- cheap source/contract tests coupled to expensive Playwright/WebServer execution.

These findings do not authorize speculative functional expansion during the CI freeze.

## Main/integration reconciliation fact

Every current `main` change after the Wave 06 merge is coordination/documentation-only. No functional `main` code delta is hidden from the Wave 07 integration branch.

Historical reconciliation with current `main` remains mandatory before final exact-head CI.

## Wave 07 final validation boundary

Static review does not replace:

- Web build;
- backend Release/full PostgreSQL tests;
- Runtime smoke;
- Chromium;
- Wave-specific visual/Python acceptance;
- Wave 06 sandbox/native-escape/cancellation regressions.

If typed-persistence work changes the product head after Actions reset, final CI must validate that exact later head.

## Wave 08 Definition of Ready

Wave 08 may be activated only when:

1. Wave 07 final integration is green and merged;
2. canonical typed visual property persistence/migration is settled;
3. stable visual definition/object identity is settled;
4. visual-property binding target semantics are settled;
5. frontend/backend property registry parity is settled and validated;
6. asset reference identity is sufficient for the Image/import slice;
7. the editor can consume canonical visual definitions without private persistence;
8. worker scopes and reserved central files are frozen.

Items 3, 4 and 6 are structurally settled; the foundation for 5/7 exists. Items 1 and 2 remain gating, and item 5 still needs execution validation.

## Current stop state

Exact integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`.

**Stop condition reached again.** Until explicit owner report of Actions reset:

- no speculative Wave 07 implementation;
- no Wave 07 PR;
- no Actions dispatch/rerun;
- no Wave 07 merge;
- no Wave 08 workers;
- no Canvas/editor/image-import/renderer work.

Resume code only for another demonstrated safe correctness blocker, or after reset permits typed-persistence work and full validation.

## After Actions reset

1. Verify real `main`, integration head, PR and CI state.
2. Reconcile integration with current `main`.
3. Finalize typed visual persistence/migration with compatibility tests.
4. Resolve reproducibility blockers necessary for trustworthy final validation.
5. Open one Wave 07 PR only when ready to spend CI.
6. Validate exact final head across Web, backend Release/full PostgreSQL, Runtime smoke, Chromium and Wave-specific visual/Python acceptance.
7. Fix root causes only; do not weaken security/validation.
8. Merge Wave 07 only fully green.
9. Activate Wave 08 only after every Definition-of-Ready item is satisfied.
