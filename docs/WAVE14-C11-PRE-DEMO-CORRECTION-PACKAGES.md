# Wave 14 — C11 Pre-DEMO Product Correction Packages

**Date:** 2026-09-03 BRT  
**Authority:** Coordinator / Development Lead + Product Owner decisions after W14-C11 Pass 2  
**State:** **PRE-DEMO PRODUCT CORRECTIONS AUTHORIZED / C11 IMPLEMENTATION LOCKED**  
**Frozen audited product-code SHA:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`  
**Integration branch:** `wave14/corrections-integration`  
**Integration PR:** #212 — **DRAFT / DO NOT MERGE TO `main`**

> GitHub is the official development memory. This document partitions the confirmed C11 Pass 2 product gaps into bounded correction packages that may be assigned to independent DEV chats. C11 itself remains an audit/requirements lane and must not implement these product fixes.

## 1. Evidence and authority

Canonical C11 Pass 2 result:

- branch `wave14/c11-pass2-product-gap-audit`;
- `docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md`;
- state `PASS 2 CONSOLIDATED / IMPLEMENTATION LOCKED`.

Canonical DEMO requirements:

- `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`.

Product Owner HMI-object clarification:

- `docs/WAVE14-C11-PASS2-HMI-OBJECTS-PRODUCT-OWNER-CLARIFICATION.md` on the C11 audit branch.

Issue #211 records the Coordinator/Product Owner decisions made after Pass 2.

The exact product audited by C11 remains:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Documentation-only commits after that SHA do not redefine the audited product.

## 2. Binding Product Owner decisions after Pass 2

### 2.1 Popup X/Y is a mandatory product correction

Persisted authorable Popup placement is required before C11 implementation release.

The accepted product must provide a canonical authoring path for Popup position that:

- is editable through normal Engineering UX;
- persists in canonical project/package contracts;
- survives `Save -> Publish -> Activate`;
- is honored by Active HMI Runtime;
- participates correctly in the fixed logical Runtime coordinate system and scaling transform;
- does not rely on DEMO-specific CSS, private `left/top` insertion, direct DOM manipulation or hidden package JSON.

Centered/shell-defined placement is not an accepted substitute for this requirement.

### 2.2 EliteSCADA must provide generic tools sufficient to build the EEE Simulation

The product must expose normal, reusable Engineering/Runtime mechanisms from which an integrator can author the required living deterministic EEE Simulation.

The correction work must **not** create an EEE-specific simulator inside product code.

The future C11 project must be able to implement, through public supported product mechanisms, behavior such as:

- inflow increasing wet-well level;
- pumps decreasing level;
- start/stop thresholds;
- duty/standby alternation;
- two-pump demand;
- fault/trip scenarios;
- unavailable/bad/stale quality scenarios;
- coherent flow/pressure/current/frequency values;
- alarms, operational events and historian samples arising from the same evolving process state.

If Server Memory + Server Runtime automation + other public contracts are insufficient to author that class of process model, the insufficiency is a **product gap**, not permission to add a private DEMO workaround.

Forbidden acceptance shortcuts include:

- `EeeSimulatorService` or equivalent EEE-specific product backend;
- historical `SimulationDriver` / `DemoRuntimeServices` as the canonical future application mechanism;
- a hard-coded DEMO webpage;
- direct DOM/React mutation;
- hidden `.escadapkg` JSON editing;
- private Driver or host-memory hooks;
- auth/licensing bypasses.

## 3. Permanent architecture gates for all C12-C18 packages

Every package must preserve:

- backend canonical authority;
- backend-side authorization;
- host-owned fail-closed licensing;
- no Preview-only product bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identity;
- lifecycle `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- normal project persistence rather than private fixture state;
- `pt-BR`, `en`, `es` where user-visible product surfaces are added or changed;
- exact-head validation;
- diagnosis before rerun when CI fails;
- no weakening of meaningful tests/security/contracts merely to obtain green.

