# Wave 14 C11 — Canonical EEE DEMO Implementation Handoff

**Coordinator status:** IMPLEMENTATION ACTIVE  
**Branch:** `wave14/c11-canonical-eee-demo`  
**Exact product base:** `3fda88061df35ad14755d22881e5d3a9216d1ff5`  
**Product tree:** `da6b406ac111cb40b99e5b13031601eb71606ddd`  
**Target integration:** `wave14/corrections-integration`  
**C11 release authority:** `docs/WAVE14-C11-IMPLEMENTATION-RELEASE.md`  
**Product Owner browser homologation:** after canonical DEMO creation.

> The historical EliteSCADA DEMO is not the reference application for this package. Build a new real EliteSCADA project using ordinary public Engineering/runtime contracts.

## 1. Objective

Create the new canonical **Estação Elevatória de Esgoto (EEE)** application as a living deterministic EliteSCADA project that simultaneously proves:

- normal Engineering project construction;
- persisted `Save -> Publish -> Activate` authority;
- server-authoritative simulated process state;
- reusable HMI Dynamos;
- real Screens/Popups/navigation;
- canonical Commands;
- Alarm state/lifecycle;
- first-class Operational Events;
- Historian/Trend behavior;
- embedded Alarm Browser/Event Browser;
- quality propagation including `Bad` / `Stale` / `Unavailable`;
- realistic operator presentation;
- future transition to a real PLC by remapping Source/address without redesigning the HMI/TAG conceptual model.

No EEE-specific product code is authorized.

## 2. Construction and artifact rule

The canonical application must be reproducible through normal product surfaces.

Accepted repository-side construction path:

1. start from a normal fresh Engineering project/workspace;
2. construct the canonical package definition through the protected **Engineering JSON Import Preview / Apply** surface or ordinary human editors, never by changing private runtime state;
3. require Preview `canApply=true` / zero errors;
4. Apply into official Working Engineering;
5. execute normal `Save -> Publish -> Activate`;
6. prove `/api/runtime/application` reflects the same project/revision and intended package;
7. exercise deterministic runtime behavior;
8. export the accepted package through the normal `/api/project-package/export` product endpoint;
9. Preview-import the exported package again to prove portability;
10. version the exported canonical `.escadapkg` plus provenance/checksum only after the Active application has passed acceptance.

The public Engineering JSON Import endpoint is an ordinary protected product surface and is acceptable for deterministic fixture regeneration. It must not replace evidence that the underlying entities are ordinarily authorable. C15–C19/C17 already provide individual authoring/lifecycle evidence; C11 must add application-level evidence for the assembled project.

Forbidden construction shortcuts:

- hand-editing opaque/private `.escadapkg` bytes as the source of truth;
- direct database writes;
- direct runtime cache writes;
- private backend object mutation;
- historical `SimulationDriver`/`DemoRuntimeServices` as the canonical engine;
- Preview-only or DEMO-only APIs.

## 3. Project identity

Use stable canonical application identity:

- project key: `eee-demo`;
- project name: `EliteSCADA — EEE Demo`;
- main Screen key: `eee.overview`;
- startup Screen: stable ID of `eee.overview`, persisted through normal `startupScreenId`;
- logical HMI design remains the canonical 1920×1080 product coordinate space.

Stable IDs must be deterministic and checked into the fixture/builder. Do not generate new identities on every regeneration.

## 4. Data Source architecture

The Simulation variant uses one shared server-authoritative Source:

- key: `eee.sim.server-memory`;
- name: `EEE Simulation — Server Memory`;
- driver: `builtin.memory.server`;
- enabled: true.

Server Memory is process truth. Client Memory may later be used only for client-local UI state; it must not own station process state.

No network Address belongs on these TAGs.

## 5. Canonical TAG model

Keep conceptual TAG names suitable for the later PLC/Modbus variant. The HMI should bind to these conceptual identities; the PLC variant should primarily replace Source/address mapping.

