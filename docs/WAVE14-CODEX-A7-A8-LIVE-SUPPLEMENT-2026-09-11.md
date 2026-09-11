# Wave 14 Codex A7/A8 live UI supplement - 2026-09-11

## Authority and scope

- GitHub issue #286 was revalidated live after the run. Comment 5629941076 remains the latest MAIN COORDINATOR -> CODEX instruction; comment 5634220264 remains the latest prior Codex handoff.
- Technical candidate: 59e815eae524b9ff043ea6bf3f797f4c01ba9143.
- Package SHA-256: e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d.
- Surface: Codespaces forwarded Engineering at port 5173, Working project demo, clean Workspace/changeVersion 0 before each editor run.
- This supplement is diagnostic evidence only. No product code, Working Apply, Save, Publish, Activate, checkout, persistence, package, test, workflow or protected branch was changed.

## W14-CODEX-A7-001 - P1 - Screen/Popup selection blanks the entire SPA

- Area: Engineering / Screen Editor / Popup Editor / Properties and bindings.
- Classification: CONFIRMED / GENERIC PRODUCT.
- Reproducibility: 5/5 total. Screen: 3/3 across two fresh tabs (pump01 twice, tank01 once). Popup: 2/2 across two fresh tabs (current twice).

### Reproduction

1. Open /engineering and wait for the Working snapshot.
2. Open Screens, choose Demo Overview, then click pump01 DYN or tank01 TANK in the object tree.
3. In a fresh session open Popups, choose Standard Pump Popup, then click current VALU in the object tree.
4. Observe the page immediately after selection.

### Expected

The object becomes selected; selection-dependent controls enable; Properties and binding editors render; the user can move, resize, copy, align, group, lock, change z-order, edit text/bindings, preview and apply through the normal guarded flow.

### Observed

The complete Engineering SPA becomes a blank dark viewport. URL and title remain /engineering and SCADA. The DOM snapshot is empty. Direct navigation back to /engineering in the poisoned tab remained blank. A new tab can load Engineering again, although one fresh load independently showed Failed to fetch and recovered through Tentar novamente.

### Evidence

- evidencias/W14-CODEX-A7-EDITOR-SELECTION-BLANK-2026-09-11.jpg: screenshot of the blank viewport after Popup current selection.
- Image SHA-256: 9de07e850c721c9e2efea07a27153bd7ca4f942c30ab9bfe2ffb9813239d1a20.
- evidencias/W14-CODEX-A7-A8-LIVE-2026-09-11.txt: reproduction matrix and DOM/code anchors.
- Before selection, the Popup editor exposed 3 objects and the full editing toolbar; after selection the browser accessibility DOM length was 0.

### Responsible layer and bounded mechanism

Both editor workspaces route object-tree selection through the shared visual editor selection/render path:

- web/scada-web/src/engineering/visual-editor/canvas/VisualEditorCanvas.tsx:109 emits selection.change.
- web/scada-web/src/engineering/visual-editor/VisualEditorWorkspaceLegacy.tsx:206 updates the canonical session selection, then mounts PropertyInspector and DynamicPropertyEditor for selected elements around lines 420-428.
- web/scada-web/src/engineering/visual-editor/PopupVisualEditorWorkspaceImpl.tsx:191-195 derives selectedElements/selectedElement; line 245 handles selection.change; lines 440-452 mount the same PropertyInspector and DynamicPropertyEditor.
- web/scada-web/src/engineering/visual-editor/property-inspector/PropertyInspector.tsx:91-92 builds the selected property model.
- web/scada-web/src/engineering/visual-editor/dynamic-property-editor/DynamicPropertyEditor.tsx:57 onward derives bindable destinations from element type.

The failure occurs during the render path entered by non-empty selection and is shared by Screen and Popup. The exact thrown exception was not captured, so attribution below PropertyInspector versus DynamicPropertyEditor versus a derived model remains UNCERTAIN - BOUNDED. Local API/Vite outage is excluded for the repeated immediate click trigger, and different object types reproduce it.

### Wave 15 correction contract

Known persisted legacy visual types must select and render compatible property/binding models without crashing. Truly unknown types must retain explicit rejection/recovery behavior. Add an editor-level error boundary that preserves navigation and reports the object id/type and recoverable action without exposing secrets. Do not weaken canonical selection, CAS Preview/Apply, lifecycle, authorization or unknown-type rejection.

### Deterministic regression

