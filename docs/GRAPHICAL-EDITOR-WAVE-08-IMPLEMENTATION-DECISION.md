# Wave 08 — Graphical Editor Foundation Implementation Decision

Status: **LOCKED WAVE 08 EXECUTION CONTRACT**  
Date: 2026-08-29  
Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`

This document converts the owner-approved v0.1 roadmap and completed Wave 07 canonical visual foundation into the executable Wave 08 boundary.

Wave 08 makes canonical visual Engineering practically editable and adds a read-only Development Monitor so engineers can observe live process/runtime behavior while building and validating an application. The editor is a projection/editor of canonical Engineering, never a second project format; the monitor is diagnostic tooling, never process-control authority.

## Entry gate

Wave 08 starts only because every item in `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md` is satisfied:
- Wave 07 merged and post-merge green;
- Engineering Schema v12 typed visual properties;
- stable nested visual identity;
- canonical visual binding semantics;
- validated C#/TypeScript property-registry parity;
- stable `assetRef` identity;
- official canonical Engineering -> Runtime projection;
- worker scopes/reserved files frozen by this contract.

## Product objective

Deliver the first practical graphical Screen editor foundation plus an Engineering Development Monitor with:

1. Canvas viewport and deterministic selection/editing interactions;
2. Property Inspector driven by the public Visual Property Registry;
3. initial Object Palette for the Wave 07 built-ins;
4. canonical TAG/property binding authoring foundation;
5. first-class project image-asset import/storage/reference seam required by `core.image`;
6. coordinator-owned Screen persistence/composition through canonical Engineering;
7. save/reopen/export/import fidelity without private canvas persistence;
8. a read-only live monitoring table for development diagnostics covering canonical TAGs, internal memories, system/runtime variables and Data Source/driver diagnostics through a unified provider/catalog boundary;
9. search/browse plus exact-reference quick-add, so an engineer may either find a variable or type a known canonical name/path directly;
10. live rows showing at minimum canonical reference/name, source kind, current value, data type, quality/state and source timestamp/last-update information.

The graphical-editor gate is:

`Create Screen -> add objects -> move/resize/rotate -> edit public properties -> bind -> image asset -> save -> reopen -> export/import`

The Development Monitor gate is:

`search or type canonical reference -> add monitored row -> observe live value/type/quality/timestamp -> source changes -> row updates -> unavailable/bad source remains explicit -> remove row`

For Image, an imported project asset must be referenced through canonical `assetRef`, never through a filesystem path or arbitrary URL.

## Locked authority rules

- Canonical Engineering is saved project truth.
- `VisualObjectPropertySchema` / public Visual Property Registry is property authority.
- `VisualElementEngineering` stable ID is object identity.
- Runtime precedence remains `Animation > Script > Binding/Expression > Engineering Base > Default`.
- Editor interaction state such as current selection, handles, hover, viewport transform and drag preview is client-local UI state only.
- UI interaction state must never be serialized as competing project authority unless an explicit canonical Engineering field exists for that exact concept.
- Development Monitor rows are observational tooling. Current monitored values, quality, timestamps and diagnostic state are Runtime/diagnostic state and must never be written back into canonical Engineering merely because they are visible in the table.
- Wave 08 Development Monitor is read-only. It must not write TAGs, memories, commands, driver configuration or process outputs.
- No worker may change Wave 07 public property semantics merely to simplify a UI implementation.

## Initial object set

The palette consumes the existing built-in type keys:
- `core.group`
- `core.rectangle`
- `core.ellipse`
- `core.line`
- `core.text`
- `core.image`
- `core.valueDisplay`
- `core.button`

Object defaults come from the shared schema. Workers must not duplicate default-property tables.

## Canvas semantics

Wave 08 Canvas foundation includes:
- viewport;
- zoom and pan;
- optional grid display;
- deterministic snap behavior when enabled;
- single selection;
- multiselect;
- drag/move;
- resize where meaningful;
- rotation where meaningful;
- duplicate/delete intent;
- z-order intent;
- hierarchy-aware stable object IDs;
- visual handles/selection adorners as UI state only.

Canvas may expose commands/callbacks to coordinator composition. It must not call persistence APIs directly.

## Property Inspector semantics

The inspector:
- receives selected canonical visual definition(s) and shared object schema;
- renders editors according to declared property type/presentation metadata;
- validates through the shared registry/schema;
- distinguishes explicit Engineering Base from registry Default;
- emits explicit property-change/remove intents;
- never writes Runtime Script/Animation layers;
- never creates its own property metadata/default table;
- fails closed for unregistered properties.

Multi-selection may support only the deterministic common-property subset during this wave. Ambiguous mixed values must not be silently overwritten.

## Object Palette and binding foundation

The palette:
- exposes only registered built-in object types;
- creates object-add intents with stable type key and schema defaults handled by canonical composition;
- does not mint persistence authority outside coordinator integration.

Binding authoring:
- destination is the registered visual property key;
- source uses canonical Engineering binding fields (`Key`, `Target`, `Kind` semantics from Wave 07);
- only properties with `supportsBinding` may accept a binding;
- unknown/unsupported destinations fail closed;
- TAG/property/expression catalogs are consumed through coordinator-provided canonical data, never private driver access.

## Image asset foundation

Coordinator owns canonical project asset authority because it crosses Engineering schema, API, package portability and persistence.

Wave 08 asset foundation must provide enough for practical Image authoring:
- stable project asset ID;
- developer-facing name/original filename metadata;
- supported raster media families required by v0.1: JPG/JPEG, PNG including alpha, BMP;
- bounded payload/file-size policy;
- safe media validation;
- canonical project/package persistence and restore semantics;
- asset selection by stable ID;
- `core.image.assetRef = null | { assetId }` only.

No filesystem path or arbitrary URL becomes saved image authority.

Advanced asset library/search/transcoding/caching belongs later unless required to make the basic path correct.

## Engineering Development Monitor — mandatory Wave 08 scope

Canonical detailed contract: `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`.

The owner requires a Watch/Monitor Table for development analysis inside Engineering. Its purpose is to let an engineer observe the behavior of variables and diagnostics while creating/testing an application without having to build a temporary HMI screen or attach custom code.

### Initial source classes

The monitor must use a provider/catalog seam so new observable domains can be added without creating a second variable model. The initial product path must cover, where canonical source authority exists:

- TAG current values;
- Client Memory and Server Memory/internal-memory values;
- system/runtime variables or diagnostics exposed by authoritative product services;
- Data Source / driver diagnostics and communication state;
- future provider extension for Gateway, bit selectors, expression diagnostics and other development sources without redesigning the table.

After 08-FOLLOW-A, first-class TAG bit selectors must be eligible monitor sources through the same canonical catalog rather than a special one-off UI path.

### Find and add behavior

The engineer must be able to:

- search/browse by name, canonical path/reference and source category;
- filter source category where useful;
- inspect enough result metadata to distinguish similarly named items before adding;
- type a known exact canonical reference/name/path and add it directly;
- receive an explicit not-found/ambiguous result rather than a silent fuzzy substitution;
- add multiple heterogeneous source kinds to the same table;
- remove individual rows and clear the table.

Search is a convenience layer only. Saved/runtime identity must remain the canonical source reference returned by the provider.

### Required columns / facts

Each row must expose at minimum:

- name/reference/path;
- source kind/category;
- current value;
- canonical data type;
- quality or authoritative diagnostic state;
- source timestamp / last update, when the source defines one.

Useful optional presentation may include engineering unit, age/staleness and a diagnostic detail affordance. A source that does not define process quality must display an explicit `N/A`/diagnostic state rather than fabricating `Good`.

### Live-update semantics

- use existing realtime/event/subscription paths where available;
- use bounded/coalesced polling only where a source has no push path;
- do not create one independent backend polling loop per monitored row;
- preserve the latest authoritative sample while bounding UI render pressure;
- preserve exact value semantics, including Int64-safe representation and explicit types;
- never coerce unavailable/bad/wrong-type data into `0`, `false` or empty string;
- distinguish stale/unavailable/disconnected/bad-quality observations visibly;
- adding a monitor row must not change driver scan rates, TAG configuration or source behavior.

Wave 08 must support a practical multi-row watch table, with acceptance proving at least 100 simultaneous monitored entries can share batched/subscription infrastructure rather than one request loop per row.

### Persistence boundary

The initial monitor selection/watchlist may be session- or user-workspace state. It is not process logic and current values are never Engineering project data.

If the implementation persists a watchlist definition, only canonical source references/order/presentation preferences may be saved. Live values, qualities, timestamps and diagnostics must never be exported as authored Engineering state. Named project-portable Watch Tables may be promoted to a first-class Engineering tooling entity in a later decision if required; Wave 08 does not need to invent that project model merely to deliver the live diagnostic workflow.

### Explicit non-goals

The Wave 08 monitor is not:

- a write/force table;
- a command console;
- a historian or trend recorder;
- an alarm acknowledgement surface;
- a driver configuration editor;
- a replacement for Runtime operator displays;
- process safety/interlock authority.

## Renderer boundary

Wave 08 may introduce the minimum renderer/composition necessary to make editor interactions understandable and to reopen authored Screens deterministically.

It must remain a consumer of canonical definitions/effective properties. Renderer-private objects, DOM nodes and CSS selectors never become persisted Engineering or Python API authority.

Full Runtime Screen/Popup/Dynamo product rendering/navigation is Wave 09/11 territory; production tween scheduling remains Wave 10.

## Worker ownership

### DEV 1 — Canvas / Selection

Branch: `feature/graphical-editor-wave-08-canvas`

**AllowedScope**
- `web/scada-web/src/engineering/visual-editor/canvas/**`
- focused Canvas/selection browser tests named `web/scada-web/tests-e2e/visual-editor-canvas*.spec.ts`

**Deliver**
- renderer-independent Canvas interaction model/component;
- viewport zoom/pan/grid/snap;
- selection/multiselect;
- move/resize/rotate interaction intents;
- duplicate/delete/z-order intents;
- deterministic selection/adornment state;
- tests proving UI state does not mutate supplied canonical definitions directly.

**ForbiddenScope**
- canonical Engineering schema/API changes;
- central editor workspace/route;
- Property Inspector;
- Object Palette/binding editor;
- asset backend/import storage;
- public Visual Property Registry semantics;
- Runtime/Python semantics.

### DEV 2 — Property Inspector

Branch: `feature/graphical-editor-wave-08-property-inspector`

**AllowedScope**
- `web/scada-web/src/engineering/visual-editor/property-inspector/**`
- focused tests named `web/scada-web/tests-e2e/visual-editor-property-inspector*.spec.ts`

**Deliver**
- inspector driven only by shared object/property schema;
- typed controls for the registered Wave 08 property family;
- explicit default-vs-engineered behavior;
- validation/error presentation contract;
- deterministic mixed/multiselect behavior where supported;
- change/remove intents without persistence calls.

**ForbiddenScope**
- duplicated property registry/default tables;
- canonical Engineering schema/API changes;
- central editor workspace/route;
- Canvas interaction implementation;
- Object Palette/binding editor;
- asset backend/import storage;
- Runtime/Python writes.

### DEV 3 — Object Palette / Binding Foundation

Branch: `feature/graphical-editor-wave-08-palette-bindings`

**AllowedScope**
- `web/scada-web/src/engineering/visual-editor/object-palette/**`
- `web/scada-web/src/engineering/visual-editor/binding-editor/**`
- focused tests named `web/scada-web/tests-e2e/visual-editor-palette*.spec.ts` and `visual-editor-binding*.spec.ts`

**Deliver**
- initial built-in palette using existing `core.*` schemas;
- object-add intents without private persistence;
- binding editor/model consuming canonical destination/source semantics;
- rejection of unsupported/unregistered binding destinations;
- coordinator-provided source catalog boundary;
- Image palette entry consuming existing `assetRef` contract without implementing asset persistence.

**ForbiddenScope**
- canonical Engineering schema/API changes;
- central editor workspace/route;
- Canvas implementation;
- Property Inspector implementation;
- asset backend/storage/import authority;
- changes to Wave 07 property or Python authority.

The original DEV 1/2/3 graphical slices are already delivered and integrated into the Wave 08 integration train. Any new Development Monitor worker assignment requires an explicit new authorization on `docs/CHAT-WORK-ASSIGNMENTS.md`; this owner scope addition does not silently reopen an old worker mission.

## Coordinator ownership

Integration branch: `integration/graphical-editor-wave-08`

Coordinator exclusively owns or explicitly delegates:
- this contract and coordination documents;
- canonical Screen visual mutations/save/reopen semantics;
- Engineering Schema/migration/import-export changes;
- first-class visual asset entity/API/persistence/package semantics;
- Development Monitor provider/catalog contract, central composition and cross-domain read-only authority boundaries unless explicitly delegated;
- `src/Scada.Api/Program.cs` and backend composition;
- central frontend Engineering route/workspace composition;
- `web/scada-web/src/engineering/EngineeringApp.tsx`;
- `web/scada-web/src/engineering/api.ts`;
- `web/scada-web/src/engineering/types.ts`;
- `web/scada-web/src/engineering/visualEngineeringRuntimeAdapter.ts`;
- shared `web/scada-web/src/visual-runtime/**` public contract changes;
- top-level `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace*` composition;
- renderer composition/shared exports;
- localization integration;
- `.github/workflows/**`, solution/global SDK files, `package.json`/lockfile;
- final cross-slice tests, PR, CI and merge.

## ReservedFiles

Workers must not edit without explicit coordinator reassignment:
- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/**`
- `.github/workflows/**`
- `ScadaPlatform.sln`
- `global.json`
- `src/Scada.Api/Program.cs`
- canonical Engineering DTO/schema/import-export/validation files
- `web/scada-web/package.json`
- `web/scada-web/package-lock.json`
- `web/scada-web/src/engineering/EngineeringApp.tsx`
- `web/scada-web/src/engineering/api.ts`
- `web/scada-web/src/engineering/types.ts`
- `web/scada-web/src/engineering/visualEngineeringRuntimeAdapter.ts`
- `web/scada-web/src/visual-runtime/**`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace*`
- any other worker branch/scope.

If a worker discovers a needed reserved-file change, it records the dependency in its delivery and stops at the boundary rather than editing around it.

## Non-goals / forbidden wave expansion

Wave 08 does not implement:
- production Popup semantics;
- reusable Dynamo authoring semantics beyond consuming already-known definitions;
- multi-Screen navigation/start-screen product flow;
- final Runtime HMI vertical slice;
- Python event editor;
- production animation/tween scheduler;
- Server Python;
- new industrial protocols;
- advanced industrial symbol libraries;
- marketplace/plugins;
- full asset management suite;
- monitoring-table writes/forces/commands;
- full historian/trend capture inside the Development Monitor.

Those belong to Wave 09/10/11 or later unless separately locked by the owner.

## Validation

Worker branches use focused tests within owned scope and do not merge themselves.

Coordinator integration must ultimately prove on one exact head:
- Web build/typecheck;
- backend Release/full PostgreSQL+Timescale tests;
- Runtime smoke;
- Chromium E2E;
- Wave 08 graphical editor acceptance;
- Wave 08 Development Monitor acceptance;
- all Wave 07 visual/Python regressions;
- all Wave 06 sandbox/native-escape/cancellation regressions.

Required graphical Wave 08 acceptance includes:
1. create/load canonical Screen definition;
2. add built-in objects;
3. select/move/resize/rotate as applicable;
4. edit registered properties;
5. author a supported canonical binding;
6. import/select a supported project image asset and persist `assetRef` by stable ID;
7. save canonical Engineering;
8. reopen with stable object IDs/property values/bindings;
9. export/import and recover the same authored definition;
10. prove transient Canvas selection/viewport state is not persisted as project authority.

Required Development Monitor acceptance includes:
1. open the Engineering Development Monitor;
2. search and add a canonical TAG from the catalog;
3. type a known exact canonical TAG/reference and add it without browsing;
4. add at least one internal-memory source;
5. add at least one system/runtime diagnostic source;
6. add at least one Data Source/driver diagnostic source;
7. display value, data type, quality/state and source timestamp/last-update information without silent coercion;
8. observe a source change reflected live in the row;
9. prove bad/unavailable/disconnected source state remains explicit;
10. remove/clear monitored rows without changing source configuration;
11. prove monitor behavior is read-only and does not issue TAG/memory/command writes;
12. prove a large watch set uses shared batching/subscription rather than independent polling per row;
13. prove live monitored values/qualities/timestamps do not become canonical Engineering/export/package state.

Wave 08 closes only after both graphical and Development Monitor gates are green on the final integrated head and the merge commit is healthy on `main`.
