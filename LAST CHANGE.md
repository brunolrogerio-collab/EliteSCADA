# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE

## Repository state

- `main` HEAD observed: `78a1656160c4317680ed54f0167537f806e104fc`.
- Open PR #35: `Add first-class operational command domain`.
- PR #35 head: `feature/operational-command-domain` at `fc15adb507db172233ed2893f65d30cdad311963`.
- PR #35 is open, mergeable and intentionally **not merged** until CI completes successfully.
- Stacked follow-up branch: `feature/runtime-read-authorization`.
- Stacked branch HEAD before this handoff update: `6714dc7c2ac1b52577d05c8fe74a70d5e0035ee2`.

## CI diagnosis for PR #35

GitHub Actions currently has several PR runs queued without jobs/runners being allocated. The current workflow file has no `concurrency` stanza or project-created queue lock, so these queued runs are not being held by EliteSCADA workflow configuration.

Recent runs:

- #133: queued, current PR head `fc15adb...`.
- #132: queued.
- #131: queued.
- #130: completed as failure at workflow level because its jobs were cancelled; it did not report a code/test failure.
- #129: actually executed and provides the useful failure diagnosis.

Run #129 results:

- Web build: success.
- Backend restore/build: success.
- Backend build: **0 warnings, 0 errors**.
- PostgreSQL/TimescaleDB service: healthy.
- Driver, historian, persistence and security test projects passed.
- One test failed: `EngineeringSchemaV6SecurityTests.SchemaV6_RoundTripsSecurityRolesAndCompilesRuntimePolicy` expected schema version 6 while the operational command domain correctly moved the current Engineering schema to version 7.
- That stale expectation was corrected after #129, before the current PR head.

Do not repeatedly close/reopen or synchronize PR #35 merely to force CI; that creates additional runs without solving hosted-runner availability. Wait for the latest queued run to receive a runner, then inspect its result. Do not merge PR #35 on static review alone.

## PR #35 — first-class operational command domain

The branch introduces first-class operational commands rather than a placeholder API:

- command definitions and stable registries;
- Engineering Schema v7 command serialization/import/export;
- command cross-reference validation against target TAGs;
- command value validation using the target TAG data type;
- command compilation into the active runtime;
- execution routed through the owning driver;
- `CommandExecute` capability enforcement with area/equipment/TAG/command scopes;
- succeeded/denied/failed audit events without recording commanded values;
- demo start/stop commands;
- Core/Engineering/Driver/Chromium coverage;
- `.escadapkg` round-trip coverage for commands;
- smoke test expectations updated for schema v7 and commands;
- validation now evaluates the incoming/post-import TAG state when a package updates the command target TAG.

## Stacked branch — runtime read authorization

`feature/runtime-read-authorization` is based on PR #35 and must remain stacked until #35 is validated and merged. After #35 merges, retarget/rebase this branch onto `main` before merge.

Already implemented on the stacked branch:

- `TagRead` protection for `/api/tags`, `/api/tags/current`, TAG-by-path and historian reads;
- collection endpoints filter out TAGs the principal cannot read;
- individual TAG/history endpoints return normal 401/403 authorization failures;
- alarm lists/definitions require readable associated TAGs and runtime `View` authorization for the alarm area;
- browser WebSocket JWT support through `access_token`, accepted only for `/ws/tags`;
- WebSocket authentication when authentication is enabled;
- per-event realtime authorization/filtering so long-lived sockets do not become permanent bypasses;
- authentication-disabled local/smoke behavior remains supported;
- Engineering persistence route group now requires `EngineeringModify` for reads/previews as well as protected mutations when authentication is enabled;
- Chromium security coverage for anonymous, invalid, operator, developer and an authenticated principal without `TagRead`;
- `EngineeringReadSecurityExtensions.cs` has been introduced to centralize Engineering/diagnostic read protection.

## Immediate continuation point

Continue on `feature/runtime-read-authorization` from the current repository truth:

1. Apply `EngineeringReadSecurityExtensions` to the remaining Engineering workspace/export/preview surfaces and runtime driver diagnostics.
2. Review `ProjectPackageEndpoints.cs` and protect package export/inspect/preview with the appropriate Engineering read/modify policy while keeping restore apply as an audited mutation.
3. Verify all remaining sensitive GET/read surfaces are covered; health may remain deliberately minimal/public unless the design is explicitly changed.
4. Expand Chromium coverage for the newly protected Engineering/diagnostic reads.
5. Review the complete stacked diff for compilation/DI/API consistency.
6. Update `docs/ROADMAP.md` and security documentation once the slice is complete.
7. Open a stacked PR against `feature/operational-command-domain` only after the slice is coherent.
8. When GitHub Actions provides runners again, validate PR #35 first. Merge #35 only after green CI, then retarget the read-authorization PR to `main` and validate it independently.

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