### 5.1 Process / station

Required shared TAGs:

- `EEE.Process.LevelPct` — `double`, %, historian enabled;
- `EEE.Process.InflowM3h` — `double`, m³/h, historian enabled;
- `EEE.Process.TotalFlowM3h` — `double`, m³/h, historian enabled;
- `EEE.Process.DischargePressureBar` — `double`, bar, historian enabled;
- `EEE.Process.AutoMode` — `boolean`;
- `EEE.Process.HighDemand` — `boolean`;
- `EEE.Process.DutyPump` — `int32`, value 1 or 2;
- `EEE.Process.CycleCount` — `int32` when naturally useful;
- `EEE.Process.BadQualityScenario` — `boolean`.

Suggested deterministic initial state:

- LevelPct = 45.0;
- InflowM3h = 20.0;
- AutoMode = true;
- HighDemand = false;
- DutyPump = 1;
- CycleCount = 0;
- BadQualityScenario = false.

### 5.2 Pump P01

- `EEE.P01.Running` — boolean;
- `EEE.P01.Available` — boolean, initial true;
- `EEE.P01.Fault` — boolean;
- `EEE.P01.Trip` — boolean;
- `EEE.P01.FrequencyHz` — double, Hz, historian enabled;
- `EEE.P01.CurrentA` — double, A, historian enabled;
- `EEE.P01.FlowM3h` — double, m³/h, historian enabled;
- `EEE.P01.PressureBar` — double, bar, historian enabled.

### 5.3 Pump P02

Mirror P01 exactly:

- `EEE.P02.Running`;
- `EEE.P02.Available`;
- `EEE.P02.Fault`;
- `EEE.P02.Trip`;
- `EEE.P02.FrequencyHz`;
- `EEE.P02.CurrentA`;
- `EEE.P02.FlowM3h`;
- `EEE.P02.PressureBar`.

### 5.4 Command/request TAGs

Commands must not write process feedback directly. Use writable request/control TAGs consumed by the normal Server Script, for example:

- `EEE.Command.AutoEnable`;
- `EEE.Command.AutoDisable`;
- `EEE.Command.P01Start`;
- `EEE.Command.P01Stop`;
- `EEE.Command.P02Start`;
- `EEE.Command.P02Stop`;
- `EEE.Command.ResetFaults`;
- `EEE.Command.InjectP01Fault`;
- `EEE.Command.InjectP02Fault`;
- `EEE.Command.HighDemandEnable`;
- `EEE.Command.HighDemandDisable`;
- `EEE.Command.BadQualityEnable`;
- `EEE.Command.BadQualityDisable`.

The Script consumes one-shot requests and clears them through normal Server Memory writes. Persistent scenario commands may directly set dedicated scenario TAGs only if the resulting semantics remain explicit and deterministic.

## 6. Deterministic process model

Implement as one normal **Server Script** with `server` scope and at least:

- `initialize` entry point;
- `timer` entry point at 1000 ms;
- explicit `serverMemoryTag` dependencies for every state TAG it may read/write/qualify.

Minimum supported timer is already 50 ms; use 1000 ms for deterministic readability and low CI flakiness.

The script must use only:

- `read_server_memory` / `read_tag`;
- `write_server_memory`;
- `publish_server_memory_sample` for deliberate quality scenarios;
- `emit_operational_event` for configured C14/C19 event definitions.

No imports, filesystem, network, database, Driver object or DI access.

### 6.1 Normal hydraulic behavior

Use a simple deterministic mass-balance model.

Recommended constants:

- normal inflow: 20 m³/h;
- high-demand inflow: 55 m³/h;
- one running pump nominal flow: 38 m³/h;
- two running pumps combined flow: approximately 70 m³/h;
- automatic start threshold: 65%;
- second-pump threshold: 80%;
- automatic stop threshold: 35%;
- valid level range: 0–100%.

Each 1 s tick:

1. resolve inflow from normal/high-demand scenario;
2. evaluate command requests and persistent mode/scenario state;
3. in Auto, start duty pump when level reaches start threshold;
4. start standby pump when level reaches second-pump threshold or high-demand logic requires it;
5. do not run a pump that is Fault/Trip/unavailable;
6. stop pumps according to low threshold and operating state;
7. derive per-pump flow/frequency/current/pressure coherently from Running state;
8. derive total flow and station pressure;
9. update level from inflow minus pumped flow using a fixed deterministic tank conversion factor;
10. clamp level to 0–100;
11. when an automatic pumping cycle completes, alternate `DutyPump` 1↔2 and increment CycleCount;
12. clear one-shot command request TAGs.

The exact conversion factor may be tuned for a visually useful cycle duration, but must be constant and deterministic. A complete automatic fill/start/drain/stop cycle should occur in a practical homologation window rather than requiring many minutes of staring at an unmoving well.

### 6.2 Pump electrical/process values

When stopped and healthy:

- frequency ≈ 0 Hz;
- current ≈ 0 A;
- flow ≈ 0 m³/h.

When running normally:

- frequency should be a plausible deterministic value (for example 45–50 Hz);
- current should rise coherently (for example 18–24 A);
- per-pump flow around the chosen nominal value;
- pressure should become positive and track the running state/number of pumps.

A fault/trip sets Running false and associated flow/frequency/current back to safe stopped values on the next deterministic tick.

## 7. Quality scenario

Use the accepted C13/C12 server-authoritative quality path.

When `EEE.Process.BadQualityScenario` is active, the Server Script must publish at least one visually important measurement using:

`publish_server_memory_sample(tagId, retainedValue, 'Unavailable')`

The scenario should exercise the operator HMI and historian with canonical non-Good quality. Prefer a pump/instrument measurement that makes the corresponding Dynamo or detail view visibly unavailable while retaining a coherent last value.

The scenario must not make a physical Driver TAG spoof quality and must not create a frontend-only quality flag.

Repository acceptance should exercise `Bad`, `Stale` and `Unavailable` where practical, while the visible Product Owner scenario may use `Unavailable` as the strongest obvious demonstration state.

## 8. Alarm model

Use canonical Alarm types exactly as supported by Engineering:

`digital`, `high`, `highHigh`, `low`, `lowLow`, `communication`, `system`.

Use priorities only from:

`low`, `medium`, `high`, `critical`.

Minimum alarm set:

1. **High level** — `high` on `EEE.Process.LevelPct`, setpoint ~75%, high priority;
2. **High-high level** — `highHigh` on LevelPct, setpoint ~90%, critical priority;
3. **Low level** — `low` on LevelPct, setpoint ~20%, medium priority;
4. **P01 fault/trip** — `digital` on fault/trip status, high priority;
5. **P02 fault/trip** — equivalent;
6. **Instrumentation communication/quality** — `communication` on the selected bad-quality demonstration TAG, high priority when canonical communication semantics support the source quality.

Use meaningful area/class/message fields, e.g. area `EEE`, classes `Process`, `Electrical`, `Communication`.

At least critical/high alarms should require acknowledgement when appropriate. Shelving may be enabled only where the ordinary Alarm workflow already supports it.

## 9. Operational Event model

Author normal C14/C19 Operational Event definitions for meaningful immutable occurrences, separate from Alarm state:

- `eee.pump.started`;
- `eee.pump.stopped`;
- `eee.pump.fault-injected`;
- `eee.pump.fault-reset`;
- `eee.duty.changed`;
- `eee.mode.changed`;
- `eee.high-demand.changed`;
- `eee.quality-scenario.changed`.

The Server Script emits these only on actual transitions, not every Timer tick.

Use canonical configured definition identity. Runtime message/context overrides may add pump number, old/new duty or scenario state, but must not forge canonical definition type/category/source.

Do not turn Alarm transitions into Operational Events automatically unless the process event itself is independently meaningful.

## 10. Command model

