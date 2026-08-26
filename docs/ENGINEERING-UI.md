# EliteSCADA Engineering UI baseline

## Status

The Engineering UI foundation is merged on `main` and available at `/engineering`.

The developer-facing Engineering UI consumes the public EliteSCADA Engineering model. It does not replace `PROJECT GOAL.md`, the public Engineering contracts, security rules, revision lifecycle or visual-runtime architecture.

Current merged baseline includes:

- Engineering shell and navigation;
- Runtime ↔ Engineering navigation;
- `pt-BR`, `en` and `es` localization;
- structured TAG, Data Source and Alarm editors;
- backend-authoritative preview/apply for supported structured mutations;
- explicit dependency-aware Delete;
- safe scoped Bulk Preview/Apply;
- workspace change-version protection for mutations;
- authorization/audit enforcement on protected mutation paths.

Relevant merged checkpoints include PR #37 for the UI foundation and PR #42 for secured Apply/Delete/Bulk workflows.

## Non-negotiable boundary

The Engineering UI is a **client of the public, versioned Engineering model**.

It must never become an alternative source of project truth. Browser-only state may represent transient presentation state such as selected navigation, filters, dialogs and unsaved form drafts, but authoritative project configuration flows through public Engineering contracts and backend validation/mutation services.

The intended lifecycle remains:

`edit draft -> validate/preview -> apply to Working Workspace -> save Revision -> publish -> activate`

The UI must not bypass this lifecycle by directly mutating Active Runtime or persisting a browser-private scene/configuration graph.

## Current draft / preview / apply semantics

Structured editors distinguish:

1. **Original** — current entity from the authoritative Engineering snapshot;
2. **Draft** — transient browser form state;
3. **Preview result** — backend validation result for the complete candidate Engineering package;
4. **Applied Working state** — authoritative Engineering Workspace only after an explicit secured Apply succeeds.

Rules:

- switching away from a changed draft requires explicit discard confirmation;
- leaving the page with changed draft state uses browser unload protection;
- any draft change invalidates a previous preview;
- preview sends canonical candidate Engineering data and does not mutate the Workspace;
- Apply is a separate deliberate operation;
- Apply requires the candidate that passed preview and backend revalidation;
- stale Workspace change versions are rejected with conflict rather than silently overwriting newer Engineering changes;
- successful Apply reloads the authoritative Engineering snapshot;
- browser drafts never become project truth merely because preview was green.

## Explicit Delete

Deletion is explicit and backend-authoritative.

Current secured mutation behavior includes supported TAG, Alarm and Data Source deletion with:

- `EngineeringModify` authorization;
- Workspace version precondition;
- dependency inspection;
- HTTP conflict on blocked deletion;
- no implicit delete-by-omission;
- no automatic cascade;
- structural audit records for success, denial and failure.

TAG deletion checks references from dependent Engineering domains such as alarms, commands, assets/views and security policy where applicable. Data Source deletion is blocked while TAGs still depend on it.

## Safe scoped Bulk edit

Current Bulk Preview/Apply supports selected homogeneous entity sets and deliberately limited fields.

Initial supported fields:

- TAG: `ReadOnly`, `HistorianEnabled`, `HistorianStrategy`;
- Alarm: `Enabled`, `Priority`, `RequiresAcknowledgement`, `ShelvingAllowed`;
- Data Source: `Enabled`.

Rules:

- selected entities are explicit;
- Apply remains disabled until Preview;
- affected quantity is shown before Apply;
- server reconstructs the candidate from authoritative Workspace state;
- normal Engineering validation still applies;
- no bulk route grants broader mutation rights than ordinary Engineering modification.

## Structured editors

### TAG editor

Current structured TAG editing covers core fields such as:

- name/path;
- data type;
- Data Source reference/address;
- engineering unit/description;
- read-only state;
- scaling;
- historian enable/strategy and timing fields;
- deadband.

Fields not exposed by a particular property panel must be preserved from the canonical entity rather than discarded.

### Data Source editor

