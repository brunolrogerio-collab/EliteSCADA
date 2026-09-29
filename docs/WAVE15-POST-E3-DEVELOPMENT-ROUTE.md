# Wave 15 — Post-E3 Development Route

**Status:** PREPARED / WAIT_REV0117_TWEEN_DIAGNOSTIC / NO_POST_E3_PRODUCT_MUTATION_YET  
**Coordinator issue:** #378  
**Execution ledger:** #305  
**Current integration baseline at preparation time:** `wave15/corrections-integration@50b2750c73623b7ffef77f0ca93755c3e8278676`  
**Current shared CODEX order:** `rev0117 / FINAL-SEQUENTIAL-CODEX-SCRIPT-TWEEN-BRIDGE-DIAGNOSTIC-98`

GitHub live is authoritative. Every gate below must be revalidated against live branches, PR heads, CI and issue handoffs before execution.

## 1. Objective

Finish the current correction wave without mixing unrelated productization work into the active validation path.

The route is:

```text
rev0117 causal TWEEN bridge-completion diagnostic after rev0116 READ+WRITE completed (#373 and #376 already E3-accepted)
  -> integrate #373/#374/#376 if Gate 0 closes
  -> recompose/integrate #375
  -> freeze Round-2 UX/product contracts
  -> R2-A: Engineering density + specialized-surface theme/contrast
  -> R2-B: Editor UX + Client branding + structured Engineering workflows
  -> R2-C: user-facing terminology + multilingual consistency
  -> exact E2/E3
  -> mandatory SECOND Preview + independent Audit
  -> residual corrections/recheck
  -> CORRECTION PHASE ACCEPTED
  -> return to deferred original Wave 15
  -> container/host productization + Local/Remote DB topology later in canonical order
```

## 2. Gate 0 — residual E3 disposition

Do not start new product branches while rev0117 is active.

Current carried-forward state:
- #376 Gateway: `E2_ACCEPTED / E3_MOUNTED_ACCEPTED / WAIT_COHORT_GATE0_CLOSE`;
- #373 HMI: `E2_ACCEPTED / E3_MOUNTED_ACCEPTED`;
- #376 Gateway: `E2_ACCEPTED / E3_MOUNTED_ACCEPTED`;
- #374 Script: Apply + persisted Key rename/reuse/no-retarget + bridge operation/render evidence accepted; only minimal Python handler completion remains;
- rev0115 was invalidated by a disposable fixture that omitted the product-generated `elite_scada` import;
- rev0116 proved Script Assistant-generated READ and WRITE complete; TWEEN emitted a real request and changed the mounted object but did not return Worker completion. rev0117 localizes that exact request/response/await seam without product mutation.

If CODEX returns:

`COMBINED_E3_PASS / LOCAL_DOCKER_MOUNTED / #373+#374+#376 COMPLETE`

Main must:
1. revalidate integration still equals the pinned base;
2. revalidate #373/#374/#376 exact heads;
3. integrate in preferred order:
   `#373 -> #374 -> #376`;
4. prove final integrated tree is byte-identical to:
   `3aaec957ce27c73bb8b7090b7cd9f412ba26b567`.

If a causal failure is returned:
- assign only the proven owning lane or cross-lane boundary;
- no speculative broad rewrite;
- rebuild the exact combined candidate after correction;
- obtain required E2/E3 again before integration.

## 3. Gate 1 — Reusable Objects R1 (#375)

After #373/#374/#376 are integrated:

- recompose `work/w15-reusable-objects-correction` from then-current integration;
- preserve accepted C-REUSE-01 semantics;
- resolve only additive composition;
- rerun E2;
- Main reviews exact diff/tree;
- integrate #375 if accepted.

No new Round-2 correction branch should be based on the old pre-#375 integration checkpoint.

## 4. Gate 2 — freeze minimal shared contracts

Before parallel Round-2 implementation, Main/CODEX freezes the bounded shared contracts below.

### C-ENG-DENSITY-01

