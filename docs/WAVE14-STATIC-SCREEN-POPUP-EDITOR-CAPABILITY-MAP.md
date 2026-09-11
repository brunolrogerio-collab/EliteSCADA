# Wave 14 — Static Screen/Popup Editor Capability Map

**Purpose:** reduce Wave 15 rediscovery by separating code that is demonstrably present/wired from behavior that still requires real UI validation.  
**Scope:** static repository analysis only.  
**Important:** `IMPLEMENTATION PRESENT` does **not** mean `WORKS` in the product UI.

> GitHub live is the official authority. Revalidate these paths against the exact Wave 15 baseline before correction work.

## 1. Classification used here

- `IMPLEMENTATION PRESENT — UI NOT YET VALIDATED`: code and wiring are visible in the current branch, but real developer behavior is not proven.
- `PARTIAL / NEEDS UI VALIDATION`: implementation exists but the end-to-end product result remains uncertain or already has audit evidence of failure.
- `CONFIRMED DEFECT`: Wave 14 already has sufficient evidence of a product defect.
- `NOT ESTABLISHED STATICALLY`: repository reading performed here does not prove the capability.

The final A7 closure still requires real UI classification as `WORKS`, `DEFECT`, `ABSENT/UNSUPPORTED`, or `NOT VALIDATED`.

## 2. Current architecture observed

### Screen workspace

`web/scada-web/src/engineering/visual-editor/VisualEditorWorkspaceLegacy.tsx`

The Screen workspace wires:

- `VisualEditorCanvas`;
- `ObjectPalette`;
- `DynamoLibraryPalette`;
- `PropertyInspector`;
- `DynamicPropertyEditor`;
- `BindingEditor`;
- session history/clipboard commands;
- canonical Preview and Apply through the Engineering API.

Screen draft mutations remain transient/session-local until the package is previewed and applied using the Workspace change version.

### Popup workspace

`web/scada-web/src/engineering/visual-editor/PopupVisualEditorWorkspaceImpl.tsx`

Popup authoring reuses the same canonical editor stack and adds:

- explicit popup logical bounds;
- runtime logical position calculation;
- logical canvas boundary;
- runtime-composition preview;
- canonical package Preview/Apply.

### Actual exported canvas

`web/scada-web/src/engineering/visual-editor/canvas/index.ts` exports `VisualEditorCanvas` from `EnhancedVisualEditorCanvas.tsx`, not directly from the older lower-level canvas file.

`EnhancedVisualEditorCanvas.tsx` wraps the lower-level canvas and adds:

- authoring toolbar;
- hierarchy outliner;
- marquee selection;
- smart alignment guides;
- authoring-lock interception;
- keyboard command resolution;
- surface and Dynamo inspectors.

This distinction matters when diagnosing a visible control: code in `VisualEditorAuthoringToolbar.tsx` is part of the active exported canvas path.

## 3. Static capability matrix

