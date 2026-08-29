# Visual Expressions, Boolean Conditions and Analog Fill — EliteSCADA

Status: **LOCKED PRODUCT DIRECTION / IMPLEMENTATION DEFERRED UNTIL CURRENT WAVE 08 WORKER SLICES DELIVER**  
Date: 2026-08-28

This document locks graphical-visual requirements added during Wave 08 without broadening the three already-active worker assignments. It is a mandatory graphical-visual follow-up before Wave 09 is activated.

The objective is to keep common HMI behavior declarative, canonical, importable/exportable, deterministic and usable without Python.

## 1. Scope and timing

The current DEV 1 Canvas, DEV 2 Property Inspector and DEV 3 Palette/Binding assignments remain unchanged.

After those slices are delivered and integrated, the coordinator must plan/assign the implementation described here before formally advancing to Wave 09.

This requirement must not be implemented as renderer-only state, private React state, opaque `metadata`, CSS tricks, arbitrary JavaScript evaluation or Python-only behavior.

Canonical Engineering remains saved truth.

## 2. Universal `visible` property

Every renderable visual object used in a Screen, Popup or Dynamo must expose the public boolean property:

`visible: boolean`

This includes built-in shapes, text, image, buttons, value displays, groups/containers and future renderable object types unless a future object is explicitly defined as non-rendering infrastructure.

`visible` follows the same public Visual Property Registry rules as other properties:

- versioned public key;
- Engineering base/default value;
- runtime readability/writability according to the schema;
- canonical binding/expression support;
- script/animation participation according to normal precedence;
- import/export/revision/package fidelity.

The default remains `true` unless an object schema deliberately specifies otherwise.

## 3. Typed visual expressions

Visual properties that declare Binding/Expression support must be able to receive the result of a **typed, side-effect-free expression** over canonical runtime data sources.

The expression system is part of the Binding/Expression layer. It is not Python and does not create a new property-precedence layer.

The existing precedence remains:

`Animation > Script > Binding/Expression > Engineering Base > Default`

### 3.1 First required source family

The first implementation must support expression dependencies from at least:

- canonical TAGs;
- Client Memory values where valid for that client-local visual context.

A later extension may allow other explicitly supported canonical sources, but expressions must never gain direct driver/database/network/DOM access.

The editor may display friendly TAG names/paths, but saved Engineering must preserve deterministic dependency resolution. An expression must not depend only on an ambiguous display label that can silently resolve to another TAG after rename/import.

The exact serialized representation is an implementation decision, but canonical Engineering must retain enough dependency information to validate references, migrate them, export/import them and diagnose missing sources.

### 3.2 Result type must match destination type

Expressions are typed.

- a boolean property requires a boolean result;
- a numeric property requires a numeric result;
- no string/object/number is silently treated as boolean;
- no boolean is silently treated as a number.

Explicit conversion functions may be provided where useful and deterministic.

Initial required conversions:

- `bool(number)` -> `false` when numeric value is exactly zero, otherwise `true`;
- `number(boolean)` -> `0` for false and `1` for true.

These conversions must be explicit in the expression so Engineering intent is visible.

### 3.3 Boolean operators

The first expression implementation must support:

- `and`;
- `or`;
- `not`;
- parentheses;
- `==` and `!=`;
- numeric comparisons `<`, `<=`, `>`, `>=` when operands are numeric.

Examples for a boolean destination such as `visible`:

`falha_inversor1 or falha_bomba1`

`falha_inversor1 and falha_bomba1`

`not permissivo_bomba1`

`(nivel1 > 80) or falha_bomba1`

If `falha_inversor1` and `falha_bomba1` are numeric 0/1 TAGs rather than boolean TAGs, arithmetic may be used but conversion to the final boolean result remains explicit:

`bool(falha_inversor1 + falha_bomba1)`

or:

`(falha_inversor1 + falha_bomba1) > 0`

If they are boolean TAGs, the correct direct expression is normally:

`falha_inversor1 or falha_bomba1`

### 3.4 Numeric operators

For numeric destinations and numeric intermediate calculations, the first implementation must support at least:

- addition `+`;
- subtraction `-`;
- multiplication `*`;
- division `/`;
- remainder/modulo `%` where numeric type rules allow it;
- unary plus/minus;
- parentheses and normal mathematical precedence.

Required examples:

`(nivel1 + nivel2) * 3`

`(pressao_saida - pressao_entrada) / 2`

`nivel1 * 100 / nivel_maximo`

The expression result may drive any compatible numeric visual property that declares binding support, such as position, dimensions, opacity, rotation, numeric display values or Analog Fill input.

