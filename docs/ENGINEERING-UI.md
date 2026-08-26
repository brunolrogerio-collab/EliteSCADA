# EliteSCADA Engineering UI baseline

## Status

Initial frontend foundation under development on `feature/engineering-ui-foundation`.

This document defines how the developer-facing Engineering UI consumes the existing EliteSCADA platform. It does not replace the public Engineering contracts, `PROJECT GOAL.md`, ADR-008 or the visual-component-library baseline.

## Non-negotiable boundary

The Engineering UI is a **client of the public Engineering model**.

It must never become an alternative source of project truth. Browser-only state may represent transient presentation state such as the selected navigation section, filters, dialog state or unsaved form input, but authoritative project configuration must flow through the public Engineering contracts and the common validation/import workflow.

The foundation reads project state directly from:

- `GET /api/engineering/workspace`;
- `GET /api/engineering/export/json`.

TAG, Data Source and Alarm sections now provide the first controlled editing stage. Their form values are **transient drafts only**. A draft is cloned into the complete canonical `scada.engineering` package and sent to:

- `POST /api/engineering/import/json/preview`.

The preview uses the same backend parsing, cross-reference checks and validation rules as ordinary Engineering Import/Export. No Apply operation is exposed by these first editors yet. Running preview must not dirty the Working Workspace, mutate the runtime or create a revision.

The intended lifecycle remains:

`edit draft -> validate -> preview -> apply to Working Workspace -> save Revision -> publish -> activate`

The exact endpoint shape for later interactive mutations may evolve, but it must preserve the same public DTO semantics and validation rules used by Import/Export.

## Draft and preview semantics

The initial structured editors deliberately separate three states:

1. **Original**: the entity from the public Engineering snapshot loaded from the backend.
2. **Draft**: transient browser form state for one selected entity or one proposed new entity.
3. **Preview result**: the backend validation result for a complete package containing that draft.

Rules:

- selecting another entity while the current draft has changes requires explicit discard confirmation;
- leaving the page with a changed draft registers browser `beforeunload` protection;
- `Discard draft` restores an existing entity exactly from the loaded public snapshot or resets a new-entity draft to its empty template;
- any edit invalidates the previous preview immediately;
- preview sends a cloned full Engineering package, not an editor-private partial DTO with different semantics;
- preview never applies the candidate;
- validation errors and warnings are shown using the backend issue codes/messages;
- a green preview does not imply the project was changed;
- future Apply must be a distinct deliberate action and must use backend authorization/audit once the security branch reaches main.

This distinction is important for industrial engineering: a form may be syntactically complete while cross-references, Data Source relationships, alarm TAG bindings, scaling, permissions or other package-level rules still make the resulting project invalid.

## Initial structured editors

### TAG editor

The first TAG editor exposes draft fields for:

- name;
- stable path;
- data type;
- Data Source reference;
- communication address;
- engineering unit;
- description;
- read-only behavior;
- scale minimum/maximum;
- historian enabled state;
- historian strategy;
- deadband;
- period;
- maximum period.

Fields not yet exposed in the form, including metadata and detailed access policy, remain preserved in the cloned canonical entity and are not discarded by preview.

The editor can also create a **new TAG draft** with no ID. The draft is appended only to the cloned candidate package. Backend preview must classify a valid unique draft as `Create`; the live Engineering export remains unchanged until a future explicit Apply exists.

### Data Source editor

The first Data Source editor exposes:

- key;
- name;
- driver type;
- enabled state;
- public technical settings as key/value pairs.

Secret material is never loaded into the editor. `secretReferences` remain reference strings only and are shown read-only in this first slice. The backend validator continues to reject plaintext-secret-like settings and invalid secret-reference schemes.

The editor can also create a **new Data Source draft** with no ID. Like TAG creation, it exists only in the cloned preview candidate and must be classified by the backend as `Create` without mutating the Working Workspace.

### Alarm editor

The Alarm section now follows the same draft/preview model and exposes:

- name;
- associated TAG path;
- alarm type (`digital`, `high`, `highHigh`, `low`, `lowLow`, `communication`, `system`);
- priority (`low`, `medium`, `high`, `critical`);
- analog setpoint where applicable;
- digital active value for digital alarms;
- alarm class;
- area;
- message;
- activation delay in milliseconds;
- enabled state;
- acknowledgement requirement;
- shelving permission.

Existing metadata not exposed by the form remains preserved in the cloned canonical alarm entity.

The public alarm contract can carry both `tagId` and `tagPath`, while backend TAG resolution prefers an existing `tagId` before `tagPath`. Therefore, when the engineer changes the TAG path in the Alarm editor, the draft explicitly clears `tagId`. This forces backend preview to validate the newly selected path rather than accidentally resolving the old TAG identity.

Alarm preview remains backend-authoritative:

- analog alarms without setpoint are rejected by the common validator;
- negative activation delay is rejected;
- missing/blank TAG association is rejected;
- a non-existent TAG reference is rejected with `ALARM_TAG_NOT_FOUND` by the existing cross-reference handler.

The editor can also create a **new Alarm draft** without ID. Valid new alarms are appended only to the cloned candidate package and must be classified as `Create` by preview. They do not appear in the live Engineering export until a future Apply operation exists.

## Initial route and shell

The Engineering environment is available at:

`/engineering`

