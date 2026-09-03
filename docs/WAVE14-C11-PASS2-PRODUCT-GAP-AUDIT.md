# W14-C11 — Pass 2 Product-Gap Audit

**Owner:** W14-C11 audit lane  
**Coordinator:** Wave 14 Coordinator / Development Lead  
**Product Owner:** source of approved DEMO intent and supplied real EEE engineering reference  
**State:** PASS 2 AUDIT WORKSPACE / IMPLEMENTATION LOCKED  
**Audit product-code SHA:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`

> This file is the canonical output workspace for C11 Pass 2. It records what the converged EliteSCADA product actually supports for the future canonical EEE DEMO. It must not silently rewrite or narrow the approved DEMO requirements.

## 1. Authority, provenance and boundaries

Requirements authority:

- `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`

Pass 2 release/freeze authority:

- `docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`

C11 audit branch:

- `wave14/c11-pass2-product-gap-audit`

The audit tests the product behavior represented by exact product-code SHA:

- `97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Documentation-only Coordinator commits after that SHA do not redefine the product under audit.

C11 implementation remains locked. This branch is not an implementation branch and must not contain the canonical `.escadapkg`, DEMO-specific product code, Preview rewiring, or workaround code.

### 1.1 Evidence discipline

This audit intentionally separates four sources of information:

1. **current product capability** — accepted only when revalidated against the frozen product SHA through code, contracts, APIs, tests or real Runtime/browser behavior;
2. **Product Owner requirement** — authoritative for what the future canonical EEE application must demonstrate;
3. **implementation proposal** — useful design direction, but not treated as a frozen product fact unless explicitly approved;
4. **uncertainty** — remains `NEEDS VALIDATION` until the required proof is executed.

A feature is not `SUPPORTED` merely because a DTO, enum, interface or document mentions it. The normal Engineering/Runtime path must be usable by an integrator without editing product internals, hand-authoring hidden package JSON, knowing undocumented driver identifiers, manipulating DOM/React directly, or depending on a DEMO-only fixture.

Conversely, a capability that exists technically but is exposed through an incoherent or misleading authoring/operator experience may be recorded as a **product UX gap** even when it does not technically block the DEMO. C11 is being used deliberately to expose product weaknesses before the canonical application is implemented.

### 1.2 Real EEE engineering reference already supplied by Product Owner

The Product Owner supplied real-world EEE material during requirements intake, including HMI captures, a TAG export and an alarm export. The reference application is not compatibility authority and is not to be copied pixel-for-pixel. It is engineering evidence for process content and realistic operator expectations.

The supplied HMI reference covered, among other things:

- overall EEE process layout;
- suction/wet well;
- two pumping units;
- common discharge;
- level, flow and pressure;
- level/setpoint adjustment concepts;
- alarms;
- electrical measurements;
- instrument measurements;
- digital and analog I/O diagnostics;
- generator/source information;
- hours/totalizers;
- pressure configuration;
- control/status information.

The supplied TAG export contained approximately 120 historical FvDesigner TAGs and demonstrated real process concepts such as level thresholds, pump running/fault status, VFD frequency/current, analog failures, generator/source states, pressure protections, command/reset points and operating counters. Historical address strings from that export are **not** automatically interpreted as current EliteSCADA addressing contracts.

The supplied alarm export contained approximately 28 historical alarm definitions, including low/high/very-high/overflow level, emergency, UPS, analog input failures, VFD faults, pressure abnormality and communication failures. Some historical entries, such as ordinary pump-running state, may be more appropriate as events in the canonical product if the current event model supports that distinction.

## 2. Required classification

Every canonical requirement is classified exactly as one of:

- `SUPPORTED`
- `PARTIALLY SUPPORTED`
- `PRODUCT GAP`
- `NEEDS VALIDATION`

For audit readability, a `PRODUCT GAP` may additionally be described by product surface, without changing the mandatory classification, for example:

- `PRODUCT GAP — FUNCTIONAL`
- `PRODUCT GAP — ENGINEERING UX`
- `PRODUCT GAP — RUNTIME UX`
- `PRODUCT GAP — AUTHORIZATION/BOUNDARY`