### 3.5 Initial pure numeric functions

The first practical implementation should provide a small whitelist of deterministic pure helpers because they remove common reasons to fall back to Python:

- `abs(x)`;
- `min(a, b, ...)`;
- `max(a, b, ...)`;
- `clamp(value, min, max)`;
- `round(x)`;
- `floor(x)`;
- `ceil(x)`;
- explicit `bool(x)` and `number(x)` conversions described above.

This is a whitelist, not a general function-call mechanism.

No expression may invoke arbitrary user code.

### 3.6 Expression safety and determinism

The expression engine must use a dedicated parser/evaluator or equivalent constrained typed representation.

Forbidden:

- JavaScript `eval`/`Function`;
- Python evaluation;
- arbitrary reflection/dynamic invocation;
- assignment/mutation;
- loops;
- filesystem/network/driver/database/DOM access;
- arbitrary function calls;
- non-deterministic functions such as unrestricted random/time access unless a future explicit canonical source is defined.

The evaluator must be bounded by reasonable limits such as expression length, token count, AST depth and operation count so malformed or intentionally pathological expressions cannot freeze a client.

Division by zero, non-finite arithmetic, missing dependencies and invalid operations fail the expression evaluation with diagnostics. They must not quietly produce `NaN`, Infinity or coerced fallback values inside the expression itself.

## 4. Generic Boolean Condition capability

Every visual property whose public schema type is boolean must be able to receive a boolean result from the Binding/Expression layer.

The editor must support simple presets in addition to free typed expressions, because common HMI conditions should not require hand-writing formulas.

### 4.1 Direct boolean preset

A boolean source can drive the destination boolean property directly.

Conceptually:

`destination = sourceBoolean`

An explicit invert/negate option may produce:

`destination = not sourceBoolean`

### 4.2 Numeric interval preset

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

The interval UI is a convenient structured authoring form over the same typed Binding/Expression semantics, not a competing runtime engine.

The exact DTO/property names are an implementation decision, but the condition/expression configuration must be first-class typed/versioned canonical Engineering, not an undocumented `metadata` convention.

## 5. Runtime dependency and quality behavior

Expression dependencies are reactive: when a referenced TAG/Client Memory value changes, the affected visual Binding/Expression is reevaluated according to normal runtime scheduling/coalescing rules.

If any required source is unavailable, unresolved, wrong type or does not have usable quality, the expression must not invent a result.

For that evaluation the Binding/Expression layer is treated as unavailable and the property falls back through the normal lower-precedence source, with diagnostics.

This is especially important for `visible`: communication failure must not silently hide an object by coercing the result to `false`.

Client Memory follows its existing client-local value semantics and must not be promoted to server process truth.

If the future expression model permits references to other visual properties, dependency cycles must be rejected deterministically. The first implementation does not need visual-property-to-visual-property expressions to satisfy this contract.

## 6. Property Inspector / expression editor behavior

The Property Inspector must eventually present an explicit source mode for bindable properties.

For boolean properties, minimum conceptual modes are:

- Engineering constant `true` / `false`;
- direct boolean binding;
- numeric interval condition;
- typed expression.

For numeric properties, minimum conceptual modes are:

- Engineering constant;
- direct numeric binding;
- typed numeric expression.

The editor must provide:

- TAG/Client Memory reference insertion or autocomplete from the canonical source catalog;
- expression syntax validation before Apply;
- resolved dependency/type validation;
- understandable errors with location where practical;
- preview/effective-result diagnostics where practical;
- no bypass around canonical Preview/Apply/CAS/revision behavior.

The expression language keywords/functions are stable technical tokens and are not translated with the UI locale.

## 7. Analog Fill capability

Closed visual objects that explicitly declare fill support must be able to display a numeric value as a proportional filled region.

Initial eligible family includes at least:

- rectangle;
- ellipse/circle;
- future closed geometric/process shapes that opt into the same capability.

Eligibility must be declared by the public visual-object/property schema. Do not infer it from DOM/CSS class names or renderer implementation.

Objects such as line, text, image or arbitrary groups are not automatically fill-capable merely because the renderer could draw a colored rectangle around them.

## 8. Analog Fill source and scaling

Analog Fill accepts a compatible numeric Binding/Expression result, not only a single raw TAG.

Therefore an engineered fill may use, for example:

`(nivel1 + nivel2) / 2`

before normal fill scaling is applied.

Required scaling behavior:

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

## 9. Fill direction

The first practical implementation must support deterministic directions:

- bottom -> top;
- top -> bottom;
- left -> right;
- right -> left.

