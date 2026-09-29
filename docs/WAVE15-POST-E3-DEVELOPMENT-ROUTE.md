# Wave 15 — Post-E3 Development Route

**Status:** PREPARED / WAIT_REV0118_TWEEN_PYPROXY_FIX / GATE0_SCRIPT_CORRECTION_ACTIVE / R2_C0_CONTRACT_LAYER_DECLARED  
**Coordinator issue:** #378  
**Execution ledger:** #305  
**Current integration baseline at preparation time:** `wave15/corrections-integration@50b2750c73623b7ffef77f0ca93755c3e8278676`  
**Current shared CODEX order:** `rev0118 / FINAL-SEQUENTIAL-CODEX-SCRIPT-TWEEN-PYPROXY-LIFETIME-FIX-99`

GitHub live is authoritative. Every gate below must be revalidated against live branches, PR heads, CI and issue handoffs before execution.

## 1. Objective

Finish the current correction wave without mixing unrelated productization work into the active validation path.

The route is:

```text
rev0118 bounded TWEEN borrowed-PyProxy lifetime correction after rev0117 causal proof (#373 and #376 E3 evidence carried forward)
  -> integrate #373/#374/#376 if Gate 0 closes
  -> recompose/integrate #375
  -> R2-C0 SHARED CONTRACT LAYER (#386) / freeze all mandatory external-test semantics
  -> R2-A foundations: Engineering shell/theme + Historian capture + Data Query core + Portability core
  -> R2-B authoring: Editor UX + Branding + structured Engineering + Script authoring + Library/Fragment consumers
  -> R2-C historical consumers: Trend/Browser/Alarm View/Report query convergence + Playback
  -> R2-D user-facing terminology + multilingual consistency
  -> exact integrated E2/E3
  -> mandatory SECOND Preview + independent Audit
  -> residual corrections/recheck
  -> THIRD-PARTY TEST CANDIDATE
  -> CORRECTION PHASE ACCEPTED
  -> return to deferred original Wave 15
  -> container/host productization + Local/Remote DB topology later in canonical order
```

## 2. Gate 0 — residual E3 disposition

Do not start unrelated product branches while rev0118 is active. The only authorized Gate-0 product mutation is the bounded #374 Script bridge fix on its existing branch.

Current carried-forward state:
- #376 Gateway: `E2_ACCEPTED / E3_MOUNTED_ACCEPTED / WAIT_COHORT_GATE0_CLOSE`;
- #373 HMI: `E2_ACCEPTED / E3_MOUNTED_ACCEPTED`;
- #376 Gateway: `E2_ACCEPTED / E3_MOUNTED_ACCEPTED`;
- #374 Script: prior authoring evidence remains accepted; rev0117 confirmed the remaining TWEEN completion failure is a Client Visual Python bridge lifetime defect, now owned by rev0118;
- rev0115 was invalidated by a disposable fixture that omitted the product-generated `elite_scada` import;
- rev0116 proved Script Assistant-generated READ and WRITE complete; rev0117 proved TWEEN response delivery succeeds but the Python await cannot resume because `normalizeBridgeValue()` prematurely destroys the borrowed inbound dict PyProxy. rev0118 applies only that fix plus regression/E2/Script-only E3 residual recheck.

The pre-fix combined tree `3aaec957ce27c73bb8b7090b7cd9f412ba26b567` is historical evidence only now that rev0117 proved a product defect requiring a #374 byte change. It must **not** be used as the final post-fix tree target.

If rev0118 returns:
`SCRIPT_TWEEN_PYPROXY_FIX / E2_GREEN / E3_TWEEN_COMPLETED / <new #374 HEAD> / <new combined tree>`

Main must:
1. revalidate integration still equals the pinned base;
2. revalidate #373/#376 remained byte-identical to their accepted E3 heads;
3. review the exact #374 correction diff and exact-head T1;
4. treat the rev0118-reported combined tree as the new Gate-0 E3 product-tree authority;
5. integrate in preferred order:
   `#373 -> #374 -> #376`;
6. prove the resulting final integrated **product tree** is byte-identical to that new rev0118 combined E3 tree.

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

