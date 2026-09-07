# CURRENT COORDINATOR HANDOFF — Wave 14

**Date:** 2026-09-07 BRT  
**State:** **C26 ACTIVE / C26.1–C26.3 IMPLEMENTED / C26.4 DIAGNOSED NOT IMPLEMENTED / C26.5+ PENDING / #212 NOT AUTHORIZED / #266+#288 NEVER MERGE / WAVE13 PAUSED**

> GitHub live is the official and sole authority. Revalidate all refs, PR states, issue state and exact-SHA workflows before every decision or mutation.

## Canonical detailed handoff

Read first:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`

Then read live:

- issue #286 — C26 coordinator issue;
- PR #287 — C26 implementation -> canonical C11;
- PR #288 — C26 validation-only -> `main`, **NEVER MERGE**.

## Current product/test authority before this docs-only handoff

C26 branch:

`wave14/c26-po-homologation-corrections`

Exact non-doc product/test head:

`b7f0566f9e30f3e4c6dee3d020710b23923b6db2`

Completed:

- C26.1 Runtime resilience — `2cfdee7b943e35d8ad04ba77e20b89da93a48113`;
- C26.2 Runtime viewport — `a918ed4dcca2a6f5fa2657d0c7108508ead91543` + scope correction `3e312bd29a8d9109a9ec354e7f579d9bbc4aef3d`;
- C26.3 Runtime technical fallback leakage — final identity-preserving head `b7f0566f9e30f3e4c6dee3d020710b23923b6db2`.

Exact-head gates on `b7f0566...`:

- EliteSCADA CI #1433 — SUCCESS;
- Preview Licensing #381 — SUCCESS;
- Interop #258 — SUCCESS;
- L3 #337 — SUCCESS;
- Wave11 Active HMI Runtime #359 — FAILURE, **diagnosed; do not rerun unchanged**.

## Exact resume point

C26.4 Runtime Popup layout is diagnosed but **no product fix has been committed**.

The Popup inherits Engineering renderer `min-height` / margin / `overflow:auto` because C26.2 intentionally scoped the Runtime renderer reset to `.runtime-visual-screen`. The result is a missing deterministic Popup box contract, internal scroll offsets and Screen/Popup visual-interaction overlap.

Implement a generic logical Popup bounds/position/scaling/stacking contract, derive bounds from authored Popup content, add Popup-specific Runtime renderer reset, preserve Screen viewport and existing position semantics, and add canonical EEE Popup regression. Do not use an EEE-only workaround or magic z-index/fixed browser pixels.

Full diagnosis and binding implementation direction are in:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`

## Other live authorities

Canonical C11 frozen head:

`a724ece64a292aa1d1dedd886a72fb28ff8d90fe`

Frozen canonical package SHA-256:

`4be1ca2338094799a8bf3c989322e5488a2381ce65d92cac3871188639e215c6`

Accepted/green integration head:

`ff185ffd67fe4abc597af9184c21f86376ba6e17`

Pre-C26 Preview #285 head:

`d92e81f821c1a9c376b39bc3684eead54b3f570e`

Preserve #285 as pre-C26 evidence. A new Preview is required after accepted C26 product/package regeneration.

## Binding requirements outside immediate C26 execution

Do not lose these Product Owner decisions:

- separate protected whole-system backup/restore;
- safe Historian backup/export/import/restore Administration workflow;
- first-class generic TAG raw-to-engineering scaling with explicit inverse writes;
- normal human decimal-place authoring and lifecycle/package persistence;
- real Modbus/PLC EEE variant only after current generic gates and fresh homologation.

They are detailed in the canonical C26 handoff and remain part of the overall Wave14 sequence.

## Permanent boundaries

- #212 remains OPEN/DRAFT -> `main` and has **no merge authorization**;
- #266 and #288 **MUST NEVER MERGE**;
- never mutate `main` directly;
- no force push/destructive rebase/branch deletion;
- diagnose CI red before rerun;
- never weaken tests/security/Identity/authorization/Engineering Lock/licensing/lifecycle/package/Active Runtime authority;
- Runtime/Active must not depend on `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- Wave13 #205/#207 remains paused.
