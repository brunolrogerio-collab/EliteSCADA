# Wave 15 R2 — Shared Contract Layer (C0)

**State:** C0_FROZEN / F0_INTEGRATED / F0_D1_INTEGRATED_VERIFIED / TAG_C_ACTIVE / R2_A_PARALLEL_RELEASED / NO_MERGE  
**Coordinator issue:** #386  
**Parent correction route:** #378  
**Control branch:** `coord/w15-correction-now-parallel-control`  
**Product integration authority:** `wave15/corrections-integration`

GitHub live is the only authority.


## LIVE R2-A PARALLEL RELEASE — 2026-09-29

This live release supersedes older `OWNER_UNASSIGNED / NO_BOOTSTRAP` wording below for the lanes listed here.

Product Owner authorization:
- explicit request to prepare every R2-A package that can safely work in parallel and generate the bootstraps one-by-one;
- Main revalidated the globally-green F0-D1 integration before release.

Exact shared creation base for all five lanes:
- integration: `wave15/corrections-integration@3140ad20b759924a15e3e29d74726b6912bf3da6`;
- tree: `97b0dba782cabb0a8becccfa736db65b4653826e`;
- evidence: F0-D1 PR #399 integrated + EliteSCADA CI #1601 / `36620256066` globally GREEN.

Released parallel lanes:

1. **I — DEV-ENG-DENSITY**
   - branch: `work/w15-r2-eng-density`;
   - owner: Engineering shell/context/info/layout only;
   - primary authority: `web/scada-web/src/engineering/EngineeringApp.tsx`, `engineering.css`, shell/info/context composition and focused shell tests;
   - forbidden: visual-editor internals, TAG commissioning, theme-engine internals, domain DTO/backend semantics, AppNavigation branding.

2. **K — DEV-THEME-CONTRAST**
   - branch: `work/w15-r2-theme-contrast`;
   - owner: semantic theme tokens + Report/Script/Python/structured-form visual styling;
   - primary authority: `web/scada-web/src/app-theme.css`, `appTheme.ts`, Report Designer theme files, Python Monaco theme files, Script/structured-form CSS and focused theme tests;
   - forbidden: Engineering layout architecture, domain models, Script runtime/lifecycle, TAG commissioning behavior.

3. **O — DEV-HISTORIAN-CAPTURE / #382**
   - branch: `work/w15-r2-historian-capture`;
   - owner: Historian capture profiles/policy admission/writer diagnostics;
   - primary authority: `src/Scada.Historian/Policies/**`, `src/Scada.Historian/Memory/BufferedInMemoryHistorian.cs`, `src/Scada.Historian.TimescaleDb/TimescaleDbHistorian.cs`, narrowly scoped capture-profile services/diagnostics and focused tests;
   - forbidden: `src/Scada.Core/HistoricalQueries/**`, `TimescaleHistoricalQueryProvider.cs`, Historical Query API, Trend/Browser/Report UI, Driver scan policy.

4. **DATA-QUERY-CORE / #384**
   - branch: `work/w15-r2-data-query-core`;
   - owner: typed query/retrieval providers, bounded aggregation and Alarm View model;
   - primary authority: `src/Scada.Core/HistoricalQueries/**`, `src/Scada.Historian.TimescaleDb/TimescaleHistoricalQueryProvider.cs`, `src/Scada.Api/Historian/HistoricalQueryApi.cs`, `HistoricalQueryConfiguration.cs`, new bounded Query-definition services and focused tests;
   - forbidden: Historian capture admission/writer queues, `src/Scada.Engineering/ImportExport/**`, Libraries mutation, Trend/Browser/Report presentation.

5. **ENGINEERING-PORTABILITY-CORE / #385**
   - branch: `work/w15-r2-engineering-portability-core`;
   - owner: Engineering Fragment plan/apply/dependency/remap and Library provenance/update backend;
   - primary authority: `src/Scada.Engineering/ImportExport/**`, `src/Scada.Engineering/Libraries/**`, new Fragment/portability services and compatibility tests;
   - forbidden: Historian capture/query execution, Screen/Popup layout, full `.escadapkg` authority replacement, Runtime dependency on external `.escadalib`.

