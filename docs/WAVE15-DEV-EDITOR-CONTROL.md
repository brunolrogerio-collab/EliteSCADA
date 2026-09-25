# Wave 15 — DEV-EDITOR Control

> GitHub live is the sole authority.

`LANE: DEV-EDITOR`
`MAIN_ORDER_REV: 0005`
`ORDER_ID: DEV-EDITOR-FC0A-01`
`ORDER_STATE: INTEGRATED_PENDING_T2 / DEV_WAIT`
`PLANNED_BRANCH: work/w15-dev-editor-single-canvas`
`WORK_BRANCH: work/w15-dev-editor-single-canvas`
`FC0A_RELEASE_APPROVED: YES`
`EXACT_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`
`EXACT_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`
`ACTIVATION_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: UI_EDITOR, RUNTIME_RENDERER`
`CANDIDATE_PR: #349`
`CANDIDATE_HEAD: 06eed31d99ddb34d99e0287e96e38bed3bf7dab5`
`CANDIDATE_TREE: 11a5a01fd805d3e068dc6e22efff7a65077c09e4`
`CANDIDATE_T1: 36066874097 / SUCCESS`

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


## Main candidate review — ACCEPTED FOR SEQUENTIAL CODEX QUEUE

Exact candidate:
- PR `#349`;
- head `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
- tree `11a5a01fd805d3e068dc6e22efff7a65077c09e4`;
- T1 `36066874097`: SUCCESS;
- Common sanity: SUCCESS;
- Web semantic build: SUCCESS;
- focused Chromium: SUCCESS / 42 passed;
- focused .NET: SUCCESS / 693 passed on the final unchanged-head attempt.

Main code/contract review disposition:

`DEV-EDITOR -> MAIN_ACCEPTED_FOR_CODEX / QUEUED`

Accepted findings:
- one primary authoring canvas replaces the stacked editor + separate canonical preview;
- `CanonicalVisualRenderer` remains the visual projection authority inside that canvas;
- interaction/adornment layer emits canonical UI/mutation intents rather than rendering a second visual implementation;
- transient drag/resize/rotate/polygon WYSIWYG projection reuses existing canonical mutation reducers;
- Screen and Popup consume the same `VisualEditorCanvas` path;
- Popup canonical layer is bounded by the authored logical Popup size;
- Property Inspector remains driven by the public Visual Property Registry/model;
- Working/Preview/Apply/Active semantics were not collapsed;
- design mode gained no process-write authority;
- no shared app/router/types/workflow/Authority/Licensing/HA surface changed.

Target reconciliation:
- integration target is ahead of the FC0-A base only by canonical coordination/documentation files;
- no overlapping product delta exists;
- PR #349 is mergeable.

### CODEX obligations once this candidate becomes the active sequential route

Validate exact accepted candidate plus any bounded validation-driven test commits:
1. execute the edited mounted Screen and Popup authoring specs explicitly;
2. execute the broader Editor 12-scenario matrix from #303 where still applicable;
3. prove canonical renderer parity during committed and transient manipulation;
4. prove known legacy `tank|value|dynamo|status` containment and arbitrary unknown fail-closed behavior through the single canvas;
5. selection/marquee/Outliner/multi-select move/resize/rotate/z-order/group/lock regressions;
6. undo/redo/history and Preview/Apply persistence after direct manipulation;
7. Screen/Popup logical-boundary behavior at representative viewport sizes/zoom/pan;
8. Property Inspector search/collapse must not mutate canonical properties merely by filtering/collapsing;
9. no process writes from design mode;
10. Web build + exact-head natural T1.

Small test/validation-driven corrections are allowed only under the common CODEX rule. Material product/design defects return to Main -> DEV-EDITOR.

Queue rule:
- shared sequential CODEX remains actively assigned to PR #344 Script Engineering;
- this Editor acceptance **does not supersede the current Script route**;
- Main will explicitly route CODEX to Editor only after the active Script validation handoff is resolved.

DEV-EDITOR must remain `DEV_WAIT` while queued.

No merge/T2 authorization yet.


## Main route activation — sequential CODEX

Exact accepted candidate remains:
- PR #349;
- head `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
- tree `11a5a01fd805d3e068dc6e22efff7a65077c09e4`;
- prior natural T1 `36066874097`: SUCCESS.

Current integration target after Script + INFRA-CI-01D:
- SHA `66694aac1408218a41d1251459c20465d990cfef`;
- tree `7c1588becd9ef2dc375d2a4d6381430c560a5875`.

Target advance is non-overlapping with Editor-owned product files:
- Script Engineering lane files;
- one IEC-104 test-infrastructure file;
- coordination documentation.

`ORDER_ID: DEV-EDITOR-CODEX-VALIDATION-V1`

`ORDER_STATE: CODEX_VALIDATION / ACTIVE_SHARED_ROUTE`

CODEX must validate the exact accepted Editor candidate and may add bounded validation-driven test commits. Material product/design defects return to Main -> DEV-EDITOR. No self-merge/T2.


## Main integration — Editor CODEX validation accepted

Final validated candidate:
- head `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
- tree `11a5a01fd805d3e068dc6e22efff7a65077c09e4`;
- CODEX source delta: none;
- existing natural T1 `36066874097`: SUCCESS;
- mounted/adversarial matrices returned without material product defect.

Protected merge:
- PR #349: MERGED;
- integration SHA `cfaafa4b29e1462bf9d304af995cc8638578a3c2`;
- integration tree `3ac411cfe2aff5451396ee6606625825d601ed1d`.

Disposition:
`DEV-EDITOR -> INTEGRATED_PENDING_T2 / DEV_WAIT`.

No broader T2 verification is claimed yet.