Every non-`SUPPORTED` finding includes, as evidence becomes available:

1. requirement;
2. exact product evidence / code / API / test / Runtime behavior;
3. current limitation;
4. impact on the canonical EEE DEMO;
5. Simulation impact;
6. PLC/Modbus impact;
7. recommended owner/action;
8. whether bounded product correction is recommended before DEMO implementation;
9. any safe mitigation, clearly separated from a real product fix.

Pass 1 findings are re-tested. Findings solved by C09/C10 are marked `RESOLVED/SUPERSEDED` with evidence rather than disappearing.

## 3. Product Owner intent that must not be narrowed by this audit

The future canonical application is an **EEE — Estação Elevatória de Esgoto** intended to function simultaneously as:

- commercial/product DEMO;
- Engineering authoring example;
- operator Runtime demonstration;
- Product Owner homologation application;
- regression/acceptance application;
- canonical example of normal EliteSCADA workflows;
- later proof against a physical PLC using Modbus.

### 3.1 Main process experience

The primary Runtime must be strongly visual and must make process state understandable without relying on tables alone.

At minimum:

- two pumps/motors;
- clear stopped, running, fault/trip, unavailable and bad-quality states;
- visual state changes must be immediate and critical states must not rely only on color;
- suction well with analog level;
- numeric `% Full`;
- visibly animated liquid rising/falling with process value;
- flow, pressure, current, frequency and other coherent process values;
- contextual Popups;
- reusable Dynamos with public properties and TAG bindings;
- alarms, events, trends and history through normal product surfaces;
- operator/Engineering boundary;
- project-owned PNG/background assets through canonical asset mechanisms.

Expected Screens currently include at least:

- `Visão Geral / EEE Principal`;
- `Instrumentação`;
- `Sistema Elétrico`;
- `Operação`.

Additional operator alarm/trend/support views may be added when justified by the actual product contracts.

### 3.2 Required Simulation behavior

`DEMO Simulation` must be visibly alive through supported EliteSCADA mechanisms. It must not be a static fixture displaying decorative values.

The intended behavior includes:

- process inflow raising well level;
- running pump(s) lowering well level;
- automatic start/stop thresholds;
- duty/standby alternation between the two pumps;
- second-pump demand under higher inflow/level conditions;
- pump fault/trip injection;
- unavailable/bad-quality scenario;
- current/frequency/flow/pressure changing coherently with equipment/process state;
- alarms occurring from simulated process conditions;
- historian/trend data evolving over time;
- deterministic/reproducible behavior sufficient for regression and homologation.

The preferred state model uses shared/authoritative process state for physical simulation and reserves client-local memory for client UI state. The exact implementation mechanism remains subject to this audit and later implementation release.

### 3.3 Required PLC/Modbus transition

`DEMO PLC` is a later validation stage against a physical PLC using Product Owner supplied Modbus addresses.

The desired architecture preserves as much as technically possible:

- Screens;
- Dynamos;
- Popups;
- conceptual TAG identities;
- alarms;
- trends/history;
- visual logic;
- operator experience.

The intended difference is primarily source/address mapping rather than building a second HMI. A Simulation design that hard-couples visual objects to simulator-only internals is therefore unacceptable even if the Simulation variant itself looks correct.

## 4. Consolidated matrix — progressive Pass 2 state

The table below is intentionally progressive. `NEEDS VALIDATION` rows are not evidence of failure; they identify proof still required before C11 implementation can be recommended.

