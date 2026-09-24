# Wave 15 — DEV-EDITOR Control

> GitHub live is the sole authority.

`LANE: DEV-EDITOR`
`MAIN_ORDER_REV: 0002`
`ORDER_ID: DEV-EDITOR-FC0A-01`
`ORDER_STATE: ACTIVE_CODING / AUTHORIZED`
`PLANNED_BRANCH: work/w15-dev-editor-single-canvas`
`WORK_BRANCH: work/w15-dev-editor-single-canvas`
`FC0A_RELEASE_APPROVED: YES`
`EXACT_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`
`EXACT_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`
`ACTIVATION_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: UI_EDITOR, RUNTIME_RENDERER`

## Activation — ACTIVE

Main activated this lane after final FC0-A audit rev 0014 returned:
`ACCEPTABLE / FC0A_RELEASE_APPROVED`.

The work branch was created directly from the exact base above.

On `SIGA`:
1. re-read this control live;
2. revalidate the exact work-branch head;
3. inspect the FC0-A base and subtract already-integrated work;
4. begin implementation only inside this lane's owned scope;
5. do not merge or widen frozen contracts.

## Activation history

Historical prerequisite — now satisfied: Main recorded `FC0A_RELEASE_APPROVED`, wrote the exact base SHA/tree, created the work branch from that checkpoint and changed the order to ACTIVE.

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
