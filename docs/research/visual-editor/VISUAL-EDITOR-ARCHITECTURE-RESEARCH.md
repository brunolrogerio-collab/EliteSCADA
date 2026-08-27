# EliteSCADA Graphical Visual Editor Architecture and Engineering UX Research

## Status

**RESEARCH IN BRANCH / NOT IMPLEMENTED.**

This document is a documentation-only architecture and UX spike for the future EliteSCADA graphical Engineering editor for Screens, Popups and Dynamos.

It does not add a production renderer/editor dependency, does not change the canonical Engineering schema, does not implement visual runtime composition, does not implement the Python editor/runtime, and does not change central frontend routing or application composition.

The purpose is to make later implementation slices concrete while preserving the public visual-property, scripting, import/export, revision and security boundaries already present in `main`.

---

## 1. Executive recommendation

The recommended first production direction is:

**EliteSCADA-authoritative Engineering model + SVG/DOM-first authoring surface + renderer-independent interaction/geometry services.**

The key rule is:

> The graphical editor edits EliteSCADA Engineering state. It must never make a Canvas/SVG/WebGL library object tree the authoritative project model.

Recommended architecture:

1. Load one editable Screen/Popup/Dynamo definition from the Engineering workspace.
2. Project the definition into an editor document model keyed by stable visual IDs.
3. Render that model through an SVG/DOM-first authoring renderer.
4. Run selection, transform, snapping, grouping, hierarchy, command history and clipboard through editor-domain services that operate on EliteSCADA geometry/properties, not renderer-private state.
5. Commit mutations back to the editable Engineering workspace only through explicit editor commands.
6. Keep runtime bindings/scripts/animations separate from Engineering base values.
7. Later permit alternative render adapters, including Canvas/WebGL optimization for exceptional object counts, without changing serialized project semantics.

Why SVG/DOM-first for the first editor:

- industrial HMI screens are predominantly vector primitives, text, symbols, images and reusable components;
- SVG provides a retained scene graph, groups, transforms and pointer targeting while remaining browser-DOM addressable;
- inline SVG can participate in the accessibility object model and supports explicit accessible names/descriptions;
- DOM/SVG elements are straightforward to inspect and exercise in Chromium E2E tests;
- the EliteSCADA public visual property set maps naturally to SVG/CSS concepts such as position, size, rotation, fill, stroke, opacity and text;
- it avoids making a third-party Canvas library's serialization/object semantics authoritative;
- it keeps the first implementation understandable before optimizing for hypothetical ten-thousand-object animated scenes.

The recommendation is not a permanent ban on Canvas/WebGL. It deliberately separates **model authority** from **render acceleration** so the implementation can evolve if measured performance requires it.

---

## 2. Repository contracts that constrain the editor

The future editor is not starting from a blank slate.

### 2.1 Public typed visual properties already exist

`VisualPropertyFoundation.cs` already defines stable public value kinds:

- Boolean;
- Number;
- Integer;
- String;
- Color;
- ResourceReference.

A `VisualPropertyDefinition` already declares:

- stable property key;
- default value/type;
- Engineering editability;
- runtime readability;
- runtime writability;
- binding support;
- animation support;
- min/max/allowed-value constraints;
- optional unit;
- optional presentation hint.

The editor property inspector must consume this schema directly.

It must not maintain a renderer-private duplicate list such as `SvgRectPropertyPanel`, `KonvaRectPropertyPanel` etc. Renderer adapters may translate public properties into implementation values, but they are downstream of the public schema.

### 2.2 Existing common property keys

Current public keys already include:

- `x`, `y`, `width`, `height`;
- `rotation`, `scaleX`, `scaleY`, `zIndex`;
- `visible`, `opacity`;
- fill/background/stroke colors;
- stroke width/style and corner radius;
- text, text color, font family/size/weight/style/alignment;
- image resource reference, fit and position.

This means editor gestures must normalize back to these public semantics.

For example, a renderer library that represents resize only by changing `scaleX` and `scaleY` must not silently store that result when the user's intent was to change `width`/`height`.

### 2.3 Runtime property precedence already exists

The merged runtime property foundation resolves effective values in this order:

`Animation -> Script -> BindingOrExpression -> EngineeringBase`

The editor should expose this precedence visibly in binding/script diagnostics and Preview tooling.

It must not invent a contradictory design-time precedence model.

### 2.4 Stable object/runtime identity already exists

The visual runtime foundation already distinguishes:

- stable visual definition ID;
- stable visual object ID;
- stable developer key;
- object type key;
- parent object ID;
- runtime instance identity per client;
- Screen/Popup/Dynamo definition kind.

The editor must preserve stable IDs across normal move/resize/reparent/property edits.

Duplicate/copy creates new stable object IDs. Rename of the developer key does not imply replacement of the object identity.

### 2.5 Scripts are stable referenced entities

The isolated Script Engineering domain already defines:

- Script stable IDs;
- ClientVisual versus Server scope;
- entry points by event kind;
- explicit dependencies;
- visual event references by visual definition/object IDs.

The graphical editor should therefore associate events with Script ID + entry point. It should not store arbitrary hidden Python strings inside object property panels.

### 2.6 Current canonical Screen/Popup/Dynamo DTOs are older/thinner than the visual runtime foundation

Current `EngineeringContracts.cs` exposes `DynamoEngineeringDto`, `ScreenEngineeringDto`, `PopupEngineeringDto` and recursive `VisualElementEngineeringDto` using string dictionaries for properties/context and legacy binding DTOs.

The future editor cannot treat that current thin DTO shape as the final visual authoring contract.

