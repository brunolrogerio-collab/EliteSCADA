# W14-C11 — Pass 2 Runtime Audit Evidence

**Date:** 2026-09-03 BRT  
**Product-code authority:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`  
**Scope:** evidence-only continuation of C11 Pass 2. C11 implementation remains locked.

This checkpoint closes two high-priority static product questions and strengthens a third. It does not authorize DEMO implementation or product correction from the C11 lane.

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

## Consequence for C11 release recommendation

The static audit now has a confirmed **blocking functional product gap** in the intended Server Python execution path, plus an explicit startup/home Screen product gap and the Popup placement gap described above.

Current recommendation therefore remains:

`KEEP C11 IMPLEMENTATION LOCKED`

Next audit priority remains deliberate Simulation bad-quality generation, followed by Memory Source E2E authoring and alarm/event/history/trend end-to-end behavior.