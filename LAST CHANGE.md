# LAST CHANGE — EliteSCADA

**Date:** 2026-09-08 BRT  
**Operational state:** **WAVE14 C26 ACTIVE / C26.1–C26.6 IMPLEMENTED + VALIDATED / C26.7 ACTIVE / #212 NOT AUTHORIZED / #266+#288 NEVER MERGE / WAVE13 PAUSED**

> GitHub live is the official and sole project memory. Revalidate refs, PR state and exact-SHA workflows before every decision or mutation.

## Current exact product/test authority

Branch:

`wave14/c26-po-homologation-corrections`

Exact product/test head validated green across all five normal gates:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

C26 progress:

- C26.1 Runtime resilience — IMPLEMENTED / VALIDATED;
- C26.2 Runtime viewport — IMPLEMENTED / VALIDATED;
- C26.3 Runtime technical-id fallback suppression — IMPLEMENTED / VALIDATED;
- C26.4 Runtime Popup composition — IMPLEMENTED / VALIDATED;
- C26.5 Alarm/Event/Historian surface — DIAGNOSED / REGRESSION LOCKED / VALIDATED;
- C26.6 Engineering shell/workspace usability — IMPLEMENTED / VALIDATED;
- C26.7 Engineering theme/contrast — ACTIVE;
- C26.8–C26.11 pending per live issue #286.

## C26.6 exact evidence

C26.6 commits after coordinator handoff `11ef0f7...`:

- `4286f2409f30492387f764b192acede6d26bd3aa` — collapsible Engineering workspace and constrained-viewport regression;
- `4b0e46d96510c6a637e12dc83c6d56dd8b9cc592` — responsive collapsed Properties behavior;
- `c1ae4bd311334835961bf4bbdcd6bd46b4acfa36` — keep outer Engineering workspace explicitly placed after navigation collapse;
- `7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11` — keep Visual Editor main explicitly placed after Screens-list collapse, with mobile single-column reset.

The Playwright regression was not weakened. It exposed two real CSS Grid auto-placement defects that collapsed the canvas to zero width after user-controlled panel collapse; both were fixed causally.

At exact SHA `7d9d977...` all five normal gates completed SUCCESS:

- EliteSCADA CI #1444 / run `34237046911`;
- Preview Licensing CI #392 / run `34237046935`;
- Interop Lab Smoke #269 / run `34237046897`;
- Wave 11 Active HMI Runtime #370 / run `34237046873`;
- L3 Seven-Driver Lab #348 / run `34237046980`.

## C26.7 active scope

Per live issue #286, C26.7 is Engineering theme/contrast. Acceptance requires readable explicit foreground/background treatment and visually distinct editable, readonly, disabled and placeholder states, with regression coverage for affected Engineering surfaces including Screens and Script Engineering.

No security, authorization, lifecycle or capability behavior may change as part of this presentation correction.

## Preserved authorities

Canonical C11 base remains:

`a724ece64a292aa1d1dedd886a72fb28ff8d90fe`

Pre-C26 Preview #285 remains preserved and untouched as historical evidence. Its HistoricalQuery HTTP 404 was diagnosed as a pre-C26 Preview harness configuration gap, not a missing backend route or valid no-data state.

## Permanent governance

- PR #287: C26 -> C11 only, DRAFT while C26 is active;
- PR #288: validation-only -> `main`, **NEVER MERGE**;
- PR #263: C11 -> integration only;
- PR #266: validation-only -> `main`, **NEVER MERGE**;
- PR #212: integration -> `main`, OPEN/DRAFT, **MUST NOT MERGE without later separate explicit Product Owner authorization**;
- no direct `main` mutation, force push, destructive rebase, blind CI rerun or contract weakening;
- Runtime/Active must not depend on `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- Wave13 #205/#207 remains paused.

Canonical detailed ledger:

`docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