Coordinator-owned canonical integration must later reconcile:

- stable object GUIDs;
- typed visual properties;
- explicit parent/hierarchy identity;
- stable Script/event references;
- asset references;
- future Dynamo public parameters/instance overrides;
- versioned bindings.

This research intentionally does not make that schema change.

---

## 3. Industrial editor workflow observations

The goal is not to imitate one vendor pixel-for-pixel. The useful lesson is which workflows have survived years of industrial Engineering use.

### 3.1 Elipse E3

Current Elipse E3 documentation shows a traditional industrial authoring model with:

- selection and pan modes;
- area zoom and fit-to-view;
- rotation;
- layers;
- front/back/step z-order controls;
- grouping/ungrouping including nested groups;
- left/right/top/bottom/center alignment;
- equal width/height/size;
- horizontal/vertical distribution;
- grid display/configuration;
- object property lists;
- property associations/bindings intended to reduce scripting;
- reusable ElipseX/XControl libraries with exported public properties.

Useful EliteSCADA lessons:

- industrial engineers expect direct alignment/distribution commands, not only free drag;
- grouping and z-order are first-class editing operations;
- bindings must be visible at property level;
- reusable symbols need explicit public interfaces/context;
- common behavior should not require scripts.

EliteSCADA should improve on older approaches by keeping public schemas typed/versioned, using stable IDs instead of fragile names/paths alone, keeping resources package-safe, and preserving deterministic binding/script precedence.

### 3.2 Ignition Perspective

Current Ignition Perspective documentation reinforces several useful patterns:

- a View is an independently identified reusable resource;
- Views can be top-level, embedded or popup content;
- coordinate containers support absolute diagram-style x/y/width/height/rotation;
- component properties expose binding controls directly in the Property Editor;
- views accept parameters for reuse/context;
- bindings remain separate configurations from persisted static values.

Useful EliteSCADA lessons:

- Screen/Popup/Dynamo definitions should be independently reusable resources;
- editor hierarchy and instance context matter as much as drawing primitives;
- property-level binding affordances are better than a separate disconnected binding spreadsheet;
- parameterized reusable views/symbols are essential for equipment-oriented HMI authoring.

### 3.3 FactoryTalk Optix

FactoryTalk Optix current documentation exposes graphical object types/instances, aliases and dynamic links.

Useful EliteSCADA lessons:

- definition/type versus instance must remain explicit;
- reusable graphics need context parameters/aliases so one definition can represent many equipment instances;
- object-property linking belongs in Engineering, not hard-coded runtime glue;
- the project tree and canvas should both represent the same object hierarchy.

EliteSCADA should preserve its own stronger canonical import/export and stable-ID rules rather than copying vendor-specific node models.

---

## 4. Renderer/editor approach comparison

### 4.1 Plain DOM/CSS

Strengths:

- strongest native accessibility and browser tooling;
- natural for forms, tables, buttons and text-heavy widgets;
- simple E2E selectors and keyboard/focus behavior;
- CSS transforms/layout are mature.

Weaknesses:

- arbitrary industrial vector geometry is awkward;
- large numbers of positioned nested HTML elements can become layout/style intensive;
- path editing, connectors and vector-specific hit testing require extra systems;
- mixing normal document flow with absolute diagram geometry can create confusing semantics.

Recommendation:

Use DOM/HTML for editor chrome, property panels, trees, dialogs and interactive form controls. Do not use generic `<div>` geometry as the only visual drawing representation.

### 4.2 SVG retained vector scene

Strengths:

- native vector primitives, groups and transforms;
- stable coordinate space through `viewBox`;
- browser pointer-event targeting;
- DOM-addressable object elements;
- accessible `<title>` / `<desc>` and ARIA integration are possible;
- crisp zoom for industrial diagrams;
- straightforward mapping for fill/stroke/text/opacity/rotation;
- easy Chromium inspection/screenshot testing;
- supports custom paths and future symbols without rasterization.

Weaknesses:

- every retained element contributes DOM cost;
- very large scenes may require viewport culling/level-of-detail or alternative render paths;
- complex HTML widgets sometimes need separate DOM overlays rather than forcing everything through SVG;
- text metrics/font differences require deterministic handling.

Recommendation:

**Preferred first authoring renderer.**

Use SVG as a view of the editor document, not as the persisted Engineering format.

### 4.3 Canvas 2D with Konva-like interaction layer

Strengths:

- strong selection/drag/transform patterns;
- hit testing and layers are provided;
- fewer DOM nodes;
- caching and selective layer redraw can improve heavy scenes;
- mature examples for marquee selection, resize/rotate and snapping.

Weaknesses:

- canvas content is not semantic DOM and needs explicit accessibility alternatives;
- library transforms may not match EliteSCADA property semantics;
- selection controls can tempt the implementation to let the library's object model become authoritative;
- memory use grows with canvas layers and device pixel ratio;
- test diagnostics can be less transparent than DOM/SVG;
- custom HTML controls/text editing often require overlays.

Important concrete mismatch:

Konva's standard Transformer resize changes scale rather than width/height. EliteSCADA already defines width/height separately from scale, so a direct serialization of renderer state would corrupt intended Engineering semantics.

Recommendation:

Do not select a Canvas library as the authoritative first editor model. Keep it as a possible interaction/render adapter or later high-count renderer after benchmarks.

### 4.4 Fabric.js

Strengths:

- rich interactive Canvas object model;
- selection, area/multi-selection, controls, groups;
- JSON/SVG serialization;
- viewport and transform helpers;
- image/path/text tools.

