# Visual Boolean Conditions and Analog Fill — EliteSCADA

Status: **LOCKED PRODUCT DIRECTION / IMPLEMENTATION DEFERRED UNTIL CURRENT WAVE 08 WORKER SLICES DELIVER**  
Date: 2026-08-28

This document locks a product requirement added during Wave 08 without broadening the three already-active worker assignments. It is a mandatory graphical-visual follow-up before Wave 09 is activated.

The objective is to keep common HMI behavior declarative, canonical, importable/exportable and usable without Python.

## 1. Scope and timing

The current DEV 1 Canvas, DEV 2 Property Inspector and DEV 3 Palette/Binding assignments remain unchanged.

After those slices are delivered and integrated, the coordinator must plan/assign the implementation described here before formally advancing to Wave 09.

This requirement must not be implemented as renderer-only state, private React state, opaque `metadata`, CSS tricks or Python-only behavior.

Canonical Engineering remains saved truth.

## 2. Universal `visible` property

Every renderable visual object used in a Screen, Popup or Dynamo must expose the public boolean property:

`visible: boolean`

This includes built-in shapes, text, image, buttons, value displays, groups/containers and future renderable object types unless a future object is explicitly defined as non-rendering infrastructure.

`visible` follows the same public Visual Property Registry rules as other properties:

- versioned public key;
- Engineering base/default value;
- runtime readability/writability according to the schema;
- canonical binding support;
- script/animation participation according to normal precedence;
- import/export/revision/package fidelity.

The default remains `true` unless an object schema deliberately specifies otherwise.

## 3. Generic Boolean Condition capability

The requirement is broader than `visible`.

**Every visual property whose public schema type is boolean must be able to receive a boolean result from the binding layer.**

The first implementation must support at least two evaluation forms.

### 3.1 Direct boolean

A boolean source can drive the destination boolean property directly.

Conceptually:

`destination = sourceBoolean`

An explicit invert/negate option may produce:

`destination = NOT sourceBoolean`

There must be no implicit conversion such as `0/1`, strings or arbitrary objects silently becoming booleans.

### 3.2 Numeric interval condition

A numeric source can be evaluated against a configured interval and converted deterministically to a boolean result.

Required interval semantics:

- optional lower bound;
- optional upper bound;
- lower bound inclusive/exclusive;
- upper bound inclusive/exclusive;
- one-sided ranges are valid;
- mode `inside` or `outside`;
- at least one bound is required;
- invalid/non-numeric configuration fails validation rather than coercing.

Examples:

- `visible = true when 20 <= Level <= 80`;
- `enabled = true when Pressure > 4.5`;
- `alarmIndicatorVisible = true when Temperature is outside 0..70`.

The exact DTO/property names are an implementation decision, but the condition must be a first-class typed/versioned canonical Engineering contract, not an undocumented `metadata` convention.

## 4. Runtime/quality behavior for Boolean Conditions

Boolean condition evaluation belongs **inside the Binding/Expression layer**. It does not create a new precedence layer.

The existing property precedence remains:

`Animation > Script > Binding/Expression > Engineering Base > Default`

If the bound source is unavailable, wrong type, unresolved, or does not have usable quality, the condition must not invent a boolean value. The Binding/Expression layer is treated as unavailable for that evaluation and the property falls back through the normal lower-precedence source, with diagnostics.

This is preferable to silently forcing `false`, especially for visibility, because hiding an object due to communication failure can conceal important process information.

Client Memory follows its existing client-local quality/value semantics and must not be promoted to server process truth.

## 5. Editor behavior for boolean properties

The Property Inspector must eventually present boolean properties with an explicit source mode rather than forcing developers into Python for common conditions.

Minimum conceptual modes:

- Engineering constant: `true` / `false`;
- direct boolean binding;
- numeric interval condition.

The UI must make the active source and interval limits understandable and must validate unsupported source types before Apply.

Changing editor controls never bypasses canonical Preview/Apply/CAS/revision behavior.

## 6. Analog Fill capability

Closed visual objects that explicitly declare fill support must be able to display an analog value as a proportional filled region.

Initial eligible family includes at least:

- rectangle;
- ellipse/circle;
- future closed geometric/process shapes that opt into the same capability.

Eligibility must be declared by the public visual-object/property schema. Do not infer it from DOM/CSS class names or renderer implementation.

