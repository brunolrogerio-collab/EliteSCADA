# Wave 14 C19 — Live Progress

**Branch:** `wave14/c19-operational-event-authoring-script-bridge`  
**PR:** #257 — DRAFT -> `wave14/corrections-integration`  
**Exact product base:** `2e284606b605a26bb9632eae5264de30bea0acde`  
**C11:** IMPLEMENTATION LOCKED

This file is intentionally short and operational. Read the full contract in `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md` first.

## Current implementation checkpoint

Latest completed product commit at time of this note:

`677149a75cd9c7e8ebe0c1558b327029c316d1cb`

Completed:

- [x] C19 branch created from exact product base `2e284606...`;
- [x] PR #257 opened DRAFT to `wave14/corrections-integration`;
- [x] full implementation handoff committed at `68ac7ed1c019954f88671a983ee328dc9a7ceb47`;
- [x] protected Operational Event Engineering editor added at `b253f3fe5671f10d5988411b7120fb3cd848a901`;
- [x] normal Engineering navigation wired to Operational Events at `677149a75cd9c7e8ebe0c1558b327029c316d1cb`;
- [x] editor uses canonical package mutation flow: load Workspace version -> Preview package -> verify CAS version unchanged -> Apply package;
- [x] editor supports create/edit, stable ID, key/name/type/category/source/area/equipment/TAG/message/enabled;
- [x] TAG association uses configured TAG selector rather than requiring raw GUID entry;
- [x] affected editor/nav copy exists for pt-BR / en / es.

Not yet complete / do not claim accepted:

- [ ] web build/TypeScript validation for the new editor/navigation;
- [ ] explicit shared `EngineeringPackageView.operationalEvents` web type cleanup if needed after build review;
- [ ] Server Script `emit_operational_event(...)` implementation;
- [ ] Active Revision-safe runtime bridge into C14 `IOperationalEventRuntime`;
- [ ] backend tests;
- [ ] Engineering/browser E2E;
- [ ] exact candidate freeze;
- [ ] five candidate gates;
- [ ] Coordinator architecture review;
- [ ] composition into integration;
- [ ] five combined gates;
- [ ] C03 DNP3 carry-forward on exact combined SHA;
- [ ] real Preview/browser carry-forward;
- [ ] final C11 revalidation.

## Runtime bridge map already confirmed

Existing production Server Script activation/host call sites that must be preserved:

1. `ScadaRuntimeFacade.Describe()` resolves the shared `ServerScriptRuntimeManager` for diagnostics;
2. `PublishedRuntimeActivationService.ActivateAsync()` resolves the shared manager and calls `ActivateRuntimeAsync(...)` for normal Publish -> Activate;
3. `PersistedRuntimeRecoveryService.RecoverAsync()` resolves the same shared manager for startup recovery.

Existing C14 event authority:

- `GatewayEngineeringRuntimeCoordinator : IOperationalEventRuntime`;
- enabled Operational Event definitions are swapped only after successful Active revision activation;
- `TryGetOperationalEvent(definitionId, ...)` reads the active definition snapshot;
- `EmitOperationalEventAsync(...)` constructs occurrence through `OperationalEventContract.CreateOccurrence(...)` and publishes it on the canonical event bus;
- disabled definitions are absent from the active snapshot, so emission naturally fails closed.

Important DI fact:

- `IEngineeringRuntimeCoordinator` may be the licensed host-owned wrapper;
- the concrete `GatewayEngineeringRuntimeCoordinator` remains registered separately and is the raw C14 Operational Event authority;
- do not accidentally create a second runtime coordinator or bind Server Script to a different runtime identity.

## Next exact implementation step

Implement Server Script emission without bypassing the canonical C14 runtime.

Preferred behavior:

Python API:

`emit_operational_event(definition_id, message=None, context=None)`

Required semantics:

- deterministic Server Script subset only;
- bounded message/context;
- stable definition ID;
- no direct event-bus access from Python;
- no history writes from Python;
- C# validates/replays request through Active `IOperationalEventRuntime`;
- stale script revision must not emit into a newer Active revision;
- unresolved/disabled definition fails closed;
- preserve existing TAG/Server Memory script behavior.

Before committing the runtime bridge, explicitly solve the concurrency boundary between Server Script execution and Active revision swap. Existing TAG writes use the `ServerScriptRuntimeManager` revision gate. Operational Event emission needs equivalent protection; do not rely only on a pre-call revision check with a race window.

## If a new Coordinator chat resumes here

1. revalidate live #257 and branch HEAD;
2. confirm HEAD descends from `2e284606...`;
3. read `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`;
4. read this file;
5. inspect the two product commits above;
6. do not restart the Engineering editor from scratch;
7. continue at the Server Script/runtime bridge concurrency step;
8. run build/tests only after inspecting current live HEAD for any newer commits;
9. diagnose all red gates before rerun.