Weaknesses for EliteSCADA:

- its object serialization is designed to restore Fabric visual state, while EliteSCADA already has a public authoritative Engineering model;
- custom IDs/properties require explicit extension/serialization discipline;
- importing/exporting Fabric JSON would create a competing project model;
- Canvas accessibility concerns remain.

Recommendation:

Useful as a benchmark/reference for interaction UX, not as the project serialization layer. If evaluated in a lab, all changes must pass through EliteSCADA editor commands and typed property adapters.

### 4.5 PixiJS/WebGL

Strengths:

- GPU-oriented scene graph;
- strong throughput for large sprite-heavy/animated scenes;
- explicit culling/resource-management options;
- useful future runtime acceleration candidate for extreme display workloads.

Weaknesses:

- substantially more renderer/GPU lifecycle complexity;
- graphics/text/resource tuning becomes application-specific;
- accessibility and DOM inspection require an additional layer;
- an always-running graphics render loop is unnecessary for much of an Engineering editor;
- high-performance raster/GPU strengths are less valuable for mostly static authoring interactions.

Recommendation:

Not the first editor renderer. Revisit for measured runtime/editor bottlenecks only after an SVG baseline benchmark proves insufficient.

### 4.6 Recommended hybrid boundary

Initial authoring surface:

- SVG for vector scene/object visualization;
- HTML/DOM for editor chrome and complex control overlays;
- one renderer-independent document/geometry API;
- optional overlay SVG layer for selection handles, guides and marquee;
- no persistent state inside renderer nodes beyond ephemeral UI state.

Future optimization path:

- viewport culling;
- cached symbols;
- simplified preview while panning/zooming;
- optional Canvas/WebGL render adapter for very large scenes;
- DOM accessibility/tree representation remains available even if the visual pixels are GPU rendered.

---

## 5. Editor document architecture

### 5.1 Authoritative edit document

The editor should maintain an in-memory projection of the Engineering working state:

`VisualEditorDocument`

Conceptual fields:

- visual definition stable ID;
- kind: Screen / Popup / Dynamo;
- developer key/name;
- design canvas width/height and view policy;
- object dictionary keyed by stable object GUID;
- parent/child ordering;
- typed Engineering base property values;
- bindings by object/property;
- event handler references;
- resource references;
- Dynamo public parameter/instance-context metadata when integrated;
- dirty/version token tied to Engineering workspace CAS/version semantics.

The renderer receives a read projection of this document.

Pointer events never mutate renderer attributes directly as the final state. They dispatch editor commands.

### 5.2 Ephemeral editor UI state

Keep separate from Engineering:

- current selection set;
- hovered object;
- active transform handle;
- zoom/pan viewport;
- visible grid/guides;
- snap candidates;
- open inspector sections;
- temporary marquee;
- drag ghost;
- current tool;
- temporary Preview values;
- runtime diagnostics overlay.

This state must not appear in canonical project export unless a deliberate user preference/document setting is separately engineered.

---

## 6. Workspace layout

Recommended desktop Engineering layout:

### Left side

**Project/visual hierarchy panel**

- Screens;
- Popups;
- Dynamos;
- Resources;
- object tree for active definition;
- search/filter;
- visibility/lock controls for editor convenience;
- stable developer key shown alongside friendly name where useful.

**Palette tab**

Initial categories:

- Basic: rectangle, ellipse, line/path, text, image;
- Display: numeric/text display, indicator;
- Input/command: later secured controls, never generic direct-driver widgets;
- Containers/groups;
- Reusable Dynamos;
- Equipment-aware reusable content;
- future trends/alarm widgets once their contracts exist.

Drag from palette creates a new Engineering object with a new stable GUID and defaults from the public property schema.

### Center

**Canvas/workspace**

- rulers;
- design bounds;
- pan/zoom;
- grid/guides;
- selection adorners;
- snap indicators;
- optional design device/frame preview;
- optional runtime-source overlay in Preview mode.

### Right side

**Inspector**

Tabs/sections:

- Properties;
- Bindings;
- Events/Scripts;
- Context/Parameters;
- Diagnostics/validation where relevant.

The same selected object set drives the canvas, hierarchy tree and inspector.

### Bottom/status area

- x/y/width/height of primary selection;
- zoom;
- snap/grid state;
- validation issue count;
- dirty/working revision status;
- optional current object stable ID/developer key for developers.

---

## 7. Selection and navigation model

### 7.1 Pointer behavior

Recommended defaults:

- click object: select only that object;
- Shift+click: add/remove object from selection;
- drag empty canvas: marquee selection;
- Space+drag or middle mouse: pan;
- wheel: vertical scroll/pan where appropriate;
- Ctrl+wheel: zoom centered near pointer;
- double-click group/Dynamo instance: enter/focus edit context where allowed;
- Escape: cancel active gesture, then clear nested edit context/selection according to state.

Touch support should use pointer events and larger transform handles, but the first Engineering target remains desktop/Windows.

### 7.2 Marquee semantics

Offer one documented default:

- intersecting objects are selected when marquee crosses them;

Optionally support an advanced fully-contained mode later.

Selection of hidden/locked editor layers is excluded.

### 7.3 Object tree selection

The hierarchy tree is not merely navigation. It is the accessible/precise selection path for:

- overlapping objects;
- invisible objects;
- deeply nested groups;
- keyboard users;
- developers working with stable object names/IDs.

Canvas and tree selections must remain synchronized.

---

## 8. Geometry and transform semantics

### 8.1 Canonical coordinates

Use the public Engineering geometry properties as canonical design values:

