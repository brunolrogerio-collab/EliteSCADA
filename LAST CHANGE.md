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
- PR #37 — `Add Engineering UI foundation and localization` — open, mergeable and intentionally **draft**, head before this handoff update `b15c1414f412fc0d4bf41578f3d88b66c92371ea`, base `main`.
- PR #37 branch: `feature/engineering-ui-foundation`, created directly from `main` and independent from #35/#36.
- This `LAST CHANGE.md` update creates a newer #37 head commit; fetch the live PR head before further work.
- Nothing from #35, #36 or #37 should be merged without the relevant green CI validation.

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

## PR #37 — Engineering UI foundation

PR #37 is deliberately independent and draft while CI is unavailable.

Implemented:

1. `/engineering` developer-facing Engineering Workspace route.
2. Existing `/` runtime route preserved.
3. Visible Runtime → Engineering and Engineering → Runtime navigation.
4. Read-only Engineering shell consuming only public backend contracts:
   - `GET /api/engineering/workspace`;
   - `GET /api/engineering/export/json`.
5. Project/workspace summary showing project identity, public schema/version, base revision, clean/dirty state and loaded snapshot time.
6. Navigation and real-model tables for:
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
7. Shared Engineering localization foundation with resource keys and selectable `pt-BR`, `en` and `es`.
8. Locale stored only as presentation preference under `elitescada.engineering.locale`.
9. Browser-locale detection with deterministic Playwright `pt-BR` coverage.
10. Chromium coverage for Runtime → Engineering navigation, model rendering, domain navigation, three-language switching, locale persistence and stable TAG identifiers across language changes.
11. Schema-version browser coverage reads the actual version from the API instead of hard-coding v6, preventing a false regression when PR #35 introduces schema v7.
12. `docs/ENGINEERING-UI.md` documents the editor boundary, localization, UX direction and phased implementation.
13. No new npm dependency.
14. No backend/runtime mutation in this branch.

## Engineering UI architectural rules

- The public versioned Engineering model remains authoritative.
- Browser state may hold transient presentation/filter/dialog/form draft state, never become the only project representation.
- The first UI slice is intentionally read-only.
- Future editor mutations must reuse public validation/preview semantics rather than bypass them.
- Localization is presentation only; IDs, TAG paths, addresses, enum/storage values, schema keys, revision identity and authorization semantics stay stable.
- Backend authorization remains authoritative; UI visibility alone is never the security boundary.
- Do not add hard-coded JWT/localStorage authentication as a shortcut. Authenticated Engineering UI must consume the future trusted login/IdP flow.
- Screen/popup graphical editing must serialize to public Screen/Popup/Dynamo Engineering contracts, not an opaque browser-only scene graph.

## Immediate continuation

1. Wait for usable GitHub Actions runners and validate PR #35 first.
2. Merge #35 only after green CI; then retarget/validate #36 against `main`.
3. PR #37 can be validated independently against `main`; keep it draft until frontend TypeScript build and Chromium E2E are green.
4. Fix build/E2E failures at the source rather than weakening tests.
5. After the Engineering UI foundation is validated, the next UI product slice should be a structured Data Source/TAG editor with draft state feeding the public validate/preview/apply model.
6. After #35 reaches `main`, expose Commands in the Engineering UI through the same public command domain.
7. After #36 and the future login/profile subsystem reach `main`, make Engineering presentation capability-aware while preserving backend enforcement.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.
- At the start of every EliteSCADA task, read both before changing code.
- Immediately before every final user-facing response, update this file with actual repository state.