- `TASK_FIRST / COMPACT_CONTEXT / SECONDARY_INFORMATION_ON_DEMAND`;
- one compact persistent Engineering context row;
- from the current metadata strip, only Workspace saved/dirty/conflict state remains permanently visible, moved into the compact top Engineering context row so the separate metadata strip disappears;
- Engineering Lock becomes compact state-first: feature name/details on demand;
- schema/base revision/snapshot timestamp move to `Informações -> Detalhes técnicos`;
- dirty/Lock/CAS/current-task state remains truthful when relevant;
- explicit wide/full-width mode for graphical editors;
- ordinary form/list surfaces retain readable widths.

### C-PRODUCT-VERSION-01

- human-facing EliteSCADA version is distinct from Engineering schema and project revision;
- current Product Owner proposal: `EliteSCADA Alpha 0.15.2.1`;
- intended cycle mapping: pre-1.0 / Wave 15 / post-Preview 2 / correction package 1;
- one canonical version source for Web/API/distribution metadata;
- exact commit/build remains technical provenance, not the visible release version;
- no component-local hard-coded version string;
- Engineering `Informações` shows product version first and technical metadata separately.

### C-EDITOR-UX-R2-01

- frequent built-in object insertion lives in compact toolbar actions;
- Structure + Dynamo/library/assets use a shared side-authoring surface;
- no Outliner overlay on canvas;
- independent scrolling for Structure and Library regions;
- collapsed state releases nearly all width;
- reopen affordance remains obvious/high-contrast;
- contextual object/group action model;
- click-vs-drag behavior preserved;
- object label visibility modes: all / selected / hidden;
- Screen/Popup parity.

### C-VISUAL-ASSET-02

Shared foundation required before J/L parallel release.

Editor authoring requirements are part of this same contract:
- image object selects/imports canonical VisualAsset;
- Screen/Popup import parity;
- background color/image/fit is visible and discoverable;
- no new private image persistence path;
- asset IDs remain canonical authority.


- one canonical VisualAsset store/API/identity;
- PNG/JPEG/BMP compatibility preserved;
- image object, Screen/Popup background and Branding all reference stable asset ID;
- no local filesystem path persistence;
- no branding-only asset/blob store;
- package/export/import preserves identity/hash/content;
- safe SVG, if implemented, extends this same authority through a static sanitized-vector boundary;
- Editor and Branding consumers return `BLOCKED_CONTRACT` instead of locally widening asset semantics.

### C-BRANDING-01

- modes: default EliteSCADA / text / image / none;
- branding is versioned application/project configuration;
- image references canonical C-VISUAL-ASSET-02 asset identity;
- Active Runtime uses Active branding;
- Working-only edits do not mutate Active Runtime;
- Engineering settings may preview Working branding without changing Active authority;
- package/restart/recovery preserve branding;
- SVG support requires the frozen C-VISUAL-ASSET-02 safe static-vector boundary.

### C-USER-COPY-I18N-01

Prepared now, implemented later:
- internal development jargon stays out of ordinary user UI;
- one deliberate glossary for pt-BR/en/es;
- no accidental language mixing;
- lifecycle/action/error copy is natural per locale;
- industrial/product terms are either intentionally retained or localized consistently;
- technical codes/details remain available under diagnostics when useful.

Implementation is intentionally deferred until R2-A + R2-B are integrated to avoid copy work colliding with structural UI rewrites.

## 5. Development Wave R2-A

Base: exact post-#375 integration checkpoint.

Two branches/chats may run in parallel because ownership is intentionally isolated.

### Chat I — DEV-ENG-DENSITY

Own:
- consolidate the two Engineering context/header rows;
- compact persistent state;
- keep only Workspace saved/dirty/conflict state from the current metadata strip;
- move schema/base revision/snapshot timestamp to `Informações -> Detalhes técnicos`;
- expose canonical product version prominently in `Informações`;
- reduce vertical chrome;
- create/own graphical-editor wide-section hook;
- preserve normal non-editor readable widths;
- dark/light smoke.

Must not:
- modify Visual Editor internal toolbar/palette/outliner behavior;
- modify global AppNavigation branding.

Expected evidence:
- focused frontend tests;
- exact-SHA E2;
- mounted Engineering smoke.

### Chat K — DEV-THEME-CONTRAST