Shared-hotspot lock for all five:
- do not edit `EngineeringContracts.cs`, schema v20 definitions, F0/F0-D1 shared public wire files or canonical TypeScript wire mirrors without Main approval;
- if a frozen semantic is insufficient, return `BLOCKED_CONTRACT / <contract-id> / <missing semantic>`;
- each lane stays on its owned subsystem and returns a handoff to Main; no lane merges itself.

Parallel compatibility:
`TAG-C + I + K + O + DATA-QUERY-CORE + PORTABILITY-CORE` may run concurrently under these locks.

Still NOT released:
`J / L / N / M / P / TAG-D / Playback` and later structural consumers.

## 1. Purpose

C0 is the semantic layer between the current Gate 0/Gate 1 integration work and all remaining R2 product implementation.

The first EliteSCADA build distributed to third-party testers must not be assembled from parallel lanes that each invent their own:
- identity;
- lifecycle;
- event;
- Historian/query;
- import/export;
- Library;
- Script;
- visual;
- terminology semantics.

The required path is:

`Gate0 -> Gate1/#375 -> C0 freeze -> F0 shared wire foundation -> implementation packages -> integration -> E2/E3 -> SECOND Preview/Audit -> external-test candidate`.

C0 does not authorize production mutation by itself.

### Current exact freeze base

- C0 freeze base: `wave15/corrections-integration@bb946f9e7d6910d59a9ac172d71361e5badab4b1` / tree `75398319b8b4630b72a525fb9bdd235dc0d541a9`;
- current post-F0/F0-D1 integration: `wave15/corrections-integration@3140ad20b759924a15e3e29d74726b6912bf3da6` / tree `97b0dba782cabb0a8becccfa736db65b4653826e`;
- Gate0 #373/#374/#376: integrated and exact-tree proven;
- Gate1 #375: recomposed by Main, exact-head T1 `36589349320` SUCCESS, integrated;
- C-REUSE-01: integrated/frozen;
- mandatory C0 contracts: `FROZEN_FOR_CONSUMERS`.

No future DEV bootstrap has been generated by this freeze.

## 2. Freeze rule

A consumer starts only when every shared contract it depends on is either:
- `FROZEN_FOR_CONSUMERS`; or
- explicitly integrated/frozen from an earlier gate.

If implementation discovers an omitted shared semantic:

`BLOCKED_CONTRACT / <contract-id> / <missing semantic>`

No local architecture fork is allowed.

## 3. Contract dependency groups

### C0-A — Product authority and evidence

- C-SURFACE-01
- C-AUTHORITY-01
- C-TRANSPORT-01 where consumed
- C-TEST-EVIDENCE-01
- C-PRODUCT-VERSION-01

Freeze outcome:
- one lifecycle/Runtime authority;
- one evidence vocabulary;
- one visible product-version authority.

### C0-B — Engineering shell and visual foundations

- C-ENG-DENSITY-01
- C-ENG-THEME-01
- C-VISUAL-IDENTITY-01
- C-VISUAL-DYNAMIC-01
- C-REUSE-01
- C-VISUAL-ASSET-02
- C-EDITOR-UX-R2-01

Critical dependency:
C-REUSE-01 is revalidated/frozen only after #375 is recomposed/integrated on the post-Gate0 base.

### C0-C — Script authoring and events

- C-SCRIPT-EVENT-LINK-01
- C-SCRIPT-AUTHORING-R2-01
- C-TAG-WRITE-01
- C-AUTHORITY-01

Must freeze:
- visual context -> existing Script/handler linking;
- continuous syntax guard;
- reference diagnostics;
- guided action/object/property discovery;
- fault isolation;
- public high-level action boundaries.

### C0-D — Historian capture and query

- C-HISTORIAN-CAPTURE-01
- C-HISTORICAL-TIME-RANGE-01
- C-DATA-QUERY-VIEW-01

Permanent separation:
`SOURCE ACQUISITION -> RAW CAPTURE -> RETENTION/DOWNSAMPLING -> QUERY/RETRIEVAL -> PRESENTATION`.

Must freeze:
- reusable capture profiles;
- periodic/on-change/deadband/quality semantics;
- relative/absolute time ranges;
- typed reusable Query definitions;
- Raw/Last/Before/After/Exact/Interpolated/FixedStep/Aggregate retrieval;
- bounded query/result behavior;
- shared Alarm filter/view vocabulary;
- historical/current Trend splice rules.

