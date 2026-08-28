# EliteSCADA — Visual Canonical Convergence 07 -> 08

Status: **COORDINATOR FOURTH STATIC AUDIT COMPLETE / CI AVAILABLE / PRE-FINALIZATION**  
Date: 2026-08-28  
Exact reviewed integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`

This document records coordinator-owned convergence between Wave 07 and Wave 08. The graphical editor is still not authorized until Wave 07 is finalized, validated and merged.

## Decision

Do **not** absorb the full Wave 08 graphical editor into Wave 07.

The previous no-Actions freeze is lifted after an owner-reported Actions allowance increase and a successful hosted-runner Web-build probe. CI capacity is available again, but it does not change the dependency order. DEV 1, DEV 2 and DEV 3 remain stopped until Wave 08 is explicitly activated.

## Actions availability evidence

On 2026-08-28 the owner reported a GitHub configuration change expected to provide roughly 1000 additional Actions minutes. Repository tooling cannot read the billing balance directly.

Availability was verified by rerunning only the historical Wave 06 Web-build job from run #488:

- attempt 2;
- job `98990802140`;
- hosted runner allocated;
- Checkout/setup/install/build all SUCCESS.

That probe proves Actions availability only. It is not Wave 07 product-validation evidence.

## Canonical convergence settled structurally

### Stable visual identity

The integration branch uses transitional Engineering **Schema v11**. `VisualElementEngineeringDto` has optional stable `Guid? Id`; legacy missing IDs are materialized/preserved through registry rules; empty/duplicate IDs are rejected across a visual definition.

Prospective Script-reference validation mirrors Screen/Popup Apply identity rules, including legacy v10 child-ID preservation by unique sibling key and removal of references when a child disappears.

### Visual binding semantics

For bindings attached to a visual element:

- `EngineeringBindingDto.Key` = destination visual property/slot;
- `EngineeringBindingDto.Target` = source TAG/property/expression reference;
- `Kind` identifies source interpretation.

No redundant editor-private target field exists.

### Runtime source semantics

C# and TypeScript preserve:

`Animation > Script > Binding/Expression > Engineering Base > Default`.

Registry defaults remain distinct from explicitly engineered values.

### Frontend Engineering -> Runtime seam

Screen/Popup visual elements are typed. The official adapter requires stable ID, declared schema, shared transition codec and canonical bindings, preserves parent identity and does not create editor-private persisted state.

### Property registry and built-ins

C# and TypeScript structurally align the common visual-property family and renderer-independent built-in types:

- `core.group`
- `core.rectangle`
- `core.ellipse`
- `core.line`
- `core.text`
- `core.image`
- `core.valueDisplay`
- `core.button`

TypeScript integer visual properties use the same signed Int32 domain as C#.

### AssetReference

Canonical visual property semantics remain:

`assetRef = null | { assetId }`

Only project-owned stable identity belongs in the reference. Name, original filename, MIME, dimensions, hash and payload belong to the future first-class asset entity. Paths/URLs are rejected.

### Client Visual Python property authority

Wave 07 supports read plus explicit Script-override set/clear within the exact current Runtime Visual Instance.

Bridge v1 keeps the existing `visualProperty.write` capability with operations `write` and `clear`. `null` is not a clear sentinel. The Worker exposes `elite_scada.visual_property_clear(target, property)` through the trusted dispatcher.

### Fail-closed Engineering Preview

Static audits closed demonstrated malformed visual/Script paths:

- null nested visual nodes/bindings;
- null/blank binding keys/targets;
- null Screen/Popup entries;
- null/blank view keys;
- null Script definitions;
- null Script visual-event references;
- no Preview-green/Apply-crash path for those demonstrated cases.

### Canonical Script -> VisualObject references

Wave 07 closes the Wave 05/07 reference gap:

- `VisualObject` dependencies resolve stable `definitionId/objectId` references;
- object-scoped visual-event references use the same catalog;
- `ScriptEngineeringReferenceResolver.FromEngineeringPackage` recursively derives nested Screen/Popup object references;
- existing Scripts invalidated by incoming Engineering changes surface blocking Preview errors;
- v10 Screen/Popup updates preserve referenced child identity when Apply would preserve it and reject removed referenced children before Apply.

## Transitional typed-persistence blocker

`VisualElementEngineeringDto.Properties` still persists as `Dictionary<string,string>`.

The schema-driven C#/TypeScript codec contains this mismatch but is not the desired final persistence model. Before Wave 08 becomes ACTIVE, settle and validate canonical JSON-native typed visual property representation and migration/compatibility.

The editor must consume canonical typed Engineering rather than establish a second private persistence model.

## Reproducibility readiness

Before spending the final Wave 07 matrix, deliberately settle the minimum reproducibility blockers needed for trustworthy evidence. Audit findings include:

- frontend floating `latest` dependencies;
- no committed `package-lock.json`;
- CI currently using `npm install` rather than `npm ci`;
- no `global.json` while .NET SDK/compiler selection floats;
- `tests-e2e` outside normal frontend typecheck.

These findings do not authorize unrelated refactoring. Fix only what is justified for deterministic final validation.

## Lower-priority convergence debt

- transitional C# `double.ToString("R")` and JavaScript `String(number)` may spell some exponent values differently;
- canonical `Tag` versus `Property` binding source discrimination should remain available before the graphical binding engine resolves sources itself;
- older Engineering collections retain broader null/empty-ID hardening debt;
- production CORS and `main` branch protection remain product/repository hardening items.

## Main/integration reconciliation fact

Functional Wave 07 code remains on `integration/visual-runtime-wave-07`. Current `main` movement after Wave 06 is coordination/documentation-only, but historical reconciliation with current `main` remains mandatory before final exact-head CI.

## Wave 07 final validation boundary

Static review does not replace:

- Web build;
- backend Release/full PostgreSQL tests;
- Runtime smoke;
- Chromium;
- Wave-specific visual/Python acceptance;
- Wave 06 sandbox/native-escape/cancellation regressions.

If typed-persistence or reproducibility work changes the product head, final CI must validate that exact later head.

## Wave 08 Definition of Ready

Wave 08 may be activated only when:

1. Wave 07 final integration is green and merged;
2. canonical typed visual property persistence/migration is settled;
3. stable visual definition/object identity is settled;
4. visual-property binding target semantics are settled;
5. frontend/backend property-registry parity is validated;
6. asset-reference identity is sufficient for Image/import;
7. the editor can consume canonical visual definitions without private persistence;
8. worker scopes and reserved central files are frozen.

Items 3, 4 and 6 are structurally settled. Items 1, 2 and execution validation for 5/7 remain gating.

## Current coordinator state

Exact integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`.

**CI is available. Finalization may resume.**

Next coordinator execution should:

1. verify current `main`/integration/PR/CI state;
2. reconcile integration with current `main`;
3. finalize typed visual property persistence/migration with compatibility tests;
4. resolve the minimum reproducibility blockers required for trustworthy final validation;
5. prepare the exact final candidate head;
6. open one Wave 07 integration PR;
7. execute the full required matrix;
8. fix root causes only;
9. merge Wave 07 only fully green;
10. activate Wave 08 only after every Definition-of-Ready item is satisfied.
