# W14-C11 — Pass 2 Product-Gap Audit

**Owner:** W14-C11 audit lane  
**Coordinator:** Wave 14 Coordinator / Development Lead  
**Product Owner:** requirements authority for the canonical EEE DEMO  
**Date:** 2026-09-03 BRT  
**State:** PASS 2 CONSOLIDATED / IMPLEMENTATION LOCKED  
**Audit product-code SHA:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`

> This document is the canonical C11 Pass 2 product-gap result. It audits the exact frozen C01-C10 product against the approved canonical EEE requirements. It does not authorize C11 implementation and does not narrow requirements to fit current product limitations.

## 1. Authority and frozen evidence

Requirements authority:

- `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`

Pass 2 release/freeze authority:

- `docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`

Audit branch:

- `wave14/c11-pass2-product-gap-audit`

Frozen product-code authority:

- `97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Exact frozen-SHA workflow evidence revalidated during Pass 2:

- EliteSCADA CI #1273 — SUCCESS;
- Wave 11 Active HMI Runtime #203 — SUCCESS;
- Preview Licensing CI #225 — SUCCESS;
- L3 Seven-Driver Lab #180 — SUCCESS;
- Interop Lab Smoke #102 — SUCCESS.

PR #212 remains DRAFT, open and unmerged to `main`. Documentation-only `[skip ci]` commits after the frozen SHA do not supersede the product-code authority.

C11 implementation remains locked. The historical `SimulationDriver`, `DemoRuntimeServices`, historical DEMO page/package, direct DOM/React manipulation, hidden package JSON, Driver internals and DEV-only shortcuts are explicitly excluded as evidence or mitigation.

## 2. Canonical EEE acceptance intent

The future canonical application remains a realistic **EEE — Estação Elevatória de Esgoto**, serving simultaneously as:

- commercial/product DEMO;
- Engineering example;
- operator Runtime demonstration;
- Product Owner homologation application;
- regression/acceptance application;
- canonical EliteSCADA application example;
- later physical PLC/Modbus proof.

Required experience remains unchanged: two pumps/motors; stopped/running/fault/trip/unavailable/bad-quality states; non-color-only critical-state semantics; wet well level and `% Full`; animated liquid; coherent flow/pressure/current/frequency; contextual Popups; reusable Dynamos/public properties/TAG bindings; alarms; events; trends; history; PNG/background assets; correct Operator versus Engineering/Diagnostics boundaries; and `pt-BR`, `en`, `es`.

The first variant must be a living, deterministic **DEMO Simulation**. The later **DEMO PLC** should preserve the conceptual HMI and logical TAG identities while replacing internal/simulated Source mapping with real Modbus mapping.

## 3. Evidence discipline

Classification is exactly one of:

- `SUPPORTED`
- `PARTIALLY SUPPORTED`
- `PRODUCT GAP`
- `NEEDS VALIDATION`

Disposition is exactly one of:

- `no action required`
- `fix before C11`
- `known mitigation acceptable`
- `defer to later Wave`
- `requires Development Lead decision`
- `requires real-browser validation`
- `requires PLC validation later`

A DTO/interface by itself is not proof. `SUPPORTED` requires a credible normal product path. Browser-only visual quality and physical PLC operation are not promoted from static evidence.

## 4. Consolidated Pass 2 matrix