- x/y;
- width/height;
- rotation;
- scaleX/scaleY;
- z-order separately.

The renderer adapter converts these into SVG/DOM transforms.

### 8.2 Resize

Default rectangle-like resize changes width/height and, depending on handle, x/y.

Do not automatically implement resize as scale unless the user explicitly uses a scale operation.

This is required to preserve the existing property contract and produce intelligible Engineering diffs.

### 8.3 Rotation

Rotation should occur around a well-defined anchor/origin.

The current public schema does not yet expose an anchor/origin property. Therefore the first implementation should either:

- use a deterministic center anchor as editor/runtime convention; or
- add anchor/origin only later through coordinator-owned public property/schema evolution.

The editor research recommends center rotation as the initial convention until the public schema deliberately evolves.

### 8.4 Multi-selection transform

A multi-selection has a temporary group bounding box for editing only.

Scaling/resizing the multi-selection must produce deterministic individual object property changes and one undoable command transaction.

It must not create a persistent group unless the user explicitly chooses Group.

---

## 9. Grid, rulers, guides and snapping

Recommended first feature set:

- toggleable grid;
- configurable major/minor spacing;
- rulers in design units;
- drag guides from rulers;
- snap to grid;
- snap to explicit guides;
- snap to other object edges/centers;
- alignment preview lines;
- temporarily disable snapping with a modifier key;
- numeric property editing always remains available for exact values.

Snap engine priority should be deterministic:

1. explicit guides;
2. nearby object edge/center guides;
3. grid;
4. raw pointer position.

Only one winning snap per axis should be applied at a time, with a small zoom-adjusted pixel threshold.

Snapping is an editor interaction rule, not a persisted transformation source.

---

## 10. Alignment, distribution and sizing commands

Industrial engineers expect toolbar/context commands for precise repetitive work.

First set:

- align left/right/top/bottom;
- align horizontal/vertical centers;
- distribute horizontal/vertical spacing;
- equal width;
- equal height;
- equal size;
- center in parent horizontally/vertically;
- bring to front/send to back;
- move forward/back one step.

Reference policy must be explicit.

Recommended behavior:

- alignment uses the primary/last explicitly selected object as reference when practical;
- distribution uses outermost selection bounds;
- status/tooltip should make the reference clear.

All operations are command-history transactions.

---

## 11. Z-order, groups and containers

### 11.1 Z-order

Persist stacking semantically through parent child order plus/public `zIndex` rules selected by the final canonical visual model.

Do not rely on incidental DOM append order as the only project truth.

### 11.2 Group

A Group is an explicit visual container/object relationship, not merely a temporary selection set.

Expected behavior:

- grouping creates a new stable parent/container ID;
- selected objects become children while preserving world-space appearance;
- group transforms operate predictably;
- ungroup restores children to the former parent while preserving appearance;
- nested groups are allowed;
- parent cycles are rejected by model validation.

The current visual runtime already validates missing parents and parent cycles, so the editor should prevent creating invalid graphs before Preview/Apply.

### 11.3 Locked versus grouped

Editor-only lock state and persistent runtime grouping are different concepts.

A locked object can remain an ordinary object that is temporarily protected from pointer edits.

Do not create fake groups just to implement editor locking.

---

## 12. Property inspector architecture

### 12.1 Schema-driven controls

Map `VisualPropertyValueKind` and constraints to inspector controls:

- Boolean -> checkbox/toggle;
- Number/Integer -> numeric editor with min/max/unit;
- String -> text input or allowed-values selector;
- Color -> stable hex color editor with alpha support;
- ResourceReference -> project resource picker.

`PresentationHint` can select specialized controls but must never redefine the property's semantic type.

### 12.2 Capability-driven affordances

Inspector behavior derives from property definition flags:

- `EngineeringEditable=false`: read-only/hidden according to presentation rules;
- `SupportsBinding=true`: show Bind action/state;
- `RuntimeWritable=true`: show that Client Visual Script runtime overrides are permitted;
- `Animatable=true`: allow animation association/diagnostics.

### 12.3 Multi-selection

For same compatible property key/type across selected objects:

- identical base value -> show value;
- mixed values -> show Mixed;
- editing applies one command transaction to all compatible objects;
- properties unavailable on some selected types are either hidden under a compatibility filter or clearly marked partial.

No coercion between incompatible property kinds.

### 12.4 Base versus effective value

Normal design mode edits **Engineering Base**.

Preview/diagnostic mode may display:

- Base value;
- Binding result;
- Script override;
- Animation override;
- Effective value;
- effective runtime source.

This directly reflects the merged runtime precedence instead of forcing engineers to infer why a property is not displaying its base value.

---

## 13. TAG / property / expression binding UX

### 13.1 Binding is property-local

Every bindable property should expose a small binding indicator/action in the inspector.

States:

- no binding;
- configured/enabled;
- configured/disabled if that concept is added;
- warning/error;
- runtime conflict/overridden indicator in Preview.

### 13.2 Binding editor

The first binding editor should support the public binding families rather than renderer-specific APIs:

- TAG;
- another public visual/property reference where supported;
- expression.

The binding editor should show:

- source/reference;
- direction where the public contract permits it;
- data type compatibility;
- preview value;
- validation result;
- dependency target identity.

### 13.3 Binding versus script visibility

If a property has a binding and a Script/Animation can override it at runtime, show that fact.

Recommended diagnostic wording concept:

`Effective precedence: Animation > Script > Binding > Base`

A direct design-time property edit never deletes a binding silently. It changes only the base value unless the user explicitly removes/replaces the binding.

### 13.4 No private driver paths