### C0-E — Historical Playback

- C-HISTORICAL-PLAYBACK-01
- consumes C-DATA-QUERY-VIEW-01 + C-HISTORICAL-TIME-RANGE-01 + C-AUTHORITY-01 + visual identity.

Must freeze:
- read-only past-state projection;
- command/write disablement;
- digital step vs analog historical semantics;
- quality/gap truth;
- explicit enter/exit behavior;
- Trend/Browser historical context propagation.

### C0-F — Engineering portability and reusable libraries

- C-ENGINEERING-PORTABILITY-01
- consumes C-REUSE-01 + visual/script/report/query identities + C-AUTHORITY-01.

Permanent distinction:
- `.escadapkg` = whole application/project;
- Engineering Fragment = selected one-time transfer;
- `.escadalib` = curated reusable definitions/version lifecycle.

Must freeze:
- Fragment schema/identity/dependency closure;
- Preview/remap/conflict operations;
- secret/reference policy;
- CSV/XLSX vs structured Fragment responsibilities;
- Library provenance/update states;
- Compare/Upgrade/Keep/Fork semantics;
- safe preservation of instance overrides;
- visual preview/usage expectations;
- component-bundle boundary.

### C0-G — Structured workflow, branding and language

- C-ENG-WORKFLOW-01
- C-BRANDING-01
- C-USER-COPY-I18N-01

Must freeze before final UI cleanup:
- task-first entity workflows;
- bulk/edit/delete interaction boundary;
- Branding asset authority;
- pt-BR/en/es glossary;
- no ordinary user-facing Monaco/Vite/React/Pyodide/Web Worker/Canvas implementation branding.

### C0-H — TAG commissioning and productivity

Post-F0 mandatory delta:
- C-TAG-COMMISSIONING-01 / #390;
- C-TAG-DUPLICATION-01 / #391.

TAG commissioning freezes:
- Data Source ConnectionTest vs draft TAG PointReadTest vs Active Development Monitor separation;
- read-only transient point test;
- raw / decoded / Engineering value layers;
- quality/timestamp/latency/issue evidence;
- effective physical transform reporting;
- no process/Active/Historian mutation.

Because F0 is already integrated, #390 has one bounded wire prerequisite:

**F0-D1 — Driver Point Read Test Wire**
- `DriverEngineeringCapabilities.PointReadTest`;
- point-read request/result/status DTOs;
- optional provider interface;
- module-registration provider slot and fail-closed capability validation;
- protected Engineering API request/result surface;
- Web type/API mirror needed by TAG editor;
- no Driver behavior and no UI in D1;
- no Engineering schema increment unless persisted Engineering payload changes.

TAG duplication freezes:
- new stable IDs on duplicates;
- configuration-only copy;
- single + multi copy/paste/duplicate;
- Modbus-aware sequential address generator;
- dependency-safe Preview/Apply;
- cross-project convergence on `.escadafrag`.

## 4. Mandatory external-test capabilities and contract owners

| Capability | Contract owner |
|---|---|
| Script syntax safety + guided authoring | C-SCRIPT-AUTHORING-R2-01 |
| Screen/Popup/object event -> existing Script | C-SCRIPT-EVENT-LINK-01 |
| Historian capture profiles/runtime enforcement | C-HISTORIAN-CAPTURE-01 |
| Relative/absolute historical period | C-HISTORICAL-TIME-RANGE-01 |
| Reusable typed Query | C-DATA-QUERY-VIEW-01 |
| Rich historical retrieval/aggregation | C-DATA-QUERY-VIEW-01 |
| Shared Alarm filter/view semantics | C-DATA-QUERY-VIEW-01 |
| Historical/live Trend semantics | C-DATA-QUERY-VIEW-01 + C-HISTORICAL-TIME-RANGE-01 |
| Browser/Report shared query behavior | C-DATA-QUERY-VIEW-01 |
| Historical application Playback | C-HISTORICAL-PLAYBACK-01 |
| Selected object/entity transfer | C-ENGINEERING-PORTABILITY-01 |
| CSV/XLSX bulk exchange role | C-ENGINEERING-PORTABILITY-01 |
| Library preview/version/update/fork | C-ENGINEERING-PORTABILITY-01 |
| Template -> Equipment -> Dynamo reuse | C-REUSE-01 |
| Editor Library/Dynamo consumption | C-EDITOR-UX-R2-01 + C-REUSE-01 |
| Product-facing terminology/i18n | C-USER-COPY-I18N-01 |
| Draft TAG read-test/raw decode/quality | C-TAG-COMMISSIONING-01 |
| TAG copy/paste/duplicate/sequential generation | C-TAG-DUPLICATION-01 |