| Capability | Static state | Confirmed implementation path / note | Real UI closure still required |
|---|---|---|---|
| Object selection | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Canvas click selection + modifier selection; Enhanced canvas also handles locked-object selection. | Prove select/deselect and multi-select in Screen + Popup. |
| Structure-tree selection | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | `VisualEditorOutliner.tsx` renders hierarchy tree and dispatches selection with modifiers. | Prove tree↔canvas selection stays coherent. |
| Marquee selection | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | `EnhancedVisualEditorCanvas.tsx` resolves logical marquee selection. | Prove drag selection under zoom/pan and hierarchy. |
| Move | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Pointer move emits `object.move`; smart guides may adjust the delta. | Prove actual geometry update, snap behavior and persistence. |
| Resize | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Lower canvas exposes resize handles and `object.resize`. | Prove all expected handles and bounds under zoom/snap. |
| Rotate | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Rotate handle emits `object.rotate`; Shift snapping exists. | Prove rotation interaction/persistence. |
| Duplicate | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Canvas toolbar + keyboard/session model support duplicate. | Prove identity/key regeneration and placement. |
| Copy/Paste | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Authoring toolbar -> keyboard command -> session clipboard model. | Prove same-parent/nested behavior and user discoverability. |
| Undo/Redo | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Authoring toolbar -> session history model with bounded history. | Prove representative multi-step history and state after Apply/reset. |
| Delete | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Canvas toolbar, Delete/Backspace and session delete path. | Prove selection/history behavior. |
| Group/Ungroup | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | `VisualEditorAuthoringToolbar` dispatches group/ungroup through session authoring operations. | Prove nested geometry/selection/persistence and invalid-state disablement. |
| Lock/Unlock | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Toolbar/session authoring operation plus lock interception in enhanced/lower canvas. | Prove locked object cannot be modified yet remains inspectable/selectable. |
| Alignment | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Toolbar supports left/centers/right/top/middle/bottom and dispatches session operations. | Prove reference/selection semantics and geometry results. |
| Distribution | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Toolbar supports horizontal/vertical center and spacing distribution. | Prove minimum selection rules and geometry results. |
| Same width/height/size | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Toolbar supports typed size operations. | Prove reference-object semantics. |
| Z-order | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Lower canvas toolbar exposes send-to-back/backward/forward/front. | Prove sibling constraints and render order. |
| Zoom | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Zoom buttons, 100% reset and Ctrl/Meta+wheel. | Prove interaction and usability across normal authoring sizes. |
| Pan | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | wheel pan and middle-button/Alt drag path in lower canvas. | Prove discoverability and no accidental object mutation. |
| Grid | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Grid toggle defaults enabled. | Prove rendering/usefulness at zoom levels. |
| Snap | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Snap toggle and grid-size coordinate snapping in move/polygon paths. | Prove move/resize semantics expected by user. |
| Smart guides | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Enhanced canvas calculates vertical/horizontal move guides. | Prove visible/useful and no incorrect snap. |
| Polygon creation/edit vertices | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Polygon mode, finish/cancel, add/remove/drag vertex code exists. | Prove end-to-end authoring and persistence. |
| Property Inspector | PARTIAL / NEEDS UI VALIDATION | `PropertyInspector.tsx` builds registered schema-driven rows and dispatches typed set/remove intents. | Prove reachability, layout, editability and feedback for representative object types. |
| Text/content property editing | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Current `core.text`/`core.valueDisplay` schema exposes text properties; Property Inspector routes typed controls. | Prove normal text authoring in real UI. |
| Binding editor | PARTIAL / NEEDS UI VALIDATION | Screen/Popup workspaces mount `BindingEditor` for selected element using canonical project source catalog. | Prove browse/exact reference/application and persistence. |
| Dynamic expressions/conditions | PARTIAL / CONFIRMED LEGACY-TYPE DEFECT | `DynamicPropertyEditor` is wired in both workspaces; known legacy persisted types can crash strict schema lookup. | After W15 legacy fix, prove direct binding/condition/expression flows. |
| Analog fill authoring | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Dynamic editor exposes Analog Fill when supported by current type. | Prove supported types/directions and runtime result. |
| Events editor | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Property Inspector mounts `EventsEditor` for one selected persisted visual object. | Prove event discoverability and valid script/event connection. |
| Dynamo insertion | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | `DynamoLibraryPalette` mounted in Screen + Popup; canonical model has `dynamo.add`. | Prove selection/preview/insertion/public parameter workflow. |
| Image asset import | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Screen workspace imports PNG/JPEG/BMP only when draft clean, then reloads snapshot. | Prove user flow and asset selection through Properties. |
| Screen Preview before Apply | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | `previewEngineeringPackage`, Workspace CAS check, candidate state and gated Apply. | Prove invalid/valid candidate messaging and Apply. |
| Screen Apply | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Applies candidate package at validated `changeVersion`, reloads authoritative snapshot. | Prove persistence and no unintended Active mutation. |
| Popup logical bounds | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Popup workspace resolves/displays logical width/height and supplies `logicalBoundary` to canvas. | Prove developer can understand/edit desired bounds through available surface controls. |
| Popup runtime composition preview | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Popup preview uses Runtime logical viewport/position and canonical renderer. | Prove parity with actual Runtime composition. |
| Popup Preview/Apply | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Same package Preview/CAS/Apply architecture as Screen. | Prove complete Popup authoring lifecycle. |