## 4. Gate 2 — R2-C0 mandatory shared contract layer

Before any new Round-2 implementation release, Main freezes the mandatory shared-contract layer tracked by #386 and `docs/WAVE15-R2-SHARED-CONTRACT-LAYER.md`.

C0 is now a **hard release gate** for the first third-party test candidate. #384 and #385 are mandatory product scope, not optional research.

No new R2 DEV bootstrap is generated until:
- Gate 0 closes;
- #375 is recomposed/integrated;
- exact post-#375 base is known;
- every contract consumed by the intended package is `FROZEN_FOR_CONSUMERS`;
- Main has checked file/authority overlap.

The contract families below are part of C0.

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

Implementation is intentionally deferred until structural R2 consumers are integrated to avoid copy work colliding with Editor/Query/Library/Playback rewrites.

### Additional mandatory C0 contracts

The detailed contracts live in `docs/WAVE15-CORRECTION-NOW-SHARED-CONTRACTS.md` and the C0 manifest.

Mandatory for the first external-test candidate:
- C-SCRIPT-EVENT-LINK-01;
- C-SCRIPT-AUTHORING-R2-01;
- C-HISTORIAN-CAPTURE-01;
- C-HISTORICAL-TIME-RANGE-01;
- C-DATA-QUERY-VIEW-01;
- C-HISTORICAL-PLAYBACK-01;
- C-ENGINEERING-PORTABILITY-01;
- C-ENG-WORKFLOW-01;
- C-BRANDING-01;
- C-USER-COPY-I18N-01.

C0 must also revalidate C-REUSE-01 on the exact post-#375 integrated base.

No consumer may locally redefine a missing semantic; it returns:
`BLOCKED_CONTRACT / <contract-id> / <missing semantic>`.

## 5. Development Wave R2-A — foundations

Base: exact post-#375 integration checkpoint after the required C0 contracts are frozen.

Existing planned chats I/K/O remain unreleased. In addition, C0 now requires two foundation implementation packages whose exact chat/branch assignment is intentionally deferred until post-#375 file-overlap review:
- DATA-QUERY-CORE / #384;
- ENGINEERING-PORTABILITY-CORE / #385.

Parallelism is allowed only when file + authority ownership is explicitly disjoint.

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

### Chat O — DEV-HISTORIAN-CAPTURE

Issue: #382.

Own:
- freeze/consume C-HISTORIAN-CAPTURE-01;
- implement Runtime enforcement of the effective Historian capture policy;
- introduce stable reusable capture-profile Engineering authority and migration/compatibility for existing inline TAG settings;
- periodic and on-change semantics;
- bounded deadband/max-period semantics only as frozen by the contract;
- quality-transition preservation;
- accepted/skipped/coalesced diagnostics;
- deterministic TimescaleDB + in-memory policy parity;
- bulk/profile API foundations consumed later by Chat N.

Must not:
- redesign Historical Query/Trend time-range semantics;
- replace TimescaleDB storage authority;
- duplicate raw streams merely to create different display resolutions;
- weaken quality semantics;
- reorganize generic TAG forms owned by Chat N;
- alter unrelated Driver scan rates.

Expected evidence:
- focused Historian policy tests;
- real TimescaleDB capture-volume regression;
- exact-SHA E2;
- proof that fast source updates are reduced to the configured raw-capture rate.

May run concurrently with Chat I/K after C-HISTORIAN-CAPTURE-01 is frozen and Main confirms no material file collision.

### Planned package — DATA-QUERY-CORE / #384

No chat/bootstrap is assigned yet.

Owns after C0 freeze:
- reusable typed Query definition;
- protected provider/query execution foundation;
- Historian retrieval modes: Raw/Last/Before/After/Exact/Interpolated/FixedStep/Aggregate as frozen;
- server-side bounds/aggregation;
- reusable typed Alarm Filter/View model;
- public contracts consumed later by Trend/Browser/Report/Playback.

Must not:
- own raw capture policy from Chat O;
- own Trend/Browser/Report layout;
- create SQL as normal user authority;
- create direct browser-to-database access.