| ID | Requirement / capability | Classification | Evidence at frozen SHA | Current behavior / limitation | EEE impact | Simulation impact | PLC/Modbus impact | Recommended disposition |
|---|---|---|---|---|---|---|---|---|
| C11-P2-GOV-01 | Exact converged product checkpoint for Pass 2 | SUPPORTED | Frozen release documentation and repository history identify `97eefd8f4377ff583d1ba20bc89203f4a82b584d` as C10 converged product code. Coordinator documentation after it is documentation-only. | Stable authority for this audit. | Prevents auditing a moving target. | Same frozen contracts. | Same frozen contracts. | no action required |
| C11-P2-DS-01 | Backend-authoritative Data Source configuration forms rather than opaque config string | SUPPORTED | Engineering Data Source editor consumes backend type/catalog configuration schemas and renders typed controls rather than requiring a developer-known arbitrary configuration string. | Driver-specific fields can be projected from catalog schema. | Normal integrator authoring path exists for driver/source configuration. | Memory providers still require separate creation-path validation below. | Supports normal Modbus Source configuration model. | no action required; keep regression coverage |
| C11-P2-TAG-01 | Protocol-aware TAG address assistance | SUPPORTED | `TagAddressEditor` has specialized assistants for Modbus TCP, OPC UA, DNP3 and IEC-104, with generic fallback for other cases. | Major industrial protocols do not require the user to memorize canonical address syntax. | Supports realistic TAG authoring. | Internal Memory must not be forced through protocol-address semantics. | Directly supports later PLC mapping, subject to real PLC validation. | no action required; retain protocol-specific validation |
| C11-P2-MEM-01 | Canonical Server Memory / Client Memory TAG concepts | SUPPORTED | Product contracts and Engineering memory settings recognize `builtin.memory.server` and `builtin.memory.client`; memory settings expose typed initial/default value semantics and explicitly describe no network address. | Product distinguishes shared server memory from client-local memory. | Provides a canonical basis for internal state rather than DEMO-only variables. | Server/shared memory is the appropriate candidate for common simulated process state; client memory remains suitable for client-only UI state. | Logical TAG model can remain distinct from physical Modbus source. | no action required on the memory model itself |
| C11-P2-MEM-02 | Internal Memory TAG authoring must not expose meaningless network `Address` semantics | PRODUCT GAP | Frozen `TagAddressEditor` renders the generic Address editor path independently of the Internal Memory-specific settings panel. Internal Memory is not one of the protocol-specialized assistant branches, while `MemoryTagSettingsPanel` correctly states that no network address exists. | Two parts of Engineering expose conflicting concepts: the memory panel says no network address, while the general TAG editor still presents address semantics. | An integrator sees an irrelevant field when authoring canonical memory TAGs. This is a product UX defect, not a DEMO-specific inconvenience. | Directly affects normal creation of Simulation memory TAGs. | No direct protocol impact, but fixing the UX clarifies the separation between internal and physical Sources. | **fix before C11**; hide/replace Address binding UI for Internal Memory through normal source capability/schema semantics |
| C11-P2-MEM-03 | Human-facing creation of both Server Memory and Client Memory Data Sources without knowing `builtin.memory.*` identifiers | NEEDS VALIDATION | Memory provider identities exist and memory TAG settings recognize both. Pass 2 must still prove both providers are published through the backend-authoritative Data Source catalog and are creatable through the normal UI. | Structural support is present; end-to-end human authoring remains unproven in this audit. | C11 must not ship a package that only works because a developer manually inserted provider identifiers. | Potentially important for Simulation authoring. | None directly. | requires real-browser/E2E validation; fix before C11 if either provider cannot be created normally |
| C11-P2-MEM-04 | E2E creation of Internal Memory Source then TAG from Engineering | NEEDS VALIDATION | Existing memory-focused test evidence observed in Pass 1 exercised a package where memory identity was already present rather than proving complete Source creation from the UI. Must be revalidated against frozen SHA. | Creation flow is not yet proven end-to-end. | Product should support an integrator from empty project to working memory TAG. | Could become blocking if authoring requires package editing. | None directly. | requires real-browser/E2E validation |
| C11-P2-VIS-01 | Analog `% Full` value | SUPPORTED | Canonical numeric TAG/binding/property infrastructure plus analog-fill scale contract support numeric process values and normalized fill. | Numeric level can drive display and visual behavior. | Required for main EEE Screen. | Simulation can publish analog level. | PLC analog level can map to same conceptual TAG. | no action required; validate final Runtime presentation |
| C11-P2-VIS-02 | Animated suction-well liquid fill driven by process value | SUPPORTED | Canonical Analog Fill engineering/runtime contract supports input min/max, clamp/invert and BottomToTop/TopToBottom/LeftToRight/RightToLeft fill projection. | Product has a first-class visual mechanism rather than requiring DOM/CSS manipulation. | Enables the key animated wet-well experience. | Simulated level can drive the same visual binding. | PLC level can later drive the same binding. | no action required structurally; requires real-browser validation for visual quality |
| C11-P2-DYN-01 | Reusable Dynamo definitions with typed public properties/TAG references | SUPPORTED | Dynamo contracts/composition reviewed in Pass 1/2 expose reusable definitions, typed public parameters including TAG reference/equipment-path concepts, per-instance identity and internal composition. | Same pump definition can represent multiple equipment instances without duplicating all internal objects. | Supports one canonical pump Dynamo for GMB-01/GMB-02. | Simulation instance bindings can target logical simulated TAGs. | Same definition should survive change to Modbus-backed logical TAGs. | no action required structurally; continue authoring/browser validation |
| C11-P2-DYN-02 | Pump visual states including bad quality/fault semantics | PARTIALLY SUPPORTED | Runtime Dynamo/state projection includes semantic states such as active/inactive, fault/alarm, bad-quality, transition and feedback mismatch. Visual dynamics also protect against blindly driving state from non-Good samples. | Product has semantic state vocabulary, but Pass 2 has not yet proven that normal Engineering authoring and final operator presentation make bad quality sufficiently explicit and non-color-only. | Needed for stopped/running/fault/unavailable/bad-quality pump representation. | Simulator must be able to produce the quality/state path through public mechanisms. | Real driver quality must project through the same visual contract. | requires real-browser validation and Simulation quality-injection audit; fix product UX before C11 if bad quality is ambiguous |
| C11-P2-POP-01 | Canonical contextual Popup open/close with equipment context | SUPPORTED | Canonical Runtime action model includes `OpenPopup` / `ClosePopup` and contextual parameters/target identity. | Popup opening is part of the normal project/runtime model rather than a DEMO-only modal. | Enables pump/instrument detail Popups. | Same Popup can show simulated logical TAGs. | Same Popup can reuse PLC-backed logical TAGs. | no action required structurally; validate actions/commands/browser behavior |
| C11-P2-POP-02 | Persisted authorable Popup X/Y placement | NEEDS VALIDATION | Known C07/C09-era concern: Popup mount/action contracts inspected so far do not expose a clear persisted per-instance authorable X/Y placement contract. Pass 2 must close this against the frozen C10 product before declaring a gap. | Contextual placement may currently be shell-defined/centralized rather than authorable. | May reduce desired equipment-context presentation. | Same limitation in PLC variant. | revalidate exact persisted definition + Property Inspector + Runtime mount/CSS; if absent classify PRODUCT GAP and request Development Lead disposition |
| C11-P2-NAV-01 | Explicit authorable Runtime startup/home Screen | NEEDS VALIDATION | Pass 2 preliminary inspection indicated Runtime may derive initial Screen from deterministic Screen ordering rather than a persisted project startup-screen contract. Must be confirmed at exact frozen contracts/tests before final gap classification. | Naming tricks such as `00_Overview` are not an acceptable substitute for product-level startup configuration if the product requirement expects a chosen home Screen. | Main EEE Screen should open deterministically as intended. | Same behavior regardless of Source. | revalidate projected package contract + navigator; if confirmed absent, classify PRODUCT GAP and **fix before C11** |
| C11-P2-VIEW-01 | Fixed logical HMI composition scaled into common Runtime resolutions | PARTIALLY SUPPORTED | C09/C10 Runtime uses a fixed logical HMI composition model and viewport scaling rather than responsive DOM reflow. Preliminary inspection observed a 1920x1080 logical composition assumption. | Preserves authored coordinates, but per-Screen logical resolution authoring has not been demonstrated. | Expected design can target canonical logical canvas and scale to 720p/1080p/1440p/4K. | Same. | Same. | requires real-browser validation at required resolutions; Development Lead decision only if per-Screen logical resolution becomes a requirement |
| C11-P2-ASSET-01 | Import/use project PNG/background assets through normal Engineering | SUPPORTED | Engineering visual workspace supports import of common raster assets and Surface/visual settings expose asset selection/preview and fit modes. | Project assets are available through canonical authoring rather than arbitrary filesystem paths. | Allows polished EEE artwork/backgrounds. | No simulation coupling. | No PLC coupling. | no action required; validate persistence Save->Publish->Activate->Runtime |
| C11-P2-SCR-01 | Canonical Server-side script model with timer/event semantics for a living process | NEEDS VALIDATION | Script contracts inspected during Pass 1/2 include Server scope and timer/server-runtime-event concepts plus mediated memory/TAG capability models. Static inspection has not yet proven the complete activated Runtime host/executor/scheduler lifecycle at the frozen SHA. | Contracts appear designed for the required model, but C11 cannot call it supported until scripts actually execute continuously after Publish/Activate through public product wiring. | **Potential blocker** for coherent autonomous Simulation. | Central to level evolution, pump logic, analog values and scenario injection if Server Script is the chosen canonical mechanism. | PLC variant may need less simulation logic, but script/runtime contract still matters for canonical project behavior. | **keep implementation locked until closed**; inspect host composition, executor, scheduler, activation lifecycle and tests; fix before C11 if wiring is absent |
| C11-P2-QUAL-01 | Official Simulation path to produce unavailable/bad/stale quality | NEEDS VALIDATION | Runtime visual/state contracts understand bad quality, but Pass 2 has not yet proven a public Simulation/Memory/Script API that can intentionally publish required quality states without internal host bypass. | DEMO must visibly demonstrate communication/quality loss honestly. | Potential blocker for mandatory bad-quality Simulation scenario. | Real PLC/driver quality is expected to originate from driver/runtime paths and requires later PLC proof. | audit public quality-write/simulation capability; fix before C11 if bad quality cannot be produced canonically in Simulation |
| C11-P2-RUNTIME-01 | Canonical Active Engineering Runtime, not hard-coded simulation fallback, is authority for C11 | SUPPORTED | Product distinguishes canonical Runtime backed by persisted Active Engineering from legacy/fallback simulation presentation. Existing fallback is not accepted as evidence for C11. | Prevents a visually convincing DEV-only page from masquerading as product capability. | C11 must go through normal Save/Publish/Activate/Runtime lifecycle. | Simulation must live inside canonical product contracts. | PLC variant likewise uses canonical Runtime. | no action required; preserve audit guardrail |
| C11-P2-FULL-01 | Fullscreen/no-document-scroll operator presentation | NEEDS VALIDATION | C09/C10 shell contains fullscreen/viewport/overflow behavior, but required multi-resolution browser behavior must be verified in a real browser. | Important for operator-quality presentation. | Same. | Same. | requires real-browser validation at 1280x720, 1920x1080, 2560x1440 and 3840x2160 |
| C11-P2-PLC-01 | Reuse conceptual HMI while swapping Simulation source/mapping to Modbus PLC | NEEDS VALIDATION | Product architecture separates visual bindings/logical TAG identity from Source/Driver concepts and provides Modbus address assistance. Complete authoring migration path still needs proof without rebuilding Screens/Dynamos/Popups. | Core requirement for one canonical HMI rather than two unrelated projects. | Simulation architecture must avoid simulator-only visual coupling. | Later physical PLC validation is mandatory. | audit stable TAG identity/source remapping workflow now; requires PLC validation later |

