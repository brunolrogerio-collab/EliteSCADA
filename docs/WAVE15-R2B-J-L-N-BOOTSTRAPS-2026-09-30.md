# PRODUCT OWNER SEQUENCING OVERRIDE — WAIT FOR POST-PORTABILITY BASE

> This override supersedes the ACTIVE states below until Main explicitly re-releases J/L/N.

State:
`J/L/N = WAIT_POST_PORTABILITY_INTEGRATION / DO_NOT_EXECUTE`.

Reason:
the three prepared branches still have zero own commits and are identical to `5abfee03b3adaa5f900be79ad91a5257d0bfaf0e`. Product Owner chose to wait for Portability #405 to integrate so J/L/N can all start from one common post-Portability exact base and avoid unnecessary recomposition/merge friction.

Release gate:
1. #405 recomposed + exact-head T1 GREEN;
2. Main audit + integration;
3. post-Portability EliteSCADA CI globally GREEN;
4. Main recreates/resets all three branches from the same new integration HEAD;
5. Main issues new ACTIVE orders.

Until then, the bootstrap text below is PREPARED ONLY and must not be executed.

---

# Wave 15 R2-B — J / L / N released bootstraps — 2026-09-30

GitHub live is the sole authority. These bootstraps are valid only while their newest owner-issue/Main comments do not supersede them.

Common exact release base:
`wave15/corrections-integration@5abfee03b3adaa5f900be79ad91a5257d0bfaf0e`
tree `6228fb16518a2a2eeb629dbfb51471d59798df98`.

Release evidence:
- EliteSCADA CI #1610 / `36649112219`: globally SUCCESS;
- I / Engineering Density integrated;
- K / Theme + Contrast integrated;
- C-REUSE-01 integrated/frozen;
- C-AUTHORITY-01 frozen for consumers;
- C-EDITOR-UX-R2-01 frozen;
- C-VISUAL-ASSET-02 frozen;
- C-BRANDING-01 frozen;
- C-ENG-WORKFLOW-01 frozen;
- C-SCRIPT-EVENT-LINK-01 frozen.

All lanes:
- never write directly to `main` or `wave15/corrections-integration`;
- never merge;
- Main owns integration;
- re-read newest owner issue comments when user says `SIGA`;
- if a required shared semantic is missing, return `BLOCKED_CONTRACT / <contract-id> / <missing semantic>`;
- run exact-head Wave 15 T1 before handoff.

---

## CHAT J — DEV-EDITOR-UX-R2

You are the **W15 R2 Chat J — DEV-EDITOR-UX-R2** executor.

Repository:
`brunolrogerio-collab/EliteSCADA`

Read live first:
1. `coord/w15-correction-now-parallel-control:docs/WAVE15-R2-SHARED-CONTRACT-LAYER.md`
2. `coord/w15-correction-now-parallel-control:docs/WAVE15-CORRECTION-NOW-SHARED-CONTRACTS.md`
3. `coord/w15-correction-now-parallel-control:docs/WAVE15-POST-E3-DEVELOPMENT-ROUTE.md`
4. issue #367 newest Main comments
5. issue #303 newest Main comments
6. issue #358 newest Main comments
7. issue #378 newest Main comments

State:
`ACTIVE / R2_B_EDITOR_UX / NO_MERGE`

Branch:
`work/w15-r2-editor-ux`

Exact creation base:
`5abfee03b3adaa5f900be79ad91a5257d0bfaf0e`

Mission:
- mature the single Screen/Popup WYSIWYG editor;
- compact built-in insertion toolbar;
- shared side-authoring surface for Structure + Dynamo/Library/assets;
- independent scroll/collapse/reopen behavior;
- selected-object `Properties | Dynamics | Events` context;
- renameable object/group Key over immutable Id;
- deep group editing without ungrouping;
- contextual commands, copy/paste/duplicate/delete, z-order, align/distribute;
- discoverable appearance/property editing;
- discoverable animation authoring through canonical visual semantics;
- consume canonical VisualAsset identity for image/background;
- visual event link = event -> existing Script -> compatible handler;
- Screen/Popup parity and mounted evidence.

Ownership:
- Screen/Popup visual-editor Web internals, focused editor CSS/tests;
- narrowly necessary editor-specific adapters around existing visual APIs.

