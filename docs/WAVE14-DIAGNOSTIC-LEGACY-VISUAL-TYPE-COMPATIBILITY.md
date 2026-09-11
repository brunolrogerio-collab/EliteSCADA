# Wave 14 — Diagnostic Closure: Persisted Legacy Visual Type Compatibility

**Closure state:** `CONFIRMED_GENERIC_PRODUCT`  
**Priority transferred to Wave 15:** P1  
**Scope:** Screen Editor + Popup Editor dynamic/property authoring paths  
**Decision boundary:** diagnostic/documentation only in Wave 14; correction belongs to Wave 15.

> GitHub live is the official authority. Revalidate current source paths and branch ancestry before implementing the Wave 15 correction.

## 1. Symptom and reproduction evidence

The post-C26 real-browser audit reproduced application blanking when persisted visual elements using legacy identifiers were selected in Screen and Popup authoring. Observed legacy identifiers include `tank`, `value` and `dynamo`.

The failure occurs before the dynamic/property authoring surface can recover normally and correlates with current built-in schema lookup rejecting an element type that is still present in persisted legacy Engineering content.

The canonical audit chronology remains in:

`docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md`

This diagnostic closes the mechanism sufficiently for Wave 15 implementation; it does not claim every historic legacy identifier has already been enumerated.

## 2. Exact confirmed source path

### UI entry point

`web/scada-web/src/engineering/visual-editor/dynamic-property-editor/DynamicPropertyEditor.tsx`

`DynamicPropertyEditor` computes:

```ts
const destinations = useMemo(() => listDynamicPropertyDestinations(element), [element.type]);
```

There is no error boundary, normalization or compatibility recovery between the persisted `element.type` and `listDynamicPropertyDestinations(...)` at this call site.

### Dynamic authoring model

`web/scada-web/src/engineering/visual-editor/dynamic-property-editor/visualDynamicAuthoringModel.ts`

`listDynamicPropertyDestinations(element)` performs:

```ts
const schema = getBuiltinVisualObjectSchema(element.type);
```

The function then enumerates bindable Boolean/numeric property definitions from that schema.

### Built-in schema registry

`web/scada-web/src/visual-runtime/builtinVisualObjectSchemas.ts`

The current registry uses canonical `core.*` identifiers, including:

- `core.group`
- `core.rectangle`
- `core.ellipse`
- `core.line`
- `core.polygon`
- `core.text`
- `core.image`
- `core.valueDisplay`
- `core.trend`
- `core.alarmBrowser`
- `core.eventBrowser`
- `core.button`
- `core.slider`

Legacy simple identifiers such as `value` and `dynamo` are not registry keys. `getBuiltinVisualObjectSchema(objectType)` explicitly throws when no registry entry exists:

```ts
throw new Error(`Unknown built-in visual object type '${objectType}'.`);
```

### Additional mutation exposure

`web/scada-web/src/engineering/visual-editor/visualEditorCanonicalModelLegacy.ts` also calls `getBuiltinVisualObjectSchema(element.type)` in multiple mutation paths such as move, resize, rotate, z-order and property mutation. Therefore the compatibility gap is broader than one rendering symptom: a persisted legacy type can reach strict current-schema operations from multiple authoring actions.

## 3. Root mechanism

The defect is an **Engineering schema compatibility boundary failure**:

1. persisted Engineering content may still contain known historical visual type identifiers;
2. the current authoring model receives the persisted `element.type` as-is;
3. current property/dynamic/mutation code assumes that identifier is already a current built-in registry key;
4. strict `getBuiltinVisualObjectSchema(...)` correctly rejects unregistered identifiers;
5. the UI does not establish a compatibility/migration layer for known legacy identifiers before invoking strict current-schema operations;
6. the thrown error can escape the authoring surface and blank the Screen/Popup application.

The strict unknown-type check itself is **not** the defect. The missing distinction is between:

- a **known legacy identifier** that the product has previously persisted and must deliberately migrate/normalize/degrade; and
- a **truly unknown/unsupported identifier**, which must remain rejected or explicitly degraded with diagnostic visibility rather than silently being accepted as another type.

## 4. Responsible layer