TAG picker selects canonical project TAG identities/paths.

The visual editor never binds directly to a PLC register, OPC UA NodeId, Modbus address or driver object.

---

## 14. Script and event association UX

### 14.1 Separate event panel

For selected Screen/Popup/Dynamo/object, show supported events based on the public Script Engineering event model.

Examples already represented by merged contracts:

- Initialize;
- Dispose;
- ObjectInteraction;
- TagChanged;
- ClientMemoryChanged;
- Timer;
- PropertyChanged;
- FrameTick.

Only events appropriate to that target should be shown.

### 14.2 Handler reference

Association stores stable:

- visual definition ID;
- optional visual object ID;
- event kind;
- Script ID;
- entry point;
- optional target reference.

The editor may offer:

- choose existing Script;
- open Script in the future script editor;
- create new ClientVisual Script through the normal Script Engineering workflow once canonical Script integration exists.

It must not create hidden inline Python source attached to a button.

### 14.3 Conflict diagnostics

If a script writes a property that also has a binding, the editor should warn but not universally reject it because the public runtime explicitly supports precedence.

The warning should explain the active precedence and offer navigation to both writers.

Repeated runtime conflicts/faults belong in Preview/runtime diagnostics, not arbitrary canvas color changes with no explanation.

---

## 15. Screen, Popup and Dynamo authoring model

### 15.1 Screen

A Screen is a top-level visual definition intended for route/navigation use.

Editor concerns:

- logical design size;
- route metadata once canonical integration supports it;
- root background/layout properties;
- child visual object hierarchy;
- lifecycle Scripts/events;
- referenced resources/Dynamos.

### 15.2 Popup

A Popup is a reusable visual definition opened within a Runtime Client context.

Editor concerns:

- logical size;
- modality/chrome/placement policy later through explicit public properties;
- input context/parameters;
- child hierarchy;
- lifecycle behavior;
- deterministic close/disposal.

Popup runtime placement should not silently rewrite its Engineering base geometry.

### 15.3 Dynamo

A Dynamo is a reusable visual class/definition, not a pasted group.

Required direction:

- stable Dynamo definition ID/version identity;
- internal object hierarchy with stable IDs;
- public typed parameters/properties;
- internal bindings to those public parameters;
- reusable Script/event behavior;
- resource dependencies;
- instances with explicit context and controlled overrides;
- definition updates handled through deliberate version/migration semantics.

A Dynamo instance should normally expose its public interface rather than every internal child property.

The editor should support an explicit **Edit Definition** action for internal content.

### 15.4 Instance context

Useful instance context may include:

- equipment reference;
- TAG root/context;
- instance parameters;
- display label;
- allowed public overrides.

Context remains Engineering data through stable references, not browser variables.

---

## 16. Stable visual assets/resources

Image selection must use a project asset/resource browser.

The property stores a stable resource reference, never an arbitrary developer-machine filesystem path.

Resource workflow later should support:

- import/upload into project resources;
- preview thumbnail;
- stable ID;
- friendly name/path;
- content type/dimensions;
- package/export inclusion;
- missing resource diagnostics;
- dependency detection for Dynamo/Screen/Popup copy/export.

The editor may cache decoded images locally, but cache URLs/blob handles are never serialized as Engineering truth.

---

## 17. Undo/redo command architecture

### 17.1 Command-based history

Use explicit editor-domain commands rather than serializing/restoring the renderer tree.

Examples:

- AddObject;
- DeleteObjects;
- MoveObjects;
- ResizeObjects;
- RotateObjects;
- SetProperty;
- SetPropertiesBatch;
- Add/Remove/ChangeBinding;
- ReparentObjects;
- Group/Ungroup;
- ReorderObjects;
- Add/Remove ScriptEventReference;
- PasteFragment;
- ReplaceResourceReference.

Each command records enough prior state for deterministic inverse/redo.

### 17.2 Transaction/coalescing behavior

- one drag gesture -> one history entry;
- one resize gesture -> one history entry;
- continuous numeric typing -> coalesced until focus/commit boundary;
- multi-object alignment -> one history entry;
- paste of a dependency-aware fragment -> one history entry after successful validation.

### 17.3 History is not revision history

Editor undo/redo is transient Working-state authoring history.

Saved immutable Engineering Revisions remain the durable project history.

Do not serialize the entire undo stack into the canonical Engineering package.

---

## 18. Copy/paste and future Engineering Fragments

### 18.1 Same-definition duplicate

Duplicate selected object(s):

- allocate new stable IDs;
- preserve relative geometry;
- preserve internal relationships among duplicated objects;
- preserve references to existing external TAGs/resources/scripts when valid;
- offset slightly for visibility;
- select the new objects.

### 18.2 Clipboard representation

Long-term clipboard semantics should use a versioned **Engineering Fragment**, not renderer JSON/SVG.

A visual fragment should carry:

- selected definitions/objects;
- stable source identities for reconciliation;
- typed base properties;
- bindings;
- Script/event references;
- resource dependencies;
- Dynamo dependencies;
- context metadata;
- fragment schema/version.

### 18.3 Cross-project paste

Cross-project paste cannot blindly preserve foreign stable IDs/references.

Required flow:

`parse fragment -> resolve dependencies -> detect conflicts/missing references -> preview rebinding/import -> apply`

Options later should include:

- selected only;
- selected + required dependencies;
- reuse compatible existing dependency;
- import dependency copy;
- remap reference;
- skip/fail.

This matches the canonical Engineering preview/apply philosophy.

---

## 19. Keyboard, mouse and accessibility

