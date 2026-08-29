# Wave 08 — Text, Project Reference Tree and Free Polygon Authoring

Status: **OWNER-LOCKED / REQUIRED BEFORE WAVE 08 CLOSES**  
Date: 2026-08-29  
Parent wave: `GRAPHICAL-EDITOR-WAVE-08`

This contract extends the active Wave 08 graphical-editor Definition of Done. It is mandatory even though parts of the capability were previously implicit in `core.text`, `core.valueDisplay`, the shared Visual Property Registry and binding infrastructure.

The purpose of this addendum is to remove ambiguity around three owner requirements:

1. practical text-box authoring, including displaying live TAG/variable values;
2. one reusable Project Reference Tree for Engineering associations/bindings;
3. free closed polygon authoring by arbitrary points.

The currently green Graphical Editor/Image checkpoint does not by itself satisfy these additions.

---

## 1. Text box / dynamic text display

### Object identity

`core.text` remains the canonical general text-box object. `core.valueDisplay` may remain as a convenience styled value-display object, but the product must not require the engineer to abandon `core.text` merely to show a live variable value.

### Required static text properties

A text box must expose through canonical Engineering and the shared Visual Property Registry at least:

- text/literal content;
- X/Y position;
- width and height;
- rotation;
- scale where supported by the common visual model;
- Z order;
- visible;
- opacity;
- text color;
- font family;
- font size;
- font weight;
- font style;
- horizontal alignment;
- vertical alignment;
- deterministic multiline/wrapping behavior for a bounded text box.

The implementation may add presentation metadata such as line height or wrapping mode when needed, but must keep it typed and canonical rather than CSS-only hidden state.

### Dynamic value display

`core.text` must support an explicit dynamic-content mode capable of displaying the current value of any authorized scalar project/runtime source that can be represented as text, including at minimum:

- Boolean;
- Int16/Int32/Int64;
- Float/Double;
- String;
- DateTime;
- Enum/state values when the source domain exposes them;
- future scalar canonical source types added through the same reference/provider contract.

This is **not** implicit type coercion of the normal `text: string` Engineering property.

The runtime/editor contract must distinguish:

- literal Engineering text; and
- a typed dynamic scalar source plus explicit display formatting.

A numeric/Boolean/DateTime source therefore does not pretend to be a String binding. The source retains its canonical type and is formatted only at the presentation boundary.

### Minimum dynamic formatting

The first implementation must provide deterministic formatting appropriate to the source type, including:

- numeric decimal precision / useful numeric format;
- optional engineering unit/suffix where available or explicitly configured;
- Boolean display text with localized/default presentation while preserving canonical Boolean value;
- DateTime unambiguous display format;
- String pass-through without numeric guessing;
- Int64 exact representation without JavaScript precision loss;
- null/unavailable/bad-quality presentation distinct from `0`, `false` and empty string.

The display may later support richer format strings, prefixes/suffixes and expression output, but Wave 08 must establish a typed formatting seam rather than renderer-specific string concatenation.

### Quality behavior

When a dynamic text source has a quality/state concept:

- bad/unavailable/stale state must not silently display a normal-looking fabricated value;
- the renderer may show the last value only if the diagnostic state remains explicit;
- no unavailable source is coerced to `0`, `false` or empty string;
- source quality/state remains presentation/runtime state and is never written into Engineering base text.

### Binding/expression relationship

The dynamic text source must use the same canonical project reference/binding infrastructure as other visual associations.

After 08-FOLLOW-A, canonical TAG bit selectors may be selected as Boolean sources.

After 08-FOLLOW-B, typed expression results may be selected as a dynamic text source when their scalar result type is supported. `core.text` must not implement its own expression evaluator.

---

## 2. Project Reference Tree for Engineering associations

### Product rule

Whenever an Engineering UI asks the developer to associate a source/reference with a bindable property, dynamic text, monitor row, alarm/reference field, or other compatible project association, the UI must provide access to one shared **Project Reference Browser** / **Project Tree** rather than forcing the user to remember and type every path.

Direct exact-reference typing may remain available and is encouraged for expert workflows. Tree browsing and direct typing are complementary, not mutually exclusive.

### Tree families

The browser must organize available references into explicit source families. Initial families include where authoritative sources exist:

- **TAGs**;
- **Client Memory**;
- **Server Memory**;
- **System / Runtime variables**;
- **Data Sources / Driver diagnostics**;
- **Project assets/references** where the destination expects an asset/reference type rather than a live scalar;
- future **TAG bit selectors**, **Gateway diagnostics**, **expression outputs** or other canonical source families without redesigning the browser.

Within TAGs, the tree should preserve project/path grouping so hierarchical TAG paths are navigable rather than flattened into one enormous list.

Internal-memory and diagnostic families must keep their own scope/identity semantics explicit.

### Search inside the tree

The Project Reference Browser must support search by useful identifying information such as:

- canonical path/reference;
- name/display name;
- family;
- Data Source/provider identity where relevant.

Search is a convenience projection. Selection authority is the stable canonical source/reference identity.

### Type compatibility

The tree must understand the destination contract.

Examples:

- a Boolean destination should offer/enable Boolean-compatible sources;
- a numeric destination should offer/enable compatible numeric sources;
- a dynamic text source may accept any supported scalar because formatting is explicit at the presentation boundary;
- `assetRef` selects project assets and must not expose TAGs as though they were assets.