## 5. Pass 1 findings revalidation register

| Pass 1 finding | Pass 2 state | Classification | Disposition / consequence |
|---|---|---|---|
| Internal Memory exists but authoring semantics may be inconsistent | **CONFIRMED in UX scope** | PRODUCT GAP for generic Address exposure; memory model itself SUPPORTED | Fix Engineering UX before C11; do not invent an address wizard for a source that has no network address |
| Human creation of Server/Client Memory Sources was not proven | **OPEN** | NEEDS VALIDATION | Prove both from normal Data Source catalog/UI; fix if either requires package editing/internal IDs |
| Server Script + Timer contracts may lack complete Runtime host/wiring | **OPEN / high priority** | NEEDS VALIDATION | Potential C11 blocker; close before implementation recommendation |
| Wet-well analog fill may require custom DOM/CSS | **RESOLVED structurally** | SUPPORTED | Canonical Analog Fill contract exists; browser quality remains validation-only |
| Pump/Dynamo state model may not expose reusable bad-quality/fault semantics | **RECLASSIFIED** | PARTIALLY SUPPORTED | Semantic runtime states exist; Engineering authorability and explicit operator bad-quality indication remain to validate |
| Reusable Dynamo public properties/TAG bindings uncertain | **RESOLVED structurally** | SUPPORTED | Continue UX/browser validation, but no structural gap currently identified |
| Popup open/context support uncertain | **RESOLVED structurally** | SUPPORTED | Open/close/context contract exists |
| Popup authorable X/Y placement absent/unclear | **OPEN** | NEEDS VALIDATION | Must inspect persisted Popup definition, authoring and Runtime mount before final disposition |
| Startup/home Screen may not be persisted | **OPEN** | NEEDS VALIDATION | If navigator truly selects by ordering rather than configured home Screen, classify product gap and fix before C11 |
| Fixed 1920x1080 logical Runtime canvas | **RECLASSIFIED** | PARTIALLY SUPPORTED | Deliberate fixed logical composition is compatible with EEE if scaling/no-scroll passes browser validation; per-Screen logical size remains a product limitation unless made a requirement |
| Project PNG/background asset flow uncertain | **RESOLVED structurally** | SUPPORTED | Normal project import/selection exists; validate lifecycle persistence |
| Legacy/fallback EEE-like simulation presentation could mask missing product capabilities | **CONFIRMED as audit guardrail** | SUPPORTED boundary | Explicitly excluded from C11 evidence/implementation |
| Bad-quality visual behavior/injection incomplete | **OPEN** | NEEDS VALIDATION | Must prove both clear operator state and public Simulation path for quality degradation |