No item in this table is optional for the first external-test candidate unless the Product Owner explicitly changes scope.

## 4.5. F0 — Shared Wire Foundation

F0 translated the frozen C0 semantics into shared additive wire/schema types before behavior packages parallelize.

**State:** INTEGRATED / ISSUE_#387 / PR_#388 / T1_GREEN / EXACT_TREE_PROVEN.

Owns only shared public/model foundations such as:
- Historian Capture Profile identity/configuration DTOs and strategy enums;
- stable TAG -> capture-profile reference;
- reusable Data Query definition/parameter/retrieval-mode DTOs;
- Alarm Filter/View definition DTOs if persisted/shared;
- Engineering Fragment envelope/manifest/conflict-operation DTOs;
- Library source provenance/version/update-state DTOs;
- any required Engineering schema-version/migration additions;
- matching Web Engineering type mirrors where the public schema requires them.

May also add deterministic serialization/roundtrip tests for those additive wire contracts.

F0 must **not** implement:
- Historian runtime capture behavior;
- query execution;
- database migrations/storage engines beyond schema representation strictly required by the frozen public contract;
- import/export execution;
- Library upgrade behavior;
- Script UX;
- Editor UX;
- Trend/Browser/Report UI;
- Playback behavior.

Hotspot ownership while F0 is active:
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
- shared contract/enum files under `src/Scada.Engineering/Contracts`;
- canonical Engineering schema/version migration definitions needed by the new DTOs;
- `web/scada-web/src/engineering/types.ts` or deliberately extracted shared type modules;
- only the minimal roundtrip tests needed for wire compatibility.

F0 is integrated. Those types are now downstream authority; lanes may extend only through Main-approved contract deltas.

The following may now run concurrently when Main releases exact ownership:
- Historian Capture;
- Data Query Core;
- Engineering Portability Core

may run concurrently if their remaining file/authority ownership is disjoint.

## 5. Implementation packages after C0

These are **packages, not released chats**. Gate1 and C0 freeze are complete; exact chat/branch ownership is still assigned only when the Product Owner requests release and Main records the exact base/path contract.

### Package F0-D1 — Driver Point Read Test Wire

Issue: #390 prerequisite.

**INTEGRATED / VERIFIED — 2026-09-29**

- owner/executor: Main Coordinator;
- release base: `e965e9f7381d332e17c792ba20f93214e7d66f78`;
- release evidence: EliteSCADA CI `#1600 / 36616608742` — globally GREEN;
- implementation: PR `#399`, exact head `87c676c6e6833603cf42206f3c6388c02f703765`;
- integration merge: `3140ad20b759924a15e3e29d74726b6912bf3da6`;
- post-merge evidence: EliteSCADA CI `#1601 / 36620256066` — globally GREEN (Web, Backend build/tests, Runtime smoke, Chromium E2E);
- work branch: `work/w15-r2-f0-d1-point-read-wire`;
- disposition: `F0_D1_COMPLETE / TAG_C_PREREQUISITE_SATISFIED`.

Scope remains frozen:

- add only the transient Driver Engineering PointReadTest wire/capability/provider/API/Web mirror;
- do not implement protocol reads or TAG editor UX;
- do not mutate Active Runtime/History/process state;
- preserve F0 integrated schema v20 because no persisted Engineering contract changes in F0-D1;
- capability/provider registration must be truthful and opt-in; protocol implementations come later in TAG-C.

Integrate before TAG Commissioning behavior.

### Package TAG-C — TAG commissioning

Issue #390.

**RELEASED / ACTIVE — 2026-09-29**

