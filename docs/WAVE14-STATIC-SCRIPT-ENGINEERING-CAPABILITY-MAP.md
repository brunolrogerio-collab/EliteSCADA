# Wave 14 — Static Script Engineering Capability Map

**Purpose:** reduce Wave 15 rediscovery by separating implemented Script Engineering infrastructure from developer workflows that still require real UI validation.  
**Scope:** static repository analysis only.  
**Important:** `IMPLEMENTATION PRESENT` does **not** mean `WORKS` in the real product UI.

> GitHub live is the official authority. Revalidate these paths against the exact Wave 15 baseline before correction work.

## 1. Classification

- `IMPLEMENTATION PRESENT — UI NOT YET VALIDATED`: code and wiring are present, but practical developer behavior is not proven.
- `PARTIAL / DISCOVERABILITY GAP`: pieces exist but the current static design exposes a usability/composition limitation that Wave 15 should verify and likely improve.
- `NOT ESTABLISHED STATICALLY`: repository reading does not prove an end-to-end supported product workflow.

Final A8 closure still requires a real UI classification as `WORKS`, `DEFECT`, `ABSENT/UNSUPPORTED`, or `NOT VALIDATED`.

## 2. Principal source paths

- `web/scada-web/src/engineering/scripts/ScriptEngineeringWorkspace.tsx`
- `web/scada-web/src/engineering/python-editor/PythonMonacoEditor.tsx`
- `web/scada-web/src/engineering/scripts/PythonScriptAssistant.tsx`
- `web/scada-web/src/engineering/scripts/ScriptAssistantPanel.tsx`
- `web/scada-web/src/engineering/scripts/scriptAssistantModel.ts`
- `web/scada-web/src/engineering/scripts/PythonPreviewTestPanel.tsx`
- `web/scada-web/src/engineering/scripts/PythonScriptReferenceDiagnostics.tsx`
- `web/scada-web/src/engineering/scripts/scriptEngineeringApi.ts`
- `web/scada-web/src/engineering/scripts/ScriptEngineeringWorkspace.logic.ts`

## 3. Static architecture observed

### 3.1 Script Engineering workspace

`ScriptEngineeringWorkspace.tsx` loads the canonical Script Engineering context, exposes script search/listing, create/update/delete operations, draft validation, entry points, dependencies, Preview/Apply and client-visual Python preview execution.

The workspace does not save a script by silently mutating browser-only state: it builds a canonical script package, obtains a Preview token and only enables Apply when the preview is current/valid and blocking Python diagnostics are absent.

### 3.2 Monaco editor

`PythonMonacoEditor.tsx` creates a Python Monaco model with line numbers, folding, indentation, cursor line/column, markers and diagnostics. It mounts:

- `PythonScriptReferenceDiagnostics`;
- `PythonScriptAssistant`;
- `PythonPreviewTestPanel`;

for editable `clientVisual` scripts.

### 3.3 Object/TAG/API assistant

`PythonScriptAssistant.tsx` loads the canonical Script Engineering context and supplies the current script's persisted visual-event references to `ScriptAssistantPanel`.

`ScriptAssistantPanel.tsx` loads the current Engineering snapshot and builds an explicit searchable catalog with tabs for:

- TAGs;
- Screens;
- Popups;
- Client Memory;
- client-visual capabilities/APIs.

The panel can insert generated snippets at the current Monaco cursor.

### 3.4 Snippet/catalog model

`scriptAssistantModel.ts` builds stable-reference-aware models for TAGs, Screen/Popup visual objects, visual properties, Dynamo public parameters, Client Memory and runtime capabilities.

Generated snippet families include:

- `tag-read` / `tag-write`;
- `client-memory-read` / `client-memory-write`;
- `visual-property-read` / `visual-property-write`;
- `visual-property-clear`;
- `visual-tween`.

The model disables writes when a TAG/property is read-only or otherwise unsuitable, rather than always emitting an unsafe write sample.

### 3.5 Preview execution

`PythonPreviewTestPanel.tsx` can run a selected handler with a JSON sample payload, supports cancellation, returns syntax/runtime diagnostics, duration, sanitized errors and a bounded traceback/failing source line projection.