### Planned package — ENGINEERING-PORTABILITY-CORE / #385

No chat/bootstrap is assigned yet.

Owns after #375 integration + C0 freeze:
- Engineering Fragment schema and selective export/import;
- dependency closure;
- Preview/remap/conflict plan;
- CSV/XLSX vs structured Fragment format authority;
- Library provenance/version/update/Compare/Upgrade/Fork backend semantics;
- component-bundle portability boundary.

Must not:
- replace .escadapkg;
- turn .escadalib into Runtime authority;
- own Screen/Popup Editor layout;
- silently auto-upgrade project behavior.

## 6. Development Wave R2-B — authoring consumers

Starts only after the required R2-A foundation packages are integrated **and**:
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
14. selected Screen/Popup/object side context with localized `Properties | Dynamics | Events`;
15. visual `Events` links triggers to already-created Scripts/handlers through C-SCRIPT-EVENT-LINK-01;
16. Screen/Popup lifecycle triggers expose friendly Open/Close semantics over canonical initialize/dispose;
17. object Click association remains stable-ID based and supports direct `Open Script` navigation without duplicating source;
18. preserve all existing library/import capability;
19. Screen/Popup parity.

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
- representative cleanup across TAG/Data Source/Alarm and other structured surfaces;
- Script metadata/event-entry-point forms outside the code editor, consuming C-SCRIPT-EVENT-LINK-01 without touching Screen/Popup visual-editor internals.

May run in parallel with J/L only under strict ownership:
- N does not modify Screen/Popup visual-editor internals;
- N does not own global branding/AppNavigation;
- J does not reorganize generic TAG/Data Source/Alarm forms;
- L does not redefine generic entity workflows.

Consumes:
- integrated C-ENG-DENSITY-01;
- integrated/frozen C-ENG-THEME-01;
- integrated C-HISTORIAN-CAPTURE-01 implementation from Chat O;
- frozen C-ENG-WORKFLOW-01;
- frozen C-SCRIPT-EVENT-LINK-01;
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

### Planned package — SCRIPT-AUTHORING-R2

No chat/bootstrap is assigned yet.

Consumes C-SCRIPT-AUTHORING-R2-01 + C-SCRIPT-EVENT-LINK-01 and the frozen visual identity/property contracts.

Owns:
- continuous real-engine syntax guard;
- distinct syntax/reference diagnostics;
- project context browser;
- guided Event -> Action -> Object -> Property/Method -> Parameters flow;
- safe generated snippets;
- high-level TAG/Client Memory/visual actions;
- declarative Binding/Dynamic recommendation where preferable;
- mounted fault-isolation proof.

It must not reopen the current Gate-0 PyProxy bridge correction unless new evidence proves a separate runtime defect.

### Portability/Library UI consumers

After ENGINEERING-PORTABILITY-CORE freezes its APIs, Main assigns the UI ownership across J/N or a new bounded lane based on live file overlap.

Required first external-test behavior:
- Export selected / Import;
- dependency/conflict Preview;
- Library real visual preview;
- Add to Library;
- update status + Compare/Upgrade/Keep/Fork;
- Template -> Equipment -> Dynamo -> Screen/Popup discoverable workflow.

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

## 8.4. Development Wave R2-C — historical consumers

Starts only after R2-B structural authoring packages and DATA-QUERY-CORE are integrated, with C-HISTORICAL-TIME-RANGE-01 + C-DATA-QUERY-VIEW-01 frozen.

### Chat P — DEV-HISTORICAL-TIME-RANGE

Issue: #383.

Own:
- converge Basic Trend compatibility surface, canonical multipen Trend and Historical Data Browser on the shared time-range semantics;
- Historical absolute `From -> To` controls;
- relative quick ranges + custom amount/unit;
- localized date/time display with UTC query authority;
- preserve active absolute range on refresh;
- bounded large-range query behavior;
- Runtime/session date selections that do not dirty Engineering.

Must not:
- create a second Trend-only database API;
- modify Historian raw-capture semantics owned by Chat O;
- create unbounded browser data loads;
- redefine Screen/Popup editor layout owned by Chat J;
- invent a private external DateTime binding contract without Main freeze.