## 6. Required focused checks still open

The following checks remain mandatory before final recommendation:

### 6.1 Engineering authoring and discoverability

- create `Server Memory` Source from an empty project through normal UI;
- create `Client Memory` Source from an empty project through normal UI;
- confirm human-facing display names and schema-driven forms, with no need to type internal provider IDs;
- create memory TAGs without meaningless Address semantics;
- confirm protocol-specific Driver/Source forms for all drivers relevant to the canonical example;
- confirm Modbus TAG address assistant normalizes/validates the later PLC address model;
- verify Property Inspector consistency between Screen, Popup and Dynamo authoring;
- verify Dynamo public-property/TAG-binding editing is understandable without exposing internal children/contracts;
- verify asset import and project lifecycle persistence.

### 6.2 Simulation/script/runtime

- locate and prove concrete activated Runtime hosting for Server Scripts;
- prove timer scheduling and script lifecycle after Publish/Activate;
- prove mediated Server Memory and TAG reads/writes from the supported scripting surface;
- prove no direct Driver/DOM/internal-host bypass is necessary;
- identify official mechanism to publish bad/stale/unavailable quality in Simulation;
- prove deterministic process update cadence suitable for regression;
- prove historian/alarm/event ingestion from the chosen simulated process state model.

### 6.3 Runtime visuals and operator UX