Package branches never merge directly to `main`. Coordinator accepts/integrates them into `wave14/corrections-integration` only after review and required validation.

## 4. Package map

| Package | Priority | Primary gap ownership | Start state | Main dependency |
|---|---|---|---|---|
| C12 | **P0 critical** | Server Runtime automation + generic Simulation authoring capability | immediate | accepted C08/server-memory contracts; coordinate with C13 quality contract |
| C13 | **P0 critical** | canonical Simulation quality production | immediate | TAG/quality/Memory runtime contracts |
| C14 | **P0** | first-class Operational Events + history/query | immediate | existing historian/event infrastructure |
| C15 | **P0** | embeddable configurable Multi-Pen Trend HMI object | immediate | C05/C07 visual contracts + historian query |
| C16 | **P0/P1** | HMI Operational Command bridge + Startup Screen + mandatory Popup X/Y | immediate | C07/C09 Runtime actions + command backend |
| C17 | **P1** | Internal Memory authoring UX + full lifecycle E2E | immediate | C02/C04 Source/TAG contracts |
| C18 | **P0 after prerequisites** | embeddable Alarm Browser + Event Browser + related history i18n | **blocked for implementation** | C14 Event contract + stabilized C15 embeddable-object pattern |

## 5. W14-C12 — Server Runtime Automation / Generic Simulation Authoring

**Branch:** `wave14/c12-server-runtime-automation`  
**Priority:** **P0 critical / C11 release blocker**

### Owns

Confirmed C11 findings:

- `C11-P2-SCR-01` — missing active Server Python host/executor/scheduler/timer lifecycle;
- `C11-P2-SIM-01` — no canonical engineer-authorable periodic producer for a living deterministic process model.

### Required outcome

Implement a normal Active Runtime server-side automation path for project-authored Server Scripts/handlers, including the product contracts necessary to support:

- loading Server Scripts from the Active project revision;
- initialization lifecycle;
- timer/scheduled execution through canonical script event contracts;
- relevant TAG/runtime event execution where already part of accepted contracts;
- bounded execution/timeout/queue behavior;
- controlled read/write access only through approved Server Memory/TAG/runtime capabilities;
- diagnostics and deterministic failure behavior;
- activation/re-activation/project-switch lifecycle correctness;
- no execution of Working-only unpublished script state in Active Runtime.

### Simulation acceptance requirement

C12 is **not complete merely because one Server Script can execute**.

The delivered generic mechanism must be demonstrably sufficient for an integrator to author a small representative stateful process loop through normal product resources, for example:

`Timer -> read shared process state -> calculate next state -> write canonical Server Memory/TAG values -> downstream cache/alarm/historian/binding observes the same values`

The representative proof must remain generic. Do not implement EEE-specific physics in product code.

### C13 coordination

C12 may start in parallel with C13. Do not invent a private script-only quality API. Consume the canonical C13 quality contract once stabilized. If that requires a bounded follow-up commit after C13 integration, keep the boundary explicit.

### Does not own

- general Operational Event persistence/query: C14;
- Trend object: C15;
- Popup/Command/navigation action contracts: C16;
- DEMO EEE process equations or final `.escadapkg`: future C11 implementation.

### Minimum validation

- focused server-script/automation unit/integration tests;
- activation lifecycle test;
- timer repetition and bounded shutdown test;
- write path authorization/capability tests where relevant;
- historian/alarm observation of values written through normal server automation;
- universal EliteSCADA CI;
- Runtime workflow when impacted.

## 6. W14-C13 — Canonical Simulation Quality Contract

**Branch:** `wave14/c13-simulation-quality-contract`  
**Priority:** **P0 critical / C11 release blocker**

### Owns

Confirmed finding:

- `C11-P2-QUAL-01` — Internal Memory/public Simulation authoring cannot deliberately originate non-Good quality.

### Required outcome

Add a canonical server-authoritative mechanism through which supported internal/simulation sources can produce quality-aware samples such as the product's accepted equivalents of:

- Good;
- Bad;
- Stale;
- Unavailable/communication loss.