Expected evidence:
- exact-SHA E2;
- mounted canonical multipen Trend absolute-range proof;
- equivalent Historical Browser query proof;
- representative `27/09/2026 01:00:00 -> 28/09/2026 12:00:00` locale-to-UTC boundary proof;
- refresh/requery preserves the absolute range.

### Additional historical-consumer packages — unassigned until C0/file review

Mandatory before the first external-test candidate:

**Browser / Alarm View / Report Query convergence**
- Historical Data Browser consumes reusable typed Query definitions;
- configurable columns/filter/sort and bounded export where authorized;
- reusable Alarm Filter/View semantics;
- Report can reference/reuse Query definitions and Runtime parameters;
- no duplicate private query languages.

**Trend convergence**
- explicit Automatic/Historical/Live Pen semantics;
- shared period;
- analog interpolation vs digital step behavior;
- historical/current join, gaps, duplicates and quality precedence;
- bounded visible-range retrieval;
- alarm/event markers where supported by the frozen contract.

**Historical Playback**
- consume C-HISTORICAL-PLAYBACK-01;
- read-only historical Screen/Popup/Dynamo projection;
- clear historical-mode indication;
- command/write/ACK mutation unavailable;
- shared Playback timestamp/context;
- explicit return to current Runtime.

Exact chat/branch slicing is deliberately deferred until the C0 dependency and file-overlap matrix is frozen.

## 8.5. Development Wave R2-D — UX copy / i18n cleanup

Issue: #379.

Starts only after all structural R2-A/R2-B/R2-C consumers, including Query/Portability/Playback surfaces, are integrated.

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

## 8.6. Mandatory cross-domain scope from E3 comparative audit

The Product Owner comparative audit on 2026-09-29 created:

- #384 / C-DATA-QUERY-VIEW-01 — shared typed Query authority across Historian retrieval, Alarm View, Browser, Trend and Report;
- #385 / C-ENGINEERING-PORTABILITY-01 — Engineering Fragment/selective import-export plus reusable Library/Dynamo version/update lifecycle.

These are **mandatory first external-test product scope**. They remain unreleased implementation packages until C0 freeze and exact post-#375 ownership review.

Main must resolve their overlap after Gate 0/1 and before releasing implementation:
- #384 overlaps O (#382 Historian capture), P (#383 time range), Historical Browser and Reporting;
- #385 overlaps G/#375 reuse, #365 Template/Equipment/Dynamo workflow, #308 Library preview, J visual Editor and N structured Engineering workflows.

Do not create Chat Q/R/S or any other bootstrap merely because these issues exist.

Required scheduling decision after the exact post-#375 base exists:
1. freeze #384/C-DATA-QUERY-VIEW-01 and assign DATA-QUERY-CORE ownership;
2. freeze #385/C-ENGINEERING-PORTABILITY-01 and assign PORTABILITY-CORE plus UI consumer ownership;
3. freeze C-SCRIPT-AUTHORING-R2-01 and assign a bounded Script-authoring package;
4. freeze C-HISTORICAL-PLAYBACK-01 and assign Playback only after Query/time-range consumers exist;
5. preserve SECOND Preview/Audit as the final acceptance authority.

## 8. Gate 4 — integrated correction E3

After all mandatory R2 foundation, authoring and historical-consumer packages are integrated, validate one exact integrated candidate with:
- real DB/API/Web/browser;
- normal Engineering navigation;
- Screen/Popup authoring;
- side surface/Structure/library;
- contextual object/group actions;
- image/background workflows;
- Report Designer dark/light;
- reusable Query -> Browser/Trend/Report flow;
- Historian capture profile enforcement;
- absolute/relative historical period;
- Alarm View/filter behavior;
- Engineering Fragment import/export Preview;
- Library visual preview + version/update workflow;
- Script syntax guard + guided authoring;
- historical Playback read-only behavior;
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

## 11. Gate 7 — THIRD-PARTY TEST CANDIDATE / CORRECTION PHASE ACCEPTED

