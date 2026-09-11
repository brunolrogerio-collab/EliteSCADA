# W14-C11 — Canonical EEE DEMO Requirements and Product-Gap Gate

**Created:** 2026-09-03 BRT  
**Owner direction:** Development Lead / Product Owner  
**State:** **REQUIREMENTS + PRODUCT-GAP AUDIT ONLY / IMPLEMENTATION LOCKED**

> This document is the repository memory for the future canonical DEMO. It is intentionally written before implementation so the DEMO cannot quietly shrink to match accidental product limitations.

## 1. Purpose

The historical EliteSCADA DEMO is no longer the reference model for Wave 14.

A new canonical application will be created from the converged product, using a real industrial **Estação Elevatória de Esgoto (EEE)** as the process model. The Product Owner supplied real HMI captures, TAG exports and alarm information as engineering reference. The objective is not pixel-for-pixel reproduction of that old HMI; it is to build a modern EliteSCADA demonstration that feels like a real operating station and exercises normal product workflows.

The DEMO must serve simultaneously as:

- a realistic sales/product demonstration;
- an Engineering authoring exercise;
- a Runtime/operator exercise;
- a regression/acceptance application;
- evidence that the current product contracts can build a real SCADA project without DEV-only hacks;
- the basis for later real-PLC/Modbus validation.

## 2. Implementation lock

C11 implementation is **not authorized yet**.

Until explicit Coordinator release, C11 must not:

- create an authoritative implementation branch;
- change product code;
- create the canonical `.escadapkg`;
- modify Preview/Codespaces to fake the future DEMO;
- add one-off HTML/CSS/JavaScript that bypasses normal Engineering capabilities;
- reduce requirements merely because the product does not currently support them.

Read-only product audit and requirements consolidation are authorized.

## 3. Product lifecycle that the DEMO must prove

The application must be constructible and executable through the real product lifecycle:

`fresh install -> first Administrator -> create project -> configure Sources/Drivers -> create TAGs -> author Screens/Popups/Dynamos -> configure Scripts/Bindings -> Save -> Publish -> Activate -> HMI Runtime`

Runtime remains authoritative only from persisted **Active** Engineering.

No DEMO-only authorization, licensing, Driver, TAG-memory or Runtime bypass is acceptable.

## 4. Process model — Estação Elevatória de Esgoto

The primary process is a sewage lift station with a suction well and two pumping units.

At minimum the process model should provide coherent behavior for:

- suction-well level;
- two pumps/motors;
- duty/standby or alternating operation;
- pump start/stop thresholds;
- discharge flow;
- discharge pressure where applicable;
- motor frequency/speed when VFD behavior is represented;
- motor current;
- equipment availability;
- fault/trip conditions;
- bad/stale/unknown quality conditions;
- alarm thresholds and events;
- transitions that are visible in Runtime rather than static sample values.

The exact process equations may be simplified for demonstration, but values and state transitions should be internally coherent enough that an industrial user recognizes the intended behavior.

## 5. Main Runtime visual requirements

The main EEE Screen should be strongly visual and operator-oriented.

### Pumps/motors

Two pump/motor units must make state obvious without requiring the operator to inspect text tables.

Expected states include:

- stopped;
- running;
- fault/trip;
- unavailable/bad quality;
- additional normal operational states when the canonical Dynamo contract supports them, such as command pending, manual/auto or local/remote.

Critical states must not depend only on color. Text/symbol/state indication should remain understandable for safety/diagnostics.

### Suction well

The well must show:

- analog level;
- `% Full` or equivalent percentage indication;
- animated liquid fill rising and falling with the process value;
- high/low/alarm state presentation where applicable.

The liquid animation must be implemented through ordinary EliteSCADA visual/property/binding/script capabilities, not DEMO-only DOM manipulation.

### Process values

The main and support Screens should expose realistic values such as:

- level;
- flow;
- pressure;
- frequency;
- current;
- operating hours/counters if current product contracts support them naturally;
- relevant statuses/qualities.

## 6. Popups

Equipment should have contextual Popups for operator detail and supported commands.

