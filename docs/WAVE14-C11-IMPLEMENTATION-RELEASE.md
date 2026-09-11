# Wave 14 C11 — Implementation Release

**Decision date:** 2026-09-04 BRT  
**Coordinator decision:** **RELEASE C11 IMPLEMENTATION**  
**Exact implementation product base:** `3fda88061df35ad14755d22881e5d3a9216d1ff5`  
**Product tree:** `da6b406ac111cb40b99e5b13031601eb71606ddd`  
**Implementation branch:** `wave14/c11-canonical-eee-demo`  
**Integration surface:** `wave14/corrections-integration` / PR #212 DRAFT  
**Final Product Owner Codespace homologation:** deferred until after canonical DEMO creation by explicit Product Owner decision.

> GitHub is the official memory. Documentation-only commits after the exact product base do not redefine C11 implementation authority.

## 1. Release decision

`RELEASE C11 IMPLEMENTATION`

C11 is released to construct the new canonical living EEE DEMO from exact product baseline `3fda880...`.

This release means the product capability blockers classified `fix before C11` in Pass 2 have been corrected, composed and coherently validated. It does **not** mean final Wave14 Product Owner acceptance has occurred.

The Product Owner explicitly decided that the real fresh-Codespace visual homologation will occur **after the canonical DEMO exists**, so the remaining visual/use evidence is intentionally evaluated on the completed application rather than used to block its construction.

## 2. Release-gate closure

The canonical C11 release gate is satisfied as follows:

1. C09 operator/runtime corrections are already part of the accepted integration lineage.
2. C12–C19 are composed into one exact pre-DEMO product baseline: `3fda880...`.
3. Required multilingual pre-DEMO corrections are present, including browser/history surfaces delivered by C18 and affected C19 Engineering chrome.
4. Pass 2 product-gap audit and Product Owner HMI-object clarification are consolidated and preserved.
5. Every required pre-DEMO product gap has a normal generic product correction and exact-head/integration validation.
6. Exact C11 implementation base and implementation branch are recorded above.
7. Current Coordinator handoffs and this document preserve the approved premise and sequencing.

## 3. Pass 2 blocking-gap disposition

### Internal Memory

- `C11-P2-MEM-02` — resolved by C17 capability-aware TAG Address authoring; source providers do not expose meaningless network Address UX.
- `C11-P2-MEM-04` — resolved by C17 real Engineering lifecycle proof creating Server/Client Memory Sources and TAGs, typed initial values, Save/Publish/Activate, Runtime read/write, retention, Historian and two-session Client Memory isolation.

### Server automation / deterministic Simulation foundation

- `C11-P2-SCR-01` — resolved by C12 persisted Active Server Script runtime host with Initialize, Timer, TagChanged, ServerRuntimeEvent and lifecycle replacement.
- `C11-P2-SIM-01` — resolved by C12 generic deterministic stateful Server Memory automation path through normal Active Runtime. No EEE-specific simulator is involved.

### Simulation quality

- `C11-P2-QUAL-01` — resolved by C13/C12 server-authoritative qualified Server Memory samples with canonical Bad/Stale/Unavailable propagation through current state, event bus, alarm, historian, realtime and visual bindings.

### Popup / startup / command

- `C11-P2-POP-02` — resolved by C16 persisted authorable Popup X/Y in logical HMI coordinates.
- `C11-P2-NAV-01` — resolved by C16 persisted stable `StartupScreenId`; no lexical-name fallback is accepted as the canonical contract.
- `C11-P2-CMD-02` — resolved by C16 authored `ExecuteCommand` HMI action delegating only to the protected canonical backend Command endpoint.

### Operational Events

- `C11-P2-EVT-01` — infrastructure resolved by C14 first-class definitions/runtime/history/query and completed as a normal integrator path by C19 Engineering authoring + generic Server Script `emit_operational_event` bridge through canonical C14 Active Runtime.

Alarm, Operational Event and Audit remain distinct.

### Trend

- `C11-P2-TREND-01` / `TREND-02` / `TREND-03` — resolved by C15 first-class `core.trend` object for Screen and Popup with native persisted Multi-Pen configuration, live/historical protected data paths and Active Runtime rendering.

### Alarm/Event Browser and historical i18n

- `C11-P2-BROWSER-01` — resolved by C18 first-class embeddable `core.alarmBrowser` with persisted per-instance configuration and backend-authorized interactions.
- `C11-P2-BROWSER-02` — resolved by C18 first-class embeddable `core.eventBrowser` consuming canonical C14 `operational.events`.
- `C11-P2-I18N-HIST-01` and affected browser multilingual gaps — resolved by C18 pt-BR/en/es visible chrome.

## 4. Exact combined automated evidence