Required:
- #373/#374/#376 integrated;
- #375 integrated;
- C0 contract layer complete;
- all mandatory R2-A/R2-B/R2-C/R2-D packages integrated;
- applicable E2/E3 green;
- CODEX second Preview complete;
- Product Owner second Preview complete;
- independent Audit complete;
- material residual findings closed and rechecked.

Then Main records:

`CORRECTION PHASE ACCEPTED / THIRD-PARTY TEST CANDIDATE`

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
| O — HISTORIAN-CAPTURE | frozen C-HISTORIAN-CAPTURE-01, C-AUTHORITY-01, C-TEST-EVIDENCE-01, existing Historian/Timescale storage authority | Historical Query time ranges, Driver scan rates, storage replacement, visual-editor internals |
| DATA-QUERY-CORE — unassigned | frozen C-DATA-QUERY-VIEW-01 + C-HISTORICAL-TIME-RANGE-01, Historical Query v1, C-AUTHORITY-01, C-TEST-EVIDENCE-01 | raw capture policy, consumer layout, free SQL/direct DB |
| PORTABILITY-CORE — unassigned | integrated/frozen C-REUSE-01, frozen C-ENGINEERING-PORTABILITY-01, C-AUTHORITY-01, C-TEST-EVIDENCE-01 | .escadapkg replacement, Runtime library dependency, Editor layout |
| J — EDITOR-UX-R2 | integrated C-ENG-DENSITY-01, C-VISUAL-IDENTITY-01, C-VISUAL-DYNAMIC-01, C-TAG-WRITE-01, integrated/frozen C-REUSE-01, frozen C-VISUAL-ASSET-02, frozen C-SCRIPT-EVENT-LINK-01, C-SURFACE-01, C-TEST-EVIDENCE-01 | renderer, identity, TAG write, reuse relationship, asset store/API, Script execution authority |
| L — BRANDING | integrated C-ENG-DENSITY-01, C-AUTHORITY-01, frozen C-VISUAL-ASSET-02, frozen C-BRANDING-01, C-SURFACE-01, C-TEST-EVIDENCE-01 | lifecycle authority, asset store/API, independent per-page branding state |
| N — ENG-WORKFLOW-FORMS | integrated C-ENG-DENSITY-01, integrated/frozen C-ENG-THEME-01, integrated C-HISTORIAN-CAPTURE-01 implementation, frozen C-ENG-WORKFLOW-01, frozen C-SCRIPT-EVENT-LINK-01, C-AUTHORITY-01, C-SURFACE-01, C-TEST-EVIDENCE-01 | visual-editor internals, branding shell, backend mutation semantics/API identity, Script execution authority |
| P — HISTORICAL-TIME-RANGE | frozen C-HISTORICAL-TIME-RANGE-01 + C-DATA-QUERY-VIEW-01, integrated DATA-QUERY-CORE + R2-B, C-SURFACE-01, C-TEST-EVIDENCE-01 | Historian capture policy, database authority, visual-editor layout, new private time API |
| SCRIPT-AUTHORING-R2 — unassigned | frozen C-SCRIPT-AUTHORING-R2-01 + C-SCRIPT-EVENT-LINK-01 + visual identity, C-TEST-EVIDENCE-01 | Gate0 bridge semantics, Driver bypass, DOM/private renderer API |
| HISTORICAL-PLAYBACK — unassigned | frozen C-HISTORICAL-PLAYBACK-01 + integrated Query/time-range foundations, C-AUTHORITY-01, C-TEST-EVIDENCE-01 | process writes/commands, alternate Active authority |
| M — UX-COPY-I18N | integrated R2-A + J/L/N + P, frozen C-USER-COPY-I18N-01, C-SURFACE-01, C-TEST-EVIDENCE-01 | lifecycle/Authority semantics, backend enum/API identity, accepted layout architecture |

Any consumer that finds an insufficient contract returns:
`BLOCKED_CONTRACT / <contract-id> / <missing semantic>`.

## 14. Parallelism matrix

