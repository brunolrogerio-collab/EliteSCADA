# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE

## Branch purpose

This branch is `feature/operational-command-domain`, PR #35 `Add first-class operational command domain`, based on `main`.

The functional command implementation immediately before this continuity-only update is commit:

`fc15adb507db172233ed2893f65d30cdad311963`

This file update intentionally creates a fresh head commit so GitHub Actions can validate the current command implementation after the GitHub Actions service incident/recovery. Do not interpret this continuity commit as a functional command-domain change.

## Command domain implemented

- first-class command definitions/registries;
- Engineering Schema v7 command serialization/import/export;
- command validation against target TAG identity/path and writability;
- runtime command compilation/execution through the destination TAG's owning driver;
- scoped `CommandExecute` authorization;
- command execution audit without leaking command values;
- demo start/stop commands;
- command-related Core/Driver/Security tests;
- package/smoke/browser expectations updated for schema v7 and commands;
- stale schema-v6 test expectation corrected.

## CI recovery state

GitHub Actions suffered a service incident on 2026-08-26. Old PR #35 run #133 (`32985066021`) remained `queued` with zero jobs allocated even after newly created PR #37 runs began receiving hosted runners normally.

PR #37 subsequently proved runner allocation had recovered: on run #143, Web build and Backend build/test/smoke passed and Chromium E2E also passed after two deterministic frontend-test fixes.

Because #133 appears orphaned/stale from the incident, this continuity-only commit is intended to trigger exactly one fresh CI run for the current #35 implementation. Do not create repeated duplicate runs if the new run receives jobs normally.

## Merge rule

Do not merge PR #35 unless a CI run for the current head completes green across:

1. Web build;
2. Backend build/test/runtime smoke;
3. Chromium end-to-end.

If the fresh run fails, inspect the actual job/log and fix the root cause rather than merging around it.

## Dependent work

PR #36 `Protect runtime read and realtime surfaces` is stacked on this branch and must remain unmerged until #35 is validated/merged. After #35 reaches `main`, retarget/rebase #36 to `main` and validate it independently.

PR #37 `Add Engineering UI foundation and localization` is independent and based directly on `main`; it has now achieved a green run on its current tested head, but remains Draft pending integration/order decisions.

## Product north added on the independent Engineering branch

Later product requirements have been documented on PR #37 and must be preserved when branches converge:

- multiple simultaneous Data Sources/driver instances and common communication diagnostics;
- built-in `builtin.memory.client` and retentive `builtin.memory.server` TAG sources before new external protocols;
- protocol-independent server TAG Gateway / TAG Bridge using `Source TAG -> Destination TAG`, with no concrete driver-to-driver coupling;
- internal memory, Gateway and common diagnostics foundations precede MQTT/OPC UA/BACnet/S7 expansion.

Repository truth and current `PROJECT GOAL.md`/roadmap on the integrating branch take precedence when these branches converge.

## Immediate continuation

1. Observe the fresh CI run created from this branch head.
2. If green, merge PR #35 to `main`.
3. Retarget/rebase PR #36 to `main` and run full CI.
4. Reconcile PR #37 against the updated `main` and rerun its full CI before any merge.
5. Preserve all locked product requirements from the current product north when resolving documentation conflicts.
