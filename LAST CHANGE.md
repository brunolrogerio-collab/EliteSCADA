# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE**

The user explicitly resumed functional development on 2026-08-26. Repository truth remains separated into **MERGED**, **IMPLEMENTED IN PR**, and **SPECIFIED / NOT IMPLEMENTED**.

## MERGED

### PR #35 — Add first-class operational command domain

- merged into `main`;
- PR CI #144 green;
- merge commit `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- Engineering Schema v7 and first-class operational Commands are official `main` state.

### PR #36 — Protect runtime read and realtime surfaces

- independently revalidated after retargeting to `main`;
- stale branch conflict was isolated to continuity/history and resolved without weakening security;
- CI #146 exposed an E2E isolation bug: the supposedly anonymous WebSocket browser context inherited the global developer `Authorization` header from Playwright configuration;
- the test was fixed by explicitly clearing the Authorization header in the anonymous browser context; backend WebSocket authorization was not weakened;
- refreshed head `81e5ba56e01cfbf94d9f6c3a3ee3b21ad2045469` passed CI #147 completely: Web build, Backend build/test/runtime smoke and Chromium E2E;
- merged into `main` as `10b0320149c1ef2109e9517539717a8800b200c2`.

Merged security behavior now includes:

- `TagRead` protection/filtering for TAG collections, individual TAG reads and historian queries;
- alarm reads filtered by readable TAG and `View` area scope;
- authenticated `/ws/tags` WebSockets using JWT query tokens on that route;
- realtime authorization re-evaluated per event plus JWT-expiration socket lifetime;
- fail-closed behavior when active runtime/policy identity changes;
- protected driver/runtime diagnostics and Engineering/project-package read/preview surfaces;
- minimal public `/health` without plant/runtime detail;
- expanded browser coverage for developer, operator, no-grant, anonymous and invalid/expiring credentials.

The pre-integration PR #36 head is preserved at `archive/runtime-read-authorization-pre-rebase-20260826`.

### Permanent architecture already consolidated on `main`

The following specifications are official `main` product north even though their functionality is not implemented yet:

- `docs/INTERNAL-MEMORY-TAGS.md`;
- `docs/TAG-GATEWAY.md`;
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`;
- Source Provider direction and multi-Data-Source isolation;
- locked external-protocol prerequisite order: **internal memory -> TAG Gateway -> common multi-driver diagnostics -> new external protocols**.

## IMPLEMENTED IN PR

### PR #37 — Add Engineering UI foundation and localization

Current integration target remains `feature/engineering-ui-foundation`.

Implemented in the PR:

- `/engineering` developer workspace while `/` remains Runtime HMI;
- Runtime <-> Engineering navigation;
- shared `pt-BR`, `en` and `es` localization foundation;
- structured TAG, Data Source and Alarm editors;
- existing/new browser-local drafts;
- canonical backend Engineering package preview;
- preservation of metadata and non-exposed fields;
- alarm TAG-path changes clear stale `tagId` before validation;
- stale-preview invalidation;
- changed-draft navigation and `beforeunload` protection;
- Chromium coverage proving preview does not mutate the Working Workspace/export.

**Safety boundary remains locked:** no Apply, Delete or bulk edit is being introduced as part of PR #37 integration. Its existing editors remain preview-only.

PR #37 was created from an older `main`; its functional UI work must be reconciled against the now-merged command and read/realtime-security baseline. A safety archive exists at `archive/engineering-ui-foundation-pre-integration-20260826`.

## SPECIFIED / NOT IMPLEMENTED

Major remaining product blocks include:

- trusted login/token acquisition or external IdP workflow plus user lifecycle/administration;
- audit buffering/outbox, retention and query policy;
- historian retention/downsampling;
- secured Engineering Apply/Delete/bulk lifecycle;
- `builtin.memory.client` and retentive `builtin.memory.server` with typed initial values and stable-ID retention/migration semantics;
- protocol-independent TAG-to-TAG Gateway with quality/rate/transform/cycle/multi-writer policy and diagnostics;
- common isolated per-Data-Source communication diagnostics;
- graphical screens/popups, reusable Equipment/Templates/Dynamos and visual libraries;
- Engineering Fragments/cross-project copy-paste;
- multi-Pen trends;
- configurable application shell;
- Engineering XLSX;
- MQTT, OPC UA, BACnet, installable Driver Modules, Siemens S7 target and later Allen-Bradley research;
- later scripting/public SDK expansion.

## Immediate continuation

1. Reconcile PR #37 with current `main`, discarding stale branch copies of official product/continuity documentation while preserving the Engineering UI implementation and `docs/ENGINEERING-UI.md`.
2. Run full Web + Backend/test/smoke + Chromium CI for the reconciled PR #37 head.
3. Merge PR #37 only if that integrated validation is green.
4. After the Engineering UI foundation is on `main`, begin the trusted identity/login/user-lifecycle slice. The protected backend now needs a legitimate browser token-acquisition/profile path before mutable Engineering UI/admin workflows are enabled.
5. Continue audit durability/retention and historian retention/downsampling according to `docs/ROADMAP.md`.
6. Then execute the locked source/protocol foundation: internal memory -> TAG Gateway -> multi-driver diagnostics -> additional external protocols.

## Permanent continuity rule

- `PROJECT GOAL.md` = official permanent product north, including architecture not yet implemented.
- `LAST CHANGE.md` = exact operational resume point with explicit MERGED / IMPLEMENTED IN PR / SPECIFIED status.
- `docs/ROADMAP.md` = ordered implementation status and next slices.
- Feature branches must never be the sole durable home of permanent architecture decisions.