Future radial/sector/path fills are allowed later but are not required for the first implementation.

Fill direction is canonical Engineering configuration, not transient renderer state.

## 10. Fill colors

The proportional filled region must have an explicit fill color, while the unfilled portion retains the object's configured base/background appearance.

Minimum first implementation:

- base/unfilled appearance from the object's normal Engineering properties;
- explicit filled-region color;
- analog percentage controls how much of the geometry uses the filled-region appearance.

The architecture must not prevent later threshold bands or color-gradient stops, but multi-band/gradient color mapping is not required by this first locked follow-up unless separately promoted.

Normal color properties remain independently bindable/scriptable/animatable according to their own public property contracts.

## 11. Canonical authority and persistence

Expression, Boolean Condition and Analog Fill configuration must participate in the normal Engineering lifecycle:

- canonical JSON import/export;
- validation and Preview/Apply;
- Working state;
- immutable revisions;
- PostgreSQL persistence;
- `.escadapkg` backup/restore;
- copy/paste / future Engineering Fragments;
- reusable Dynamo dependencies where applicable;
- public version migration.

Canonical persistence must include expression source/configuration plus deterministic source dependencies/references required for validation and portability.

Runtime-effective expression results, condition booleans and calculated fill percentages are presentation state and are **not** written back into saved Engineering merely because Runtime evaluated them.

## 12. Python and runtime interoperability

Python is not required for ordinary visual formulas, boolean conditions or analog fills.

Python remains appropriate for procedural/multi-step behavior, events and advanced application logic.

Python may still read/write/animate public visual properties according to normal capabilities and precedence.

A script must see stable public property/behavior semantics rather than renderer-private expression ASTs, fill clips, CSS masks or internal condition objects.

Expression execution and Python execution remain distinct. A visual expression cannot call Python and Python source is not accepted as an expression.

## 13. Diagnostics

Runtime/Engineering diagnostics should identify, where applicable:

- destination property;
- active source kind;
- expression text or safe normalized representation;
- resolved dependencies;
- expression result type;
- syntax/type error and location where practical;
- invalid operation such as division by zero;
- unavailable source/quality;
- effective property source after precedence resolution;
- Boolean Condition mode;
- Analog Fill source/expression, scale and resulting normalized percentage when useful for troubleshooting.

## 14. Acceptance requirements for the follow-up implementation

Before this requirement is considered implemented, automated acceptance must prove at least:

1. every built-in renderable object exposes `visible`;
2. a boolean TAG/Client Memory source can directly control a boolean visual property;
3. `falha_inversor1 or falha_bomba1`-style boolean expressions evaluate deterministically;
4. `and`, `or`, `not`, comparisons and parentheses obey defined precedence;
5. numeric expressions such as `(nivel1 + nivel2) * 3` can drive a compatible numeric property;
6. explicit numeric/boolean conversions work while implicit coercion is rejected;
7. division by zero/non-finite results fail the Binding/Expression evaluation with diagnostics;
8. a numeric source can control `visible` through an inside range;
9. one-sided and outside-range conditions behave deterministically;
10. bad/unavailable dependencies do not silently force false/zero and fall back according to normal precedence;
11. canonical dependency validation rejects missing/ambiguous expression sources;
12. import/export and revision/package round-trip preserve expression/condition configuration and dependencies;
13. rectangle Analog Fill maps configured engineering values to 0%, intermediate percentage and 100%;
14. Analog Fill accepts a numeric expression as its input;
15. clamping and all four initial fill directions work;
16. ellipse/circle uses the same canonical Analog Fill contract rather than a separate object-private implementation;
17. save/reopen/export/import preserve Analog Fill configuration;
18. Runtime-calculated expression/boolean/fill state is not persisted as Engineering base state;
19. expression evaluation is bounded and cannot execute arbitrary code;
20. existing Python/binding/property-precedence regressions remain green.

## 15. Explicit non-goals of the first follow-up

Not required initially:

- a general-purpose programming language;
- assignments, loops or mutable variables inside expressions;
- arbitrary custom functions;
- JavaScript/Python evaluation;
- visual-property-to-visual-property expression dependencies unless separately promoted with cycle validation;
- string interpolation/formatting beyond existing dedicated display behavior;
- hysteresis/debounce for visual conditions;
- radial/path/tank-shape fill geometries beyond objects explicitly supported;
- threshold color bands or gradient color stops;
- safety/interlock logic based on client visual expressions.

Those may be added through later typed contracts where appropriate. Visual expressions and conditions are presentation behavior and must never become an industrial safety/interlock authority.
