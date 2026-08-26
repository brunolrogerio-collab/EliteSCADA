# EliteSCADA Engineering UI baseline

## Status

Initial frontend foundation under development on `feature/engineering-ui-foundation`.

This document defines how the developer-facing Engineering UI consumes the existing EliteSCADA platform. It does not replace the public Engineering contracts, `PROJECT GOAL.md`, ADR-008 or the visual-component-library baseline.

## Non-negotiable boundary

The Engineering UI is a **client of the public Engineering model**.

It must never become an alternative source of project truth. Browser-only state may represent transient presentation state such as the selected navigation section, filters, dialog state or unsaved form input, but authoritative project configuration must flow through the public Engineering contracts and the common validation/import workflow.

The first UI slice is intentionally read-only. It proves that the browser can render the project directly from:

- `GET /api/engineering/workspace`;
- `GET /api/engineering/export/json`.

Later editing will continue to use the platform flow:

`edit draft -> validate -> preview -> apply to Working Workspace -> save Revision -> publish -> activate`

The exact endpoint shape for interactive editor mutations may evolve, but it must preserve the same public DTO semantics and validation rules used by Import/Export.

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

After the read-only foundation is validated:

### Phase A — engineering data editors

1. Data Sources / driver configuration;
2. TAG table/tree editor;
3. alarm editor;
4. historian policy editor;
5. security-role policy editor when backend lifecycle is ready.

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

The read-only foundation is based on current `main`. PRs #35 and #36 add command-domain and read/realtime security changes independently. When those branches merge, this UI must adapt to their authenticated API behavior without introducing ad-hoc hard-coded JWT storage.

## Testing baseline

The Engineering UI should maintain browser coverage for:

- `/engineering` startup against the real API;
- rendering entities from the exported public Engineering package;
- navigation among current domains;
- `pt-BR`, `en` and `es` switching;
- locale persistence as a presentation preference;
- stable Engineering identifiers across language changes;
- Runtime ↔ Engineering navigation;
- future authorization-aware visibility once login/profile flow exists.

Backend and browser test failures must be fixed at the source rather than bypassed to make the editor appear functional.