### 19.1 Keyboard baseline

Recommended desktop shortcuts following common Engineering/editor expectations:

- Delete: delete selected objects with normal confirmation policy where destructive dependencies exist;
- Ctrl+C / Ctrl+X / Ctrl+V;
- Ctrl+D duplicate;
- Ctrl+Z / Ctrl+Y or Ctrl+Shift+Z;
- arrow keys: nudge 1 design unit;
- Shift+arrow: larger nudge, proposed 10 units;
- Ctrl+A: select all objects in current edit scope;
- Escape: cancel current gesture/context;
- +/- or Ctrl+wheel: zoom;
- Space+drag: pan;
- F2: rename developer-friendly name/key where allowed;
- Tab/Shift+Tab: move focus through editor chrome and hierarchy/inspector.

Exact shortcut localization/help is an implementation choice, but keyboard coverage is required.

### 19.2 Accessible authoring surface

A graphical canvas is inherently visual, but the Engineering tool itself should remain operable through:

- accessible object tree;
- property inspector labels/controls;
- keyboard selection/nudge/reorder operations;
- announced validation errors;
- non-color-only binding/error indicators;
- visible focus states.

SVG supports accessible names/descriptions and DOM integration, which is another reason to prefer it for the initial authoring surface.

If a future Canvas/WebGL renderer is used, keep the hierarchy/inspector DOM as the accessible control surface and provide an accessibility mapping/overlay rather than assuming pixels are accessible.

### 19.3 Localization

Localize editor chrome, category names, commands, tooltips and validation descriptions through existing `pt-BR` / `en` / `es` infrastructure.

Never translate:

- stable property keys;
- Script API identifiers;
- object type keys;
- developer keys/paths unless the project itself stores localized HMI content separately.

---

## 20. High-performance-HMI authoring considerations

Editor appearance should help engineers build restrained industrial displays rather than encourage decorative noise.

Recommended UX support:

- neutral default workspace;
- project palette/theme resources later;
- warnings/preview for low contrast where appropriate;
- reusable alarm/state color tokens later through public resources/styles, not hard-coded renderer constants;
- alignment/spacing tools that encourage consistent layouts;
- avoid automatically adding gradients, shadows or animation merely because a component library offers them.

The visual editor should allow rich graphics, but its defaults should remain operationally calm.

---

## 21. Performance targets and test strategy

These are **research guardrails for later implementation**, not locked product guarantees.

### 21.1 Proposed scene-size targets

Initial engineering targets:

- normal design target: 2,000 visual objects in one definition;
- stress target: 5,000 objects;
- extreme research fixture: 10,000 simple objects to determine when SVG/DOM degradation becomes unacceptable.

Nested Dynamo instances count by rendered/interactive object cost, not merely top-level instance count.

### 21.2 Proposed interaction targets on a representative development PC

At 2,000 simple objects:

- pan/zoom/drag should normally remain visually smooth near 60 fps;
- selected-object drag frame work target under ~16.7 ms when feasible;
- marquee selection result under 100 ms after pointer release;
- property edit visible update under 50 ms;
- undo/redo of ordinary operations under 100 ms;
- definition open-to-interactive target under 1 second after data is locally available.

At 5,000 objects:

- pan/zoom should remain usable, target >= 30 fps;
- no multi-second main-thread stalls during selection/property edits;
- viewport culling/simplified adorners should activate before adding a heavier renderer.

Exact benchmark hardware and thresholds must be frozen during implementation, not treated as universal browser laws.

### 21.3 Optimization order

Optimize in this order:

1. avoid React rerendering the whole tree for one object change;
2. key objects by stable IDs and memoize render adapters;
3. isolate selection overlay from scene content;
4. batch editor commands/state updates;
5. cull clearly off-viewport complex content;
6. simplify non-selected Dynamo internals at distant zoom if semantically safe;
7. cache expensive resource/path computations;
8. only then evaluate Canvas/WebGL replacement for measured bottlenecks.

Do not jump to GPU rendering before profiling state-management and unnecessary re-render costs.

### 21.4 Automated test layers

**Domain/unit tests**

- coordinate transforms;
- snap candidate selection;
- alignment/distribution;
- parent graph validation;
- z-order/reparent behavior;
- command inverse/redo;
- fragment ID remapping;
- schema-driven property editor mapping;
- binding conflict diagnostics.

**Chromium E2E**

- create/move/resize/rotate object;
- multi-select/marquee;
- keyboard nudge/delete/undo/redo;
- grouping/ungrouping;
- exact numeric property edit;
- grid/guide snap;
- bind one property to TAG/expression;
- attach existing Script event reference;
- add resource reference;
- create/use Dynamo instance after prerequisites exist;
- save/reload Working state without geometry drift.

**Visual regression**

Use a bounded set of deterministic screenshots for primitive/object rendering and selection adorners. Do not make pixel screenshots the only correctness test.

**Performance fixtures**

Generate 500 / 2,000 / 5,000 / 10,000-object synthetic definitions and measure:

- mount/open time;
- pan/zoom frame time;
- selection latency;
- single-property update cost;
- memory use trend;
- undo/redo latency.

**Accessibility tests**

- object tree keyboard operation;
- inspector label/control associations;
- focus order;
- non-color-only errors;
- automated accessibility scan of editor chrome.

---

## 22. Why renderer serialization must never be project truth

Konva and Fabric both provide their own JSON/object serialization mechanisms. That is useful for applications whose Canvas scene is their domain model.

It is unsafe for EliteSCADA because:

