# Wave 15 — DEV-EDITOR Control

> GitHub live is the sole authority.

`LANE: DEV-EDITOR`
`MAIN_ORDER_REV: 0001`
`ORDER_ID: DEV-EDITOR-FC0A-01`
`ORDER_STATE: PREPARED / WAIT_FC0A_RELEASE`
`PLANNED_BRANCH: work/w15-dev-editor-single-canvas`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: UI_EDITOR, RUNTIME_RENDERER`

## Activation

Do not mutate product until Main records `FC0A_RELEASE_APPROVED`, writes exact base SHA/tree here, creates the work branch from that exact checkpoint and changes ORDER_STATE to ACTIVE.

While PREPARED, the chat may read GitHub and understand scope, then wait.

## Mission after activation

Implement the remaining downstream single-primary-canvas Engineering Editor maturity:
- one primary WYSIWYG canvas rather than a permanent stacked second preview;
- direct manipulation and deterministic selection;
- Outliner/canvas synchronization;
- Properties and binding UX;
- move/resize/group/lock/z-order/alignment/distribution/size;
- grid/snap/zoom/pan and workspace density;
- Screen/Popup authoring parity where canonical model supports it;
- consume frozen FND-06 known-legacy compatibility and canonical renderer.

Before coding, revalidate exact FC0-A base and subtract anything already closed by PR #340/consolidated FC0-A.

## Primary ownership

- `web/scada-web/src/engineering/visual-editor/**`
- lane-local Editor components

Shared hotspots require Main coordination:
- `EngineeringApp.tsx`;
- common types;
- app/router/shell;
- common localization/layout files;
- CI/workflows.

## Forbidden

No second renderer, no second compatibility registry, no Working->Active collapse, no process-write authority from design mode, no FND-06 redesign.

## DEV delivery

DEV is primarily a code implementer.

Deliver:
- one isolated branch/PR;
- exact head/tree;
- coherent changed-file map;
- implemented acceptance items;
- cheap/focused build/sanity evidence where practical;
- known gaps;
- required broad tests not executed marked `PENDING_FOR_CODEX`;
- explicit non-actions.

Material Main review defects return to this DEV.

After Main accepts candidate direction, CODEX owns focused/mounted/adversarial validation and exact-head T1.

Required prefix:
`DEV-EDITOR -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Cross-lane control:
`docs/WAVE15-PARALLEL-DEV-CONTROL.md`.
