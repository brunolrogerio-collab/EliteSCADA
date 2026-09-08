# Current Coordinator Handoff

> GitHub live is the official memory and sole authority for EliteSCADA. Revalidate live before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge. Any divergence here is resolved in favor of GitHub.

## Current Wave 14 topology

- Repository: `brunolrogerio-collab/EliteSCADA`
- Integration: `wave14/corrections-integration`, PR #212 -> `main` — **OPEN/DRAFT; NO MERGE without later explicit Product Owner authorization**
- Active C26 branch: `wave14/c26-po-homologation-corrections`
- Coordinator issue: #286
- Implementation PR: #287 C26 -> C11, DRAFT
- Validation PR: #288 C26 -> `main` — **VALIDATION ONLY / MUST NEVER MERGE**
- C11 validation PR #266 — **MUST NEVER MERGE**
- Pre-C26 Preview #285 — preserve untouched as historical homologation evidence
- Wave13 #205/#207 — paused

## Exact current product/test authority

Last exact C26 product/test SHA validated across all five normal gates:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

At that SHA:

- EliteSCADA CI #1444 / `34237046911` — SUCCESS
- Preview Licensing CI #392 / `34237046935` — SUCCESS
- Interop Lab Smoke #269 / `34237046897` — SUCCESS
- Wave 11 Active HMI Runtime #370 / `34237046873` — SUCCESS
- L3 Seven-Driver Lab #348 / `34237046980` — SUCCESS

## C26 progress

- C26.1 Runtime transient-failure resilience — **IMPLEMENTED / VALIDATED**
- C26.2 Runtime logical viewport/layout — **IMPLEMENTED / VALIDATED**
- C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED / VALIDATED**
- C26.4 Runtime popup composition — **IMPLEMENTED / VALIDATED**
- C26.5 Alarm/Event/Historian Runtime surface — **DIAGNOSED / REGRESSION LOCKED / VALIDATED**
- C26.6 Engineering shell/workspace usability — **IMPLEMENTED / VALIDATED**
- C26.7 Engineering theme/contrast — **ACTIVE**
- C26.8 Screen editor functionality audit/fixes — pending
- C26.9 Popup editor functionality audit/fixes — pending
- C26.10 canonical EEE residual cleanup — pending
- C26.11 repackage/new Preview/homologation sequence — pending according to live issue #286

## C26.6 validated correction

Commits:

- `4286f2409f30492387f764b192acede6d26bd3aa` — user-controlled collapse/restore model and constrained-viewport browser regression;
- `4b0e46d96510c6a637e12dc83c6d56dd8b9cc592` — mobile collapsed-Properties behavior;
- `c1ae4bd311334835961bf4bbdcd6bd46b4acfa36` — explicit outer workspace grid placement after navigation collapse;
- `7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11` — explicit inner Visual Editor main grid placement after Screens-list collapse with mobile reset.

The regression intentionally exercises a 1024x720 viewport, collapses Engineering navigation, Screens and Palette, verifies canvas recovery/dominance, and proves Properties can collapse and be restored. The first two validation attempts found real zero-width canvas defects caused by CSS Grid auto-placement after hidden side panels. Their assertions were not relaxed; product CSS was corrected.

C26.6 preserves all capabilities and backend authority. Long panels retain independent scrolling and no feature was removed merely to gain canvas width.

## C26.7 immediate scope

Live issue #286 acceptance:

- explicit readable foreground/background tokens for Engineering fields;
- editable, readonly, disabled and placeholder states must be visually distinguishable;
- regression coverage for affected Engineering surfaces;
- scope includes Screens and Script Engineering;
- presentation only: no authentication, authorization, Engineering Lock, lifecycle or capability changes.

Before any C26.7 write, locate/fetch the exact live Screens and Script Engineering components/CSS and closest canonical tests. Prefer shared generic form-state styling over one-off screen-specific colors.

## Preserved C26.5 Preview diagnosis

`/api/historical/query` exists but is feature-gated. Preview #285 starts Historian without enabling the HistoricalQuery/cursor-key configuration. The PO-observed 404 was a pre-C26 Preview harness configuration gap, not a missing backend route and not a valid empty result. Keep #285 untouched. A new post-C26 Preview must configure HistoricalQuery safely for Preview/development.

## Mandatory follow-on Wave14 obligations

Still binding after C26:

- protected whole-system backup/restore distinct from `.escadapkg`;
- Historian Administration backup/export/import/restore;
- generic TAG raw -> engineering scaling with explicit inverse-write semantics;
- human decimal-place authoring persisted through Save -> Publish -> Activate -> Runtime -> package export/import;
- real EEE Modbus/PLC variant only after generic gates, corrected package and fresh homologation.

## Guardrails

- never modify `main` directly;
- never merge #288 or #266;
- never merge #212 without later separate explicit Product Owner authorization;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose every CI red before rerun; no blind rerun;
- never weaken tests, validation, security, authentication, authorization, Identity, Engineering Lock, licensing, lifecycle, package, drivers or Runtime Active Revision authority;
- Runtime/Active remains self-contained and cannot depend on `.escadalib`;
- Alarm / Operational Event / Audit remain separate.

When the Product Owner says `siga`, continue autonomously through subsequent safe work. It never authorizes protected merges.