Mount a persisted Screen containing tank, dynamo and value objects and a persisted Popup containing value/status objects. For each object: select from tree and canvas, assert SPA remains mounted, Properties and binding panel render, selection controls become truthful, then exercise move/resize, copy/paste, undo/redo, group/ungroup, lock, align/distribute, z-order, zoom/pan, grid/snap, text/binding edit and Preview. Add a truly unknown type fixture and assert explicit bounded error/recovery instead of blank SPA.

## A7 capability classification from this live run

- WORKS: Engineering snapshot load/retry, Screen/Popup list, canvas render before selection, object palette, dynamo library, tree population, editing toolbar display, Runtime-composition preview display.
- DEFECT: selecting an existing object from either Screen or Popup tree.
- NOT VALIDATED because selection crashes first: move/resize, copy/paste, undo/redo, group/lock, align/distribute/same-size, z-order, Properties, text, bindings and guarded Preview/Apply after edits.
- NOT VALIDATED: persistence/reopen. No Apply/Save/Publish/Activate was performed.

## W14-CODEX-A8-001 - P2 - Script assistant cannot compose the requested two-TAG visual-state rule safely

- Area: Engineering / Scripts / discovery, authoring and validation.
- Classification: CONFIRMED / GENERIC PRODUCT.
- Reproducibility: 1/1 exact PO attempt.

### Reproduction and observed behavior

1. Open Scripts and create a client draft.
2. In the TAG assistant click Inserir - Ler for Flow and then Discharge Pressure at the current Monaco caret.
3. Add initialize as the entry point and run Validar / Preview.

The editor produced concatenated snippets with no safe separator and reused the same variable:

~~~python
from elite_scada import tag_read
value = await tag_read("00000000-0000-0000-0000-000000000007")from elite_scada import tag_read
value = await tag_read("00000000-0000-0000-0000-000000000006")pass
~~~

Validation correctly blocked Preview/Apply with one Python compilation error, but the assistant did not create a usable two-value comparison. The UI offered no guided condition builder or complete example to compare the two values and then change a selected object's color/state.

The assistant did expose seven TAGs with stable ids, types and read/write affordances. Its Screens tab exposed tank01, pump01, pressure and flow objects, but every object showed Propriedades (0). The APIs tab listed tag_read/tag_write, client_memory_read/write, visual_property_read, visual_property_write/clear and visual_tween_request, but provided no signatures, parameters, property keys, target-selection guidance, examples or insert actions. A developer therefore cannot discover the exact object/property/API call needed for the PO recipe without guessing or external source inspection.

### Wave 15 correction contract and regression

Snippet insertion must be syntax-safe at any caret, avoid duplicate imports and allocate distinct editable variables. Provide searchable API signatures and runnable examples that connect TAG ids, visual object ids, supported property keys, condition expression and visual_property_write/clear. A guided recipe for compare two TAGs -> update object color/state should validate without manual syntax repair. Regression: starting from an empty client script, insert two reads, a comparison and a visual write through visible UI only; assert Python validation passes, Preview is available, the target property changes when true and clears/restores when false, with no persistence until Apply.

## W14-CODEX-A8-002 - P2 - Server scope retains client-oriented authoring state without server guidance

- Area: Engineering / Scripts / scope and lifecycle.
- Classification: CONFIRMED / GENERIC PRODUCT.
- Reproducibility: 1/1 draft scope transition.

Changing the same draft from Cliente to Servidor removed the client assistant and controlled Preview, but retained client imports/source, the Client Visual API v1 header and client visual entry points such as initialize, interaction, property changed and frame tick. No equivalent Server Script API assistant, signatures or examples appeared. This compounds the already-confirmed timer/tagChanged event-field gap documented separately by the Main Coordinator.

Wave 15 must make a scope change explicit and safe: either migrate compatible state or request a deliberate reset; reject or visibly flag incompatible imports/events; show server-specific APIs, lifecycle, trigger requirements and observability. Regression: create a client draft, change to server, and assert incompatible source/event state cannot remain silently valid; author timer and tagChanged with required fields through the UI, validate, persist through the guarded flow in a disposable test project, and observe diagnostics/runtime behavior.

## Recommended order

1. Correct A7 selection/render crash first because it blocks practical validation of almost every Screen/Popup authoring function.
2. Add the editor crash boundary and known-legacy compatibility regression without weakening unknown-type rejection.
3. Complete Script discovery/composition and scope-specific authoring together with the already accepted event-field gap.
4. Re-run the full A7/A8 functional inventory on a disposable Wave 15 project, including persistence/reopen and observable Runtime behavior.