The exact quality vocabulary must remain consistent with existing core TAG contracts.

### Security/authority boundary

Do **not** create a generic client-side API that allows a Client Visual script to falsify the quality of arbitrary real Driver TAGs.

Quality origin must respect Source/runtime authority. Real physical Driver quality remains Driver/runtime-owned. The new capability must be appropriate to canonical Memory/Simulation/server-side testable sources and reusable by future non-EEE applications.

### Required downstream proof

Demonstrate that a quality sample produced through the new canonical path reaches existing product consumers without a private bridge:

`source/sample -> CurrentTagCache/realtime -> alarm communication semantics -> visual/binding quality semantics -> historian/query where quality is represented`

### Does not own

- Server Script scheduler: C12;
- real Driver protocol quality behavior;
- final pump bad-quality artwork: future C11 browser acceptance using product capabilities.

### Minimum validation

- focused TAG/source quality tests;
- Memory/Simulation quality write/origin test;
- propagation tests;
- ensure ordinary Memory value write default semantics remain safe/compatible;
- universal EliteSCADA CI;
- Runtime/historian/alarm specialized tests when affected.

## 7. W14-C14 — First-Class Operational Events

**Branch:** `wave14/c14-operational-events`  
**Priority:** **P0 / C11 release blocker**

### Owns

Confirmed finding:

- `C11-P2-EVT-01` — no engineer-authorable first-class operational Event model/history distinct from Alarm and Audit.

### Required outcome

Implement a protocol-neutral/application-neutral operational Event model suitable for normal SCADA use, covering the canonical path:

`event definition/source -> Active Runtime emission -> timestamp/context -> persistence -> protected historical query`

The model must be able to represent ordinary operational transitions honestly, including future examples such as:

- pump start/stop;
- Auto/Manual mode transitions;
- duty/standby alternation;
- operator/command-related operational transitions where appropriate.

Do not fabricate ordinary events as alarms. Do not overload security Audit as the process-event historian.

### Event information

Provide stable canonical fields appropriate to the architecture, including enough context for downstream filtering such as category/type, source/origin, equipment/TAG scope, timestamp and user/operation context when legitimately applicable.

### Dependency exported to C18

C14 owns the backend/domain/persistence/query contract that the later Event Browser consumes. C18 must not independently invent a frontend Event schema.

### Minimum validation

- domain/model tests;
- Runtime event production test;
- persistence/query test;
- authorization tests for query/action endpoints;
- alarm/audit separation regression tests;
- universal EliteSCADA CI;
- historian/runtime specialized workflow as appropriate.

## 8. W14-C15 — Embeddable Multi-Pen Trend HMI Object

**Branch:** `wave14/c15-hmi-trend-multipen`  
**Priority:** **P0 / C11 release blocker**

### Owns

Confirmed/refined findings:

- `C11-P2-TREND-01` — process trend is not exposed through a complete normal operator/authored path;
- `C11-P2-TREND-02` — no canonical embeddable Trend visual object for Screen/Popup;
- `C11-P2-TREND-03` — current viewer does not satisfy Multi-Pen requirement.

### Required authoring contract

Trend must be a first-class project visual object:

`Engineering palette -> insert Trend in Screen/Popup -> configure persisted properties/Pens -> Save -> Publish -> Activate -> Active Runtime render`

It must use normal visual X/Y/width/height composition and work correctly with multiple instances.

### Multi-Pen contract

One Trend object must support multiple independently persisted Pens. At minimum each Pen must support, where relevant to the product model:

- TAG/reference;
- display/legend label;
- visible state;
- unit;
- line appearance including color and thickness/style;
- axis assignment where multiple axes are implemented;
- automatic/manual scaling;
- manual min/max for manual scale.

Trend-level configuration must support practical operator use, including:

- live, historical or combined mode consistent with the existing architecture;
- time window/range;
- update/refresh behavior;
- legend;
- grid;
- axes/scale presentation;
- quality/no-data presentation;
- protected historian query;
- cursor/value inspection if included in the accepted implementation.

