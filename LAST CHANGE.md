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
- PR #37 — `Add Engineering UI foundation and localization` — open, mergeable and intentionally draft, base `main`, branch `feature/engineering-ui-foundation`.
- PR #37 head immediately before this checkpoint update: `d6d2b8747216a9d0e648b30d2aff8af0a3d108d5`.
- This checkpoint commit creates the next #37 head; fetch live PR metadata before continuing.
- Nothing from #35, #36 or #37 should be merged without the relevant green validation.

## GitHub Actions situation — latest verified state

The official GitHub incident update supplied by the user at 2026-08-26 13:50 BRT says the underlying issue was identified/addressed and traffic is being ramped back gradually; some customers can still see delays. A later incident update says Pages is operating normally, but does not yet state that Actions is fully recovered.

Repository-specific verification performed just before this task:

- PR #35 run #133 (`32985066021`) remained `queued` with zero allocated jobs.
- Do not close/reopen the PR or manufacture another run while #133 is already waiting.
- The useful executed diagnosis remains run #129: backend build completed with 0 warnings/0 errors and the only test failure was the stale schema-v6 expectation after the command-domain branch moved current schema to v7. That expectation is fixed on the current #35 head.
- Repository CI must be checked separately even after the GitHub incident is marked resolved.

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

Foundation already implemented:

- `/engineering` developer-facing workspace while `/` remains runtime HMI;
- Runtime ↔ Engineering navigation;
- public Engineering Workspace/export consumption only;
- overview plus Data Sources, TAGs, Alarms, Templates, Equipment, Dynamos, Screens, Popups, Historian, Security and Diagnostics;
- shared `pt-BR` / `en` / `es` localization with browser presentation preference;
- no new npm dependency and no backend/runtime mutation.

### Existing-entity preview editing

TAG and Data Source sections provide **draft + backend preview only**:

- TAG editor: name, path, data type, Data Source, address, unit, description, scaling, read-only and historian fields;
- Data Source editor: key, name, driver, enabled and public technical settings;
- secret references remain references only/read-only;
- complete canonical package is cloned and sent to existing `POST /api/engineering/import/json/preview`;
- backend validation remains authoritative;
- any draft change invalidates stale preview results;
- no Apply action exists, so preview cannot dirty Working Workspace or mutate runtime.

### New work completed in this task — draft protection

- changing from one TAG/Data Source to another while the current draft has changes now requires explicit localized confirmation;
- cancelling the confirmation preserves both the selected entity and current draft;
- accepting it intentionally discards the current transient draft and loads the next canonical entity;
- changed drafts register browser `beforeunload` protection when navigating away/reloading the page;
- new localization keys for the discard warning were added to `pt-BR`, `en` and `es`.

### New work completed in this task — create preview

TAG and Data Source editors now support **new-entity drafts without Apply**:

- `Nova TAG` creates an ID-less canonical TAG draft in browser state;
- `Novo Data Source` creates an ID-less canonical Data Source draft in browser state;
- new drafts are appended only to the cloned candidate package sent to backend preview;
- `CreateAndUpdate` preview semantics are reused; no custom browser-only create contract was invented;
- backend `EngineeringHandlerSupport` classifies valid non-existing entities as `Create`;
- valid create previews expose the create count in the preview result;
- no new entity is added to the real Engineering export until a future explicit Apply exists;
- new draft templates deliberately start with required identity fields blank so backend validation remains meaningful rather than silently manufacturing permanent identifiers.

### Browser coverage now includes

- valid existing TAG preview;
- invalid TAG preview expecting backend `TAG_PATH_WHITESPACE`;
- proof that existing TAG preview leaves Workspace/export unchanged;
- changed TAG draft selection guard, covering both cancel and confirmed discard;
- valid new TAG preview classified with one create and proof it does not appear in live export;
- valid existing Data Source preview and secret-reference UI boundary;
- valid new Data Source preview classified with one create and proof it does not appear in live export;
- locale behavior and stable Engineering identifiers remain covered.

## Validation status of PR #37

- Static review completed against current public Engineering contracts, Data Source handler and shared preview operation semantics.
- Compare against `main` still shows only frontend/documentation/checkpoint files; there is no backend/source-project or npm dependency change in #37.
- Local environment has Node/npm/TypeScript, but cannot materialize the private GitHub branch/dependencies into the container and external npm access times out, so no truthful local `npm build` or Chromium E2E has been run.
- Keep #37 draft until TypeScript/Vite build and Chromium E2E execute successfully.

## Immediate continuation

1. When Actions runners are usable, validate PR #35 first and inspect individual jobs.
2. Merge #35 only after green CI.
3. After #35 merges, retarget #36 to `main`, run full CI, fix any integration issue, then merge only if green.
4. Validate #37 independently against `main`; keep draft until frontend TypeScript/Vite build + Chromium E2E are green.
5. Fix any #37 compile/E2E failures at the source rather than weakening tests.
6. Do **not** add Apply to TAG/Data Source editors until the current preview/create-preview slice is validated and the Engineering authorization boundary from #36 is integrated/understood on mainline.
7. After validation/security integration, next editor step is explicit Apply to Working Workspace with confirmation/reload, then delete and bulk/multi-selection workflows.
8. After #35 reaches `main`, expose Commands in the Engineering UI through the same public model.
9. Identity/login/user lifecycle remains the next major backend security slice after #35/#36.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.
- At the start of every EliteSCADA task, read both before changing code.
- Immediately before every final user-facing response, update this file with actual repository state.
