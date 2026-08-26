# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE

## Live repository state

- `main` HEAD observed: `78a1656160c4317680ed54f0167537f806e104fc`.
- PR #35 — `Add first-class operational command domain` — open and mergeable, head `fc15adb507db172233ed2893f65d30cdad311963`, base `main`.
- PR #36 — `Protect runtime read and realtime surfaces` — open and mergeable, head `1df64077b235321f0c3318b994f7b89632261cee`, stacked on PR #35.
- PR #37 — `Add Engineering UI foundation and localization` — open, mergeable and intentionally **draft**, base `main`.
- PR #37 head immediately before this handoff update: `095337728577bf010d014eab02a8d1159e3b4cf3`.
- This handoff update creates a newer #37 head commit; fetch live PR metadata before further work.
- PR #37 branch remains `feature/engineering-ui-foundation`, created directly from `main` and independent from #35/#36.
- Nothing from #35, #36 or #37 should be merged without the relevant green validation.

## GitHub Actions situation

GitHub Actions has been affected by a service outage / hosted-runner queue disruption. The useful executed diagnosis for PR #35 remains run #129: backend build completed with 0 warnings/0 errors and nearly all tests passed; the single failure was a stale schema-v6 expectation after the command-domain branch moved the current schema to v7. That expectation was corrected before the current #35 head.

Later runs remained queued/cancelled at infrastructure level. The user also attempted cancellation through the GitHub UI and received `Failed to cancel workflow`, confirming that the disruption affected workflow management itself.

Do not repeatedly close/reopen PRs merely to manufacture new workflow runs. PR #36 adds `concurrency`/`cancel-in-progress` so superseded runs of the same PR/ref are automatically cancelled once GitHub Actions is healthy.

## PR #35 — operational command domain

Implemented on `feature/operational-command-domain`:

- first-class command definitions/registries;
- Engineering Schema v7 command serialization/import/export;
- command validation against target TAGs and typed configured values;
- command compilation into active runtime;
- execution through the owning communication driver;
- `CommandExecute` authorization using area/equipment/TAG/command scopes;
- succeeded/denied/failed audit without persisting commanded values;
- demo start/stop commands;
- Core/Engineering/Driver/Chromium coverage;
- `.escadapkg` command round-trip coverage;
- validation against incoming/post-import TAG state.

## PR #36 — read/realtime authorization

Implemented on `feature/runtime-read-authorization`:

- `TagRead` protection/filtering for TAG lists, current values and historian reads;
- alarm reads filtered by readable TAG plus `View` area scope;
- JWT-authenticated `/ws/tags` browser WebSockets;
- per-event realtime authorization;
- WebSocket closure at JWT expiration;
- fail-closed runtime-change checks and canonical live TAG resolution;
- protected Engineering/diagnostic/project-package read surfaces;
- minimal public `/health` plus protected runtime diagnostics;
- group-wide Engineering persistence read/preview authorization;
- expanded Chromium security coverage;
- frontend login/token acquisition intentionally deferred to the identity/login slice rather than adding an ad-hoc browser token store.

## PR #37 — Engineering UI foundation and preview editors

PR #37 is deliberately independent and draft while CI is unstable.

Foundation already implemented:

1. `/engineering` developer-facing Engineering Workspace route while `/` remains the runtime HMI demonstration.
2. Visible Runtime ↔ Engineering navigation.
3. Engineering shell consuming public backend contracts:
   - `GET /api/engineering/workspace`;
   - `GET /api/engineering/export/json`.
4. Project/workspace summary with project identity, public schema/version, base revision, clean/dirty state and snapshot time.
5. Navigation/views for Overview, Data Sources, TAGs, Alarms, Templates, Equipment, Dynamos, Screens, Popups, Historian, Security and Diagnostics.
6. Shared `pt-BR` / `en` / `es` localization with browser preference key `elitescada.engineering.locale`.
7. Schema/version E2E assertions read the real API value instead of hard-coding v6.
8. No new npm dependency and no backend/runtime mutation in this branch.

New preview-editor slice completed in this task:

### TAG editor

- structured entity picker/search;
- transient draft state for the selected TAG;
- editable name, path, data type, Data Source, address, unit and description;
- editable read-only flag and scaling limits;
- editable historian enabled/strategy/deadband/period/maximum-period fields;
- metadata/access-policy fields not exposed in the form remain preserved in the cloned canonical entity;
- Data Source and TAG identity remain canonical public-model fields, not browser-only IDs.

### Data Source editor

- structured entity picker/search;
- transient draft state for the selected Data Source;
- editable key, name, driver and enabled state;
- editable public technical `settings` key/value dictionary;
- `secretReferences` displayed read-only as references only;
- no secret value is materialized into the editor.

### Preview behavior

- added frontend client for `POST /api/engineering/import/json/preview`;
- preview clones the **complete** canonical `scada.engineering` package and substitutes the current draft entity;
- backend parse/cross-reference/validation logic remains authoritative;
- no Apply action exists in PR #37;
- preview cannot dirty the Working Workspace, mutate runtime state or create a revision;
- any field edit invalidates the previous preview immediately so stale green validation is never presented as current;
- preview result shows create/update/skip/error counts and backend issue codes/messages.

### Browser coverage added

- valid TAG draft preview;
- invalid TAG path preview expecting backend `TAG_PATH_WHITESPACE`;
- proof that TAG preview leaves Workspace `isDirty`/`changeVersion` unchanged;
- proof that TAG preview leaves exported TAG identity/path/name unchanged;
- valid Data Source draft preview;
- proof that Data Source preview also leaves Workspace state unchanged;
- explicit UI copy that secret references are reference-only.

## Validation status of PR #37

- Static review completed against the current public Engineering contracts and validator behavior.
- Diff remains frontend/documentation only; compare against `main` shows no backend/source project changes and no npm dependency change.
- The local environment has Node 22 / npm 10 / TypeScript available, but the private GitHub branch cannot be materialized into the container because the container has no external network access and connector content is not directly mounted into the filesystem.
- Therefore a truthful local `npm build`/Chromium run could not be performed from this environment.
- Keep PR #37 draft until GitHub Actions can run the frontend build and Chromium E2E. Do not treat static review as CI success.

## Engineering UI architectural rules

- The public versioned Engineering model remains authoritative.
- Browser state may hold transient presentation/filter/dialog/form draft state, never become the only project representation.
- Most current Engineering sections remain read-only; TAG and Data Source sections are **draft + preview only**, still without Apply.
- Future Apply must be a distinct deliberate action using backend validation, authorization and audit; do not turn preview into an implicit save.
- Localization is presentation only; IDs, TAG paths, addresses, enum/storage values, schema keys, revision identity and authorization semantics stay stable.
- Backend authorization remains authoritative; UI visibility alone is never the security boundary.
- Do not add hard-coded JWT/localStorage authentication as a shortcut. Authenticated Engineering UI must consume the future trusted login/IdP flow.
- Screen/popup graphical editing must serialize to public Screen/Popup/Dynamo Engineering contracts, not an opaque browser-only scene graph.

## Immediate continuation

1. Wait for usable GitHub Actions runners and validate PR #35 first.
2. Merge #35 only after green CI; then retarget/validate #36 against `main`.
3. Validate PR #37 independently against `main`; keep it draft until TypeScript/Vite build and Chromium E2E are green.
4. Fix any #37 compile/E2E failures at the source rather than weakening tests.
5. Do **not** add Apply to the preview editors until the current preview slice is validated and the Engineering authorization boundary from #36 is available/understood on mainline.
6. After validation/security integration, the next editor step is explicit Apply into the Working Workspace with confirmation + reload, followed by create-new/delete/bulk workflows.
7. After #35 reaches `main`, expose Commands in the Engineering UI through the same public command domain.
8. After #36 and the future login/profile subsystem reach `main`, make Engineering presentation capability-aware while preserving backend enforcement.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.
- At the start of every EliteSCADA task, read both before changing code.
- Immediately before every final user-facing response, update this file with actual repository state.