On exact product SHA `3fda88061df35ad14755d22881e5d3a9216d1ff5`:

- EliteSCADA CI #1370 / `33934982242` — SUCCESS;
- Wave11 Active HMI Runtime #298 / `33934982300` — SUCCESS;
- Preview Licensing CI #320 / `33934982254` — SUCCESS;
- L3 Seven-Driver Lab #276 / `33934982215` — SUCCESS;
- Interop Lab Smoke #197 / `33934982216` — SUCCESS.

Dedicated C03 carry-forward:

- Wave 14 C03 DNP3 Adapter #113 / `33935067545` — SUCCESS through documentation-only validation overlay, closed without merge.

Automated Test Preview harness:

- harness SHA `086b5d6c92ccfa4f5dc7e947c787ffd953acc5e2`;
- Test Preview run `33935493882` — SUCCESS;
- validation PR #262 closed without merge.

## 5. Evidence intentionally deferred until completed DEMO

The following are not ignored. They are retained for the real Product Owner Codespace/browser pass after the canonical DEMO exists:

- visibly live Analog Fill from changing canonical TAG values;
- final Dynamo stopped/running/fault/trip/unavailable/bad-quality presentation, including non-color-only critical semantics;
- two independent pump Dynamo instances with independent bindings;
- canonical Runtime scaling at 1280×720, 1920×1080, 2560×1440 and 3840×2160 plus mismatched aspect ratio;
- fullscreen/no document scroll/alarm overlay or Popup without HMI reflow;
- contextual Popups and hit targets under scaling;
- the complete living chain `automation -> TAG/quality -> alarm/event/history -> HMI objects -> command`;
- overall commercial/visual quality and usability of the finished canonical EEE DEMO.

These checks require the application being built by C11. They are therefore final homologation evidence, not a reason to prevent C11 from constructing the application.

## 6. C11 implementation contract

The canonical EEE DEMO must be built exclusively with ordinary generic EliteSCADA mechanisms already available to a normal Engineering user/project:

- Data Sources / Drivers;
- TAGs;
- Server Memory / Client Memory where appropriate;
- Server Scripts;
- Operational Event definitions;
- Alarms;
- Historian;
- Trend;
- Alarm Browser;
- Event Browser;
- Commands;
- Screens;
- Popups;
- Dynamos;
- normal project assets/backgrounds;
- Startup/Home.

The Simulation process truth must use server-authoritative shared state. Client Memory must not become shared process truth.

## 7. Forbidden implementation shortcuts

C11 must not introduce or use as canonical behavior:

- EEE-specific simulator service;
- EEE-specific Driver;
- `SimulationDriver` or historical `DemoRuntimeServices` as the canonical simulation engine;
- hidden/private `.escadapkg` editing as the normal authoring mechanism;
- DEMO-only route/API/runtime;
- direct backend/Driver/DI object access from scripts;
- direct historical insertion;
- frontend-only Operational Event fabrication;
- Alarm/Event/Audit semantic conflation;
- security, authorization, licensing or lifecycle bypass;
- direct DOM/React manipulation to fake HMI behavior.

If DEMO implementation reveals that a required canonical behavior still cannot be built through normal product mechanisms, stop the DEMO workaround and classify a new product gap. Implement the narrow generic product correction on a separate branch, validate exact bytes, compose it into integration, then rebase/restart C11 from the new accepted product authority as appropriate.

## 8. Implementation target

Build the deterministic **DEMO Simulation** first.

Required process behavior includes at minimum:

- two pumps/motors;
- wet-well inflow increasing level;
- pump operation decreasing level;
- automatic start/stop thresholds;
- duty/standby alternation;
- second pump under high demand;
- fault/trip injection;
- unavailable/bad-quality scenario;
- coherent current/frequency/flow/pressure behavior;
- alarms arising from process state;
- operational events for meaningful transitions;
- evolving historian/trend data;
- operator commands through canonical Command entities;
- deterministic behavior suitable for regression and repeatable homologation.

The later DEMO PLC variant must preserve conceptual HMI/TAG identity and primarily replace Source/address mapping with the Product Owner supplied real Modbus mapping.

## 9. Next route

1. implement the canonical EEE DEMO on `wave14/c11-canonical-eee-demo` from exact base `3fda880...`;
2. validate repository-side lifecycle and deterministic behavior without adding product shortcuts;
3. prepare the current Preview harness to launch/import the new canonical DEMO;
4. Product Owner performs fresh Codespace visual homologation on completed product + DEMO;
5. classify/fix/revalidate any defect found there;
6. final Wave14 acceptance;
7. only then resume Wave13 #205/#207 packaging/signing on final accepted Wave14 bytes.

## 10. Permanent boundary

PR #212 remains OPEN/DRAFT. C11 release does not authorize any merge of #212 into `main`.
