# W14-C11 — Pass 2 Runtime Audit Evidence

**Date:** 2026-09-03 BRT  
**Product-code authority:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`  
**Scope:** evidence-only continuation of C11 Pass 2. C11 implementation remains locked.

This checkpoint closes several high-priority static product questions. It does not authorize DEMO implementation or product correction from the C11 lane.

## C11-P2-SCR-01 — Server Python Runtime host

**Classification:** `PRODUCT GAP — FUNCTIONAL`  
**Recommended disposition:** `fix before C11`

### Requirement

The canonical EEE Simulation needs a normal engineer-authorable server-owned execution mechanism capable of continuously updating shared process state after Publish/Activate, including periodic/timer behavior, shared TAG/Server Memory reads and writes, deterministic sequencing and fault containment.

### Frozen-SHA evidence

1. `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md` at the frozen SHA explicitly describes Server Scripts as a **separate later runtime capability** and says they may **eventually** read/write shared TAGs and `builtin.memory.server` values. The same architecture document leaves the exact server Python host/isolation mechanism deferred.
2. `src/Scada.Api/Program.cs` at the frozen SHA registers the Engineering Script registry as project data, Engineering/runtime coordinators, historian, legacy `DemoRuntimeServices`, legacy `SimulationDriver` and `SimulationDriverHostedService`, but contains no Server Python executor registration, no Server Script hosted service and no Server Script timer scheduler/activation host.
3. Repository search found no implementation/registration of `IPythonScriptHandlerExecutor` and no commit implementing a Server Script runtime host. The historical Python runtime implementation commits are explicitly Client Visual/Pyodide runtime work.
4. The only continuously hosted simulation visible in `Program.cs` is the legacy hard-coded `SimulationDriverHostedService`. C11 requirements explicitly prohibit using that historical DEMO mechanism as proof or as the canonical Simulation implementation path.

### Current product behavior

The product has Server Script contracts/Engineering representation and client-side Python runtime capability, but the frozen product does not provide the normal activated Server Python execution host required to execute project-authored Server Scripts continuously.

### EEE impact

The canonical EEE cannot rely on the documented Server Script mechanism for its autonomous process physics because that runtime mechanism is not implemented in the accepted product.

### Simulation impact

**Blocking.** The intended mass-balance model, automatic pump sequencing, duty/standby alternation, coherent analog evolution and deterministic scenario progression need a server-owned periodic execution mechanism. Client Visual scripts are client-local and cannot become physical process truth. The legacy Simulation Driver is excluded by requirement.

### PLC/Modbus impact

The later PLC variant can obtain physical state from the PLC/Driver and therefore does not require the same simulator physics host. However, this does not remove the Simulation blocker and does not justify building a Simulation-only private architecture.

### Disposition

`fix before C11`.

A bounded product correction should implement the already-designed Server Script runtime host/executor/scheduler/lifecycle through normal Active Engineering authority, with exact-head CI and affected C11 revalidation before implementation release.

## C11-P2-NAV-01 — Explicit startup/home Runtime Screen

**Classification:** `PRODUCT GAP — FUNCTIONAL/ENGINEERING UX`  
**Recommended disposition:** `fix before C11`

### Requirement

An engineer must be able to explicitly choose which Screen is the Runtime startup/home Screen. Naming or lexical ordering is not a canonical product contract.

### Frozen-SHA evidence

`web/scada-web/src/runtime/application/RuntimeApplicationMount.tsx` derives `initialScreenKey` by collecting Screen keys, sorting them alphabetically with `localeCompare`, and selecting `keys[0]`. It then passes that derived key into `RuntimeVisualNavigator`.

`RuntimeVisualNavigator` accepts an `initialScreenKey`, but the Runtime application mount does not obtain that value from a persisted authorable project startup-screen setting.

### Current product behavior

The first Runtime Screen is selected by deterministic lexical Screen-key ordering.

### EEE impact

The desired `Visão Geral / EEE Principal` can only be forced first by manipulating its key/name ordering, which is precisely the naming workaround excluded by the audit requirement.

### Simulation impact

No process-physics effect, but it directly affects canonical Runtime startup and acceptance/homologation behavior.

### PLC/Modbus impact

Same limitation because startup Screen selection is independent of Source type.

### Disposition

`fix before C11`.

Add a persisted, authorable startup/home Screen reference to the canonical project/runtime projection and Engineering UI, validated through Save -> Publish -> Activate -> Runtime.

## C11-P2-POP-02 — Persisted authorable Popup X/Y placement

**Classification:** `PRODUCT GAP — FUNCTIONAL/ENGINEERING UX`  
**Recommended disposition:** `requires Development Lead decision`

### Requirement

If the product is expected to support authored contextual Popup placement, X/Y must be represented through canonical persisted contracts and survive the project lifecycle. DEMO-only CSS or private coordinates are forbidden.

### Frozen-SHA evidence

`RuntimeVisualNavigator.tsx` mounts Popups from navigation state using runtime instance identity, stack index and the Popup definition. The Popup `<section className="runtime-visual-popup">` receives no authored X/Y placement style or mount coordinates. The Popup content receives `resolveVisualDefinitionSurfaceStyle(...)`, which is the visual-definition surface styling path rather than a persisted popup mount-position contract.

The accepted C09/C10 coordination documentation already carried this as an unresolved known concern and explicitly prohibited ad-hoc CSS persistence. Reinspection of the frozen Runtime mount still exposes no canonical per-popup X/Y mount contract.

### Current product behavior

Popup open/close/context is supported, but authored per-popup Runtime placement is not represented in the accepted mount path.

### EEE impact

Contextual equipment Popups can still function as generic overlays/modals, but an engineer cannot canonically position them near equipment through a persisted X/Y contract.

### Simulation impact

Presentation-only; no process-physics effect.

### PLC/Modbus impact

Same presentation limitation.

### Disposition

`requires Development Lead decision`.

If contextual authored placement is mandatory for the canonical EEE experience, fix before C11. If centered/shell-defined Popup placement is explicitly accepted as the product behavior, record that as a known mitigation rather than inventing coordinates in the DEMO.

## C11-P2-QUAL-01 — Deliberate bad/stale/unavailable quality generation in engineer-authored Simulation

**Classification:** `PRODUCT GAP — FUNCTIONAL`  
**Recommended disposition:** `fix before C11`

### Requirement

The canonical DEMO Simulation must deliberately enter an unavailable/bad-quality scenario through normal product contracts. This is distinct from receiving bad quality from a real communication Driver.

### Frozen-SHA evidence

1. `TagQuality` and `TagValue` support quality as runtime data, including `Good`, `Bad` and `Uncertain`.
2. `ISourceProvider.WriteAsync` accepts only `(tagId, value)` and exposes no quality argument or quality-state operation.
3. `ServerMemorySourceProvider` creates every startup, restored, reset and written value through `CreateGoodValue(...)`, always forcing `TagQuality.Good`.
4. `ClientMemorySourceProvider` likewise creates initial and written values with `TagQuality.Good`.
5. `InternalMemoryApi` exposes Client Memory definitions and Server Memory retained-value reset, but no public quality injection/degradation endpoint.
6. `ServerMemoryRuntimeSource.WriteAsync` obtains the provider value and pushes it through `CurrentTagCache`; since the provider always emits `Good`, normal Server Memory writes cannot create the mandatory bad-quality scenario.
7. The legacy Simulation Driver can manufacture quality, but C11 requirements explicitly exclude that historical hard-coded mechanism from canonical Simulation evidence.

### Current product behavior

The runtime data model can carry abnormal quality, but engineer-authored Internal Memory cannot deliberately set or degrade quality through its public runtime/source-provider boundary.

### EEE impact

The required pump/instrument communication-loss scenario cannot be authored honestly using the proposed canonical shared Server Memory model.

### Simulation impact

**Blocking.** A required canonical Simulation scenario is impossible through the currently exposed normal memory/source contract.

### PLC/Modbus impact

A real communication Driver can originate bad quality independently, so physical PLC quality remains a separate later validation. The Simulation gap must not be hidden by claiming that real Drivers can fail.

### Disposition

`fix before C11`.

The product needs a bounded, explicit and safe Simulation/source-quality mechanism. It must not weaken normal process-write semantics or allow arbitrary clients to forge Driver quality outside the intended simulation/engineering boundary.

## C11-P2-QUAL-02 — Runtime propagation and alarm interpretation of non-Good source quality

**Classification:** `SUPPORTED` structurally  
**Recommended disposition:** `requires real-browser validation` for final visual/operator presentation only

### Frozen-SHA evidence

`TagValue` carries quality; `CurrentTagCache` transports complete TAG samples; `InMemoryAlarmEngine` evaluates non-`Good` samples and activates `AlarmType.Communication`; Runtime TAG/trend models expose quality. Therefore a real Source/Driver that emits abnormal quality has a canonical downstream path.

### Current product behavior

The product can propagate source quality through runtime state and alarm semantics. The remaining C11 question is presentation quality, already tracked separately under pump/Dynamo/browser validation.

### Impact

This is not the same as QUAL-01. PLC/Driver-originated bad quality is structurally supported; Simulation-originated deliberate quality remains a gap.

## C11-P2-MEM-03 — Human creation/discovery of Server Memory and Client Memory Sources

**Classification:** `SUPPORTED`  
**Recommended disposition:** `no action required`

### Requirement

An engineer must be able to discover/select both built-in memory source types through normal Data Source Engineering without knowing or manually typing `builtin.memory.server` / `builtin.memory.client`.

### Frozen-SHA evidence

1. `EngineeringDataSourceTypeCatalog.BuildForCurrentSchema()` explicitly publishes Core source-provider descriptors with human display names `Server Memory` and `Client Memory`.
2. The catalog marks them as source providers rather than communication Drivers.
3. `DataSourceCatalogEditor` loads `/api/engineering/data-source-types` from the backend and renders a normal Source type selector using the returned display name. The internal type key is the option value, not a field the user must invent.
4. The same editor builds the candidate package and uses normal Preview/Apply APIs.

### Current product behavior

Both Internal Memory source kinds are part of the backend-authoritative product catalog and are discoverable/selectable through the ordinary Data Source editor.

### EEE / Simulation impact

The Product Owner/integrator does not need private provider knowledge to choose Server Memory for shared simulated state.

### PLC/Modbus impact

None directly; it reinforces the normal Source abstraction used later for Modbus.

## C11-P2-MEM-04 — Complete Memory Source + TAG + lifecycle E2E

**Classification:** `NEEDS VALIDATION`  
**Recommended disposition:** `requires real-browser validation`

### Frozen-SHA evidence

The existing `internal-memory.spec.ts` does not create a Memory Data Source from an empty project. Its Engineering test fixture already contains a `builtin.memory.client` Data Source and TAG, then validates typed initial-value Preview/Apply. The client-memory runtime test mocks pre-existing definitions.

### Current product behavior

The catalog and authoring code strongly support Source creation, and the runtime memory provider is real, but the exact requested E2E proof remains absent:

`create Memory Source -> create TAG -> configure value -> Save -> Publish -> Activate -> Runtime`.

### Impact

This remains validation-only unless browser execution reveals a defect. It must not be upgraded to `SUPPORTED` merely by combining disconnected unit/static evidence.

## C11-P2-ALM-01 — Alarm activation, acknowledgement and return-to-normal

**Classification:** `SUPPORTED`  
**Recommended disposition:** `no action required`

### Frozen-SHA evidence

`InMemoryAlarmEngine` subscribes to `TagValueChanged`, evaluates Digital/High/HighHigh/Low/LowLow/Communication conditions, transitions into Active/Acknowledged/Returned states, publishes `AlarmStateChanged`, and exposes acknowledgement. The protected Runtime API performs backend authorization before acknowledgement.

### EEE / Simulation impact

Once canonical Simulation TAGs evolve, the same values can drive process alarms without DEMO-only alarm code. Communication alarms can react to non-Good quality after QUAL-01 is solved.

### PLC/Modbus impact

The same alarm definitions remain bound to logical TAG identities and can consume Driver-backed samples later.

## C11-P2-HIST-01 — Historian capture from canonical TAG changes

**Classification:** `SUPPORTED`  
**Recommended disposition:** `no action required`

### Frozen-SHA evidence

1. `ServerMemoryRuntimeSource` registers active Memory TAGs and pushes initialization/writes into the same `CurrentTagCache` used by runtime TAGs.
2. `BufferedInMemoryHistorian` and the Timescale historian subscribe to `TagValueChanged`; capture is controlled by normal TAG historian policy.
3. Runtime/history APIs query by stable TAG identity.
4. Historical Query with Timescale provides the durable `historian.samples` dataset and protected operator query boundary.

### Current product behavior

Canonical TAG updates can feed the historian through the normal event pipeline. The lack of Server Script execution prevents autonomous Simulation generation but does not constitute a historian gap.

### PLC/Modbus impact

Logical TAG identity remains the historian association, supporting later Source remapping in principle.

## C11-P2-ALMHIST-01 — Alarm history persistence/query

**Classification:** `SUPPORTED`  
**Recommended disposition:** `no action required`

### Frozen-SHA evidence

When Historical Query is enabled with Timescale/PostgreSQL, `AlarmHistoryPersistenceHostedService` subscribes to `AlarmStateChanged` and writes to `PostgreSqlAlarmHistoryStore`. The protected Historical Query API exposes the allowlisted `alarm.events` dataset. The operator Historical Data Browser explicitly supports persisted historian samples and alarm events.

### EEE impact

Alarm activation, ACK and recovery transitions can be retained and consulted through a normal product surface rather than only the current alarm list.

## C11-P2-EVT-01 — Canonical general operational event model/history distinct from alarms

**Classification:** `PRODUCT GAP — FUNCTIONAL`  
**Recommended disposition:** `fix before C11`

### Requirement

The canonical EEE must demonstrate events as well as alarms. Ordinary state transitions such as pump start/stop should be representable as operator events without abusing alarm definitions.

### Frozen-SHA evidence

1. The canonical `EngineeringPackage` contains TAGs, Alarms, Data Sources, Templates, Equipment, Dynamos, Screens, Popups, Roles, Commands, Gateways, Scripts, Visual Assets and Reports, but no Event definition collection/entity kind.
2. `ImportEntityKind` likewise has no Event entity.
3. Historical Query allowlists `historian.samples` and `alarm.events`; no general operational-event dataset exists.
4. `IScadaEventBus` is an internal runtime message bus for subsystem coordination. Its existence is not an engineer-authorable, persisted operator event model.

### Current product behavior

The product can retain alarm transitions and internal runtime events, but does not expose a canonical Engineering definition/history/operator surface for general operational events.

### EEE impact

Normal equipment transitions cannot be modeled as first-class events without either misclassifying them as alarms or inventing DEMO-only storage/logic.

### Simulation impact

The living Simulation can create process state transitions after SCR-01 is fixed, but the product still lacks the required canonical event destination/history.

### PLC/Modbus impact

Same gap for real PLC-backed state changes.

### Disposition

`fix before C11`.

Add a bounded canonical operational-event contract and history/query/operator surface, preserving the distinction between events, alarms and security/audit records.

## C11-P2-TREND-01 — Operator-accessible trend rendering for evolving process values

**Classification:** `PRODUCT GAP — RUNTIME UX/FUNCTIONAL`  
**Recommended disposition:** `fix before C11`

### Requirement

The EEE operator experience must expose real process trends, not only raw historical rows.

### Frozen-SHA evidence

1. `BasicTrendViewer.tsx` is a genuine protected single-Pen trend component with live/historical windows, chart plotting, quality display and `pt-BR` / `en` / `es` copy.
2. The converged application router mounts `/runtime/history` as `HistoricalDataBrowserRuntime`, not `BasicTrendViewer`.
3. The runtime navigation exposes `Overview` and `History`; there is no mounted Trend route/surface in `main.tsx` / `AppNavigation.tsx`.
4. `HistoricalDataBrowser` is a tabular persisted-data explorer. It does not provide the trend chart required by the canonical EEE operator experience.
5. No canonical Screen visual type was identified that would let an integrator place `BasicTrendViewer` inside the EEE through normal Screen Engineering.

### Current product behavior

Historian data and a trend component both exist, but the accepted C09/C10 product does not expose that chart as a normal operator surface or authorable HMI object.

### EEE impact

The Product Owner cannot demonstrate level/flow/pressure/current trends from the canonical operator Runtime without a product correction or forbidden custom React/DOM composition.

### Simulation impact

Historian samples could accumulate after SCR-01 is fixed, but the operator cannot consume them as the required trend chart through the current canonical shell.

### PLC/Modbus impact

Same presentation gap for real PLC data.

### Disposition

`fix before C11`.

Expose the existing trend capability through a normal capability-controlled Runtime route/surface or a canonical Engineering Trend visual contract. Do not mount a DEMO-only React component.

## C11-P2-I18N-HIST-01 — `pt-BR` / `en` / `es` operator Historical Data Browser

**Classification:** `PRODUCT GAP — RUNTIME UX`  
**Recommended disposition:** `fix before C11`

### Frozen-SHA evidence

`HistoricalDataBrowser.tsx` and substantial controller chrome in `HistoricalDataBrowserRuntime.tsx` contain hard-coded English labels/messages such as `Historical Data Browser`, `Refresh`, `Dataset`, `Period`, `Relative`, `Absolute`, `Query`, result-state messages, search/sort controls and paging. This mounted `/runtime/history` surface does not consume the common Runtime locale in the accepted product.

By contrast, `BasicTrendViewer` already has explicit `pt-BR`, `en` and `es` copy, showing that the gap is the mounted historical-browser surface rather than a technical inability to localize Runtime history UI.

### EEE impact

A required operator-facing C11 surface fails the multilingual product requirement in `pt-BR` and `es`.

### Disposition

`fix before C11`.

Localize visible historical-browser/operator-history copy while preserving dataset keys, column keys, protocol identifiers and persisted data unchanged.

## Consequence for C11 release recommendation

The static audit now contains multiple confirmed product gaps, including blocking Simulation gaps:

- missing Server Python execution host for living shared process physics;
- no canonical deliberate bad-quality generation path for engineer-authored Simulation;
- no canonical general operational-event model/history;
- no mounted/operator-authorable trend chart despite backend history and a dormant trend component;
- no explicit persisted startup/home Screen;
- Internal Memory Address authoring UX gap recorded in the main audit;
- mounted Historical Data Browser multilingual gap;
- Popup X/Y remains a Development Lead disposition item.

Memory Source discoverability itself is now supported. Alarm lifecycle, alarm history and historian ingestion are also structurally supported; they do not need to be reinvented inside the DEMO.

Current recommendation therefore remains:

`KEEP C11 IMPLEMENTATION LOCKED`

Next audit priority is remaining Runtime/browser behavior, multilingual surfaces beyond history, and Simulation-to-Modbus logical TAG/source reuse. Real PLC operation remains explicitly deferred to later hardware validation.