- canonical Engineering must remain versioned independently of renderer choice;
- bindings, security-sensitive references, Scripts, resources, IDs and revision semantics are larger than drawing attributes;
- third-party upgrade semantics could become accidental project migrations;
- copy/paste must be dependency aware;
- renderer convenience fields may contradict public property semantics;
- Runtime may eventually use a different renderer from Engineering.

Therefore:

`Engineering DTO/model -> editor document -> renderer adapter`

Never:

`renderer JSON -> canonical Engineering truth`.

---

## 23. Recommended future implementation slices

These slices respect the locked prerequisite chain. They are not authorization to start them now.

### Slice A - canonical Script/visual integration prerequisite

Coordinator-owned after Gateway central-contract ownership clears:

- first-class Scripts in canonical Engineering;
- typed stable Script references from visual definitions/objects;
- reconcile stable object IDs and typed visual properties into canonical Screen/Popup/Dynamo model;
- canonical resources/reference model foundation;
- JSON/preview/apply/revision/package compatibility.

### Slice B - Client Python editor/sandbox prerequisite

Uses DEV 3 research output:

- selected browser Python engine in isolated worker;
- script editor;
- compile/syntax diagnostics;
- EliteSCADA API stubs/autocomplete;
- bounded test/preview.

### Slice C - visual runtime composition prerequisite

- compile canonical Screen/Popup/Dynamo definitions into `VisualRuntimeDefinition`;
- instantiate per-client runtime objects;
- binding adapters;
- script-event adapters;
- animation/tween renderer bridge;
- deterministic disposal and diagnostics.

### Slice D - editor domain foundation

No rich canvas yet.

- frontend typed editor document projection;
- command/undo/redo engine;
- stable selection model;
- property-schema renderer controls;
- geometry transforms;
- fragment/clipboard abstractions;
- unit tests.

### Slice E - SVG authoring canvas MVP

- SVG viewport;
- pan/zoom;
- primitive object renderers;
- single/multi/marquee selection;
- move/resize/rotate;
- grid/snap;
- hierarchy tree;
- property inspector;
- z-order/group commands.

### Slice F - bindings/events/resources

- property-level binding UX;
- TAG/property/expression picker;
- Script/event reference UI;
- resource picker;
- Preview effective-value/source diagnostics.

### Slice G - Screen/Popup/Dynamo reusable composition

- definition navigator;
- Dynamo public parameters/context;
- instance override UX;
- Popup-specific authoring properties;
- Screen route/navigation authoring hooks;
- dependency inspection.

### Slice H - Engineering Fragments and cross-project paste

- canonical fragment schema;
- dependency graph;
- Preview/Apply conflict/rebinding UI;
- same-project/cross-project tests.

### Slice I - performance hardening

- measured SVG baseline;
- culling/memoization;
- large Dynamo simplification where safe;
- optional Canvas/WebGL render adapter spike only if benchmark evidence justifies it.

### Slice J - advanced libraries

Only after the basic graphical editor/runtime contracts are stable:

- nested reusable components;
- versioned Dynamo/library migration;
- richer public parameters;
- project/shared library workflows;
- controlled instance override migration.

---

## 24. INTEGRATION REQUIRED before production editor work

The research identifies these coordinator-owned hooks that must exist before the final graphical editor is production-ready.

1. **Canonical visual model reconciliation**
   - stable visual definition/object GUIDs;
   - typed public visual property values;
   - parent/container hierarchy;
   - deterministic z-order representation.

2. **Canonical Script integration**
   - Script collection/entity kind;
   - stable object/definition event references;
   - revision/import/export/package persistence.

3. **Binding contract evolution**
   - binding target/reference types aligned to stable TAG/object/property identities;
   - typed compatibility/validation;
   - expression representation/versioning.

4. **Resource/asset model**
   - stable asset IDs;
   - package/import/export semantics;
   - resource dependency validation.

5. **Dynamo class/instance contract**
   - public typed parameters;
   - definition/instance identity;
   - instance context;
   - allowed overrides;
   - dependency/version migration semantics.

6. **Visual runtime composition**
   - canonical model -> `VisualRuntimeDefinition` adapter;
   - bindings/scripts/animations;
   - per-client lifecycle/disposal.

7. **Client Python editor/sandbox**
   - must be official before the graphical editor exposes production Script-event editing workflows.

8. **Engineering workspace integration**
   - CAS/version token;
   - dirty state;
   - Preview/Apply/Save Revision behavior;
   - dependency-aware delete.

9. **Central frontend route/shell/localization**
   - coordinator owned when implementation begins.

None of these hooks is implemented by this research branch.

---

## 25. Risks and decisions to validate during implementation

### Risk 1 - SVG object-count ceiling

The first editor should benchmark realistic industrial screens early. If 5,000-object scenes are not usable after normal React/SVG optimization, evaluate a Canvas/WebGL adapter while keeping the editor document unchanged.

### Risk 2 - font/text measurement drift

Browser font metrics can change geometry. Project fonts/resources and text measurement rules need deterministic packaging and testing.

### Risk 3 - transforms with nested groups

Reparent/group/ungroup under rotation/scale requires robust matrix math to preserve world-space appearance. This belongs in a tested geometry service, not ad-hoc pointer handlers.

### Risk 4 - Dynamo override explosion

If every internal Dynamo child can be overridden, instances become unmaintainable and definition upgrades become unsafe. Expose deliberate public parameters/override points.

### Risk 5 - binding/script writer confusion

The runtime already permits layered sources. The editor must make source/precedence visible or developers will diagnose timing instead of configuration.

### Risk 6 - renderer leakage into persistence