Obviously incompatible nodes must be filtered or disabled with an understandable reason. The editor must fail early rather than save a reference the Runtime will reject later.

### Reuse rule

The same reference-browser model must be reusable by:

- visual property/binding authoring;
- `core.text` dynamic-value selection;
- `core.valueDisplay` source selection;
- Engineering Development Monitor add/search workflow;
- later Alarm/reference editors and future tooling where compatible.

Individual screens must not create independent copies of TAG catalogs with different identity/type rules.

### Security

The tree must expose only references the current Engineering identity is authorized to discover/read. Hiding an already-returned global catalog in the browser is not sufficient authorization.

Secrets, credentials and driver-private implementation objects are never tree nodes.

---

## 3. Free closed polygon authoring

### New built-in visual object

Wave 08 must add a first-class built-in polygon object:

`core.polygon`

A polygon is a developer-authored arbitrary closed shape defined by ordered points.

### Geometry model

Canonical polygon geometry must contain an ordered list of vertices with at least **3 distinct finite points**.

Recommended canonical model:

```text
points = [
  { x, y },
  { x, y },
  { x, y },
  ...
]
```

The points are stored once in order. The first point is **not duplicated** as the final list entry merely to indicate closure.

Closure is intrinsic to the object:

`last vertex -> first vertex`

The editor and renderer must always close the polygon.

An open polyline is a different future object/semantic and must not be accidentally represented by `core.polygon`.

### Coordinate semantics

Polygon vertices should use local object coordinates under a stable object origin so normal object operations remain coherent:

- move via object X/Y;
- rotate via object rotation;
- scale/resize through the canonical transform/geometry contract;
- grouping without rewriting every point into page-global coordinates.

The implementation may derive bounds from vertices or maintain validated bounds metadata, but there must be one deterministic source of geometric truth and no renderer-private competing points list.

### Authoring interaction

The development Canvas must allow the engineer to:

- choose Polygon from the palette;
- click/add successive vertices;
- see a transient preview segment while placing points;
- finish creation with at least three valid points;
- have the shape close automatically;
- select the polygon later;
- move the polygon;
- edit/move individual vertices;
- add/remove vertices subject to the minimum-three-points rule;
- delete/duplicate/z-order the polygon through normal object operations;
- cancel an unfinished polygon without persisting partial invalid Engineering.

The exact mouse/keyboard gesture may follow product UX conventions, but completion/cancel behavior must be deterministic and testable.

### Polygon visual properties

`core.polygon` must participate in the common visual property system and expose at minimum applicable properties for:

- X/Y;
- rotation/scale;
- Z order;
- visible;
- opacity;
- fill color;
- stroke color;
- stroke width;
- stroke style;
- ordered points/vertices through a typed canonical geometry contract.

After the Analog Fill follow-up, polygon participation in fill behaviors may be considered only if the geometry/runtime clipping contract is explicitly implemented and tested. Do not silently assume rectangle fill behavior applies to arbitrary polygons.

### Validation

Canonical validation must reject at minimum:

- fewer than three vertices;
- non-finite coordinates;
- malformed point structures;
- a persisted unfinished polygon;
- duplicate/degenerate geometry that cannot form a meaningful closed area according to the chosen validator policy;
- renderer/DOM handles or Canvas-private state embedded in points.

Self-intersecting polygons may initially be allowed if rendering follows one documented fill rule consistently. If disallowed, the validator must reject them explicitly rather than producing renderer-dependent results.

### Persistence and portability

Polygon type and vertices are first-class canonical Engineering and therefore must round-trip through:

- Preview/Apply/CAS;
- JSON Import/Export;
- revisions;
- PostgreSQL Engineering persistence where applicable;
- `.escadapkg` backup/restore.

---

## Wave 08 acceptance impact

Wave 08 Graphical Editor gate now additionally requires one exact integrated head to prove:

1. `core.text` can be added and edited with position, size, typography, color and alignment.
2. `core.text` can display at least one live numeric TAG/variable and one Boolean or String source through typed dynamic-content formatting.
3. dynamic text preserves unavailable/bad source state instead of fabricating a normal value.
4. a bindable visual property can open the shared Project Reference Tree and select a compatible source.
5. the tree visibly separates source families and supports search.
6. direct exact-reference authoring remains available where applicable.
7. incompatible source/destination types fail before persistence.
8. `core.polygon` can be created from arbitrary Canvas points and closes automatically.
9. polygon vertices can be edited after creation while retaining a valid closed polygon.
10. polygon styling and normal move/rotate/duplicate/delete/z-order operations work through canonical intents.
11. text dynamic-source references, tree-selected references and polygon point geometry survive save/reopen/export/import.
12. no tree UI state, polygon placement preview, vertex handles or other transient Canvas state leaks into canonical Engineering.
13. existing Graphical Editor/Image, Development Monitor, Wave 07 visual/Python and Wave 06 security/sandbox regressions remain green.

## Definition of Done impact

The previously green Wave 08 Graphical Editor/Image checkpoint remains valid evidence for unchanged features, but it is now only a partial checkpoint.

Wave 08 may close only after all of the following are implemented and green together:

- existing Graphical Editor/Image functionality;
- this Text + Project Reference Tree + Free Polygon contract;
- Engineering Development Monitor contract;
- final exact-head full CI;
- post-merge `main` health confirmation.

08-FOLLOW-A and 08-FOLLOW-B remain subsequent mandatory work before Wave 09, except where this contract deliberately establishes reusable seams that those follow-ups will later extend.