| ID | Requirement / capability | Classification | Evidence | Current product behavior | EEE impact | Simulation impact | PLC/Modbus impact | Recommended disposition |
|---|---|---|---|---|---|---|---|---|
| C11-P2-GOV-01 | Exact C10 converged product checkpoint | SUPPORTED | Frozen release docs, commit `97eefd8...`, five exact-SHA workflows green | Stable product authority for audit | Prevents moving-target acceptance | Same frozen contracts | Same frozen contracts | no action required |
| C11-P2-DS-01 | Backend-authoritative Data Source forms | SUPPORTED | `EngineeringDataSourceTypeCatalog`, `/api/engineering/data-source-types`, `DataSourceCatalogEditor` schema-driven fields | Human-facing typed Source configuration exists | Normal integrator path | Supports Memory Source authoring | Supports Modbus Source authoring | no action required |
| C11-P2-TAG-01 | Protocol-aware TAG address assistance | SUPPORTED | `TagAddressEditor` specialized assistants for Modbus TCP, OPC UA, DNP3, IEC-104 | Major protocols do not require memorized opaque syntax | Realistic TAG authoring | Internal Memory handled separately | Modbus mapping supported structurally | no action required |
| C11-P2-MEM-01 | Canonical Server Memory / Client Memory concepts | SUPPORTED | `builtin.memory.server`, `builtin.memory.client`, typed initial values, Server Memory retention and runtime composition | Shared server state and client-local state are distinct | Correct internal-memory foundation | Server Memory is appropriate shared process-state candidate | Clean conceptual separation from physical Sources | no action required |
| C11-P2-MEM-02 | Internal Memory TAG authoring without meaningless network Address | PRODUCT GAP | `MemoryTagSettingsPanel` states no network address; generic `TagAddressEditor` still renders Address for Memory | Engineering exposes conflicting semantics | Confusing canonical authoring | Directly affects Simulation TAG creation | Clarifying fix improves internal vs physical Source boundary | fix before C11 |
| C11-P2-MEM-03 | Human creation/discovery of both Memory Source types without private IDs | SUPPORTED | Backend catalog publishes `Server Memory` and `Client Memory`; UI selector shows human display names while type keys remain internal | Both are normal Data Source choices | Integrator need not know `builtin.memory.*` | Shared/client memory can be selected normally | None direct | no action required |
| C11-P2-MEM-04 | Full Memory Source -> TAG -> Save -> Publish -> Activate -> Runtime E2E | NEEDS VALIDATION | Existing `internal-memory.spec.ts` starts from preconfigured Memory Source/TAG; static authoring/runtime pieces exist but requested complete flow is not proven | End-to-end human flow remains unexecuted | Must prove empty-project workflow | Could expose hidden lifecycle defect | None direct | requires real-browser validation |
| C11-P2-SCR-01 | Activated Server Python host/executor/scheduler/timer lifecycle | PRODUCT GAP | Frozen scripting architecture describes Server Scripts as later capability; `Program.cs` has no Server Python host/executor/scheduler; only Client Visual runtime and excluded legacy Simulation host exist | Server Script project data/contracts exist without executable active server host | Canonical server automation unavailable | **Blocks autonomous mass-balance, sequencing, alternation and deterministic physics** | PLC can obtain physical state externally, but Simulation gap remains | fix before C11 |
| C11-P2-SIM-01 | Deterministic living EEE process model through normal product mechanisms | PRODUCT GAP | Server Memory is writable/shared, but no canonical periodic server execution producer exists at frozen SHA | Downstream TAG/alarm/historian pipeline exists; autonomous process producer does not | Required living station cannot be built canonically | **Blocking** | PLC variant not dependent on simulator physics | fix before C11 |
| C11-P2-QUAL-01 | Deliberate bad/stale/unavailable quality generation in Simulation | PRODUCT GAP | `ISourceProvider.WriteAsync` accepts value only; Server/Client Memory force every authored write/reset/init to `TagQuality.Good`; no public quality-degradation API | Memory Simulation cannot intentionally publish non-Good quality | Mandatory communication-loss scenario unavailable | **Blocking** | Real Driver can originate quality later; not a Simulation substitute | fix before C11 |
| C11-P2-QUAL-02 | Propagation/interpretation of non-Good quality from a real Source | SUPPORTED | `TagValue` carries quality; `CurrentTagCache` preserves sample; alarms treat non-Good as Communication condition; runtime/trend models carry quality | Downstream runtime understands abnormal quality | Supports quality-aware operator state structurally | Waiting on QUAL-01 for simulated origin | Supports Driver-originated quality structurally | no action required |
| C11-P2-VIS-01 | Numeric `% Full` | SUPPORTED | Numeric TAG/binding/property contracts and scale semantics | Numeric well percentage can be shown | Required primary-screen value available | Driven by simulated level after producer exists | Same logical TAG can be PLC-backed | no action required |
| C11-P2-VIS-02 | Analog Fill for wet-well liquid | SUPPORTED | `VisualAnalogFillEngineeringDto` supports min/max, clamp, invert and four fill directions | First-class canonical fill behavior exists | Enables animated wet well without DOM manipulation | Same simulated level binding | Same PLC level binding | no action required |
| C11-P2-VIS-03 | Actual live Analog Fill/operator visual quality in mounted browser Runtime | NEEDS VALIDATION | Structural contract exists; no Pass 2 real-browser homologation of changing wet-well value was executed | Runtime visual quality not yet observed for C11 target | Must prove intended appearance/animation | Same | Same | requires real-browser validation |
| C11-P2-DYN-01 | Reusable Dynamos with typed public parameters/TAG references | SUPPORTED | Canonical Dynamo definitions/parameters include `TagReference`, `EquipmentPath`, typed values and reusable child composition | One pump definition can represent GMB01/GMB02 | Canonical reuse possible | Simulation bindings can target logical TAGs | Same definitions survive physical remap conceptually | no action required |
| C11-P2-DYN-02 | Pump running/fault/unavailable/bad-quality operator semantics | PARTIALLY SUPPORTED | Runtime state vocabulary includes active/inactive/fault/alarm/bad-quality/transition concepts; quality-aware dynamics exist | Semantic ingredients exist, but final non-color-only presentation is not proven | Critical for operator comprehension | QUAL-01 also blocks simulated bad-quality state | Real Driver quality can feed semantics | requires real-browser validation |
| C11-P2-DYN-03 | Multiple instances of same Dynamo with independent bindings | NEEDS VALIDATION | Per-instance identity and parameters exist structurally; C11 has not run the two-pump browser authoring/runtime proof | Architecture supports independence, mounted proof pending | Needed for GMB01/GMB02 reuse | Same | Same | requires real-browser validation |
| C11-P2-POP-01 | Canonical Popup open/close/context | SUPPORTED | `VisualNavigationActionKind` has `OpenPopup`/`ClosePopup`; runtime navigator mounts instance context | Contextual Popups are product behavior | Equipment detail can be reused | Same | Same | no action required |
| C11-P2-POP-02 | Persisted authorable Popup X/Y placement | PRODUCT GAP | Popup navigation/mount contracts expose target/context/stacking but no authored mount X/Y; runtime `<section>` receives no canonical position coordinates | Shell-defined/centralized placement only | Limits contextual placement near equipment | Presentation only | Same limitation | requires Development Lead decision |
| C11-P2-NAV-01 | Explicit authorable startup/home Runtime Screen | PRODUCT GAP | `RuntimeApplicationMount` sorts Screen keys and selects `keys[0]`; no persisted project home reference is consumed | Startup is lexical-order driven | `00_...` naming would be a workaround, not product contract | Same | Same | fix before C11 |
| C11-P2-CMD-01 | Backend canonical Operational Command execution | SUPPORTED | `/api/commands/{id}/execute` resolves Active Runtime command, enforces `CommandExecute`, scopes authorization and audits result | Secure command backend is real | Command definitions are meaningful product entities | Could execute against Simulation TAGs if UI can invoke them | Same for PLC-backed commands | no action required |
| C11-P2-CMD-02 | Invoke canonical Operational Command from authored Screen/Dynamo/Popup | PRODUCT GAP | Visual actions only Navigate/OpenPopup/ClosePopup; default Client Visual Python provider exposes TAG/memory/visual operations but does not wire `requestBackendOperation`; no canonical `ExecuteCommand` visual action found | Backend Command exists but normal HMI authoring has no command invocation bridge | Equipment Popup cannot canonically trigger a Command entity | Required operator control would otherwise fall back to direct TAG write | Same gap for PLC commands | fix before C11 |
| C11-P2-WRITE-01 | Authorized process TAG write from normal Runtime visual/script boundary | SUPPORTED | Client Visual Python `writeTag` uses `/api/tags/{id}/write`; backend enforces writable TAG, authorization and audit | Safe mediated TAG writes exist | Enables setpoints/simple control without Driver access | Can write writable Server Memory TAGs | Can write writable Driver TAGs according to runtime rules | no action required |
| C11-P2-ALM-01 | Alarm activation, ACK and return-to-normal | SUPPORTED | `InMemoryAlarmEngine` subscribes `TagValueChanged`, evaluates alarm types, supports Active/Acknowledged/Returned/Shelved; protected API authorizes ACK | Real operational alarm lifecycle exists | EEE alarms can use normal definitions | Same simulated TAGs feed alarms once Simulation producer exists | Same logical TAGs under PLC | no action required |
| C11-P2-ALMUI-01 | Operator Alarm Center in pt-BR/en/es | SUPPORTED | `RuntimeAlarmCenter` has localized copy and server-confirmed ACK flow; C09 operator overlay mounts it without reflowing HMI | Operator alarm UI is present | Required alarm handling available | Same | Same | no action required |
| C11-P2-HIST-01 | Historian capture from canonical TAG changes | SUPPORTED | Server Memory runtime writes update `CurrentTagCache`; historian subscribes `TagValueChanged`; Timescale and memory providers honor historian policy | Same TAG stream feeds current/historical state | Process history architecture exists | Missing producer is SCR-01, not historian defect | Logical TAG identity remains historian association | no action required |
| C11-P2-ALMHIST-01 | Durable alarm-history persistence/query | SUPPORTED | With Historical Query + Timescale/PostgreSQL, hosted service persists `AlarmStateChanged`; `alarm.events` is queryable | Alarm activation/ACK/recovery history is durable | Required alarm history available | Same | Same | no action required |
| C11-P2-EVT-01 | First-class general operational events distinct from alarms/audit | PRODUCT GAP | `EngineeringPackage` and `ImportEntityKind` contain no Event entity; Historical Query exposes historian samples and alarm events only; internal event bus is not engineer-authorable operator history | Ordinary equipment transitions have no canonical event definition/history surface | Pump start/stop events cannot be represented honestly as events | Process transitions lack canonical event sink/history | Same for PLC-backed transitions | fix before C11 |
| C11-P2-TREND-01 | Operator-accessible process trend chart | PRODUCT GAP | `BasicTrendViewer` exists and is localized, but `main.tsx` mounts `/runtime/history` as tabular `HistoricalDataBrowserRuntime`; AppNavigation has no Trend route; no canonical Screen Trend visual identified | Historian exists but chart is not exposed as normal operator/authored surface | Required level/flow/pressure/current trends unavailable canonically | Samples could exist but operator cannot consume required chart normally | Same | fix before C11 |
| C11-P2-HISTORY-01 | Operator historical data browser | SUPPORTED | `/runtime/history` mounts protected `HistoricalDataBrowserRuntime` over Historical Query datasets | Tabular historian/alarm-history consultation exists | Useful support surface | Same | Same | no action required |
| C11-P2-I18N-HIST-01 | Historical Data Browser visible UI in pt-BR/en/es | PRODUCT GAP | Mounted historical browser/controller contains hard-coded English labels, states, search/sort/paging copy rather than common locale | History surface is English-only in significant chrome | Violates canonical multilingual operator requirement | Same | Same | fix before C11 |
| C11-P2-I18N-01 | C11-relevant visible surfaces overall in pt-BR/en/es | PARTIALLY SUPPORTED | C10 localized shell, Engineering diagnostics/script surfaces; Runtime Alarm Center and BasicTrendViewer have three-language copy; Historical Browser remains English-hardcoded | Multilingual infrastructure is strong but incomplete on a required mounted surface | Cannot declare whole EEE path multilingual | Same | Same | fix before C11 |
| C11-P2-SHELL-01 | Operator vs Engineering/Diagnostics separation | SUPPORTED | Capability-driven AppNavigation plus Playwright shell test verifies Runtime without Engineering chrome/TAG inspector and capability-gated navigation | Runtime-only users receive operator-focused shell | Correct operator boundary | Same | Same | no action required |
| C11-P2-VIEW-01 | Fixed logical HMI scaling across 720p/1080p/1440p/4K | PARTIALLY SUPPORTED | Runtime uses deterministic 1920x1080 logical size; tests cover 1280x720, 1920x1080, 2560x1440, 3840x2160 plus letterbox and pointer inverse transform | Architecture/scaling math is deliberate; real C11 visual homologation pending | Suitable fixed-coordinate HMI strategy | Same | Same | requires real-browser validation |
| C11-P2-FULL-01 | Fullscreen/no document scroll/alarm overlay without HMI reflow | NEEDS VALIDATION | CSS uses `overflow:hidden`, fullscreen 100vw/100vh and absolute alarm overlay; C09 browser tests cover shell composition but Pass 2 has not exercised all target resolutions/fullscreen states | Static/browser-test evidence is strong but acceptance observation incomplete | Important commercial/operator quality | Same | Same | requires real-browser validation |
| C11-P2-ASSET-01 | Project PNG/background assets | SUPPORTED | Project raster asset import/selection, stable asset references and surface fit/preview mechanisms exist | Canonical project-owned visuals available | Enables polished EEE presentation | No simulation coupling | No PLC coupling | no action required |
| C11-P2-PLC-01 | Preserve conceptual HMI/TAG identity when moving Simulation -> Modbus | SUPPORTED | `TagEngineeringDto` has stable ID separate from DataSource/address; `assignTagDataSource` preserves TAG and clears only source-specific binding when Source changes; visual bindings use stable `TagValueReference`; alarms reference `TagId`; historian keyed by TAG ID | Logical identity is decoupled from physical mapping | Simulation can target logical TAGs | Screens/Dynamos/Popups/alarms/history can remain while Source/address changes | requires PLC validation later |
| C11-P2-MODBUS-01 | Current Modbus mapping/address assistant | SUPPORTED | Modbus assistant builds canonical area/reference, zero/one-based policy, unit ID, value type, word order, scaling, offset and bit selector through backend builder | Current product has human-facing Modbus binding workflow | None until PLC stage | Enables remapping without historical FvDesigner syntax assumptions | requires PLC validation later |
| C11-P2-CHAIN-01 | End-to-end `Simulation -> TAG -> Alarm/Event/Historian -> Binding -> Dynamo/Screen/Popup` | PARTIALLY SUPPORTED | TAG/current-cache, alarm, historian, visual binding/Dynamo/Popup downstream are present; Server Simulation producer, general Event model, trend exposure and deliberate bad-quality origin have confirmed gaps | Downstream product chain is real but the complete canonical EEE chain cannot currently be assembled | Required acceptance chain incomplete | **Blocked by SCR-01, QUAL-01 and EVT-01** | PLC removes simulator producer dependency but EVT/TREND/UI gaps remain | fix before C11 |