Desired Popup content may include:

- current state;
- process values;
- quality;
- alarms/interlocks;
- mode/status;
- permitted operator commands;
- feedback/command result.

Commands must travel through the authenticated/authorized Runtime command path. No Dynamo, Script or Popup may write directly to a Driver.

Known contract concern to audit: the accepted C07/C09-era Popup runtime mount contract did not yet provide a canonical persisted authorable X/Y placement model. Do not invent CSS-only persisted coordinates. If contextual placement is required and still absent after C09/C10, classify it as a product gap.

## 7. Dynamos

The DEMO must use reusable Dynamos rather than individually hand-coding every equipment symbol.

Use the canonical built-in inventory where suitable and verify:

- stable definition/type identity;
- typed/versioned public properties;
- public TAG bindings;
- public actions/command keys where applicable;
- deterministic state projection;
- quality/fault precedence;
- instance reuse without exposing internal children to ordinary Engineering.

The DEMO is also allowed to expose that an existing built-in Dynamo is visually or functionally inadequate. That is a product finding, not a reason to silently replace the requirement with primitive shapes.

## 8. Screens and navigation

Expected application structure should include at least:

- **Visão Geral / EEE Principal**;
- **Instrumentação**;
- **Sistema Elétrico**;
- **Operação**;
- additional alarm/trend/support views where they improve the product demonstration without violating the operator-vs-Engineering boundary.

Screen changes must occur inside the Runtime application viewport and preserve the logical HMI coordinate model.

No responsive web reflow of authored HMI content.

## 9. Alarms, events, trends and history

The DEMO should naturally exercise:

- alarm generation and state changes;
- alarm acknowledgement/interaction where supported by the normal product;
- event/history persistence where supported;
- process trends for meaningful analog values such as level, flow, pressure/current/frequency;
- historical consultation that demonstrates actual historian behavior rather than static chart fixtures.

If the current product can display these only through an Engineering-only diagnostic surface, classify the operator-experience requirement accurately instead of pretending it is solved.

## 10. Operator vs Engineering boundary

The DEMO must validate role/capability separation.

A Runtime-only identity should primarily experience the HMI and normal operator surfaces.

Engineering/Diagnostics should remain separate and capability-controlled. TAG Monitor remains an Engineering Diagnostics tool observing Active Runtime, not a permanent operator Runtime panel.

Backend authorization is the actual security boundary; hiding a menu is not authorization.

## 11. Assets and richer visual design

The DEMO may use project-owned PNGs and other supported visual assets/backgrounds to achieve a more polished industrial presentation.

Do not artificially restrict the DEMO to primitive vector shapes when normal project asset support exists.

Requirements:

- assets must belong to the project/canonical asset model;
- no arbitrary filesystem/external unsafe path dependency;
- authored HMI artwork must not be recolored by application Dark/Light shell themes;
- asset behavior must survive Save/Publish/Activate/Runtime.

## 12. Script/animation requirements

Use the canonical Script Assistant/Python APIs where useful for normal product behavior, for example:

- visual property read/write/clear;
- tween/animation;
- safe TAG reads;
- safe mediated TAG writes only where a legitimate operator/runtime command is intended;
- Client Memory for client-only UI state where appropriate.

Do not use Python to bypass Driver, backend authorization, Dynamo encapsulation, DOM/React or Runtime property precedence.

## 13. DEMO Simulation variant

The first executable variant is **DEMO Simulation**.

The station should have life through supported product mechanisms such as Simulation sources, internal/client memory where appropriate, bindings and scripts.

Desired simulated behavior:

- well inflow raises level;
- pump operation lowers level;
- automatic start/stop thresholds;
- alternating duty between pump 1 and pump 2;
- realistic relationship between running pump(s), flow, pressure, frequency and current;
- injected pump fault/trip scenario;
- bad-quality/unavailable scenario;
- alarm transitions;
- history/trend data that evolves over time.

The Simulation model should be deterministic/reproducible enough for browser homologation and regression, while still looking alive to the user.

If a requirement cannot be implemented through current public product capabilities, record a gap rather than embedding a hidden special-purpose simulator into the DEMO package.