Own:
- Report Designer theme-token correction;
- Script/Python editor theme-token correction;
- shared structured Engineering form/mutation-panel theme correction;
- Engineering semantic theme consumption;
- Monaco light/dark theme synchronized with the active EliteSCADA theme;
- dark/light parity;
- disabled/hover/selected/focus/error/warning contrast;
- white report paper remains semantically white;
- no report model/data redesign;
- no Script lifecycle/capability/runtime redesign;
- no entity/bulk/delete workflow redesign (that belongs to Chat N).

Source-confirmed Script/Python defect to correct:
- outer Script workspace already consumes `--eng-*` through `--script-*`;
- `python-editor.css` independently uses generic `--surface` / `--border` with light fallbacks;
- `PythonMonacoEditor` creates Monaco without an explicit theme, allowing a light editor inside dark Engineering.

Acceptance includes toolbar, path/cursor text, handler context, chips, Client Visual API help, Monaco surface, diagnostics/status and focus states in both themes.

May run concurrently with Chat I.

## 6. Development Wave R2-B

Starts only after R2-A is integrated **and**:
- C-VISUAL-ASSET-02 is frozen;
- C-BRANDING-01 is frozen;
- C-REUSE-01 is integrated/frozen after #375;
- the integrated C-ENG-DENSITY-01 layout hook is available.


### Chat J — DEV-EDITOR-UX-R2

Single owner for Editor interaction/layout hotspots.

Own:
1. built-in objects as compact toolbar insert actions;
2. full Dynamo/library/assets surface outside permanent narrow palette;
3. Structure/Outliner moved off canvas;
4. independent Structure and Library scrolling;
5. high-contrast minimal-width reopen affordance;
6. recovered canvas width/height;
7. object/group contextual menu;
8. click vs drag guard;
9. group deep-edit;
10. labels: all / selected / hidden;
11. selected image object: choose project image / import from computer;
12. Popup image-import parity;
13. visible Screen/Popup background authoring;
14. preserve all existing library/import capability;
15. Screen/Popup parity.

Do not split this lane into simultaneous Editor branches. The files/hotspots overlap too heavily.

### Chat L — DEV-BRANDING

Issue: #377.

Own:
- canonical branding contract implementation;
- default/text/image/none;
- Engineering branding settings;
- shell resolution/rendering;
- VisualAsset reuse;
- safe SVG static subset if technically accepted;
- save/reopen/publish/activate/recovery/package fidelity;
- responsive shell;
- none mode reclaims width.

Must not modify Visual Editor layout internals.

Chat J and Chat L may run in parallel after R2-A integration.

### Chat N — DEV-ENG-WORKFLOW-FORMS

Issue: #380.

Starts after R2-A integrates and C-ENG-WORKFLOW-01 freezes.

Owns structured/non-graphical Engineering workflows:
- make the selected entity the single normal interaction context;
- contextualize Delete/secondary actions instead of permanent duplicate mutation panels;
- introduce intentional multi-select/bulk-edit mode;
- reuse entity browser selection where practical;
- group fields by user mental model;
- basic vs advanced progressive disclosure;
- conditional field relevance;
- helper text/unit/example/range for non-obvious fields;
- actionable empty states;
- coherent Preview/Apply/status region;
- representative cleanup across TAG/Data Source/Alarm and other structured surfaces.

May run in parallel with J/L only under strict ownership:
- N does not modify Screen/Popup visual-editor internals;
- N does not own global branding/AppNavigation;
- J does not reorganize generic TAG/Data Source/Alarm forms;
- L does not redefine generic entity workflows.

Consumes:
- integrated C-ENG-DENSITY-01;
- integrated/frozen C-ENG-THEME-01;
- frozen C-ENG-WORKFLOW-01;
- C-AUTHORITY-01;
- C-SURFACE-01;
- C-TEST-EVIDENCE-01.

Must preserve:
- CAS;
- secure delete/dependency validation;
- bulk Preview-before-Apply;
- stable entity/API identity;
- Working/Published/Active semantics.

Evidence:
- exact-SHA E2;
- mounted TAG/Data Source/Alarm first-user workflows;
- at least one additional structured surface;
- create/edit/preview/apply;
- contextual delete;
- bulk mode;
- empty state;
- advanced disclosure;
- dark/light.

## 7. Gate 3 — validation/integration

