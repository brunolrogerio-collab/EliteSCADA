# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE

The user explicitly resumed EliteSCADA development on 2026-08-26. Repository truth remains separated into **MERGED**, **IMPLEMENTED IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## MERGED

### Current `main`

Before this PR #36 refresh, `main` was verified at:

`4f6c9bea29500858cfab3e070f5dcb3bd44e29ab`

Permanent architecture is already consolidated on `main`, including:

- PR #35 operational command domain / Engineering Schema v7;
- `docs/INTERNAL-MEMORY-TAGS.md`;
- `docs/TAG-GATEWAY.md`;
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`;
- locked implementation order: internal memory -> TAG Gateway -> common multi-driver diagnostics -> new external protocols.

### PR #35 — Add first-class operational command domain

Merged into `main` as `2fd568976fc6277d0b069adeeb560f6ea3d8205f` after CI run #144 completed green across Web build, Backend build/test/smoke and Chromium E2E.

Commands and Engineering Schema v7 are therefore `main` truth, not pending work.

## IMPLEMENTED IN PR

### PR #36 — Protect runtime read and realtime surfaces

Branch: `feature/runtime-read-authorization`.

This branch implements the read/realtime security slice on top of the command-domain code:

- `TagRead` filtering for TAG collections/current values;
- protected TAG-by-path and historian reads;
- alarm visibility filtered by readable TAG plus `View` area scope;
- JWT-authenticated `/ws/tags` using query bearer extraction only on the WebSocket route;
- per-event realtime authorization and JWT-expiration socket lifetime;
- fail-closed runtime-policy resolution when the active runtime changes;
- protected driver/runtime diagnostics through runtime `EngineeringModify`;
- minimal public `/health` response;
- protected Engineering workspace/entity/export/import-preview reads;
- protected persistence metadata/read/preview surfaces;
- protected project-package export/inspect/import-preview surfaces;
- authentication-disabled local/smoke compatibility;
- expanded Chromium security/realtime coverage;
- GitHub Actions concurrency cancellation for superseded runs.

The previous branch head `1df64077b235321f0c3318b994f7b89632261cee` was preserved before integration work as:

`archive/runtime-read-authorization-pre-rebase-20260826`

At resume time the PR branch was 25 commits ahead and 8 commits behind current `main`. The differing functional files are the read/realtime security slice; the additional `main` commits are the PR #35 merge finalization and documentation/architecture consolidation.

This checkpoint commit intentionally refreshes PR #36 after it was retargeted to `main` so GitHub Actions can perform the required independent validation. Do not merge PR #36 unless its refreshed head receives green Web, Backend build/test/smoke and Chromium E2E.

## PR #37 — Engineering UI foundation and localization

Still open as Draft. Last verified tested head: `74307b51df65a71ce0a5179deb957ffea958a440`, with CI run #143 green against its older base.

Implemented there:

- `/engineering` workspace and Runtime <-> Engineering navigation;
- `pt-BR`, `en`, `es` localization foundation;
- structured TAG, Data Source and Alarm editors;
- existing/new browser-local drafts validated through canonical backend preview;
- metadata preservation and stale-preview/unsaved-draft protection;
- no Apply, Delete or bulk edit.

PR #37 must be reconciled with current `main` and PR #36 before merge. Do not weaken its preview-only safety boundary during that integration.

## SPECIFIED / NOT IMPLEMENTED

Permanent requirements already stored on `main` but not yet functional include:

- general Source Provider architecture;
- `builtin.memory.client` and retentive `builtin.memory.server` with typed initial values;
- Server Memory retention keyed primarily by stable TAG ID and explicit incompatible-type reset/migration;
- protocol-independent TAG-to-TAG Gateway with OnChange/Periodic, deadband, minimum interval, coalescing, Good-quality default, gain/offset, loop/multi-writer rejection and route diagnostics;
- common per-Data-Source multi-driver diagnostics and independent failure isolation;
- identity/login/user lifecycle;
- audit buffering/retention;
- historian retention/downsampling;
- complete Engineering Apply lifecycle, screens/popups/Dynamos, trends and later external protocols/modules.

## Immediate continuation

1. Validate this refreshed PR #36 head in GitHub Actions.
2. If CI is green and GitHub reports a clean merge, merge PR #36 to `main`.
3. Reconcile PR #37 with the resulting `main`, preserve preview-only editors, rerun full CI and integrate the Engineering UI foundation.
4. Then continue the roadmap from the integrated baseline. The current ordered roadmap places identity/login, audit durability/retention and historian retention/downsampling before the internal-memory -> Gateway -> diagnostics protocol foundation.
5. Before every final user-facing EliteSCADA response, update this file again with the actual repository state.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent official product north and permanent architecture.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.
- `docs/ROADMAP.md` = ordered implementation plan/status.
- Permanent architectural decisions must not live only in feature branches.