Create normal canonical Command entities with stable IDs and HMI `executeCommand` actions using the same runtime contract already proven by C16.

Commands should include at minimum:

- Enable Auto;
- Disable Auto;
- Start P01;
- Stop P01;
- Start P02;
- Stop P02;
- Reset faults;
- Inject P01 fault;
- Inject P02 fault;
- Enable/disable high demand;
- Enable/disable bad-quality scenario.

Command targets are request/control TAGs, not feedback TAGs.

For Auto mode, manual start/stop requests may be ignored or normalized by the Script according to an explicit deterministic policy; the HMI should make the mode visible so this is not surprising.

## 11. HMI application structure

Required Screens:

- `eee.overview` — main operator overview and startup Screen;
- `eee.instrumentation` — level/flow/pressure/frequency/current detail + Trend;
- `eee.electrical` — pumps/motors electrical state and command detail;
- `eee.operation` — Alarm Browser, Event Browser, scenarios and operating controls.

Screen navigation must use ordinary Runtime actions inside the logical viewport. Do not use web-page routing/reflow as authored HMI navigation.

## 12. Main overview

The overview must be operator-oriented and strongly visual.

Required content:

- station title and concise mode/status header;
- suction well graphic;
- live **Analog Fill** bound to `EEE.Process.LevelPct`, 0–100%, bottom-to-top;
- numeric level value and unit;
- inlet and discharge flow indication;
- two independent instances of one reusable pump Dynamo;
- discharge pressure;
- obvious Auto/Manual and duty indication;
- navigation to support Screens;
- access to contextual pump Popups;
- high-level alarm state indication where normal objects support it.

Do not make the Runtime look like Engineering or a permanent diagnostic dashboard.

## 13. Reusable pump Dynamo

Create one canonical Dynamo definition, e.g.:

`eee.dynamo.pump`

Use stable ID and reusable internal elements.

The definition must support per-instance binding through canonical public parameters / `{equipmentPath}` context. Two overview instances must bind independently to P01 and P02.

At minimum the Dynamo must project:

- equipment label/identity;
- Running;
- Fault/Trip;
- Available/Unavailable or selected canonical quality state;
- frequency/current/flow summary when useful;
- an interaction opening the correct contextual Popup.

State precedence for the visible symbol must be deterministic:

1. non-Good/unavailable quality or unavailable equipment;
2. fault/trip;
3. running;
4. stopped.

Critical states must include text/symbol semantics, not color only.

C11 must add a deterministic browser/runtime test with **two simultaneous instances** proving that changing P01 does not accidentally change P02 and vice versa.

## 14. Pump Popups

Use contextual Popups for P01 and P02. If parameterized Popup identity is not a canonical supported contract, two Popup definitions are acceptable as long as they reuse the same visual pattern and normal TAG bindings.

Each Popup should show:

- equipment state;
- quality;
- Running/Fault/Trip/Available;
- frequency;
- current;
- flow;
- pressure;
- mode/duty context;
- permitted Commands;
- close action.

Persist logical Popup X/Y using the C16 contract. No CSS-only positioning.

## 15. Trend

Embed first-class `core.trend` objects, not the legacy permanent shell trend.

Minimum Multi-Pen process Trend:

- LevelPct;
- InflowM3h;
- TotalFlowM3h;
- optionally pressure.

A second electrical/drive Trend may show P01/P02 CurrentA and FrequencyHz.

Use persisted `browserConfig.pens` with stable TAG IDs, meaningful labels/units, live + historical mode and sensible scales.

## 16. Alarm Browser / Event Browser

Embed first-class C18 objects:

- `core.alarmBrowser`;
- `core.eventBrowser`.

Place them on `eee.operation` or an operator support Popup/Screen where they remain readable within 1920×1080 logical composition.

Use persisted per-instance `browserConfig`, ordinary protected backend query/mutation authority and current pt-BR/en/es product chrome.

## 17. Visual assets and design