Every user-facing lane requires:
- Main code review;
- exact candidate SHA/tree;
- E2;
- E3 before Main acceptance.

Mechanical `mergeable=true` is never sufficient.

If Main builds a combined E3 candidate:
- exact input heads must be pinned;
- pairwise overlap must be reviewed;
- combined tree must be recorded;
- no extra files may enter via conflict resolution;
- post-integration final tree must match the validated composition where that exact-tree model is used.

## 8.5. Development Wave R2-C — UX copy / i18n cleanup

Issue: #379.

Starts only after R2-A and all R2-B lanes (J/L/N) are integrated.

### Chat M — DEV-UX-COPY-I18N

Own:
- inventory user-visible strings across the corrected product surfaces;
- freeze/consume C-USER-COPY-I18N-01 glossary;
- remove internal coordination jargon from ordinary UI;
- normalize pt-BR/en/es user-facing terminology;
- migrate common concepts to shared translation keys where practical;
- preserve backend/API/internal semantic identifiers;
- add focused localization/copy regressions.

Must not:
- redesign lifecycle/Authority semantics;
- rename backend enums/contracts for cosmetic reasons;
- reopen layout/component architecture already accepted from I/J/K/L;
- silently remove technical diagnostics that are useful to support; move them behind an appropriate details surface instead.

Required evidence:
- exact-SHA E2;
- mounted pt-BR/en/es smoke on affected surfaces;
- no known internal-only vocabulary in ordinary first-user paths;
- Human Preview remains final language-quality evidence.

## 8. Gate 4 — integrated correction E3

After R2-A and R2-B are integrated, validate one exact integrated candidate with:
- real DB/API/Web/browser;
- normal Engineering navigation;
- Screen/Popup authoring;
- side surface/Structure/library;
- contextual object/group actions;
- image/background workflows;
- Report Designer dark/light;
- branding default/text/image/none;
- Save/Reopen;
- Publish/Activate/restart where affected;
- no mock substitution for accepted user behavior.

## 9. Gate 5 — mandatory SECOND Preview + Audit

CI and E3 do not replace this gate.

Use one exact integrated candidate.

### Preview A — CODEX black-box

- fresh environment;
- normal-user exploration;
- no source/control lookup during the user journey;
- findings held until Product Owner journey completes.

### Preview B — Product Owner human

- independent fresh real-browser journey;
- no detailed CODEX findings shown first;
- validates discovery, density, readability and natural workflows.

### Audit

Independent technical review of:
- exact integrated tree;
- contracts;
- persistence/lifecycle;
- security/Authority boundaries;
- regression risk;
- evidence integrity.

Only after all three complete should Main correlate findings.

## 10. Gate 6 — residual correction loop

Material finding:

```text
finding
  -> bounded owner
  -> correction
  -> E2/E3 as applicable
  -> exact integration
  -> targeted recheck
```

Do not declare acceptance while material P0/P1/P2 findings remain unresolved.

## 11. Gate 7 — CORRECTION PHASE ACCEPTED

Required:
- #373/#374/#376 integrated;
- #375 integrated;
- R2-A/R2-B integrated;
- applicable E2/E3 green;
- CODEX second Preview complete;
- Product Owner second Preview complete;
- independent Audit complete;
- material residual findings closed and rechecked.

Then Main records:

`CORRECTION PHASE ACCEPTED`

Only then return to the deferred original Wave 15 roadmap.

## 12. Deferred original Wave 15 after correction acceptance

Do not activate these merely because they are documented here.

Resume canonical sequencing for:
- EliteGO;
- Installation UX;
- downstream HA;
- container/native deployment productization #363;
- database topology #366:
  - Local managed PostgreSQL/TimescaleDB by default;
  - supported Remote PostgreSQL/TimescaleDB;
  - optional Historian override;
  - Local -> Remote;
  - Remote -> Local;
  - Remote -> Remote;
  - safe migration/cutover/rollback;
- product convergence / industrial visuals / help / localization / EEE v15;
- final exact validation and final Preview.

## 13. Shared-contract dependency matrix