## 4. Confirmed static implications

### 4.1 The problem is not simply missing editor internals

The current repository contains a substantial canonical authoring/session implementation. Therefore a Wave 15 correction plan should not begin by replacing the editor wholesale without first distinguishing:

- code that exists but is visually unreachable or awkward;
- controls that are rendered but fail at runtime;
- interaction bugs under real layout/zoom/selection;
- legacy-data compatibility failures;
- genuinely absent developer workflows.

### 4.2 Active toolbar path exists

Because `canvas/index.ts` exports the Enhanced canvas, the active Screen/Popup workspace path includes `VisualEditorAuthoringToolbar`, `VisualEditorOutliner`, smart guides and keyboard-command handling. If alignment/group/lock/copy/paste are absent or inoperative in the actual product, Wave 15 should investigate rendering/layout/event wiring/state rather than assuming no implementation exists.

### 4.3 Canonical persistence boundary already exists

Both Screen and Popup workspaces use Preview/Apply and Workspace `changeVersion` checking. Wave 15 usability fixes must preserve that authority instead of introducing a browser-only second project truth.

### 4.4 Popup authoring has explicit logical geometry code

The Popup workspace already calculates and exposes logical bounds/position and has a runtime-composition preview. Real UI validation must determine whether these controls/information are usable and whether actual Runtime parity holds; the capability should not be described as completely absent solely from the owner-visible symptom.

## 5. Known defect intersecting this map

`docs/WAVE14-DIAGNOSTIC-LEGACY-VISUAL-TYPE-COMPATIBILITY.md`

Known persisted legacy identifiers can reach strict current schema lookup and blank Screen/Popup authoring. This can make otherwise-present Property/Dynamic/Binding functionality look absent or unusable for affected objects. Wave 15 must correct that compatibility boundary before using legacy-object failures as proof that the entire editor feature is unimplemented.

## 6. Required live A7 test script for Codex / Wave 15 audit

When a stable Engineering Working state is available, test one current canonical Screen and one Popup using a current `core.*` object first, then repeat selected flows with migrated legacy content after W15-P1-01.

For each feature record only one of:

- `WORKS` — completed in the real product and persisted as expected;
- `DEFECT` — surfaced capability exists but actual behavior is wrong;
- `ABSENT/UNSUPPORTED` — no supported product action exists;
- `NOT VALIDATED` — environment/other blocker prevented a fair test.

Minimum sequence:

1. create/select a current rectangle/text/value-display object;
2. canvas and Outliner selection/multiselect;
3. move/resize/rotate;
4. copy/paste/duplicate/delete + undo/redo;
5. group/ungroup + lock/unlock;
6. align/distribute/same-size + z-order;
7. zoom/pan/grid/snap and marquee;
8. edit representative Properties including text/geometry/appearance;
9. create/remove a binding and a dynamic expression/condition;
10. Preview, inspect validation, Apply, reload and confirm persistence;
11. perform equivalent core selection/move/resize/property/Preview/Apply in Popup;
12. inspect Popup logical bounds and runtime-composition preview;
13. record any UI control that is visible/enabled but not actionable as a functional defect.

## 7. Wave 14 conclusion from static analysis

A7 remains **not closed**, because repository code cannot prove real developer usability. However, Wave 14 has now bounded the implementation surface enough that the remaining real UI pass should be a deterministic capability verification rather than open-ended exploration.

No product code was changed by this analysis.