Use ordinary project-owned assets where they improve presentation. Assets must survive Save/Publish/Activate and be carried by the normal package model.

Do not encode the entire Screen as one static screenshot. Important process objects must remain live, bound and interactive.

Shell Dark/Light theme must not recolor authored process artwork unpredictably.

## 18. Application-level automated acceptance

Add dedicated C11 browser/runtime coverage. Minimum required proof:

### Construction/lifecycle

- canonical builder/import Preview has zero errors;
- Apply succeeds;
- Save -> Publish -> Activate succeeds;
- `/api/runtime/application` reports exact project/revision;
- StartupScreenId resolves to `eee.overview`;
- exported `.escadapkg` Preview-imports cleanly.

### Living process

- level changes over time without browser-side simulation;
- start threshold starts the configured duty pump;
- pump flow causes level to decrease;
- cycle completion alternates duty;
- high demand can cause two-pump operation;
- fault injection stops/faults the affected pump and allows deterministic fallback when possible;
- quality scenario produces canonical non-Good quality;
- reset/disable scenarios recover deterministically.

### Alarm/Event/History

- high/high-high or fault alarm becomes Active from process state;
- acknowledgement uses normal protected alarm interaction when exercised;
- process transitions generate configured C14 Operational Events;
- Event Browser reads those canonical events;
- Historian receives evolving analog values;
- Trend obtains live and historical samples.

### HMI

- overview starts automatically from persisted stable ID;
- Analog Fill changes with LevelPct;
- two reusable pump Dynamo instances render simultaneously with independent state;
- fault/unavailable state has non-color-only indication;
- contextual Popup opens at persisted logical coordinates;
- canonical Command buttons execute through `/api/commands/{id}/execute` indirectly via HMI action;
- screen navigation remains in Runtime logical viewport;
- Alarm Browser/Event Browser are embedded normal HMI objects.

### Scaling repository-side

At least automate representative 1280×720 and 1920×1080 geometry/hit-target checks. Final Product Owner homologation will additionally inspect 2560×1440, 3840×2160 and mismatched aspect ratio visually after the DEMO is ready.

## 19. Determinism / anti-flake rules

- no `Math.random`, wall-clock-dependent process equations or unbounded sleeps;
- fixed 1 s process tick;
- fixed constants and initial state;
- state transition tests use polling on canonical TAG/runtime state rather than arbitrary long sleeps;
- use bounded timeouts;
- do not weaken existing workflow/test timeouts to accommodate a slow DEMO;
- every scenario must have an explicit reset/recovery path.

## 20. Package/provenance deliverables

After repository-side acceptance, produce:

- canonical `.escadapkg` exported from the accepted Active project;
- human-readable provenance JSON/Markdown containing project key/name, source PRODUCT SHA, C11 DEMO commit SHA, package SHA-256 and construction method;
- deterministic application-level tests;
- updated Preview launcher/fixture pointing to the new canonical package rather than historical Wave11 DEMO;
- C11 progress/handoff documentation with exact CI run IDs.

Do not overwrite historical fixture provenance silently; the old Wave11 DEMO remains historical evidence.

## 21. Validation gates

Every product-code change requires the ordinary universal/specialized gates dictated by `docs/CI-VALIDATION-POLICY.md`.

A DEMO-only application/fixture/test change still requires at minimum the relevant build/browser/lifecycle lane and any workflow whose path/contract is affected. Before isolated C11 acceptance, Coordinator must explicitly review exact changed files and ensure the application does not smuggle product behavior into fixture/test code.

Any red must be diagnosed before rerun.

## 22. Hard stop rule

If any required behavior cannot be achieved using the normal product contracts described above, **stop the DEMO workaround** and report a new PRODUCT GAP.

Open a separate narrow generic correction package, validate it on exact bytes, compose it into integration, then rebase/restart C11 from the new accepted product authority as appropriate.

The DEMO is evidence of the product. It is not permission to build a second product inside a fixture.