### Engineering UX

Scalar visual properties should consume canonical schema/Property Inspector patterns. Complex Pen collections may use a dedicated editor, but the result must remain canonical persisted project data, not private UI state.

### Multilingual

All visible product chrome added by this object must support `pt-BR`, `en`, `es`.

### Does not own

- generic Events: C14;
- Alarm/Event Browser tables: C18;
- EEE-specific Trend configuration: future C11.

### Minimum validation

- serialization/persistence tests;
- Screen and Popup insertion tests;
- multiple Trend instances;
- Multi-Pen rendering/query tests;
- Save/Publish/Activate/Runtime E2E/browser coverage;
- quality/no-data behavior;
- `pt-BR`, `en`, `es` tests;
- universal EliteSCADA CI;
- Wave 11 Active HMI Runtime workflow.

## 9. W14-C16 — HMI Command / Startup Screen / Popup Positioning

**Branch:** `wave14/c16-hmi-command-navigation-popup`  
**Priority:** **P0/P1**

### Owns

Confirmed findings:

- `C11-P2-CMD-02` — backend Operational Command exists, but authored Screen/Dynamo/Popup cannot canonically invoke it;
- `C11-P2-NAV-01` — Runtime startup Screen is lexical-order driven rather than explicitly authorable;
- `C11-P2-POP-02` — persisted authorable Popup X/Y is absent.

Product Owner decision: **Popup X/Y is mandatory before C11 release.**

### 9.1 Canonical HMI Operational Command invocation — P0

Add an authored visual action/bridge that can reference and execute a canonical Operational Command entity from normal Screen/Dynamo/Popup interaction.

The UI/action layer only requests execution. Existing backend authorization, Active Runtime resolution and audit remain authoritative.

Do not duplicate command semantics in React and do not replace a Command entity with direct TAG writes merely because `writeTag` already exists.

Direct TAG writes remain valid where the authored operation is genuinely a TAG/setpoint write. Command invocation must exist for actual Operational Command entities.

### 9.2 Explicit Startup/Home Screen — P1

Add a canonical persisted project/runtime home Screen reference configurable in normal Engineering.

Requirements:

- no naming-prefix workaround such as `00_Overview` as the product contract;
- validate referenced Screen existence;
- clear/fail-safe behavior if the configured Screen is removed/unresolved;
- Save/Publish/Activate projection;
- Active Runtime navigator consumes the configured home Screen.

### 9.3 Persisted authorable Popup X/Y — mandatory P0/P1

Add canonical Popup placement semantics.

Requirements:

- authorable through normal Engineering;
- persisted through project/package contracts;
- position survives Save/Publish/Activate;
- Active Runtime mount honors position;
- Popup position uses the same logical HMI coordinate space/transform as authored Screen content;
- scaled viewport/hit-target behavior remains correct;
- sane bounded/clamped behavior must be defined for invalid/off-canvas values rather than browser-dependent accidents;
- no DEMO-specific CSS/DOM positioning shortcut.

### Minimum validation

- action schema/serialization tests;
- command authorization and Runtime invocation test;
- Screen/Dynamo/Popup command action browser coverage;
- home Screen lifecycle tests;
- Popup position authoring/persistence/runtime tests;
- multi-resolution scaling/pointer tests for positioned Popup;
- universal EliteSCADA CI;
- Wave 11 Active HMI Runtime workflow.

## 10. W14-C17 — Internal Memory Authoring UX + Full Lifecycle E2E

**Branch:** `wave14/c17-memory-authoring-e2e`  
**Priority:** **P1 / required before C11 release**

### Owns

C11 findings:

- `C11-P2-MEM-02` — generic network-style Address semantics are incorrectly exposed for Internal Memory TAGs;
- `C11-P2-MEM-04` — full human Memory Source/TAG lifecycle remains unproven.

`C11-P2-MEM-03` is already supported: both Server Memory and Client Memory are discoverable as human-facing Data Source types. Preserve that behavior.