Primary ownership for the Wave 15 correction is the **Visual Engineering schema compatibility/migration boundary**, with coordinated handling in the Screen/Popup authoring surfaces.

The correction must not be implemented as an EEE-specific special case inside one screen, one popup or one demo package.

The preferred architectural direction is to centralize compatibility before strict current-schema consumers rather than scattering aliases across every caller. Exact implementation shape is a Wave 15 design decision after live source revalidation.

## 5. Wave 15 correction contract

Wave 15 must implement a generic version-aware contract with these properties:

1. enumerate every **known persisted legacy visual type identifier** supported by migration/compatibility;
2. map/migrate those identifiers through an explicit, testable compatibility table or migration function;
3. perform normalization at a boundary that covers Screen and Popup authoring and all strict schema consumers that operate on persisted elements;
4. preserve canonical current identifiers in new/updated Engineering state rather than perpetuating unnecessary legacy aliases;
5. preserve element identity, properties, bindings, children, Dynamo metadata and other semantically compatible content during migration;
6. where a legacy type cannot be losslessly mapped, preserve the object and surface an actionable unsupported/migration diagnostic instead of crashing or silently discarding it;
7. keep `getBuiltinVisualObjectSchema(...)` strict for genuinely unsupported current identifiers unless a deliberately designed safe-degradation API is introduced alongside it;
8. ensure one malformed/unsupported persisted element cannot blank the whole Engineering application;
9. apply the same compatibility behavior to Screen and Popup authoring;
10. keep Runtime/Active, lifecycle and security authority unchanged.

`value -> core.valueDisplay` is an obvious candidate mapping suggested by the current catalog naming, but Wave 14 does **not** promote that inference into an implementation fact. Wave 15 must confirm the historical semantics before committing each mapping. The same applies to `tank` and `dynamo`: diagnose their historical representation and migration target rather than guessing from the identifier alone.

## 6. Deterministic regression matrix required in Wave 15

At minimum:

### R1 — Screen / known legacy type

- load persisted Screen content containing a verified known legacy identifier;
- select the element;
- open/use the dynamic/property authoring surface;
- prove no app blank/crash;
- prove expected canonical schema/properties are available or an explicit safe migration diagnostic is shown;
- save/reload and prove the intended canonical representation is stable.

### R2 — Popup / known legacy type

Repeat the equivalent test through Popup authoring. The fix is not accepted if only Screen is covered.

### R3 — mutation paths

For migrated/compatible persisted objects, cover representative operations that currently invoke strict schema lookup: move/resize and at least one property or dynamic-binding mutation.

### R4 — truly unknown type negative case

- inject a deliberately unsupported identifier not present in the legacy compatibility table;
- prove it does **not** become silently valid through a broad fallback;
- prove the UI remains available and exposes actionable diagnostic/degradation behavior;
- prove invalid content is not silently rewritten as an unrelated type.

### R5 — current canonical type

Load and edit a normal current `core.*` object and prove compatibility handling introduces no regression to standard authoring.

## 7. Non-actions / protected boundaries

Wave 14 does not patch this defect.

Wave 15 must not:

- add `tank`, `value` or `dynamo` blindly as permanent first-class current built-in types merely to suppress the exception;
- catch every schema exception and pretend an unknown object is valid;
- map all unknown types to `core.group`, `core.rectangle`, `core.valueDisplay` or another arbitrary fallback;
- delete unsupported persisted objects silently;
- implement an EEE Demo-only migration;
- weaken public Engineering validation;
- alter Runtime Active authority, lifecycle, security, Identity or Licensing to solve an editor compatibility defect.

## 8. Wave 14 closure decision

This finding satisfies the Wave 14 diagnostic-closure bar as `CONFIRMED_GENERIC_PRODUCT` because:

- the user-visible failure was reproduced in real Screen/Popup authoring;
- the responsible subsystem is identified;
- the exact strict lookup chain is identified;
- the strict registry behavior is identified;
- the causal compatibility gap is bounded;
- the Wave 15 correction contract and deterministic regression matrix are explicit.

What remains for Wave 15 is implementation-level historical mapping verification for each legacy identifier and the minimal generic migration/recovery design. Repeating exploratory diagnosis of why strict schema lookup crashes on a known legacy identifier should not be necessary.