| Phase | Chat | May run with | Must wait for |
|---|---|---|---|
| Current | Shared CODEX rev0118 TWEEN PyProxy fix | only bounded #374 worker + regression test mutation | active now |
| Gate 1 | G / #375 recomposition | none required | E/F/H integration |
| C0 Contract | Main / #386 | no product DEV required | post-#375 exact base |
| R2-A | I / Engineering density | K | frozen C-ENG-DENSITY-01 + C-PRODUCT-VERSION-01 + post-#375 base |
| R2-A | K / Theme + contrast | I + O | frozen C-ENG-THEME-01 + post-#375 base |
| R2-A | O / Historian capture | I + K + DATA-QUERY-CORE + PORTABILITY-CORE when ownership is clean | frozen C-HISTORIAN-CAPTURE-01 + post-#375 base + no material file overlap |
| R2-A | DATA-QUERY-CORE / unassigned | I + K + O + PORTABILITY-CORE when clean | C-DATA-QUERY-VIEW-01 frozen + post-#375 base |
| R2-A | PORTABILITY-CORE / unassigned | I + K + O + DATA-QUERY-CORE when clean | C-ENGINEERING-PORTABILITY-01 + C-REUSE-01 frozen + post-#375 base |
| Contract | Main C-VISUAL-ASSET-02 + C-BRANDING-01 + authoring consumer revalidation | none | required R2-A foundation integration |
| R2-B | J / Editor UX | L | I/K integrated + C-REUSE-01 integrated/frozen + C-VISUAL-ASSET-02 + C-SCRIPT-EVENT-LINK-01 frozen |
| R2-B | L / Branding | J | I/K integrated + C-VISUAL-ASSET-02 + C-BRANDING-01 frozen |
| R2-B | N / Engineering workflow + forms | J + L + SCRIPT-AUTHORING-R2 when clean | foundation packages integrated + C-ENG-WORKFLOW-01 + C-SCRIPT-EVENT-LINK-01 + Portability APIs frozen |
| R2-B | SCRIPT-AUTHORING-R2 / unassigned | J + L + N when clean | C-SCRIPT-AUTHORING-R2-01 frozen + visual identity/event contracts frozen |
| R2-C | P / Trend + historical time range | Browser/Report/Alarm consumers when clean | authoring + DATA-QUERY-CORE integrated + C-HISTORICAL-TIME-RANGE-01 frozen |
| R2-C | Historical Query consumers / unassigned | P | C-DATA-QUERY-VIEW-01 frozen + Query core integrated |
| R2-C | Historical Playback / unassigned | none or isolated consumer lane | Query/time-range/visual authority integrated + C-HISTORICAL-PLAYBACK-01 frozen |
| R2-D | M / UX copy + i18n | none | all structural R2 consumers integrated + C-USER-COPY-I18N-01 frozen |
| Validation | Shared CODEX / E3 | no DEV mutation of candidate | all target lanes delivered |
| Preview/Audit | CODEX Preview + Human Preview + AUD | independent evidence paths | exact integrated candidate |

## Coordinator / Product Owner operating convention

This route carries the following interaction rules across coordinator rotations:

- every Main status/update ends with a simple table of chat/lane, current live status and the exact Product Owner action;
- status/action must be revalidated from GitHub live, not memory;
- Product Owner is not a courier between agents; durable handoffs live in GitHub;
- a Product Owner `SIGA` to Main means execute the next safe already-authorized action after live revalidation;
- future chats I/K/J/L/N/M/O/P are not released yet and have no bootstrap text;
- DATA-QUERY-CORE, PORTABILITY-CORE, SCRIPT-AUTHORING-R2, Historical Query consumer and Playback packages are planned but intentionally have no assigned chat/bootstrap yet;
- the Product Owner will explicitly ask Main for each bootstrap when its release gate is reached;
- do not tell the Product Owner to open a future chat before its base/contracts are ready.

## 15. Current disposition

`PREPARED / WAIT_REV0118_TWEEN_PYPROXY_FIX / GATE0_SCRIPT_CORRECTION_ACTIVE / C0_DECLARED_NO_NEW_DEV_RELEASE`.