`ScriptEngineeringWorkspace.tsx` also has a handler-preview path in addition to mutation Preview/Apply.

## 4. Capability matrix

| Capability | Static state | Evidence / implication | Real UI closure required |
|---|---|---|---|
| List/search scripts | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Workspace loads and filters canonical scripts. | Prove discoverability/performance with real project. |
| Create new script | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | `startNewScript()` builds a new definition. | Prove normal authoring flow. |
| Edit name/path/description/enabled | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Draft fields are wired in workspace. | Prove usability and validation feedback. |
| Scope selection | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Workspace exposes configured script scopes. | Prove scope semantics are understandable. |
| Monaco Python editing | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Python model, line numbers, folding, indentation, cursor position. | Prove editor loads reliably and is usable. |
| Syntax/semantic diagnostics | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Monaco markers + diagnostic projection + Preview diagnostics. | Prove messages/line-column feedback with deliberate errors. |
| Entry-point authoring | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Add/remove event kind, handler name and target reference. | Prove event selection and target-reference usability. |
| Entry-point autocomplete | IMPLEMENTATION PRESENT — NARROW | Monaco provider maps `buildEntryPointCompletions(...)`. | Verify usefulness; it is not a general project/API completion provider. |
| TAG discovery | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Assistant builds TAG catalog with stable identity/source/read-only state. | Prove a developer can find the intended TAG quickly. |
| TAG read/write snippets | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Generates `tag_read` and guarded `tag_write`. | Prove insertion, syntax and actual allowed preview/runtime use. |
| Screen/Popup object discovery | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Assistant recursively catalogs visual definitions/objects. | Prove navigation/search with realistic project size. |
| Visual property discovery | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Uses registered visual-property schemas and reports read/write/binding/animation capability. | Prove developer can choose the right object/property. |
| Visual property read/write snippets | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Generates read/write/clear and tween samples where allowed. | Prove runtime-context targeting and useful disabled reasons. |
| Client Memory discovery/snippets | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Catalog plus guarded read/write samples. | Prove normal developer flow. |
| API/capability discovery | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Separate Capabilities tab maps contract names to `elite_scada.*` APIs. | Prove understandable API help and examples. |
| General TAG/API Monaco autocomplete | PARTIAL / DISCOVERABILITY GAP | Current completion provider in `PythonMonacoEditor` is built from entry points; TAG/API discovery is primarily in the separate assistant. | Determine whether context-aware completion should be integrated into Monaco. |
| Compound script recipe | PARTIAL / DISCOVERABILITY GAP | Primitive snippets exist, but no static evidence here of a guided recipe composing multiple TAG reads/conditions/visual writes into one task. | Prove representative compound flow; likely add recipes/templates/contextual assistance. |
| Visual event context | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Assistant uses persisted `ScriptVisualEventReference` and restricts visual-property action snippets to an unambiguous selected target. | Prove event-to-script/target association is discoverable. |
| Dependencies | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Workspace exposes typed stable-reference dependencies. | Prove when/why user must manage these. |
| Script validation before apply | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Local draft validation plus Python diagnostic blocking. | Prove actionable errors rather than cryptic codes. |
| Mutation Preview | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Canonical Preview token includes create/update/skip/error counts. | Prove user understands Preview vs execution test. |
| Apply to Working Engineering | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Apply gated on current valid Preview and change-version semantics. | Prove persistence/reload and conflict behavior. |
| Delete with dependency protection | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Delete conflict extracts and displays dependencies. | Prove safe behavior and useful remediation. |
| Handler sandbox preview | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Selected handler execution with diagnostics/result. | Prove representative success/failure/cancel. |
| Preview payload | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | JSON sample payload can be provided to handler preview. | Prove relation to actual event payload is clear. |
| Traceback/failing source line | IMPLEMENTATION PRESENT — UI NOT YET VALIDATED | Preview projects bounded trace frames and failing source line. | Prove developer can debug a realistic error. |
| Save Revision / Publish / Activate integration | NOT ESTABLISHED AS ONE SCRIPT-SCREEN FLOW | Script Apply writes Working Engineering; broader lifecycle exists elsewhere. Static inspection does not prove the user is guided from script edit through Revision/Publish/Activate. | Prove end-to-end lifecycle in real UI. |
| Observe script in real Runtime | NOT ESTABLISHED STATICALLY | Engineering preview exists, but repository reading here does not prove a developer-complete runtime observation/debug surface for an activated script. | Required real UI/runtime proof. |
| Server Script developer flow | NOT ESTABLISHED BY THIS CLIENT-VISUAL MAP | `PythonMonacoEditor` exposes rich assistant/preview only for `clientVisual` scope in the inspected path. | Separately validate Server Script authoring/execution diagnostics. |