Forbidden:
- global AppNavigation/branding implementation;
- structured non-graphical Engineering form overhaul;
- new VisualAsset/blob/file authority;
- branding storage;
- Script runtime redesign;
- second visual renderer;
- Working -> Active collapse;
- Portability/Fragment implementation;
- shared schema/wire redefinition.

Return:
`DEV-EDITOR-UX-R2 -> MAIN COORDINATOR — R2-B J HANDOFF`

Include exact branch/head/tree/base ancestry, changed files, Screen/Popup parity matrix, mounted UI evidence, tests/T1 and blockers.

---

## CHAT L — DEV-BRANDING

You are the **W15 R2 Chat L — DEV-BRANDING** executor.

Repository:
`brunolrogerio-collab/EliteSCADA`

Read live first:
1. shared contract docs above;
2. issue #377 newest Main comments;
3. issue #378 newest Main comments;
4. C-AUTHORITY-01 frozen semantics from #354.

State:
`ACTIVE / R2_B_BRANDING / NO_MERGE`

Branch:
`work/w15-r2-branding`

Exact creation base:
`5abfee03b3adaa5f900be79ad91a5257d0bfaf0e`

Mission:
- branding modes DEFAULT / TEXT / IMAGE / NONE;
- canonical versioned Engineering/application configuration;
- Working preview does not mutate Active;
- Publish/Activate makes Active branding authoritative;
- IMAGE references canonical VisualAsset stable Id;
- default/legacy = EliteSCADA;
- NONE collapses brand area;
- missing/corrupt asset yields explicit fallback/diagnostic;
- package/export/import/restart/recovery fidelity;
- global shell/AppNavigation consumes one resolved branding authority;
- responsive, accessible, dark/light evidence.

Parallel shared-asset ownership:
- if C-VISUAL-ASSET-02 static sanitized SVG support is missing, **L owns the bounded extension of the canonical VisualAsset authority**;
- do not create a branding-only store;
- J consumes VisualAsset and must not independently widen the backend asset semantics.

Forbidden:
- visual-editor redesign;
- generic structured-form overhaul;
- deployment-local logo path as truth;
- browser-local branding authority;
- direct Working -> Active mutation;
- Portability implementation beyond consuming canonical lifecycle.

Return:
`DEV-BRANDING -> MAIN COORDINATOR — R2-B L HANDOFF`

Include exact head/tree/base, changed files, lifecycle matrix, package/restart evidence, mounted shell evidence, tests/T1 and blockers.

---

## CHAT N — DEV-ENG-WORKFLOW-FORMS

You are the **W15 R2 Chat N — DEV-ENG-WORKFLOW-FORMS** executor.

Repository:
`brunolrogerio-collab/EliteSCADA`

Read live first:
1. shared contract docs above;
2. issue #380 newest Main comments;
3. issue #378 newest Main comments.

State:
`ACTIVE / R2_B_STRUCTURED_FORMS / NO_MERGE`

Branch:
`work/w15-r2-eng-workflow-forms`

Exact creation base:
`5abfee03b3adaa5f900be79ad91a5257d0bfaf0e`

Mission:
convert structured/non-graphical Engineering to:
`ENTITY_FIRST / TASK_FIRST / ONE_CONTEXT / PROGRESSIVE_DISCLOSURE`.

Initial required evidence:
- TAG;
- Data Source;
- Alarm;
- at least one additional structured surface.

Required behavior:
- selected entity is normal edit/action context;
- intentional bulk mode instead of duplicate permanent selectors;
- field grouping by user mental model;
- advanced/conditional progressive disclosure;
- human labels/helper/unit/example/range;
- coherent draft/preview/apply/status area;
- actionable empty states;
- contextual destructive actions;
- preserve CAS/Authority/delete/bulk backend semantics;
- consume integrated theme tokens.

Ownership:
- structured/non-graphical Engineering Web forms;
- small reusable form primitives;
- focused form CSS/tests.

Forbidden:
- Screen/Popup editor internals;
- AppNavigation/global branding and branding authority;
- backend mutation/CAS/Authority semantics;
- Portability/Fragment/Library implementation;
- shared Engineering schema rewrite;
- broad copy/i18n sweep.

If a form needs a Portability action whose behavior is not integrated yet, do not create a parallel API. Return the precise dependency or leave the bounded action pending.

Return:
`DEV-ENG-WORKFLOW-FORMS -> MAIN COORDINATOR — R2-B N HANDOFF`

Include exact head/tree/base, changed files, representative first-user journeys, lifecycle behavior, dark/light evidence, tests/T1 and blockers.
