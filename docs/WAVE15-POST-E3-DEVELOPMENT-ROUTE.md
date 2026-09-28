# Wave 15 — Post-E3 Development Route

**Status:** PREPARED / WAIT_REV0112 / NO_POST_E3_PRODUCT_MUTATION_YET  
**Coordinator issue:** #378  
**Execution ledger:** #305  
**Current integration baseline at preparation time:** `wave15/corrections-integration@50b2750c73623b7ffef77f0ca93755c3e8278676`  
**Current shared CODEX order at preparation time:** `rev0112 / COMPLETE-SEQUENTIAL-CODEX-COMBINED-E3-LOCAL-DOCKER-MOUNTED-93`

GitHub live is authoritative. Every gate below must be revalidated against live branches, PR heads, CI and issue handoffs before execution.

## 1. Objective

Finish the current correction wave without mixing unrelated productization work into the active validation path.

The route is:

```text
rev0112 E3
  -> integrate #373/#374/#376 if accepted
  -> recompose/integrate #375
  -> freeze Round-2 UX/product contracts
  -> R2-A: Engineering density + Report theme
  -> R2-B: Editor UX + Client branding
  -> exact E2/E3
  -> mandatory SECOND Preview + independent Audit
  -> residual corrections/recheck
  -> CORRECTION PHASE ACCEPTED
  -> return to deferred original Wave 15
  -> container/host productization + Local/Remote DB topology later in canonical order
```

## 2. Gate 0 — rev0112 disposition

Do not start new product branches while rev0112 is active.

If CODEX returns:

`COMBINED_E3_PASS / LOCAL_DOCKER_MOUNTED`

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

Before parallel Round-2 implementation, Main/CODEX freezes four bounded contracts.

### C-ENG-DENSITY-01

- `TASK_FIRST / COMPACT_CONTEXT / SECONDARY_INFORMATION_ON_DEMAND`;
- one compact persistent Engineering context row;
- dirty/Lock/CAS/current-task state remains truthful;
- schema/revision/snapshot metadata becomes on-demand unless contextually relevant;
- explicit wide/full-width mode for graphical editors;
- ordinary form/list surfaces retain readable widths.

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

### C-VISUAL-ASSET-AUTHORING-02

- image object selects/imports canonical VisualAsset;
- Screen/Popup import parity;
- background color/image/fit is visible and discoverable;
- no new private image persistence path;
- asset IDs remain canonical authority.

### C-BRANDING-01

- modes: default EliteSCADA / text / image / none;
- branding is versioned application/project configuration;
- image references canonical project VisualAsset;
- Active Runtime uses Active branding;
- Working-only edits do not mutate Active Runtime;
- package/restart/recovery preserve branding;
- SVG support requires an explicit safe static-vector validation/sanitization boundary.

## 5. Development Wave R2-A

Base: exact post-#375 integration checkpoint.

Two branches/chats may run in parallel because ownership is intentionally isolated.

### Chat I — DEV-ENG-DENSITY

Own:
- consolidate the two Engineering context/header rows;
- compact persistent state;
- move secondary metadata to Info/Diagnostics;
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

### Chat K — DEV-REPORT-THEME

Own:
- Report Designer theme-token correction;
- Engineering semantic tokens;
- dark/light parity;
- disabled/hover/selected/focus contrast;
- white report paper remains semantically white;
- no report model/data redesign.

May run concurrently with Chat I.

## 6. Development Wave R2-B

Starts only after R2-A is integrated.

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

## 13. Parallelism matrix

| Phase | Chat | May run with | Must wait for |
|---|---|---|---|
| Current | Shared CODEX rev0112 | nothing new mutating same product | active now |
| Gate 1 | G / #375 recomposition | none required | E/F/H integration |
| R2-A | I / Engineering density | K | post-#375 base |
| R2-A | K / Report theme | I | post-#375 base |
| R2-B | J / Editor UX | L | R2-A integration |
| R2-B | L / Branding | J | R2-A integration |
| Validation | Shared CODEX / E3 | no DEV mutation of candidate | all target lanes delivered |
| Preview/Audit | CODEX Preview + Human Preview + AUD | independent evidence paths | exact integrated candidate |

## 14. Current disposition

`PREPARED / WAIT_REV0112 / NO_POST_E3_PRODUCT_MUTATION_YET`.
