# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE

## Live repository state

- `main` HEAD observed: `78a1656160c4317680ed54f0167537f806e104fc`.
- PR #35 — `Add first-class operational command domain` — open, head `fc15adb507db172233ed2893f65d30cdad311963`, base `main`.
- PR #36 — `Protect runtime read and realtime surfaces` — open, head `1df64077b235321f0c3318b994f7b89632261cee`, stacked on PR #35.
- PR #37 — `Add Engineering UI foundation and localization` — open, mergeable and intentionally draft, base `main`, branch `feature/engineering-ui-foundation`.
- PR #37 head immediately before this checkpoint update: `00db711dd4f50e23232d8787f9e25244947ce394`.
- This checkpoint commit creates the next #37 head; fetch live PR metadata before continuing.
- Nothing from #35, #36 or #37 should be merged without the relevant green validation.

## GitHub Actions situation — latest verified state

Repository-specific verification at the end of this task:

- PR #35 run #133 (`32985066021`) is still `queued`.
- Run #133 still has zero allocated jobs.
- No hosted runner has started the current #35 head.
- Do not close/reopen the PR or manufacture another run while #133 is already waiting.
- The last actually executed useful diagnosis remains run #129: backend build completed with 0 warnings/0 errors and the only test failure was a stale schema-v6 expectation after the command branch moved current schema to v7. That expectation is fixed on the current #35 head.
- GitHub incident recovery may be gradual; repository CI must be checked separately even if public status later says Actions recovered.

## PR #35 — operational command domain

Implemented on `feature/operational-command-domain`:

- first-class command definitions/registries;
- Engineering Schema v7 command serialization/import/export;
- command validation against target TAGs and typed configured values;
- command compilation into active runtime and execution through owning driver;
- `CommandExecute` authorization with area/equipment/TAG/command scopes;
- succeeded/denied/failed audit without persisting commanded values;
- demo start/stop commands;
- Core/Engineering/Driver/Chromium and `.escadapkg` coverage;
- validation against incoming/post-import TAG state.

## PR #36 — read/realtime authorization

Implemented on `feature/runtime-read-authorization`:

- `TagRead` filtering/protection for TAGs, current values and historian;
- alarm reads filtered by readable TAG plus `View` area scope;
- JWT-authenticated `/ws/tags`, per-event authorization and socket closure at JWT expiration;
- fail-closed runtime-change checks/canonical live TAG resolution;
- protected Engineering/diagnostic/project-package read surfaces;
- minimal public `/health` plus protected runtime diagnostics;
- group-wide Engineering persistence read/preview authorization;
- expanded Chromium security coverage;
- CI `concurrency`/`cancel-in-progress` for future superseded PR runs;
- frontend login/token acquisition intentionally deferred to the identity/login slice.

## PR #37 — Engineering UI foundation and preview editors

Independent frontend/documentation branch created directly from `main`.

Foundation implemented:

- `/engineering` developer-facing workspace while `/` remains runtime HMI;
- Runtime ↔ Engineering navigation;
- public Engineering Workspace/export consumption only;
- overview plus Data Sources, TAGs, Alarms, Templates, Equipment, Dynamos, Screens, Popups, Historian, Security and Diagnostics;
- shared `pt-BR` / `en` / `es` localization with browser presentation preference;
- no new npm dependency and no backend/runtime mutation.

### Shared draft/preview behavior

TAG, Data Source and Alarm editors now follow the same safety boundary:

- existing entities are cloned from the canonical public Engineering package into transient browser draft state;
- new entities are ID-less canonical DTO drafts in browser state;
- candidate preview clones the complete `scada.engineering` package and substitutes/appends the draft;
- candidate is sent to existing `POST /api/engineering/import/json/preview` using `CreateAndUpdate` semantics;
- backend validation and cross-reference rules remain authoritative;
- draft edits invalidate stale preview results immediately;
- switching entity with changed draft requires explicit localized discard confirmation;
- changed drafts register browser `beforeunload` protection;
- no Apply action exists, so preview cannot dirty the Working Workspace, mutate runtime or create a revision.

### TAG editor

Supports existing and new TAG drafts with:

- name/path/data type;
- Data Source/address/unit/description;
- scale min/max;
- read-only;
- historian enabled/strategy/deadband/period/max-period;
- preservation of unexposed metadata/access policy;
- valid create preview classification;
- backend validation such as `TAG_PATH_WHITESPACE`;
- proof in E2E that preview/create-preview does not change Workspace/export.

### Data Source editor

Supports existing and new Data Source drafts with:

- key/name/driver/enabled;
- public technical settings key/value dictionary;
- secret references displayed only as references/read-only;
- no secret value materialization;
- valid create preview classification;
- proof in E2E that preview/create-preview does not change Workspace/export.

### Alarm editor — new in latest task

New file: `web/scada-web/src/engineering/AlarmEditor.tsx`.

The Alarms section is no longer just a read-only table. It now provides preview-only editing for the full useful public alarm definition surface:

- name;
- associated TAG path;
- alarm type: `digital`, `high`, `highHigh`, `low`, `lowLow`, `communication`, `system`;
- priority: `low`, `medium`, `high`, `critical`;
- analog setpoint when applicable;
- digital active value for digital alarms;
- alarm class;
- area;
- message;
- activation delay milliseconds;
- enabled state;
- acknowledgement requirement;
- shelving permission;
- existing metadata remains preserved even though it is not yet edited directly.

Important TAG-reference rule:

- backend `ResolveAlarmTag` prioritizes `tagId` before `tagPath`;
- therefore, if the engineer changes Alarm `tagPath`, the frontend draft explicitly clears `tagId`;
- this forces preview to validate the newly entered TAG path rather than silently resolving the previous TAG stable ID.

New Alarm creation:

- `Novo Alarme` creates an ID-less browser draft;
- new alarm drafts are appended only to the cloned candidate package;
- valid new alarms are classified as `Create` by backend preview;
- live Engineering export remains unchanged until a future Apply exists.

Alarm E2E coverage added:

- existing `High discharge pressure` alarm loads through the structured editor;
- valid message edit previews successfully without changing Workspace/export;
- changing associated TAG to `Demo.Missing.Tag` expects backend `ALARM_TAG_NOT_FOUND`;
- new `Preview pressure alarm` bound to `Demo.Discharge.Pressure` with setpoint previews as one create;
- new alarm does not appear in live export after preview.

## Validation status of PR #37

- Static review completed against current public Engineering contracts, validators, Alarm handler, TAG resolver and preview operation semantics.
- Alarm enum values match canonical JSON camel-case serialization (`highHigh`, `lowLow`, etc.).
- Compare against `main` still shows only frontend/documentation/checkpoint changes; there is no backend/source-project mutation and no npm dependency change in #37.
- Current branch has 14 changed files relative to `main`, including the new Alarm editor and expanded Chromium tests.
- Local environment still cannot truthfully run the private branch frontend build/E2E because branch/dependencies cannot be materialized with external package/network access in this environment.
- Keep #37 draft until TypeScript/Vite build and Chromium E2E execute successfully.

## Immediate continuation

1. When Actions runners are usable, validate PR #35 first and inspect individual jobs.
2. Merge #35 only after green CI.
3. After #35 merges, retarget #36 to `main`, run full CI, fix integration issues, then merge only if green.
4. Validate #37 independently against `main`; keep draft until frontend TypeScript/Vite build + Chromium E2E are green.
5. Fix any #37 compile/E2E failures at the source rather than weakening tests.
6. Do **not** add Apply to TAG/Data Source/Alarm editors until the preview slices are validated and the Engineering authorization boundary from #36 is integrated/understood on mainline.
7. Safe frontend-only work while CI is unavailable may continue with reference-selection UX, validation/problem-list UX and design/specification for bulk/historian editing, but avoid another large uncompiled mutation surface.
8. After validation/security integration, next mutation step is explicit Apply to Working Workspace with confirmation/reload, then delete and bulk/multi-selection workflows.
9. After #35 reaches `main`, expose Commands in the Engineering UI through the same public model.
10. Identity/login/user lifecycle remains the next major backend security slice after #35/#36.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.
- At the start of every EliteSCADA task, read both before changing code.
- Immediately before every final user-facing response, update this file with actual repository state.