Current editing covers:

- key/name;
- driver type;
- enabled state;
- public technical settings.

Secret material is never edited as plaintext. `secretReferences` remain references; backend validation continues to reject plaintext-secret-like configuration.

### Alarm editor

Current editing covers:

- name;
- associated TAG;
- alarm type;
- priority;
- setpoint/digital active value where applicable;
- class/area/message;
- activation delay;
- enabled state;
- acknowledgement requirement;
- shelving permission.

Changing TAG association must not leave a stale stable-ID binding that causes preview to resolve the old TAG.

## Initial route and shell

Engineering environment:

`/engineering`

Runtime HMI demonstration remains available separately.

The Engineering shell includes:

- product/environment header;
- Runtime navigation;
- project/workspace identity;
- schema/base-revision summary;
- dirty/clean state;
- language selection;
- Project Explorer/navigation;
- main Engineering content region.

Current/future navigation domains include project overview, Data Sources, TAGs, Alarms, Equipment Templates, Equipment, Dynamos, Screens, Popups, Historian, Security, Commands, Users and Diagnostics as their product surfaces mature.

## Localization

Supported Engineering/development locales:

- `pt-BR`;
- `en`;
- `es`.

Language selection is presentation state only. Localization must never alter:

- stable Engineering IDs;
- TAG paths;
- Data Source keys;
- addresses;
- enum/storage values;
- public JSON/CSV/XLSX keys;
- revision identity;
- authorization semantics.

Browser tests must continue proving stable Engineering identifiers across locale changes.

## Engineering UX direction

The Engineering UI follows a high-performance industrial/developer-workspace direction:

- neutral/dark engineering workspace;
- restrained accent use;
- strong colors reserved for warnings/errors/dirty/abnormal state;
- dense but readable tables and property panels;
- stable monospace treatment for technical identifiers;
- responsive behavior without turning the workstation UI into a consumer/mobile-first interface.

The Engineering UI is broader than the future runtime HMI canvas. It hosts communication, TAG, alarm, historian, security, revision, scripting and administration workflows around the same public model.

## Next editor progression

### Engineering data/product integration

Near-term work should follow roadmap dependencies rather than adding arbitrary panels:

- Internal Memory public Engineering configuration and runtime integration;
- historian retention/downsampling public Engineering policy integration;
- later Gateway and common Data Source diagnostics;
- additional security/diagnostic/editor surfaces only when their public contracts are authoritative.

### Reusable object engineering

Future work includes:

- Equipment Templates and instances;
- Dynamos;
- bindings/context/property inspectors;
- version-aware reusable libraries.

### Python and graphical visualization engineering

The graphical Screen/Popup/Dynamo editor must not be implemented before the locked scripting/property prerequisites.

Required sequence remains:

`public Script/visual Engineering integration -> Python editor/sandbox -> visual runtime instance/property API integration -> graphical Screens/Popups/Dynamos editor`

The final canvas must serialize to public Engineering definitions and must not persist an opaque browser-private scene graph as the only representation.

## Security boundary

Backend authorization remains authoritative.

The Engineering UI may hide or disable controls based on the current principal, but the browser cannot grant itself authority.

Protected mutations require backend capabilities such as `EngineeringModify`; trusted identity comes from the authenticated principal; mutation audit metadata is structural and must not copy process values, credentials or authorization material.

## Testing baseline

Maintain browser/API coverage for, as applicable:

- `/engineering` startup against the real API;
- navigation and localization;
- stable identifiers across language changes;
- Runtime ↔ Engineering navigation;
- valid/invalid preview paths;
- preview not mutating Workspace state;
- exact preview/apply candidate behavior;
- stale Workspace version rejection;
- explicit dependency-aware Delete;
- Bulk Preview quantity and Apply gating;
- authorization denial for anonymous/insufficient principals;
- structural Audit evidence for protected mutations;
- preservation of canonical Engineering fields not exposed in a particular editor.

Backend and browser failures must be fixed at the source rather than bypassed to make the editor appear functional.