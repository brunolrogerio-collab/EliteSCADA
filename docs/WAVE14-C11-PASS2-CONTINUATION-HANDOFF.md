# W14-C11 — Pass 2 Continuation Handoff

**Date:** 2026-09-03 BRT  
**Purpose:** preserve C11 Pass 2 audit state across chat/session handoff  
**Authoritative product-code SHA under audit:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`  
**Coordinator integration branch:** `wave14/corrections-integration`  
**Integration PR:** #212 — remains DRAFT / DO NOT MERGE TO `main`  
**C11 audit branch:** `wave14/c11-pass2-product-gap-audit`  
**Canonical progressive audit:** `docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md` on the C11 audit branch

> GitHub is the official memory. This document exists specifically so a new chat can continue C11 without relying on hidden conversation state. Revalidate live repository state before taking action.

## 1. Current authorization boundary

C11 is authorized for **PASS 2 PRODUCT-GAP AUDIT ONLY**.

Implementation remains locked. The C11 lane must not:

- create an authoritative DEMO implementation branch;
- build the canonical EEE DEMO;
- create the canonical `.escadapkg`;
- alter product code to make the future DEMO fit;
- alter Preview/Codespaces to simulate the future project;
- introduce DEMO-only HTML/CSS/JS or direct DOM bypasses;
- use hidden package editing, Driver internals, private host memory, security bypasses or developer-only shortcuts;
- reduce a legitimate Product Owner requirement merely because the current product lacks it.

If a canonical requirement cannot be implemented through normal EliteSCADA Engineering/Runtime contracts, classify it as a product gap and preserve the requirement.

## 2. Frozen Pass 2 product authority

All C11 Pass 2 product findings must be revalidated against exact product-code SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

This SHA is the C01-C10 converged product freeze. Later `[skip ci]` documentation commits on `wave14/corrections-integration` do not supersede it as product-code authority.

Required exact-SHA validation evidence already recorded by Coordinator:

- EliteSCADA CI #1273 — SUCCESS;
- Wave 11 Active HMI Runtime #203 — SUCCESS;
- Preview Licensing CI #225 — SUCCESS;
- L3 Seven-Driver Lab #180 — SUCCESS;
- Interop Lab Smoke #102 — SUCCESS.

PR #212 was revalidated during this handoff and remains OPEN, DRAFT, unmerged to `main`.

## 3. Mandatory reading for the next C11 chat

Read/revalidate in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. this file;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. `docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`;
7. `docs/WAVE14-CORRECTION-PACKAGES.md`;
8. `docs/CI-VALIDATION-POLICY.md`;
9. issue #211;
10. draft PR #212;
11. `docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md` on branch `wave14/c11-pass2-product-gap-audit`.

Do not resume from old chat conclusions without revalidation when source evidence can be checked directly.

## 4. Canonical EEE intent that must remain binding

The future canonical application is a realistic **EEE — Estação Elevatória de Esgoto**, intended simultaneously as product/commercial DEMO, Engineering example, operator Runtime demonstration, regression/acceptance application, Product Owner homologation project and later physical PLC/Modbus proof.

Main required experience includes:

- two pumps/motors;
- stopped/running/fault/trip/unavailable/bad-quality states;
- critical states not represented by color alone;
- suction/wet well analog level;
- numeric `% Full`;
- visibly animated liquid level;
- coherent flow, pressure, current, frequency and other process values;
- contextual Popups;
- reusable Dynamos with public properties and real TAG bindings;
- alarms, events, trends and history;
- normal PNG/background/project asset use;
- clear Operator versus Engineering/Diagnostics boundary;
- `pt-BR`, `en`, `es` user-facing surfaces;
- Screens including at least `Visão Geral / EEE Principal`, `Instrumentação`, `Sistema Elétrico`, `Operação`.

The historical DEMO/reference HMI is process/engineering reference only. It is not a UI compatibility authority.

## 5. Required Simulation behavior

The first future executable variant is **DEMO Simulation**. It must be visibly alive using supported product resources, not a static fixture.

Expected process behavior includes:

- inflow increases wet-well level;
- running pump(s) decrease level;
- automatic start/stop thresholds;
- duty/standby alternation;
- second pump under high demand;
- pump fault/trip injection;
- unavailable / bad-quality scenario;
- current/frequency/flow/pressure reacting coherently;
- alarms arising naturally from process conditions;
- evolving historian/trend data;
- deterministic enough behavior for regression and browser homologation.

The desired conceptual model is shared authoritative process state. Client-local memory is not appropriate as the global truth of the simulated physical process.

## 6. Required PLC/Modbus transition

A later **DEMO PLC** variant must validate the same conceptual application against a physical PLC using Product Owner supplied Modbus addresses.

Preserve as much as technically possible:

- Screens;
- Dynamos;
- Popups;
- conceptual TAG identities;
- alarms;
- trends/history;
- visual logic;
- operator experience.

The intended change should be primarily Source/address mapping, not rebuilding the HMI around simulator-specific internals.

Historical FvDesigner address examples supplied by the Product Owner, such as `@0:4x2005.0`, `@0:4x2031` and `@0:4xD2100`, are reference data only. Do not assume they are current EliteSCADA Modbus syntax. Use the current Modbus catalog/address assistant and validate later against the real PLC.

## 7. Important correction from Pass 1: Internal Memory exists

Do **not** repeat the obsolete claim that EliteSCADA lacks internal memory.

The accepted product contains canonical Internal Memory implementation, including Server Memory and Client Memory infrastructure. Relevant accepted concepts include:

- `builtin.memory.server` — shared/server-owned authoritative memory;
- `builtin.memory.client` — isolated client/session memory;
- typed initial/default values;
- Server Memory retention;
- current-value cache participation;
- historian metadata support;
- writable typed memory values.

For the EEE Simulation process model, **Server Memory** is the correct candidate for shared process state. Client Memory is appropriate only for client-local UI/transient state where needed.

## 8. Confirmed C11 Pass 2 finding already safe to carry forward

### C11-P2-MEM-02 — Internal Memory TAG editor exposes meaningless network Address semantics

**Classification:** `PRODUCT GAP — ENGINEERING UX`

Exact frozen-SHA evidence already revalidated:

- `web/scada-web/src/engineering/TagAddressEditor.tsx` keeps the generic `Address` authoring path and has specialized assistants for industrial protocols, but not Internal Memory;
- `web/scada-web/src/engineering/MemoryTagSettingsPanel.tsx` correctly recognizes `builtin.memory.server` / `builtin.memory.client` and explicitly models Internal Memory as having no network address.

Current product therefore presents conflicting Engineering semantics: the memory-specific panel says no network address exists, while the generic TAG editor still exposes address semantics.

Impact:

- affects normal human authoring of Simulation memory TAGs;
- does not mean Internal Memory itself is absent;
- does not directly break PLC/Modbus, but obscures the conceptual separation between internal and physical Sources.

Recommended disposition already recorded in the progressive audit:

**fix before C11** through normal source capability/schema-aware UI behavior. Do not hide it with DEMO package editing or magic values.

## 9. Internal Memory findings still requiring closure

### C11-P2-MEM-03

Human-facing creation of both Server Memory and Client Memory Data Sources through the normal backend-authoritative Data Source catalog remains `NEEDS VALIDATION` until proven end-to-end at the frozen SHA.

The generic Data Source editor is schema-driven and backend-authoritative, which is structurally good. What still needs proof is that both memory providers are actually published in that catalog and creatable without knowing `builtin.memory.*` implementation identifiers.

### C11-P2-MEM-04

End-to-end creation from an empty/normal project:

`create Memory Source -> create Memory TAG -> configure typed value -> save/publish/activate -> Runtime value`

still needs direct E2E/browser proof. Existing historical memory tests that start with a pre-existing provider identity are insufficient by themselves.

## 10. Data Source / address-authoring state already established

The current Engineering architecture does use backend-authoritative typed Data Source configuration schemas rather than one opaque driver configuration string.

`DataSourceCatalogEditor` projects typed configuration fields from the backend catalog/schema. This is the correct general product direction.

`TagAddressEditor` has specialized address-assistant branches for at least:

- Modbus TCP;
- OPC UA;
- DNP3;
- IEC-104;

with a generic fallback for other sources.

This means the earlier Product Owner concern that driver Sources/TAGs should have human-facing forms/wizards rather than requiring secret strings was substantially addressed by C02/C04. Internal Memory remains the specific inconsistent case identified above.

## 11. Visual / Dynamo / Popup structural findings already established

The progressive C11 audit currently has structural support evidence for:

- numeric `% Full` display;
- canonical Analog Fill with min/max, clamp/invert and directional fill modes, suitable for the animated wet well;
- reusable Dynamo definitions;
- typed public Dynamo parameters, including TAG references/equipment-path concepts;
- canonical `OpenPopup` / `ClosePopup` action model;
- project-owned raster assets/background use through Engineering.

Do not equate structural DTO existence with full browser acceptance. Final operator presentation and authoring still require the specific validations listed below.

## 12. Popup X/Y placement remains an important unresolved item

A known C07/C09-era concern remains:

**canonical persisted authorable Popup X/Y placement has not yet been proven.**

Do not invent persisted `left/top`, CSS coordinates or DEMO-specific overlay hacks.

The next C11 chat must audit, on the frozen SHA:

- Popup persisted definition/action contracts;
- Property Inspector/authoring UI;
- Runtime Popup mount/presentation;
- tests proving authored position survives Save/Publish/Activate/Runtime.

If no canonical persisted authorable X/Y exists, classify it `PRODUCT GAP` and disposition it explicitly.

## 13. Server Python / living Simulation is the highest-priority unresolved technical question

Pass 1 established strong **contracts** for Python scripts:

- ClientVisual and Server scopes;
- Server-side read/write Server Memory / shared TAG capability;
- event kinds including Initialize, TagChanged, Timer and ServerRuntimeEvent;
- bounded execution policy, timeout, queueing, timer rules and diagnostics;
- `ScriptRuntimeExecutionCoordinator` and `IPythonScriptHandlerExecutor` abstractions.

However, Pass 1 could not prove an actual normal product **Server Python Runtime host** that loads active project Server Scripts, materializes Timer subscriptions and continuously executes them in the published/active Runtime lifecycle.

During the final minutes of the expired chat, `src/Scada.Api/Program.cs` was re-fetched at exact SHA `97eefd8f...`. It registers Engineering scripts as project data, the engineering/runtime coordinator, SimulationDriver legacy/demo services, historian and normal APIs, but the inspected portion did **not** show an `IPythonScriptHandlerExecutor` registration or an obvious Server Python hosted service/scheduler.

This is **not yet a final gap classification** because the repository still needs an exact-SHA search for:

- every implementation of `IPythonScriptHandlerExecutor`;
- every DI registration of that interface;
- Server Python host/executor classes;
- hosted services or activation hooks;
- Timer subscription materialization;
- active workspace/project script loading;
- Server Script integration tests.

If the accepted product has only contracts/coordinator abstractions and no active Server Python execution host, that is likely a **blocking PRODUCT GAP** for the intended continuously living Simulation, unless another normal engineer-authorable canonical product mechanism satisfies the same physics/state-model requirement. The old hardcoded `SimulationDriver`, `DemoRuntimeServices` or historical DEMO runtime code must not be used as proof or mitigation for the future canonical application.

## 14. Bad-quality scenario must be split into two separate questions

Do not audit “bad quality” as one vague capability. Separate:

1. Can normal Runtime visuals/alarms consume and represent a `Bad`/stale/unavailable quality received from a real source?
2. Can the **DEMO Simulation** deliberately generate/inject bad quality through supported normal project contracts?

The first may be supported by current TAG quality propagation while the second may still be a product gap.

For the canonical EEE Simulation, deliberate communication-loss/bad-quality behavior is mandatory. A visual label that can display Bad Quality does not prove that an engineer-authored Simulation can create that quality state.

Audit exact-SHA quality model, current cache/realtime payload, visual binding/expression access to quality, alarm behavior on quality, and any supported simulation-side quality write/injection mechanism.

## 15. Other high-priority open C11 Pass 2 validations

The next chat must continue and close, with evidence:

### Runtime/script/simulation
- Server Python host/executor/scheduler/lifecycle;
- mass-balance state-model feasibility through normal product resources;
- safe TAG writes/commands from authored UI actions/scripts;
- historian capture from Server Memory updates;
- alarms/events triggered by the same evolving TAG values;
- deterministic simulation control/injection scenarios.

### Quality/state
- real-source bad-quality visualization;
- normal Simulation bad-quality injection;
- pump unavailable/fault/bad-quality operator semantics;
- critical-state presentation not dependent on color alone.

### Visuals/Dynamos/Popups
- actual Analog Fill live rendering on changing TAG values;
- motor running/fault visual dynamics through canonical properties;
- Popup X/Y authorability;
- contextual Popup behavior in real browser;
- repeated Dynamo instances with independent TAG bindings.

### Navigation/Runtime shell
- explicit authorable startup/home Runtime Screen rather than relying on name/sort order;
- Runtime-only capability-driven shell;
- fullscreen/no-scroll behavior;
- fixed logical HMI scaling at 720p/1080p/1440p/4K;
- alarm overlay behavior without HMI reflow;
- Operator versus Engineering/Diagnostics separation.

### Alarms/events/history/trends
- active alarm lifecycle;
- acknowledge;
- clear/return-to-normal;
- retained/history query;
- events distinct from alarms where appropriate;
- trend component/query with evolving data;
- end-to-end chain from Simulation TAG update through historian/alarm/binding to Runtime.

### PLC/Modbus reuse
- logical TAG identity remains stable while Source/address binding changes;
- visual/Dynamo/Popup references target logical TAGs rather than source-specific addresses;
- current Modbus address assistant syntax and mapping behavior;
- real PLC operation remains a later `requires PLC validation later`, not something to fake in C11 Pass 2.

### Multilingual
Audit visible normal product surfaces in:

- `pt-BR`;
- `en`;
- `es`.

Do not translate persisted identifiers, protocol keys, NodeIds, canonical property names, stable references or data contracts. C10 intentionally corrected visible UI while preserving persisted canonical data.

## 16. Progressive classifications already in the audit workspace

The C11 audit document already records, among others:

- exact C10 freeze authority — `SUPPORTED`;
- backend-authoritative Data Source forms — `SUPPORTED`;
- protocol-aware TAG address assistants — `SUPPORTED`;
- Server/Client Internal Memory concepts — `SUPPORTED`;
- Internal Memory generic Address UX inconsistency — `PRODUCT GAP`;
- human UI creation of both memory Sources — `NEEDS VALIDATION`;
- E2E Memory Source + TAG authoring — `NEEDS VALIDATION`;
- numeric `% Full` — `SUPPORTED`;
- Analog Fill — structurally `SUPPORTED`, real-browser validation still relevant;
- reusable typed Dynamos — structurally `SUPPORTED`;
- pump semantic state/bad-quality presentation — `PARTIALLY SUPPORTED` pending authorability/operator proof;
- contextual Popup open/close — structurally `SUPPORTED`;
- persisted authorable Popup X/Y — `NEEDS VALIDATION`;
- explicit Runtime startup/home Screen — `NEEDS VALIDATION`;
- fixed logical Runtime scaling — `PARTIALLY SUPPORTED` pending real-browser validation;
- project PNG/background asset support — `SUPPORTED`.

Treat the progressive matrix as work in progress, not final disposition.

## 17. Required final Pass 2 matrix format

The completed C11 Pass 2 matrix must contain:

- ID;
- Requirement / capability;
- Classification;
- Evidence;
- Current product behavior;
- EEE impact;
- Simulation impact;
- PLC/Modbus impact;
- Recommended disposition.

Allowed disposition vocabulary:

- `no action required`;
- `fix before C11`;
- `known mitigation acceptable`;
- `defer to later Wave`;
- `requires Development Lead decision`;
- `requires real-browser validation`;
- `requires PLC validation later`.

Required final sections:

A. Requirements fully supported  
B. Partial capabilities  
C. Confirmed product gaps  
D. Validation-only uncertainties  
E. Blocking gaps  
F. Non-blocking gaps  
G. Recommended coordinator action

Final recommendation must be exactly one of:

- `RELEASE C11 IMPLEMENTATION`
- `KEEP C11 IMPLEMENTATION LOCKED`

C11 does not itself release implementation. Coordinator/Development Lead owns that decision.

## 18. Current conservative coordinator posture

Because at least one confirmed product gap already exists (`MEM-02`) and major Simulation/runtime questions remain unresolved, no one should interpret the current progressive audit as permission to begin the canonical DEMO.

Until Pass 2 is completed and gaps are dispositioned, the safe recommendation remains:

`KEEP C11 IMPLEMENTATION LOCKED`

The Product Owner has indicated a preference to correct legitimate product gaps **before** authorizing C11 implementation, rather than accepting avoidable weaknesses merely because a DEMO could route around them.

## 19. Next exact action for the new C11 chat

Resume from repository evidence, not chat memory:

1. revalidate the frozen SHA and current audit branch head;
2. read the progressive C11 audit document;
3. close **Server Python Runtime execution** first;
4. close **Simulation bad-quality injection** second;
5. close **Popup X/Y authoring** and **Runtime startup Screen**;
6. close Memory Source creation E2E;
7. close alarm/event/history/trend end-to-end behavior;
8. validate visual/Dynamo/Runtime presentation and multilingual behavior;
9. assess Simulation-to-Modbus TAG/source reuse;
10. write every conclusion into the audit document;
11. produce the consolidated Pass 2 matrix and recommendation;
12. do not implement the DEMO until Coordinator/Development Lead explicitly releases it.
