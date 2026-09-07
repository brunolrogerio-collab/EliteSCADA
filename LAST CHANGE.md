# LAST CHANGE — EliteSCADA

**Date:** 2026-09-07 BRT  
**Operational state:** **WAVE14 C26 ACTIVE / C26.1–3 IMPLEMENTED / C26.4 DIAGNOSED NOT IMPLEMENTED / 4 OF 5 NORMAL GATES GREEN / #212 NOT AUTHORIZED / #266+#288 NEVER MERGE / WAVE13 PAUSED**

> GitHub live is the official and sole project memory. Revalidate refs, PR state and exact-SHA workflows before every decision or mutation.

## Current C26 product/test authority before this docs-only handoff

Branch:

`wave14/c26-po-homologation-corrections`

Exact non-doc head:

`b7f0566f9e30f3e4c6dee3d020710b23923b6db2`

Implemented:

- C26.1 Runtime resilience — `2cfdee7b943e35d8ad04ba77e20b89da93a48113`;
- C26.2 Runtime viewport — `a918ed4dcca2a6f5fa2657d0c7108508ead91543`, refined by `3e312bd29a8d9109a9ec354e7f579d9bbc4aef3d`;
- C26.3 Runtime technical fallback suppression — final `b7f0566f9e30f3e4c6dee3d020710b23923b6db2`.

Exact-head validation:

- EliteSCADA CI #1433 — SUCCESS;
- Preview Licensing #381 — SUCCESS;
- Interop #258 — SUCCESS;
- L3 #337 — SUCCESS;
- Wave11 #359 — FAILURE, diagnosed; do not rerun unchanged.

## Immediate blocker

C26.4 Runtime Popup layout is diagnosed but not implemented.

Runtime Popup still inherits Engineering renderer `min-height`, margin and `overflow:auto` because the C26.2 reset is intentionally Screen-scoped. This leaves Popup rendering without a deterministic Runtime box and causes internal scroll/clipping/Screen overlap.

Next product task: implement generic logical Popup bounds + position + scaling + sane stacking, a Popup-specific Runtime renderer reset, and canonical EEE Popup regression. No EEE-only workaround.

## Authorities to preserve

Canonical C11 frozen head:

`a724ece64a292aa1d1dedd886a72fb28ff8d90fe`

Frozen package SHA-256:

`4be1ca2338094799a8bf3c989322e5488a2381ce65d92cac3871188639e215c6`

Accepted/green integration head:

`ff185ffd67fe4abc597af9184c21f86376ba6e17`

Pre-C26 Preview #285:

`d92e81f821c1a9c376b39bc3684eead54b3f570e`

## Binding work not to lose

The overall Wave14 sequence still includes, beyond C26.1–C26.10:

- protected whole-system backup/restore;
- Historian Administration backup/export/import/restore;
- generic TAG raw-to-engineering scaling with inverse-write semantics;
- decimal-place human authoring persisted through lifecycle/package;
- later real Modbus/PLC EEE variant after generic gates and fresh homologation.

## Permanent governance

- PR #287: C26 -> C11 only, DRAFT while active;
- PR #288: validation-only -> `main`, **NEVER MERGE**;
- PR #263: C11 -> integration only;
- PR #266: validation-only -> `main`, **NEVER MERGE**;
- PR #212: integration -> `main`, OPEN/DRAFT, **MUST NOT MERGE without later separate explicit Product Owner authorization**;
- no direct `main` mutation, force push, destructive rebase, blind CI rerun or contract weakening;
- Wave13 #205/#207 remains paused.

Canonical detailed handoff:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