## 14. DEMO PLC / Modbus variant

A second variant will validate the product against a real PLC using Modbus addresses supplied by the Product Owner.

Design goal:

- same or nearly identical Screens/Popups/Dynamos;
- same conceptual TAG names/contracts;
- Source/address configuration changes to map those TAGs to the real PLC;
- minimal duplication of HMI authoring.

The DEMO Simulation must not create a visual or TAG architecture that prevents this transition.

Real PLC validation is a separate proof from Simulation. A green Simulation DEMO does not prove Modbus/PLC integration, and a connected PLC does not excuse broken Simulation reproducibility.

## 15. Multilingual requirement

The final experience must be audited in:

- `pt-BR`;
- `en`;
- `es`.

This includes shell/operator controls, Engineering surfaces needed to build the DEMO, errors, dialogs, assistant copy, empty states and context menus.

Canonical persisted identifiers and protocol values are not translated as data.

## 16. Runtime presentation / C09 dependency

C11 implementation waits for accepted C09/C10 because the DEMO strongly depends on:

- operator-focused shell;
- capability-pruned navigation;
- fixed logical Runtime scaling;
- fullscreen;
- no document scroll;
- Screen navigation;
- Popup presentation;
- alarm overlay behavior;
- Dark/Light shell themes;
- shared multilingual shell behavior.

Do not assume an unintegrated C09 branch is accepted capability.

## 17. Two-pass product-gap audit

### Pass 1 — C01-C08

Authorized baseline:

`97b275b9f413c57031e28ac21a08e6190747e7f5`

C11 has already begun this audit and has reported preliminary gaps in its requirements lane. Those findings are intentionally provisional where C09/C10 may solve or alter them.

### Pass 2 — post-C09/C10

Mandatory after:

- C09 final green candidate is integrated;
- combined exact-head regression is green;
- C10 multilingual/cross-package convergence is complete enough to freeze an exact SHA.

C11 must then:

1. re-test every pass-1 finding;
2. remove findings genuinely solved by convergence;
3. reclassify partial support;
4. add newly exposed gaps;
5. distinguish product defects from DEMO-specific desires;
6. return one consolidated evidence-backed gap list to Coordinator/Development Lead.

## 18. Gap classification and disposition

For every requirement classify:

- **SUPPORTED** — normal product workflow fully supports it;
- **PARTIALLY SUPPORTED** — relevant contract exists but does not satisfy the intended experience;
- **PRODUCT GAP** — capability is absent/inadequate;
- **NEEDS VALIDATION** — appears implemented but requires integrated real Runtime/browser proof.

Every non-SUPPORTED finding should record:

- requirement;
- expected behavior;
- current contract/evidence;
- limitation;
- impact on the DEMO;
- recommended owner/action.

Confirmed gaps require an explicit decision:

- fix before C11;
- mitigate knowingly;
- defer to a later Wave;
- intentionally narrow the requirement only by Development Lead decision.

No silent requirement reduction.

## 19. Release gate for implementation

C11 implementation may be released only when:

1. C09 is accepted into the integration branch;
2. C10 has a green converged product SHA;
3. multilingual audit is sufficiently closed;
4. C11 pass 2 has returned the consolidated gap list;
5. required pre-DEMO product fixes are dispositioned and, when implemented, integrated/validated;
6. the Coordinator records the exact implementation base and branch;
7. this document or its successor reflects the final approved premise.

At implementation release, the Coordinator must preserve in repository memory:

- full DEMO premise;
- functional architecture;
- approved requirements;
- Simulation vs PLC variants;
- process model;
- validation goals;
- known limitations/gaps;
- Product Owner decisions.

## 20. Final acceptance route

`converged product -> C11 pass 2 -> gap disposition -> C11 implementation -> canonical EEE DEMO Simulation -> PLC-compatible mapping/validation -> full CI -> Preview harness updated -> clean Codespace -> real browser Product Owner homologation`

The new DEMO becomes the Wave 14 owner-validation application. The historical DEMO remains only historical evidence or a narrowly justified legacy fixture.