- Product Owner authorization: explicit Main `SIGA` after F0-D1 completion and green post-merge CI;
- owner: `DEV-TAG-COMMISSIONING`;
- exact base: `3140ad20b759924a15e3e29d74726b6912bf3da6`;
- exact tree: `97b0dba782cabb0a8becccfa736db65b4653826e`;
- release evidence: F0-D1 PR #399 integrated + EliteSCADA CI `#1601 / 36620256066` globally GREEN;
- branch: `work/w15-r2-tag-c-commissioning`.

Owned paths / mutation authority:
- `src/Scada.Drivers/Modbus/**` only for PointReadTest provider/read/decode support;
- `src/Scada.Drivers/SiemensS7Iso/**` only for PointReadTest provider/read/decode support;
- `src/Scada.Drivers/OpcUa/**` only for PointReadTest provider/read/decode support;
- `src/Scada.Api/Engineering/EngineeringDriverTooling.cs` and minimal `EngineeringDriverCatalogApi.cs` factory/provider registration needed by TAG-C;
- TAG-commissioning UI only under `web/scada-web/src/engineering/**`, primarily `StructuredEditors.tsx`, `TagSourceSelector.tsx`, existing Driver Engineering API mirror consumption, and a dedicated bounded commissioning component if extracted;
- owning driver/unit/E2E tests.

Read-only frozen dependencies unless Main explicitly approves a contract delta:
- `src/Scada.Drivers/Abstractions/DriverEngineeringContracts.cs`;
- `src/Scada.Drivers/Abstractions/CommunicationDriverModuleRegistry.cs`;
- F0/F0-D1 shared contracts and Web wire types;
- `src/Scada.Engineering/Contracts/**` / schema v20;
- Runtime/process-write/Historian/query/portability kernels.

Mission:
- protocol-specific protected PointReadTest providers;
- first targets Modbus TCP, S7 ISO, OPC UA;
- TAG editor `Testar leitura / Test read / Probar lectura`;
- optional bounded short monitor using only the transient PointReadTest wire;
- raw/decoded/engineering value diagnostics;
- quality, observed/source timestamps, latency, issues and sanitized endpoint;
- effective byte/word transform visibility with no duplicate swap mechanism;
- Development Monitor handoff only after Apply/Activate.

Hard boundaries:
- no process writes;
- no Active Runtime mutation;
- no Historian writes;
- no automatic TAG creation/Apply;
- no secret disclosure;
- no persisted Engineering/schema increment;
- no second Driver runtime;
- no changes to Data Query/Portability/Historian Capture foundations.

Return:
`DEV-TAG-COMMISSIONING -> MAIN COORDINATOR — #390 TAG-C HANDOFF`
with exact branch/head/tree, changed files, protocol coverage matrix, focused tests, T1 evidence and any `BLOCKED_CONTRACT` item.

### Package TAG-D — TAG duplication/productivity

Issue #391.

- structured TAG list selection;
- copy/paste/duplicate;
- generated draft Preview;
- sequential Modbus generator;
- consume Portability/Fragment contracts for cross-project direction.

Prefer ownership with structured Engineering (Chat N) after Portability foundation is stable rather than inventing another TAG form implementation.

### Package A — Engineering shell foundation
Likely consumers: existing I + K.

- density;
- version/info hierarchy;
- theme/contrast.

### Package B — Historian capture foundation
Likely consumer: existing O.

- capture profiles;
- runtime enforcement;
- policy diagnostics;
- API foundation for bulk assignment.

### Package C — Data Query core
Contracts frozen; implementation owner still unassigned.

- reusable Query definition;
- Historian retrieval modes;
- aggregation/bounds;
- Alarm filter/view typed model;
- backend/provider foundation.

Must not own Trend/Browser/Report layout.

### Package D — Engineering Portability core
Contracts frozen; implementation owner still unassigned.

- Engineering Fragment;
- dependency closure;
- conflict/remap Preview;
- Library provenance/update/compare/upgrade/fork backend semantics;
- exchange profiles/format authority.

Must consume post-#375 C-REUSE-01.

### Package E — Editor / structured authoring
Likely consumers: J + N, with exact ownership split after Package D APIs freeze.

- Screen/Popup Editor UX;
- Properties/Dynamics/Events;
- Library/Dynamo visual consumption;
- entity workflows;
- Fragment/Library actions in normal Engineering surfaces.

### Package F — Script authoring R2
Contracts frozen; implementation owner still unassigned.

