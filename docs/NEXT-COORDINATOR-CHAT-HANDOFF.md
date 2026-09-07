# EliteSCADA — Next Coordinator Chat Handoff

**Prepared:** 2026-09-07 BRT  
**Purpose:** resume Wave14/C26 in a new coordinator chat without reconstructing project decisions from conversation history.

> **GitHub live is the official memory. Revalidate everything before acting.**
>
> The documentation handoff commit that contains this file is docs-only. The exact C26 **product/test authority immediately before the handoff** is `b7f0566f9e30f3e4c6dee3d020710b23923b6db2`. Do not treat a later docs-only SHA as a newly validated product candidate.

## Mandatory first read

1. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
2. `docs/CURRENT-COORDINATOR-HANDOFF.md`
3. `LAST CHANGE.md`
4. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
5. live issue #286
6. live PR #287
7. live validation-only PR #288
8. live exact-head workflows

## Exact resume point

C26.1, C26.2 and C26.3 are implemented.

C26.4 Runtime Popup layout is **diagnosed but not implemented**. Wave11 Active HMI Runtime #359 / run `34154058296` is red on `b7f0566...`; the other four normal gates are green. Do not rerun the unchanged red.

The failure is not merely a z-index issue. Runtime Popup rendering still inherits Engineering renderer `min-height:450px`, `margin:18px` and `overflow:auto` because C26.2 correctly scoped the Screen reset to `.runtime-visual-screen`. This leaves the Popup without a deterministic Runtime box and creates internal scroll offsets/clipping/overlap with the base Screen.

Next code task: implement the generic C26.4 Popup bounds/position/scaling/stacking contract and regression described in the detailed handoff.

## Do not lose the non-C26 binding backlog

The following Product Owner decisions remain part of Wave14 even though C26.1–C26.10 is not currently executing them:

- protected whole-system backup/restore distinct from `.escadapkg`;
- Historian Administration backup/export/import/restore;
- generic TAG raw -> engineering scaling with explicit fail-closed inverse writes;
- human numeric decimal-place authoring persisted through lifecycle/package;
- later real Modbus/PLC EEE variant only after generic gates and fresh homologation.

## Absolute guards

- PR #212 -> `main`: OPEN/DRAFT, **MUST NOT MERGE without later separate explicit Product Owner authorization**.
- PR #266: validation-only, **NEVER MERGE**.
- PR #288: C26 validation-only, **NEVER MERGE**.
- No direct `main` mutation.
- No force push/destructive rebase/branch deletion.
- Diagnose every CI red before rerun.
- Never weaken tests, security, identity, authorization, Engineering Lock, licensing, lifecycle, package or Active Revision Runtime authority.
- Wave13 #205/#207 remains paused.

The detailed handoff contains the authoritative sequence and exact SHAs known at transfer time. If GitHub live differs, GitHub wins.