Objects such as line, text, image or arbitrary groups are not automatically fill-capable merely because the renderer could draw a colored rectangle around them.

## 7. Analog Fill scaling

Analog Fill consumes a numeric source and maps an engineered input range to a normalized visual percentage.

Required behavior:

- configured input minimum;
- configured input maximum;
- input minimum and maximum must not be equal;
- value maps deterministically to `0..100%`;
- clamping is enabled by default so out-of-range values do not produce unbounded geometry;
- reverse/inverted fill must be explicit rather than relying on accidental negative geometry;
- invalid source/type/scale fails closed with diagnostics.

Conceptually, for the normal ascending case:

`percent = clamp((value - inputMin) / (inputMax - inputMin), 0, 1) * 100`

The exact internal representation may use `0..1` or `0..100`, but the public Engineering/UI semantics must be unambiguous and portable.

## 8. Fill direction

The first practical implementation must support deterministic directions:

- bottom -> top;
- top -> bottom;
- left -> right;
- right -> left.

Future radial/sector/path fills are allowed later but are not required for the first implementation.

Fill direction is canonical Engineering configuration, not transient renderer state.

## 9. Fill colors

The proportional filled region must have an explicit fill color, while the unfilled portion retains the object's configured base/background appearance.

Minimum first implementation:

- base/unfilled appearance from the object's normal Engineering properties;
- explicit filled-region color;
- analog percentage controls how much of the geometry uses the filled-region appearance.

The architecture must not prevent later threshold bands or color-gradient stops, but multi-band/gradient color mapping is **not required by this first locked follow-up** unless separately promoted.

Normal color properties remain independently bindable/scriptable/animatable according to their own public property contracts.

## 10. Canonical authority and persistence

Boolean Conditions and Analog Fill configuration must participate in the normal Engineering lifecycle:

- canonical JSON import/export;
- validation and Preview/Apply;
- Working state;
- immutable revisions;
- PostgreSQL persistence;
- `.escadapkg` backup/restore;
- copy/paste / future Engineering Fragments;
- reusable Dynamo dependencies where applicable;
- public version migration.

Runtime-effective condition results and calculated fill percentages are presentation state and are **not** written back into saved Engineering merely because Runtime evaluated them.

## 11. Python and runtime interoperability

Python is not required for ordinary boolean conditions or analog fills.

Python may still read/write/animate public visual properties according to normal capabilities and precedence.

A script must see stable public property/behavior semantics rather than renderer-private fill clips, CSS masks or internal condition objects.

## 12. Diagnostics

Runtime/Engineering diagnostics should identify, where applicable:

- destination property;
- active source kind;
- source reference;
- condition mode;
- invalid bound/type/range configuration;
- unavailable source/quality;
- effective property source after precedence resolution;
- Analog Fill source, scale and resulting normalized percentage when useful for troubleshooting.

## 13. Acceptance requirements for the follow-up implementation

Before this requirement is considered implemented, automated acceptance must prove at least:

1. every built-in renderable object exposes `visible`;
2. a boolean TAG/Client Memory source can directly control a boolean visual property;
3. a numeric source can control `visible` through an inside range;
4. one-sided and outside-range conditions behave deterministically;
5. bad/unavailable source does not silently force false and falls back according to normal precedence;
6. import/export and revision/package round-trip preserve condition configuration;
7. rectangle Analog Fill maps configured engineering values to 0%, intermediate percentage and 100%;
8. clamping and all four initial fill directions work;
9. ellipse/circle uses the same canonical Analog Fill contract rather than a separate object-private implementation;
10. save/reopen/export/import preserve Analog Fill configuration;
11. Runtime-calculated boolean/fill state is not persisted as Engineering base state;
12. existing Python/binding/property-precedence regressions remain green.

## 14. Explicit non-goals of the first follow-up

Not required initially:

- arbitrary formula/expression language beyond existing expression direction and the numeric interval predicate described here;
- multi-condition AND/OR trees;
- hysteresis/debounce for visual conditions;
- radial/path/tank-shape fill geometries beyond objects explicitly supported;
- threshold color bands or gradient color stops;
- safety/interlock logic based on client visual conditions.

Those may be added through later typed contracts. Visual conditions are presentation behavior and must never become an industrial safety/interlock authority.