Any library chosen later will tempt developers to serialize it directly. Code review and architecture tests should enforce the adapter boundary.

### Risk 7 - huge clipboard fragments

Copying a large equipment screen with dependencies can become expensive. Fragment preview should be asynchronous/cancellable and bounded.

### Risk 8 - resource duplication

Cross-project copy can duplicate identical images/scripts/Dynamos. Future fragment import should support stable/content-aware dependency reconciliation rather than blind duplication.

---

## 26. Concrete decision summary

Recommended decisions for later implementation review:

1. **Authoritative state:** EliteSCADA Engineering, never renderer state.
2. **First authoring renderer:** SVG/DOM-first.
3. **Editor chrome:** normal React/HTML DOM.
4. **Selection/handles/guides:** separate interaction overlay, preferably SVG.
5. **Geometry:** renderer-independent service normalizing to x/y/width/height/rotation/scale.
6. **Inspector:** generated from `VisualObjectPropertySchema`.
7. **Bindings:** property-local and stable-reference based.
8. **Scripts/events:** Script ID + entry point references, no hidden inline source.
9. **Runtime diagnostics:** visibly distinguish Base/Binding/Script/Animation effective source.
10. **Dynamo:** class/instance system with public parameters and controlled overrides, not pasted groups.
11. **Resources:** stable Engineering asset references, never filesystem paths.
12. **Undo/redo:** command-based Working-state history.
13. **Copy/paste:** future versioned Engineering Fragments with dependency Preview/Apply.
14. **Performance:** benchmark SVG first at 2k/5k/10k fixtures, optimize before changing renderer.
15. **Canvas/WebGL:** optional measured optimization adapter, never canonical serialization.
16. **Accessibility:** hierarchy/inspector keyboard-first; SVG/DOM supports richer accessibility than Canvas-only rendering.
17. **Implementation order:** canonical Script integration -> Python editor/sandbox -> visual runtime integration -> graphical editor.

---

## 27. Sources reviewed

### EliteSCADA repository

- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ROADMAP.md`
- `src/Scada.Engineering/VisualScripting/VisualPropertyFoundation.cs`
- `src/Scada.Engineering/VisualScripting/VisualRuntimeFoundation.cs`
- `src/Scada.Engineering/Scripts/ScriptEngineeringContracts.cs`
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`

### Industrial product references

Elipse E3:

- Screen: https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_screen_screen.html
- Screen toolbar / alignment / grid / layers: https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_screen_toolbar.html
- Group/Ungroup: https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_screen_general_config_group_ungroup.html
- Associations: https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_link.html
- Libraries / ElipseX: https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_library.html
- XControls: https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_library_elipsex_xcontrol.html

Ignition Perspective:

- Views: https://www.docs.inductiveautomation.com/docs/8.1/ignition-modules/perspective/views-in-perspective
- Coordinate Container: https://docs.inductiveautomation.com/docs/8.1/appendix/components/perspective-components/perspective-container-palette/perspective-coordinate-container
- Component Properties: https://docs.inductiveautomation.com/docs/8.1/ignition-modules/perspective/working-with-perspective-components/perspective-component-properties
- Bindings: https://www.docs.inductiveautomation.com/docs/8.1/ignition-modules/perspective/working-with-perspective-components/bindings-in-perspective

FactoryTalk Optix:

- Graphic object management: https://www.rockwellautomation.com/pt-br/docs/factorytalk-optix/current/contents-ditamap/creating-projects/graphic-objects/manage-graphic-objects.html
- Aliases: https://www.rockwellautomation.com/en-us/docs/factorytalk-optix/1-00/contents-ditamap/using-the-software/aliases.html
- Dynamic links: https://www.rockwellautomation.com/en-us/docs/factorytalk-optix/current/contents-ditamap/creating-projects/dynamic-links.html

### Web rendering/editor references

MDN:

- Canvas accessibility warning/reference: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/canvas
- SVG `viewBox`: https://developer.mozilla.org/en-US/docs/Web/SVG/Reference/Attribute/viewBox
- SVG pointer events: https://developer.mozilla.org/en-US/docs/Web/SVG/Reference/Attribute/pointer-events
- SVG accessibility via inline DOM/title/desc: https://developer.mozilla.org/en-US/docs/Web/SVG/Guides/SVG_in_HTML

Konva:

- Select/resize/rotate Transformer behavior: https://konvajs.org/docs/select_and_transform/Basic_demo.html
- Resize snapping: https://konvajs.org/docs/select_and_transform/Resize_Snaps.html
- Performance tips: https://konvajs.org/docs/performance/All_Performance_Tips.html
- Framework overview/serialization: https://konvajs.org/docs/overview.html

Fabric.js:

- Core concepts: https://fabricjs.com/docs/core-concepts/
- Why Fabric: https://fabricjs.com/docs/why-fabric/
- Custom properties/serialization: https://fabricjs.com/docs/using-custom-properties/

PixiJS:

- Introduction: https://pixijs.com/8.x/guides/getting-started/intro
- Performance tips: https://pixijs.com/8.x/guides/concepts/performance-tips
- Render loop: https://pixijs.com/8.x/guides/concepts/render-loop

---

## 28. Scope boundary reminder

This document is a future implementation input only.

It does **not** mean:

- the graphical editor exists;
- SVG is already a locked production renderer dependency;
- canonical visual schema integration is complete;
- Client Python is implemented;
- visual runtime composition is integrated;
- Engineering Fragments exist;
- Dynamo class/instance migration is implemented.

Those remain separate future implementation slices subject to the coordinator-owned dependency order and CI/review process.