## 5. Important static findings

### 5.1 The infrastructure is not absent

The repository already contains a substantial Script Engineering stack: canonical CRUD/Preview/Apply, Monaco editing, diagnostics, entry points, dependencies, searchable project-reference assistance, generated snippets and a bounded client-visual preview executor.

Therefore Wave 15 should not begin from the premise that scripting must be rewritten from zero.

### 5.2 Discoverability is fragmented

The current static design splits developer assistance across several surfaces:

- Monaco completion provider: entry-point completions;
- API Help details: capability documentation;
- Script Assistant: TAG/object/property/Client Memory/API lookup and snippet insertion;
- Preview Test: execution/diagnostics.

This may be functionally valid, but it creates a real Wave 15 usability question: can a developer discover how to go from intent to working code without already understanding how these separate tools fit together?

### 5.3 Primitive snippets do not prove compound authoring

The assistant can generate individual reads/writes/tweens. That does not by itself prove a developer can easily author a compound requirement such as:

`read TAG A + read TAG B -> compare condition -> write visual property -> bind to intended event -> preview -> persist -> activate -> observe`

Wave 15 acceptance must test this complete workflow.

### 5.4 Engineering Preview is not the same as deployed Runtime debugging

A bounded handler Preview exists and provides useful diagnostics. Wave 15 still needs to establish the developer-facing path from an applied Working script through Revision/Publish/Activate to observable runtime behavior, including failure diagnostics after activation.

### 5.5 Current static helper degrades unknown visual schemas more safely than the editor path

`scriptAssistantModel.ts` catches unknown visual schema lookup and marks the object `schemaStatus: 'unknown'` with no properties instead of throwing. This is useful precedent for robust UI degradation, but it does not solve the separate Screen/Popup authoring compatibility defect documented in `docs/WAVE14-DIAGNOSTIC-LEGACY-VISUAL-TYPE-COMPATIBILITY.md`.

## 6. Required real A8 verification

When Engineering Working is stable, perform this deterministic product test and classify each step only as `WORKS`, `DEFECT`, `ABSENT/UNSUPPORTED`, or `NOT VALIDATED`:

1. open Scripts and select an existing script;
2. create a new client-visual script;
3. edit identity/path/description and source;
4. deliberately introduce a Python syntax error and verify understandable line/column diagnostics;
5. add/edit an entry point and associate its intended target/context;
6. use the assistant to locate two specific TAGs;
7. insert/read both TAGs;
8. locate a target Screen/Popup visual object and a runtime-writable property;
9. compose a condition comparing the two TAG values and conditionally write that visual property;
10. run the handler Preview with representative payload and inspect success/failure/trace feedback;
11. Preview the Engineering mutation and Apply it to Working;
12. verify persistence after reload;
13. Save Revision / Publish / Activate through the normal lifecycle as applicable;
14. exercise the actual event in Runtime and observe the visual result;
15. deliberately introduce a runtime-safe failure and prove the developer can discover its diagnostic without repository/DB/manual-API access;
16. verify security/capability denial remains explicit and scripts do not bypass the logged-in principal.

Also test one Server Script flow separately; do not infer Server Script usability from the richer `clientVisual` tools.

## 7. Wave 14 closure state

A8 is **not yet closed**, because static code cannot prove practical developer usability or deployed runtime debugging. However, the implementation surface and the likely discoverability gaps are now bounded enough that the remaining live test is deterministic rather than exploratory.

No product code was changed by this analysis.