- validate Analog Fill visually with a changing analog level;
- validate pump stopped/running/fault/unavailable/bad-quality states and precedence;
- confirm critical state is understandable without color alone;
- validate Popup open/close/context behavior;
- close the persisted authorable Popup X/Y question;
- close the explicit startup/home Screen question;
- validate Runtime navigation, fullscreen, logical scaling and no document scroll at required resolutions;
- validate operator-only capabilities and absence of Engineering/Diagnostics leakage;
- validate alarm/operator overlays and Popup stacking/focus behavior.

### 6.4 Alarm/event/history/trend semantics

- create/activate process alarms from canonical project definitions;
- validate acknowledgement path and authorization;
- validate alarm history/persistence;
- identify and validate event-history surface for ordinary state transitions where appropriate;
- validate historian recording of meaningful analog variables;
- validate operator trend/history consultation without relying on Engineering-only Diagnostics;
- decide, based on actual product support, which historical reference conditions belong as alarms versus events in the canonical EEE model.

### 6.5 Multilingual

Audit `pt-BR`, `en`, and `es` for the surfaces required to build and operate the EEE, including:

- Data Sources/Drivers;
- TAG authoring/address assistants;
- Screen/Popup/Dynamo Engineering;
- Property Inspector;
- Script Assistant/Object Browser;
- Runtime shell/navigation/fullscreen;
- operator alarms/trends/history;
- Diagnostics where Engineering users require it;
- dialogs, errors, empty states and context menus.