The existing `/` route remains the runtime HMI demonstration.

The Engineering shell contains:

- product/environment header;
- explicit link back to Runtime;
- project/workspace identity;
- schema and base-revision summary;
- dirty/clean Working Workspace state;
- developer-selectable language;
- Project Explorer/navigation grouped by Engineering domain;
- main content region consuming public Engineering entities.

Initial navigation domains:

1. Project overview;
2. Data Sources;
3. TAGs;
4. Alarms;
5. Equipment Templates;
6. Equipment;
7. Dynamos;
8. Screens;
9. Popups;
10. Historian policies;
11. Security roles/capabilities;
12. Diagnostics.

Future additions such as Commands, Trends, Users, Driver Modules, Libraries, Engineering Fragments and application-shell engineering join this navigation when their public contracts exist on the target branch/mainline.

## Localization

ADR-008 is active from the first Engineering UI component rather than being retrofitted later.

Supported locales:

- `pt-BR`;
- `en`;
- `es`.

Product-owned Engineering UI text is addressed through stable resource keys. The selected locale is stored under the browser preference key:

`elitescada.engineering.locale`

This local browser persistence is only a presentation preference. Once the user/profile subsystem exists, the same preference should be associated with the authenticated profile according to ADR-008.

Changing locale must not modify:

- Engineering IDs;
- TAG paths;
- Data Source keys;
- addresses;
- enum/storage values;
- public JSON/CSV/XLSX keys;
- revision identity;
- authorization semantics.

Browser tests must explicitly preserve stable Engineering identifiers while switching language.

## Engineering UX direction

The visual style follows the high-performance-HMI principle already adopted for the visual component library:

- neutral/dark engineering workspace;
- restrained accent color;
- strong colors reserved for warnings, errors, dirty state and abnormal conditions;
- dense but readable tabular engineering data;
- stable monospace treatment for keys, TAG paths and technical identifiers;
- responsive navigation for smaller engineering workstations without turning the desktop UI into a consumer/mobile-first interface.

The Engineering UI is not the runtime HMI editor itself. It is the broader development environment that will host that editor alongside communication, TAG, alarm, historian, security, revision and administration tools.

## Planned editor progression

### Phase A — engineering data editors

Current:

1. Data Source structured draft + backend preview, including create preview;
2. TAG structured draft + backend preview, including create preview;
3. Alarm structured draft + backend preview, including create preview and TAG cross-reference validation;
4. changed-draft protection when switching entities or leaving the page.

Next after validation:

5. explicit Apply into the Working Workspace with confirmation and refresh;
6. delete workflows with preview;
7. bulk/multi-selection editing where appropriate;
8. dedicated historian-policy/bulk editor on top of the TAG historian contract;
9. security-role policy editor when backend lifecycle is ready.

These editors should favor structured tables, property panels and bulk workflows where that is more efficient than modal forms.

### Phase B — reusable object engineering

1. Equipment Templates;
2. Equipment instances;
3. Dynamos;
4. bindings/context/property inspectors;
5. version-aware reusable libraries.

### Phase C — graphical visualization engineering

1. screen/popup tree;
2. canvas/SVG editor;
3. reusable component palette;
4. property and binding inspector;
5. faceplates/popups;
6. command/setpoint widgets respecting backend capabilities;
7. Engineering Fragment copy/paste with dependency preview.

The canvas must serialize to the same public Screen/Popup/Dynamo Engineering model. It must not persist an opaque browser-specific scene graph as the only representation.

### Phase D — project and administration workflows

1. validation/problem list;
2. Import/Export;
3. save/revision comparison;
4. publish/activate;
5. Users/Roles once identity lifecycle exists;
6. Driver Module catalog/administration;
7. diagnostics.

## Security boundary

Backend authorization remains authoritative.

The Engineering UI may hide or disable controls based on the authenticated principal's capabilities, but this is presentation only. A browser cannot grant itself Engineering or process authority by changing local state.

PRs #35 and #36 add command-domain and read/realtime security changes independently. When those branches merge, this UI must adapt to their authenticated API behavior without introducing ad-hoc hard-coded JWT storage.

The current preview-only editor deliberately avoids Apply while those authorization changes remain outside `main`. This keeps the branch independent and prevents a frontend mutation path from getting ahead of the backend security boundary.

## Testing baseline

The Engineering UI should maintain browser coverage for:

- `/engineering` startup against the real API;
- rendering entities from the exported public Engineering package;
- navigation among current domains;
- `pt-BR`, `en` and `es` switching;
- locale persistence as a presentation preference;
- stable Engineering identifiers across language changes;
- Runtime ↔ Engineering navigation;
- valid TAG draft preview;
- invalid TAG draft preview using backend issue codes;
- valid Data Source draft preview;
- valid Alarm draft preview;
- invalid Alarm TAG reference expecting `ALARM_TAG_NOT_FOUND`;
- changed-draft confirmation when switching entities;
- new TAG preview classified as create without mutating export;
- new Data Source preview classified as create without mutating export;
- new Alarm preview classified as create without mutating export;
- proof that preview does not change Workspace dirty state/change version;
- proof that preview does not mutate exported Engineering entities;
- future authorization-aware visibility once login/profile flow exists.

Backend and browser test failures must be fixed at the source rather than bypassed to make the editor appear functional.