## 5. Pass 1 revalidation outcome

Pass 1 conclusions were not carried forward blindly.

- “Internal Memory absent” is **superseded and corrected**: Server Memory and Client Memory are real canonical product concepts.
- Internal Memory Address semantics concern is **confirmed only as Engineering UX gap** (`MEM-02`).
- Memory Source discoverability is **resolved** (`MEM-03 SUPPORTED`).
- full empty-project Memory E2E remains **validation-only** (`MEM-04`).
- Analog Fill concern is **resolved structurally** (`VIS-02 SUPPORTED`).
- reusable Dynamo/public TAG references are **resolved structurally** (`DYN-01 SUPPORTED`).
- Popup open/close/context is **resolved structurally** (`POP-01 SUPPORTED`).
- Popup X/Y absence is **confirmed** (`POP-02 PRODUCT GAP`).
- startup Screen concern is **confirmed** (`NAV-01 PRODUCT GAP`).
- Server Script runtime-host uncertainty is **confirmed as a blocking product gap** (`SCR-01`).
- bad-quality support is split correctly: downstream propagation is supported (`QUAL-02`), deliberate Simulation origin is a gap (`QUAL-01`).
- legacy Simulation/Demo behavior remains excluded from C11 authority.

## A. Requirements fully supported

The frozen product already provides a substantial canonical base:

- exact converged C01-C10 product/CI checkpoint;
- backend-authoritative schema-driven Data Source forms;
- protocol-aware TAG address assistants;
- discoverable Server Memory and Client Memory Sources;
- typed/retentive Server Memory and client-local Client Memory concepts;
- numeric `% Full` and canonical Analog Fill;
- reusable typed Dynamos and stable TAG-reference concepts;
- contextual Popup open/close/context;
- safe authorized TAG writes;
- secure backend Operational Command execution;
- alarm activation/ACK/return/shelving semantics;
- localized operator Alarm Center;
- historian capture from canonical TAG changes;
- durable alarm-history persistence/query;
- operator tabular Historical Data Browser;
- project PNG/background asset flow;
- capability-driven Operator vs Engineering/Diagnostics boundary;
- logical TAG/source separation suitable for later Modbus remapping;
- current Modbus Address Assistant.

## B. Partial capabilities

The following product areas have real supporting architecture but are not sufficient for final C11 acceptance as-is:

- pump/Dynamo fault/unavailable/bad-quality visual semantics: vocabulary exists, final non-color-only operator presentation needs browser proof;
- fixed logical 1920x1080 HMI scaling: transform tests cover all requested target resolutions, but real-browser presentation/fullscreen must still be homologated;
- multilingual: most C10/shell/alarm surfaces are localized, but the mounted Historical Data Browser is not;
- complete Simulation-to-visual downstream chain: alarm/historian/binding/Dynamo/Popup pieces exist, but missing Simulation producer, general events and trend exposure prevent full assembly.