Persisted identifiers, protocol tokens, canonical property names, TAG keys, NodeIds and addresses remain data and must not be translated.

### 6.6 Simulation -> PLC/Modbus reuse

- prove logical TAG identity can remain stable while Source/address mapping changes;
- prove visual bindings/Dynamos/Popups remain intact across that mapping change;
- identify any simulator-specific coupling that would force duplicate HMI authoring;
- record what can be proven statically now versus what explicitly requires physical PLC validation later.

## 7. Preliminary supported capabilities

The following are structurally supported at this stage of Pass 2, subject to any explicit validation notes in the matrix:

- backend-authoritative schema-driven Data Source forms;
- protocol-aware TAG address assistance for Modbus/OPC UA/DNP3/IEC-104;
- Server Memory and Client Memory concepts;
- typed Internal Memory initial/default values;
- numeric process values and canonical Analog Fill;
- reusable Dynamo definitions and typed public references;
- semantic Dynamo/runtime state vocabulary including fault and bad-quality concepts;
- canonical Popup open/close/context action model;
- normal project raster asset import/selection;
- canonical Active Engineering Runtime boundary rather than DEMO-only fallback.

## 8. Preliminary partial capabilities

- bad-quality state projection is structurally present, but explicit operator UX and Simulation quality injection remain incomplete proofs;
- fixed logical Runtime canvas/scaling appears aligned with the intended HMI model, but multi-resolution browser validation is still required and per-Screen logical resolution is not currently treated as proven capability;
- several Runtime/Engineering contracts exist for scripts, but complete autonomous Server Script execution after activation remains unproven.

## 9. Confirmed product gaps so far

### C11-P2-MEM-02 — Internal Memory exposes generic Address semantics

**Classification:** `PRODUCT GAP — ENGINEERING UX`

**Requirement:** An engineer creating a Server Memory or Client Memory TAG must not be asked for a network/protocol Address that does not exist.

**Expected behavior:** Selecting an Internal Memory Source should present only meaningful memory/TAG settings such as type, initial/default value, access/persistence/history settings where applicable, without communication-address authoring.

**Current product behavior:** Internal Memory-specific UI correctly explains that no network address exists, while the general TAG address editor still exposes generic Address semantics because Internal Memory is not excluded/specialized in that path.

**EEE impact:** Creates confusing and conceptually incorrect authoring in the exact flow needed by DEMO Simulation.

**Simulation impact:** Direct. C11 should be constructible normally without teaching the Product Owner/integrator which meaningless field to ignore.

**PLC/Modbus impact:** None directly. The correction improves the conceptual boundary between internal and physical Sources.

**Recommended disposition:** `fix before C11`.

**Mitigation:** Leaving the irrelevant field unused may technically permit authoring, but that is not considered an acceptable product-level resolution for the canonical DEMO gate.

## 10. Validation-only uncertainties

Current high-priority uncertainties include:

1. complete Server Script Runtime host/executor/timer lifecycle;
2. official Simulation quality-degradation/injection path;
3. normal UI creation of both Internal Memory Source types;
4. persisted authorable Popup X/Y positioning;
5. explicit startup/home Screen configuration;
6. alarm/event/history/trend operator path;
7. authenticated/authorized equipment command path from Dynamo/Popup;
8. Runtime-only capability pruning and backend enforcement;
9. multilingual browser quality across all C11-relevant surfaces;
10. Simulation-to-Modbus remapping without duplicated HMI authoring;
11. viewport/fullscreen/no-scroll behavior at all required resolutions.

## 11. Blocking gaps / potential blockers

### 11.1 Confirmed blockers

No final blocking PRODUCT GAP is declared solely from static evidence yet except where the Coordinator/Development Lead chooses to make an already-confirmed UX correction a release prerequisite.

The Product Owner has explicitly requested that product gaps exposed by this audit be corrected where appropriate before C11 implementation rather than hidden inside the DEMO. Therefore even non-runtime-blocking UX gaps may legitimately keep implementation locked until disposition.