| Chat | Must consume/freeze before start | May not redefine |
|---|---|---|
| I — ENG-DENSITY | C-AUTHORITY-01, C-SURFACE-01, C-TEST-EVIDENCE-01, frozen C-ENG-DENSITY-01, frozen C-PRODUCT-VERSION-01 | Working/Published/Active, Lock/CAS authority |
| K — THEME-CONTRAST | C-SURFACE-01, C-TEST-EVIDENCE-01, frozen C-ENG-THEME-01, existing Engineering semantic theme tokens | report/script data models, lifecycle, Script capability/runtime semantics |
| J — EDITOR-UX-R2 | integrated C-ENG-DENSITY-01, C-VISUAL-IDENTITY-01, C-VISUAL-DYNAMIC-01, C-TAG-WRITE-01, integrated/frozen C-REUSE-01, frozen C-VISUAL-ASSET-02, C-SURFACE-01, C-TEST-EVIDENCE-01 | renderer, identity, TAG write, reuse relationship, asset store/API |
| L — BRANDING | integrated C-ENG-DENSITY-01, C-AUTHORITY-01, frozen C-VISUAL-ASSET-02, frozen C-BRANDING-01, C-SURFACE-01, C-TEST-EVIDENCE-01 | lifecycle authority, asset store/API, independent per-page branding state |
| N — ENG-WORKFLOW-FORMS | integrated C-ENG-DENSITY-01, integrated/frozen C-ENG-THEME-01, frozen C-ENG-WORKFLOW-01, C-AUTHORITY-01, C-SURFACE-01, C-TEST-EVIDENCE-01 | visual-editor internals, branding shell, backend mutation semantics/API identity |
| M — UX-COPY-I18N | integrated R2-A + J/L/N, frozen C-USER-COPY-I18N-01, C-SURFACE-01, C-TEST-EVIDENCE-01 | lifecycle/Authority semantics, backend enum/API identity, accepted layout architecture |

Any consumer that finds an insufficient contract returns:
`BLOCKED_CONTRACT / <contract-id> / <missing semantic>`.

## 14. Parallelism matrix

| Phase | Chat | May run with | Must wait for |
|---|---|---|---|
| Current | Shared CODEX rev0117 TWEEN diagnostic | nothing new mutating same product | active now |
| Gate 1 | G / #375 recomposition | none required | E/F/H integration |
| Contract | Main/CODEX | no product DEV required | post-#375 base |
| R2-A | I / Engineering density | K | frozen C-ENG-DENSITY-01 + C-PRODUCT-VERSION-01 + post-#375 base |
| R2-A | K / Theme + contrast | I | frozen C-ENG-THEME-01 + post-#375 base |
| Contract | Main/CODEX C-VISUAL-ASSET-02 + C-BRANDING-01 | none | I/K integration |
| R2-B | J / Editor UX | L | I/K integrated + C-REUSE-01 integrated/frozen + C-VISUAL-ASSET-02 frozen |
| R2-B | L / Branding | J | I/K integrated + C-VISUAL-ASSET-02 + C-BRANDING-01 frozen |
| R2-B | N / Engineering workflow + forms | J + L | I/K integrated + C-ENG-WORKFLOW-01 frozen |
| R2-C | M / UX copy + i18n | none | I/J/K/L/N integrated + C-USER-COPY-I18N-01 frozen |
| Validation | Shared CODEX / E3 | no DEV mutation of candidate | all target lanes delivered |
| Preview/Audit | CODEX Preview + Human Preview + AUD | independent evidence paths | exact integrated candidate |

## Coordinator / Product Owner operating convention

This route carries the following interaction rules across coordinator rotations:

- every Main status/update ends with a simple table of chat/lane, current live status and the exact Product Owner action;
- status/action must be revalidated from GitHub live, not memory;
- Product Owner is not a courier between agents; durable handoffs live in GitHub;
- a Product Owner `SIGA` to Main means execute the next safe already-authorized action after live revalidation;
- future chats I/K/J/L/N/M are not released yet and have no bootstrap text;
- the Product Owner will explicitly ask Main for each bootstrap when its release gate is reached;
- do not tell the Product Owner to open a future chat before its base/contracts are ready.

## 15. Current disposition

`PREPARED / WAIT_REV0117_TWEEN_DIAGNOSTIC / NO_POST_E3_PRODUCT_MUTATION_YET`.
