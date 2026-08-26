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
- Independent frontend branch: `feature/engineering-ui-foundation`, created directly from `main` so it does not depend on #35/#36.
- No changes from #35 or #36 have been merged into `main` without green CI.

## GitHub Actions situation

GitHub Actions has been affected by a service outage / hosted-runner queue disruption. The useful executed diagnosis for PR #35 remains run #129: backend build completed with 0 warnings/0 errors and nearly all tests passed; the single failure was a stale schema-v6 expectation after the command-domain branch moved the current schema to v7. That expectation was corrected before the current #35 head.

Later runs remained queued/cancelled at infrastructure level. The user also attempted cancellation through the GitHub UI and received `Failed to cancel workflow`, confirming that the service disruption affected workflow management itself.

Do not merge #35 or #36 until CI executes successfully. Do not repeatedly close/reopen PRs merely to manufacture new workflow runs.

PR #36 adds `concurrency`/`cancel-in-progress` to the workflow so superseded runs of the same PR/ref are automatically cancelled once GitHub Actions is healthy.

## PR #35 — operational command domain

Implemented on `feature/operational-command-domain`:

- first-class command definitions/registries;
- Engineering Schema v7 command serialization/import/export;
- command validation against target TAGs and typed configured values;
- command compilation into the active runtime;
- execution through the owning communication driver;
- `CommandExecute` authorization using area/equipment/TAG/command scopes;
- succeeded/denied/failed command audit without persisting commanded values;
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

## Parallel Engineering UI foundation

Because GitHub Actions is unstable, development was advanced in a separate frontend-only branch based on `main`: `feature/engineering-ui-foundation`.

Implemented in this branch:

1. `/engineering` developer-facing Engineering Workspace route.
2. Existing `/` runtime route preserved.
3. Visible Runtime → Engineering navigation and Engineering → Runtime navigation.
4. Read-only Engineering shell consuming only public backend contracts:
   - `GET /api/engineering/workspace`;
   - `GET /api/engineering/export/json`.
5. Project/workspace summary showing project identity, public schema/version, base revision, clean/dirty state and loaded snapshot time.
6. Engineering navigation for:
   - Overview;
   - Data Sources;
   - TAGs;
   - Alarms;
   - Equipment Templates;
   - Equipment;
   - Dynamos;
   - Screens;
   - Popups;
   - Historian policies;
   - Security roles/capabilities;
   - model diagnostics.
7. Generic read-only Engineering tables displaying real public model entities rather than a browser-private project representation.
8. Engineering UI localization foundation with resource keys and selectable:
   - `pt-BR`;
   - `en`;
   - `es`.
9. Locale stored as presentation preference under `elitescada.engineering.locale`; changing language does not alter Engineering identifiers.
10. Browser-locale detection with deterministic Playwright `pt-BR` test context.
11. Chromium coverage for:
   - Runtime → Engineering navigation;
   - public Engineering-model rendering;
   - current schema/version read dynamically from the API instead of hard-coding v6, so future schema v7 does not create a false regression;
   - navigation across current Engineering domains;
   - Portuguese/English/Spanish switching;
   - locale persistence;
   - stable TAG paths while language changes.
12. `docs/ENGINEERING-UI.md` documenting the editor boundary, localization, UX direction and phased progression.
13. No new npm dependency was introduced; the foundation uses the existing React/TypeScript stack.
14. No backend/runtime mutation was introduced in this independent branch.

## Architectural rules for the Engineering UI

- The public versioned Engineering model remains authoritative.
- Browser state may hold presentation/filter/dialog/form draft state, never become the only project representation.
- The first UI slice is intentionally read-only.
- Future editor mutations must reuse the platform validation/preview semantics rather than bypass them.
- Localization is presentation only. IDs, TAG paths, addresses, enum/storage values, schema keys, revision identity and authorization semantics stay stable.
- Backend authorization remains authoritative; hiding a UI control never grants or denies permission.
- Do not add hard-coded JWT/localStorage authentication as a shortcut. Authenticated Engineering UI must consume the future trusted login/IdP flow.
- Screen/popup graphical editing must ultimately serialize to the public Screen/Popup/Dynamo Engineering model, not to an opaque browser-only scene graph.

## Immediate continuation

For `feature/engineering-ui-foundation`:

1. Keep the branch independent from #35/#36 until their backend changes are validated.
2. Validate frontend TypeScript build and Chromium E2E when GitHub Actions runners are available.
3. Fix any build/E2E issue before merge; do not weaken tests.
4. The next Engineering UI product slice after the foundation is validated should be a structured Data Source/TAG editor with draft form state feeding the public validation/preview/apply model.
5. When #35 eventually reaches `main`, extend the Engineering UI to expose the new Command domain without creating a second command representation.
6. When #36 reaches `main`, wire the Engineering UI to the authenticated login/profile flow once that subsystem exists and respect capability-aware presentation while keeping backend enforcement authoritative.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.
- At the start of every EliteSCADA task, read both before changing code.
- Immediately before every final user-facing response, update this file with actual repository state.