### 11.2 Potential blocking findings requiring closure

The following are potential blockers until proven:

- Server-side periodic script execution after Publish/Activate;
- canonical bad-quality Simulation injection;
- normal authoring of Internal Memory Sources;
- authenticated/authorized Runtime command path needed by equipment Popups/Dynamos;
- any missing product contract that would force Simulation-only visual architecture and prevent later Modbus reuse.

## 12. Non-blocking gaps / limitations

At this stage:

- fixed logical 1920x1080-style composition is treated as a product limitation/strategy rather than an automatic C11 blocker, provided required viewport scaling and no-scroll behavior pass browser validation;
- lack of custom Popup placement, if ultimately confirmed, may be non-blocking for an initially centered/modal Popup experience, but must still be recorded as a product gap if the persisted authoring requirement is absent. Only Development Lead may accept that limitation knowingly.

## 13. Current Coordinator recommendation

### `KEEP C11 IMPLEMENTATION LOCKED`

Reason:

- Pass 2 is not complete;
- at least one real Engineering UX product gap has been confirmed;
- several potentially blocking runtime/simulation/authorization findings remain unresolved;
- implementation before those findings are dispositioned would invite DEMO-specific workarounds and contaminate the product audit.

This recommendation is provisional until the complete matrix is closed. C11 itself does not release implementation.

## 14. Future DEMO intent, not implementation authority

The Coordinator intends the later canonical application to be a realistic, visually strong EEE demonstration that proves EliteSCADA through normal Engineering and Runtime workflows rather than through a pre-baked webpage.

The planned application direction remains:

- primary `Visão Geral / EEE Principal` with suction well and two pumping units;
- animated well liquid level and numeric `% Full`;
- motor/pump state projection for stopped, running, fault/trip and unavailable/bad quality;
- realistic analog process values and coherent relationships between level, flow, pressure, frequency and current;
- contextual equipment Popups;
- reusable equipment Dynamos rather than duplicated hand-authored symbols;
- support Screens for Instrumentação, Sistema Elétrico and Operação;
- real alarms, events, trends and history where supported by operator-facing product contracts;
- project PNG/background/assets where useful for a polished industrial presentation;
- multilingual `pt-BR` / `en` / `es` experience;
- a deterministic but visibly alive Simulation variant using supported EliteSCADA mechanisms;
- a later PLC/Modbus variant using Product Owner supplied addresses while preserving the same conceptual Screens, Dynamos, Popups and TAG model as far as product contracts allow.

### 14.1 Process scenarios already accepted for audit purposes

The audit must ensure the product can support at least the following canonical behaviors without DEV-only bypass:

- normal fill/pump-down cycle;
- duty pump alternation between GMB-01 and GMB-02;
- high inflow requiring both pumps where process logic chooses;
- GMB-01 fault/trip with GMB-02 takeover when available;
- very-high level;
- overflow condition;
- bad-quality/communication-loss scenario;
- alarm activation, acknowledgement where permitted, process recovery and historical persistence.

Pressure abnormality and generator/source scenarios remain useful secondary scenarios if supported naturally by the product and final implementation premise.

### 14.2 Conceptual reusable equipment direction

The Product Owner expects repeated equipment to exercise canonical reuse rather than duplicated authored objects. Candidate definitions include:

- VFD pump/motor Dynamo;
- wet-well level Dynamo/visual composition;
- generic instrument/value Dynamo;
- electrical source/generator representation where retained in final scope.

Exact names, parameter sets, visual palette and thresholds are **not frozen by this audit** unless later recorded by explicit Product Owner/Coordinator decision.

## 15. Post-audit Coordinator flow

After this file is complete:

`C11 Pass 2 result -> Coordinator/Development Lead gap disposition -> bounded pre-DEMO product corrections if required -> exact-head convergence revalidation if product changes -> explicit C11 implementation release -> docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md -> DEMO Simulation -> PLC/Modbus validation -> full CI -> Preview/Codespaces -> Product Owner browser homologation`

The implementation premise document becomes the authoritative memory of the application only after explicit implementation release. Until then this file remains the canonical memory of the product-gap audit.