## C. Confirmed product gaps

### Blocking/functional gaps

1. **C11-P2-SCR-01 — Server Python Runtime host**  
   No active project Server Script executor/scheduler/timer lifecycle exists at the frozen product SHA.

2. **C11-P2-SIM-01 — Living deterministic Simulation producer**  
   No normal engineer-authorable server periodic mechanism can currently implement the EEE physics.

3. **C11-P2-QUAL-01 — Deliberate Simulation bad-quality generation**  
   Internal Memory writes always produce `Good`; no canonical Simulation quality-degradation mechanism exists.

4. **C11-P2-EVT-01 — General operational events**  
   No first-class Engineering/runtime/history model exists for ordinary operational events distinct from alarms/audit.

5. **C11-P2-TREND-01 — Operator trend surface**  
   A trend component exists in source, but the converged operator shell does not expose it and Screen Engineering has no identified canonical Trend visual.

6. **C11-P2-CMD-02 — Operational Command invocation from authored HMI**  
   Secure backend execution exists, but canonical visual actions/default Client Visual runtime do not bridge to Command execution.

### Product/Engineering/Runtime UX gaps

7. **C11-P2-MEM-02 — Internal Memory Address semantics**  
   Memory TAG authoring still displays an irrelevant network-style Address field.

8. **C11-P2-NAV-01 — Explicit startup/home Screen**  
   Runtime chooses the first Screen by lexical key order rather than a persisted authorable home reference.

