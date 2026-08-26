# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE, with merges blocked on GitHub Actions recovery/green CI.

## Repository state

- `main` HEAD last observed: `78a1656160c4317680ed54f0167537f806e104fc`.
- PR #35 is open: `Add first-class operational command domain`.
- PR #35 base: `main`.
- PR #35 head: `feature/operational-command-domain` at `fc15adb507db172233ed2893f65d30cdad311963`.
- PR #35 is intentionally **not merged** until current-head CI completes successfully.
- PR #36 is open: `Protect runtime read and realtime surfaces`.
- PR #36 base: `feature/operational-command-domain` (intentionally stacked on #35).
- PR #36 head branch: `feature/runtime-read-authorization`.
- PR #36 was confirmed mergeable with its stacked base.
- Branch HEAD immediately before this handoff commit: `37e6e363a2211c385a61b5682637bf1f154949d1`; this `LAST CHANGE.md` update advances that branch again.

## GitHub Actions / PR #35 validation state

GitHub Actions suffered a service outage / hosted-runner queue disruption during this work.

The useful executed run is #129:

- Web build: success.
- Backend restore/build: success.
- Backend build: **0 warnings, 0 errors**.
- PostgreSQL/TimescaleDB service: healthy.
- Driver, historian, persistence and security test projects passed.
- Exactly one stale test failed because it expected Engineering Schema v6 while the command domain correctly advanced the current schema to v7.
- That stale schema assertion was corrected before the current PR #35 head.

Runs #131/#132/#133 were then left queued without jobs/runners being allocated. The user tried the GitHub UI `Cancel run` action on redundant queued work, but GitHub returned **`Failed to cancel workflow.`** during the outage. Do not record manual cancellation as successful.

Run #133 was checked again during this task and remained `queued` on PR #35 head `fc15adb...`.

Do not repeatedly close/reopen/synchronize PR #35 to force CI. Do not merge #35 from static review alone. The existing automation will notify when GitHub Actions returns to normal.

## PR #35 — first-class operational command domain

The parent branch implements:

- first-class command definitions and stable registries;
- Engineering Schema v7 command serialization/import/export;
- command target/value validation against the incoming/post-import TAG state;
- command compilation into the active runtime;
- execution through the communication driver that owns the target TAG;
- `CommandExecute` enforcement with area/equipment/TAG/command scopes;
- succeeded/denied/failed command audit without persisting commanded values;
- real demo `demo.p01.start` / `demo.p01.stop` commands;
- Core/Engineering/Driver/Chromium coverage;
- `.escadapkg` command round-trip and inspection coverage;
- smoke expectations updated to Engineering Schema v7.

## PR #36 — runtime read/realtime authorization

PR #36 is now a coherent stacked security slice and must not be merged before #35.

Implemented:

- `/api/tags` and `/api/tags/current` enforce TAG read policy and filter unreadable TAGs;
- TAG-by-path and historian reads return 401/403 for unauthorized access;
- alarm reads require the associated TAG to be readable and the alarm area to satisfy runtime `View`;
- browser JWT query-token extraction is accepted only for `/ws/tags`;
- WebSocket connections require trusted identity when authentication is enabled;
- outbound realtime TAG events are authorization-checked per event;
- WebSocket lifetime is bound to the validated JWT `exp`; expired credentials terminate the realtime session;
- realtime socket shutdown handles cancellation/dispose races defensively;
- runtime authorization canonicalizes supplied TAG definitions against the live runtime;
- runtime authorization fails closed if the active runtime changes during the authorization decision;
- `/api/drivers` and `/api/diagnostics/runtime` require runtime `EngineeringModify`;
- public `/health` is reduced to only `{ status, service }`, with driver/project/revision/historian details moved to protected diagnostics;
- Engineering workspace entity reads, JSON/CSV exports and import previews require workspace `EngineeringModify`;
- the entire Engineering persistence group requires `EngineeringModify` when authentication is enabled, including status, lifecycle/revision metadata and previews;
- `.escadapkg` export, inspect and import-preview require `EngineeringModify`; restore/apply remains an audited protected mutation;
- authentication-disabled local/demo/smoke behavior remains supported;
- Chromium security coverage now distinguishes developer, operator, authenticated-no-grant, anonymous and invalid-token cases across runtime and Engineering reads;
- Chromium realtime coverage checks anonymous rejection, no-`TagRead` event suppression and automatic connection termination after JWT expiration;
- public health minimality is explicitly tested;
- the smoke workflow now obtains historian/runtime technical status from the protected diagnostics endpoint in no-auth CI mode;
- CI now contains a `concurrency` group with `cancel-in-progress: true`, so superseded runs of the same PR/ref automatically cancel when GitHub Actions itself is healthy;
- `docs/SECURITY-AUTHORIZATION-AUDIT.md` has been updated to the new read/realtime security boundary.

## Frontend authentication boundary

The current React demo runtime screen still has no product login/token-acquisition flow. That is intentional at this stage: user lifecycle / login / external IdP integration is the next identity slice.

Do **not** add an ad-hoc production JWT store, hard-coded Vite access token or similar shortcut merely to make authenticated browser mode appear complete. The E2E harness supplies a trusted test-only credential. Local/demo operation with authentication disabled remains supported. A real authenticated UI must consume the future trusted login/IdP token flow.

## Validation limitations

- .NET is not installed in the ChatGPT execution container, so the current PR #36 changes could not be compiled locally.
- Static review was performed across endpoint owners, authorization helpers, persistence group filters, WebSocket behavior, smoke workflow and Chromium coverage.
- PR #36 is mergeable against its stacked parent but has not received independent full CI validation.
- The repository workflow triggers pull-request CI for `main`; while #36 targets the feature parent it should remain a stacked review artifact. After #35 merges, retarget/rebase #36 to `main` and validate it independently.

## Immediate continuation point

1. Wait for GitHub Actions / run #133 to recover and execute.
2. Inspect all #35 jobs, not only the combined status. Merge #35 only if the current head is green.
3. After #35 merges, retarget/rebase `feature/runtime-read-authorization` / PR #36 onto the new `main` and verify the resulting diff contains only the read/realtime slice.
4. Run full backend build/tests/smoke, frontend build and Chromium E2E for #36. Fix anything found; merge only when green.
5. Update `docs/ROADMAP.md` after these stacked security milestones are validated/merged so it does not present unvalidated branch work as `main` truth.
6. Next product/security slice after #36: real login/token issuance or external IdP plus user/role lifecycle (`UserRoleAdmin`), followed by access-aware UI integration using that trusted identity flow.
7. Continue later roadmap items for audit buffering/retention, historian retention/downsampling, editor/productization and additional protocols/modules.

## Product north reminders

- Public versioned Engineering model remains authoritative.
- Engineering Import/Export is mandatory for every new engineering domain.
- Backend/API authorization is the security boundary; UI visibility alone never grants or denies authority.
- Runtime policy must resolve from the exact persisted Active Revision and fail closed on mismatch.
- Secret material must never be serialized in Engineering packages.
- Current real industrial-driver baseline is Modbus TCP; future locked targets include MQTT, OPC UA, BACnet, installable Driver Modules, Siemens S7 as the first intended installable module, and later Allen-Bradley research.
- Engineering UI localization remains locked for `pt-BR`, `en` and `es` without changing stable Engineering identifiers/contracts.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.
- At the start of every EliteSCADA task, read both before changing code.
- Immediately before every final user-facing response, update this file with actual repository state.