- continuous syntax guard;
- context browser;
- guided actions;
- generated snippets;
- Script/event navigation.

Must not reopen the current Gate0 Script bridge fix.

### Package G — Branding
Likely consumer: L.

### Package H — Historical consumers
Includes existing P plus additional ownership assigned after C0.

- absolute/relative Trend period;
- Automatic/historical/live Pen behavior;
- Historical Data Browser convergence;
- Alarm View consumers;
- Report Query reuse;
- bounded export/view behavior.

### Package I — Historical Playback
Contract frozen; implementation remains intentionally downstream of Query/time-range foundations.

- read-only historical Screen/Popup/Dynamo projection;
- playback controls/context;
- command/write suppression.

### Package J — UX copy/i18n
Likely consumer: M.

Runs after structural consumers stabilize.

### Frozen ownership matrix before bootstrap release

| Package | Primary authority after F0 | Must not own |
|---|---|---|
| F0-D1 / TAG Point Read wire | Driver Engineering transient capability/request/result contracts | protocol behavior, UI, Engineering schema changes without blocker |
| TAG-C / Commissioning | Driver point-read providers + TAG read-test UX | Active Runtime mutation, process writes, Historian capture |
| TAG-D / Duplication | TAG structured workflow + Fragment-compatible copy semantics | Driver point-read, Runtime state/history copy |
| A / I Engineering density + product info | Engineering shell/layout/info hierarchy | theme engine internals, visual editor internals, domain backend semantics |
| A / K Theme + contrast | semantic theme tokens + specialized editor styling | data models, Runtime behavior, layout architecture |
| B / O Historian Capture | capture-profile service/policy, writer admission, diagnostics | Driver scan, query retrieval, Trend UI |
| C Data Query Core | typed query/retrieval providers, aggregation/bounds, Alarm View model | raw capture admission, presentation/layout |
| D Portability Core | Fragment plan/apply, dependency/remap, Library provenance/update backend | full project package replacement, Editor layout |
| E / J Editor UX | Screen/Popup visual authoring, Library/Dynamo visual consumption, Events surface | backend query/capture semantics, portability kernel |
| E / N Structured Engineering | task-first entity forms, bulk UX, profile/fragment consumers | graphical editor internals, domain backend authority |
| F Script Authoring R2 | code validation UX, context browser, guided snippets | Python runtime bridge redesign, Driver/backend bypass |
| G / L Branding | branding config + shared asset consumer | independent asset store, shell lifecycle authority |
| H / P Historical consumers | Trend period/source UX and shared historical consumers | capture policy, second query API |
| I Historical Playback | read-only historical projection/context | writes/commands, alternate Active authority |
| J / M Copy+i18n | product terminology/localization | backend enums/wire identity, structural redesign |

Shared hotspot rule:
- if a package needs an F0-owned wire/schema change after F0 integrates, it returns `BLOCKED_CONTRACT` rather than editing the shared type hotspot opportunistically.

## 6. Parallelism rule

Parallelism is based on **file + authority ownership**, not conceptual independence.

Main records before release:
- exact base SHA;
- exact contract states;
- owned paths/subsystems;
- forbidden paths/subsystems;
- upstream/downstream dependencies;
- evidence tier required.

Potential concurrency:
- shell/theme may run with Historian/Query/Portability core when file overlap is clean;
- Historian capture and Data Query may run in parallel only if storage/capture vs retrieval/provider ownership is explicit;
- Portability core may run in parallel with data work after #375, but not with another lane mutating the same reuse/import/export kernels;
- Editor and structured forms may run together only with strict hotspot separation;
- Playback waits for query/time-range semantics and historical consumer primitives.

## 7. External-test candidate gate

The candidate is not ready merely because all branches merge.

Required:
1. every mandatory C0 capability implemented/integrated;
2. exact integrated E2;
3. exact integrated mounted E3 for user-facing paths;
4. fresh CODEX black-box Preview;
5. fresh Product Owner human Preview;
6. independent technical audit;
7. material findings corrected/rechecked;
8. user-facing pt-BR/en/es pass;
9. no known implementation-brand leakage;
10. then record:
   `CORRECTION PHASE ACCEPTED / THIRD-PARTY TEST CANDIDATE`.

Only after that does the project return to deferred original Wave 15/productization flow.