9. **C11-P2-I18N-HIST-01 — Historical Browser localization**  
   Required mounted operator-history UI contains hard-coded English copy.

10. **C11-P2-POP-02 — Popup X/Y placement**  
    Open/context works, but persisted authorable mount coordinates do not exist in the accepted contract. Development Lead must decide whether centered/shell-defined placement is an acceptable product limitation or whether C11 requires a fix.

## D. Validation-only uncertainties

These are not currently classified as failures:

1. **MEM-04:** complete empty-project Memory Source + TAG + Save/Publish/Activate/Runtime path;
2. **VIS-03:** live wet-well Analog Fill presentation under changing TAG values;
3. **DYN-02/DYN-03:** final non-color-only pump state UX and independent two-instance Dynamo bindings;
4. **VIEW-01/FULL-01:** actual browser behavior at 1280x720, 1920x1080, 2560x1440, 3840x2160, including fullscreen, no unwanted document scroll, popup/alarm overlay stacking and hit targets;
5. final Product Owner visual acceptance of Runtime at target resolutions;
6. physical Modbus PLC connectivity/address correctness, explicitly deferred to hardware validation.

These must remain `requires real-browser validation` or `requires PLC validation later`; static contracts are not substituted for the missing acceptance evidence.

## E. Blocking gaps