### Required UX correction

Internal Memory TAG authoring must not expose a meaningless network Address field/assistant.

Use normal Source capability/schema semantics so the UI understands that Server/Client Memory has typed internal settings rather than a network binding address.

Do not special-case the future EEE package by name or ID.

### Required lifecycle proof

Add real product coverage for the normal human path from an ordinary project:

`create Server/Client Memory Source -> create Memory TAG -> configure typed initial/default value -> Save -> Publish -> Activate -> observe Active Runtime value -> authorized write/read -> historian behavior when configured`

The test must not start from hidden package JSON or pre-insert private provider IDs as the only path.

### Minimum validation

- focused Engineering component tests;
- backend catalog/source regression tests;
- browser E2E covering the full authoring lifecycle;
- Server Memory and Client Memory distinction;
- Save/Publish/Activate/Runtime proof;
- universal EliteSCADA CI;
- Runtime workflow as appropriate.

## 11. W14-C18 — Embeddable Alarm + Event Browser HMI Objects

**Planned branch:** `wave14/c18-hmi-alarm-event-browsers`  
**Priority:** **P0 after prerequisites**  
**Implementation state:** **BLOCKED UNTIL COORDINATOR RELEASES A BASE**

### Why C18 does not start implementation immediately

C18 consumes two contracts still being corrected in parallel:

1. the first-class operational Event model/history/query from C14;
2. the stabilized reusable embeddable HMI-object/configuration pattern developed through C15/C07 contracts.

Starting implementation against guessed contracts would create avoidable divergence and merge conflict. The Coordinator will publish a new exact C18 base after the needed upstream contracts are accepted into integration.

A DEV chat may read/audit/design C18 meanwhile, but must not commit product implementation until that explicit base/release.

### Owns when released

Confirmed/refined findings:

- `C11-P2-BROWSER-01` — configurable embeddable Alarm Browser;
- `C11-P2-BROWSER-02` — configurable embeddable Event Browser;
- `C11-P2-I18N-HIST-01` — related mounted Historical Browser visible chrome remains English-hardcoded;
- relevant remaining `C11-P2-I18N-01` history/browser multilingual convergence.

### Alarm Browser requirements

First-class visual object for Screen and Popup with persisted configuration for practical filtering/presentation, including where the data model supports it:

- current vs historical mode;
- state/returned filters;
- acknowledged/unacknowledged;
- priority/severity;
- Area/Equipment/TAG scope;
- search/text;
- time range;
- visible columns;
- column order/sort;
- bounded row/page/result behavior;
- ACK/shelve interaction only through backend-authorized product APIs.

### Event Browser requirements

First-class visual object for Screen and Popup using C14's Event model, with persisted configuration for relevant filters such as:

- type/category;
- source/origin;
- Area/Equipment/TAG scope;
- user/operator and command/operation context where applicable;
- time range;
- search;
- visible columns;
- column order/sort;
- bounded result behavior.

### Multilingual

Alarm/Event/history-browser visible UI covered by this package must support `pt-BR`, `en`, `es`.

### Minimum validation after release

- Screen and Popup insertion;
- persisted configuration;
- multiple instances;
- protected backend interactions;
- Save/Publish/Activate/Runtime E2E;
- Event Browser against C14 persisted/query data;
- three-language browser tests;
- universal EliteSCADA CI;
- Wave 11 Active HMI Runtime workflow.

## 12. Parallel execution plan

### Stage P1 — immediate independent DEV work

Start in parallel:

- C12 — Server Runtime Automation;
- C13 — Simulation Quality;
- C14 — Operational Events;
- C15 — Trend Multi-Pen;
- C16 — HMI Command/Home/Popup X/Y;
- C17 — Memory authoring/E2E.

All six receive the same coordinator-published documentation checkpoint as their initial branch base. They must revalidate live ancestry before changing code.

### Stage P2 — dependent work

C18 implementation starts only after Coordinator acceptance/integration of:

- C14 event domain/query contract;
- the visual-object/configuration contracts needed from C15 or an equivalent accepted common baseline.

Coordinator will then create or reset C18 from the exact accepted integration SHA and issue an explicit implementation release.

## 13. Conflict-avoidance ownership guidance

The following boundaries are intentional:

- C12 owns server script/runtime automation host, not Event persistence or HMI objects;
- C13 owns quality-origin/source contract, not Script scheduling;
- C14 owns Event domain/persistence/query, not Event Browser UI;
- C15 owns Trend visual/schema/editor/runtime, not Alarm/Event Browser;
- C16 owns visual command actions, startup navigation and Popup positioning, not the broad visual-property registry unless a bounded shared extension is required;
- C17 owns Memory Engineering semantics/E2E, not generic Driver/Source redesign;
- C18 consumes C14 and common visual contracts, and does not redefine them independently.

When a package needs a shared contract owned by another active package, document the dependency and coordinate through the Coordinator rather than silently editing the same contract in incompatible ways.

## 14. Expected integration sequence

Development may run in parallel. Integration order is decided by accepted contracts, not package number.

Initial intended sequence:

1. C13 quality contract;
2. C17 Memory UX/E2E;
3. C14 Operational Events;
4. C16 command/navigation/Popup positioning;
5. C15 Trend Multi-Pen;
6. C12 Server Runtime Automation, including final alignment with accepted C13 quality contract;
7. establish C18 base;
8. C18 Alarm/Event Browser objects;
9. coordinator C10 convergence cycle 2.

The Coordinator may adjust exact ordering when real diffs show a safer dependency route. No package may reinterpret that flexibility as permission to merge directly to `main`.

## 15. Per-package DEV completion contract

Every DEV must return:

- exact branch name;
- exact starting/base SHA;
- exact final candidate HEAD;
- summary of files/contracts changed;
- mapping from C11 finding(s) to delivered behavior;
- tests added/updated;
- exact workflow/run evidence;
- known limitations/deferred items;
- explicit statement that no DEMO-specific/private bypass was introduced;
- recommended coordinator integration notes and dependency assumptions.

A package is not accepted merely because its local tests pass.

## 16. Coordinator convergence after C12-C18

After approved corrections are integrated:

1. run universal EliteSCADA CI plus every impacted specialized workflow;
2. diagnose/fix integration regressions;
3. perform **C10 convergence cycle 2**;
4. freeze a new exact product-code SHA;
5. revalidate every affected C11 Pass 2 finding against that new SHA;
6. execute remaining real-browser validations for Memory, visual states, Multi-Pen Trend, Popup positioning, fullscreen/resolutions and HMI object composition;
7. only when blocking gaps are cleared, issue a separate explicit `RELEASE C11 IMPLEMENTATION` decision.

## 17. C11 implementation remains locked

None of C12-C18 is permission to build the canonical EEE DEMO.

After convergence and affected C11 revalidation, Coordinator/Development Lead must explicitly release implementation. At that point the repository must create/finalize:

`docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`

The future EEE Simulation is then authored as a normal EliteSCADA application using the corrected public product mechanisms. If implementation still requires a private shortcut, that is evidence that a product gap remains and the release decision must be revisited.

## 18. Final route

`C11 Pass 2 consolidated`
`-> C12-C17 parallel product corrections`
`-> integrate prerequisite contracts`
`-> C18 Alarm/Event Browser product correction`
`-> C10 convergence cycle 2`
`-> new exact product freeze`
`-> affected C11 revalidation + real-browser acceptance`
`-> explicit C11 implementation release`
`-> canonical DEMO premise/architecture`
`-> EEE DEMO Simulation through normal product tools`
`-> Save/Publish/Activate/Runtime acceptance`
`-> physical PLC/Modbus remap/validation later`
`-> full CI + Preview/Codespaces`
`-> Product Owner browser homologation`
`-> accepted Wave 14 baseline`
`-> resume Wave 13 packaging/signing on accepted Wave 14 bytes`