The following gaps prevent release of canonical DEMO Simulation implementation under the current Product Owner direction:

- `C11-P2-SCR-01` Server Python Runtime host;
- `C11-P2-SIM-01` living deterministic Simulation producer;
- `C11-P2-QUAL-01` canonical bad-quality Simulation injection;
- `C11-P2-EVT-01` first-class operational events;
- `C11-P2-TREND-01` operator trend surface;
- `C11-P2-CMD-02` canonical HMI-to-Operational-Command bridge, unless the Development Lead explicitly removes Operational Command usage from the canonical operator-control requirement rather than silently replacing it with direct TAG writes.

The Product Owner preference is to correct legitimate product gaps before DEMO implementation. Therefore a technically possible private workaround is not a release argument.

## F. Non-blocking gaps / limitations requiring disposition

- `C11-P2-MEM-02` Internal Memory Address UX: technically authorable, but misleading and explicitly recommended `fix before C11`.
- `C11-P2-NAV-01` startup/home Screen: naming workaround exists but is not accepted as a product contract; recommended `fix before C11`.
- `C11-P2-I18N-HIST-01` Historical Browser localization: does not stop process physics but violates the approved multilingual application requirement; recommended `fix before C11`.
- `C11-P2-POP-02` Popup X/Y: centered/shell-defined placement may be acceptable if explicitly approved; otherwise fix before C11.
- fixed 1920x1080 logical canvas is treated as an intentional product strategy, not a gap, provided real-browser scaling acceptance passes.

## G. Recommended coordinator action

### `KEEP C11 IMPLEMENTATION LOCKED`

C11 does not have release authority. This is a recommendation to Coordinator/Development Lead.

Recommended route:

1. keep PR #212 DRAFT and do not merge Wave 14 to `main`;
2. disposition every confirmed gap above;
3. create bounded pre-DEMO correction lanes for the fixes approved before C11;
4. do **not** implement those fixes inside the C11 audit branch;
5. integrate approved corrections into `wave14/corrections-integration`;
6. run universal EliteSCADA CI plus affected specialized workflows on exact candidate heads;
7. if product code changes, perform a new C10 convergence freeze on a new exact product SHA;
8. revalidate all affected C11 rows against that new freeze;
9. execute the remaining real-browser Memory/visual/multi-resolution acceptance checks;
10. only if blocking gaps are cleared or explicitly accepted by Development Lead, issue a separate explicit C11 implementation release;
11. at that later release, create/finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md` and record the full DEMO architecture in GitHub before or alongside implementation;
12. build DEMO Simulation through normal product contracts;
13. later map the same logical application to the physical Modbus PLC and mark hardware behavior `requires PLC validation later` until executed;
14. after canonical DEMO acceptance, update Preview/Codespaces and complete Product Owner browser homologation;
15. only after accepted Wave 14 product baseline resume Wave 13 Windows packaging/signing.

### Pass 2 conclusion

The product is much closer to being able to host a serious canonical EEE than the original Pass 1 suggested: Internal Memory, Analog Fill, Dynamos, Popup context, alarms, historian, alarm history, operator shell and logical Source/TAG separation are all real product capabilities.

However, the exact frozen product still cannot build the required canonical EEE Simulation without violating approved boundaries. The missing Server Python execution host, deliberate Simulation quality control, general operational events, operator trend exposure and canonical HMI Command bridge are product gaps, not DEMO authoring inconveniences.

Therefore the conservative recommendation is no longer merely provisional:

`KEEP C11 IMPLEMENTATION LOCKED`
