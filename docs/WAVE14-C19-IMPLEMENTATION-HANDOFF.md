# Wave 14 C19 — Operational Event Authoring + Server Script Emission Bridge

**Coordinator status:** IMPLEMENTATION STARTED / C11 REMAINS LOCKED  
**Branch:** `wave14/c19-operational-event-authoring-script-bridge`  
**Exact product base:** `2e284606b605a26bb9632eae5264de30bea0acde`  
**Target:** `wave14/corrections-integration`  
**PR:** DRAFT only; never retarget to `main` and never merge without Coordinator acceptance.

## 1. Why C19 exists

C10 Convergence Cycle 2 revalidated C12–C18 successfully on product SHA `2e284606...`, including:

- EliteSCADA CI #1360 / `33920467689` — SUCCESS;
- Wave11 Active HMI Runtime #288 / `33920467682` — SUCCESS;
- Preview Licensing CI #310 / `33920467789` — SUCCESS;
- L3 Seven-Driver Lab #265 / `33920467676` — SUCCESS;
- Interop Lab Smoke #187 / `33920467665` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #91 / `33921310281` — SUCCESS on exact product SHA;
- Test Preview #16 / `33921526826` — SUCCESS using a disposable Preview-infrastructure overlay whose direct product parent was `2e284606...`; overlay PR #256 was closed without merge.

During the subsequent C11 Pass-2 revalidation, all major pre-DEMO gaps from C12/C13/C15/C16/C17/C18 were found supportable except one functional hole in Operational Events.

### Remaining blocker

`C11-P2-EVT-01`

C14 delivered canonical Operational Event definitions, Active Runtime occurrence emission, event-bus publication, durable historical persistence and protected query semantics. C18 delivered first-class Event Browser HMI consumption.

However, the product at `2e284606...` still lacks two ordinary integrator surfaces required to build a living EEE Simulation without private or DEMO-only wiring:

1. normal Engineering UI authoring for `OperationalEventEngineeringDto` definitions;
2. a generic Server Script API to emit a configured Operational Event through the accepted C14 Active Runtime authority.

C19 closes only those two gaps.

## 2. Confirmed existing architecture — do not duplicate it

### Engineering model/persistence already exists

Do not create a second Operational Event registry or persistence model.

`EngineeringExchangeService` already owns an `IOperationalEventEngineeringRegistry` and exports `operationalEvents` in the canonical Engineering package.

The current `EngineeringWorkspace.Scripts` object is `InMemoryScriptEngineeringRegistry`, which also implements `IOperationalEventEngineeringRegistry`. It has its own Operational Event collection and shares the normal workspace dirty/Clear lifecycle. This was a C14 design choice and must be preserved unless a separately proven defect requires change.

Therefore the normal protected package mutation path already supports `operationalEvents`:

`Working package -> Preview -> Apply/CAS -> Save -> Publish -> Activate`.

### Runtime emission already exists

`ScadaRuntimeFacade.EmitOperationalEventAsync(definitionId, context, cancellationToken)` delegates to the active `IOperationalEventRuntime` and fails if there is no Active Engineering runtime or the runtime does not expose Operational Event support.

`OperationalEventContract.CreateOccurrence(...)` is the canonical occurrence constructor.

Do not write directly to historical storage.  
Do not publish an ad-hoc alternate event type.  
Do not make Command execution implicitly emit Operational Events.

### Alarm / Operational Event / Audit remain separate

Preserve the C14 semantic boundary:

- Alarm = state/condition lifecycle;
- Operational Event = immutable process/operation occurrence;
- Audit = security/accountability record.

`POST /api/commands/{id}/execute` does not automatically create a C14 Operational Event and C19 must not change that merely to make testing convenient.

## 3. C19 binding product contract

C19 must make the following entirely possible using normal generic EliteSCADA mechanisms:

`Engineering -> author Operational Event definition -> Preview -> Apply -> Save -> Publish -> Activate -> Server Script -> emit configured occurrence -> C14 event bus -> durable operational.events -> C18 Event Browser`

No EEE-specific type, service, route, Driver, package patch, simulator host or hidden DEMO code is allowed.

## 4. Engineering authoring requirements

Add Operational Events as an ordinary Engineering section.

Minimum authorable fields, matching the existing canonical DTO:

- stable `id` managed normally;
- `key`;
- `name`;
- `type`;
- `category`;
- `source`;
- optional `area`;
- optional `equipmentPath`;
- optional `tagId` / `tagPath` according to existing contract;
- optional `message`;
- `enabled`;
- metadata when supported by the ordinary editor pattern.

Use the existing protected package Preview / Apply / CAS workflow. Prefer reuse of `useSecuredMutation`/Structured Editor patterns or a similarly canonical shared helper. Do not introduce an unprotected direct mutation endpoint solely for this editor.

The UI must:

- list/search existing definitions;
- create a new definition;
- edit a selected definition;
- preview before Apply;
- preserve stable identity on edits;
- visibly report validation/backend errors;
- have affected visible chrome in `pt-BR`, `en`, `es`;
- never require manual JSON editing for the normal path.

Suggested product files to inspect first:

- `web/scada-web/src/engineering/types.ts`;
- `web/scada-web/src/engineering/EngineeringApp.tsx`;
- `web/scada-web/src/engineering/StructuredEditors.tsx`;
- `web/scada-web/src/engineering/api.ts`;
- `web/scada-web/src/engineering/editorI18n.ts`;
- existing Alarm/Data Source/Script Engineering editors for interaction conventions.

Expected minimal type addition in web model:

`OperationalEventEngineering`

and:

`EngineeringPackageView.operationalEvents?: OperationalEventEngineering[]`.

## 5. Server Script emission requirements

Expose a generic deterministic Python API in Server Script. Working name:

`emit_operational_event(...)`

Preferred bounded surface:

`emit_operational_event(definition_id, message=None, context=None)`

The exact argument shape may be adjusted if the existing deterministic interpreter makes another equally generic form substantially safer, but the following are binding:

- Server Script only;
- stable Operational Event definition identity, not display-name matching;
- occurrence is created only by the canonical C14 runtime authority;
- active revision identity must be checked under the existing Server Script revision/lifecycle protection;
- unresolved definition fails closed;
- disabled definition fails closed or is rejected according to the existing C14 runtime contract;
- no direct history insert;
- no client-side emission authority;
- no hidden EEE-specific event names/IDs;
- sanitization/size limits remain bounded by canonical Operational Event contract;
- stale script generation cannot emit into a newer Active revision.

### Suggested execution architecture

Extend the isolated Python runner response with a separate collection of requested Operational Event emissions, analogous to but distinct from TAG write requests.

Conceptually:

Python process result:

- `writes[]` — existing TAG/Memory requests;
- `operationalEvents[]` — requested C14 emissions.

Then `IsolatedPythonScriptHandlerExecutor` replays each event request into the host only after validating the response structure.

`ServerScriptRuntimeManager` should expose an internal revision-gated method which delegates to the canonical Active Runtime Operational Event authority. Do not let the Python child publish directly to the event bus.

Relevant files:

- `src/Scada.Api/Runtime/ServerScriptRunner.py`;
- `src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs`;
- `src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs`;
- `src/Scada.Api/Runtime/ScadaRuntimeFacade.cs`;
- runtime/coordinator C14 implementation exposing `IOperationalEventRuntime`.

Before changing constructor/`GetShared` signatures, locate every `ServerScriptRuntimeManager.GetShared(...)` call and every activation path. Preserve the same instance across describe/activation. Do not accidentally bind the shared host to two different event/runtime authorities.

## 6. Tests required before candidate freeze

### Focused backend tests

Add deterministic tests proving at least:

1. configured Active Operational Event can be emitted by Server Script;
2. occurrence carries canonical definition identity/type/category/source;
3. optional message/context override flows through canonical C14 occurrence contract;
4. unknown definition is rejected;
5. stale Active revision cannot emit;
6. disabled definition behavior is fail-closed;
7. Python cannot forge arbitrary occurrence/history payload outside the exposed request shape;
8. existing TAG/Server Memory Script behavior remains unchanged.

### Engineering/browser tests

Add browser coverage proving:

1. Operational Events appears in normal Engineering navigation;
2. a new definition is authored without JSON/manual backend calls;
3. Preview occurs before Apply;
4. Apply persists it to Working Engineering;
5. Save -> Publish -> Activate carries the definition into Active Runtime;
6. a normal Server Script emits that definition;
7. C18 Event Browser on authored Screen or Popup shows the resulting occurrence from protected `operational.events`;
8. pt-BR / en / es affected Engineering chrome;
9. negative authorization/fail-closed behavior is preserved where applicable.

The strongest preferred E2E is one integrated generic flow rather than separate fixtures that bypass each other:

`author Event + author/activate Server Script -> emit -> query/browser observes occurrence`.

## 7. Candidate acceptance gates

Do not call C19 accepted merely because focused tests pass.

Exact C19 candidate HEAD must pass, at minimum:

- EliteSCADA CI;
- Wave11 Active HMI Runtime;
- Preview Licensing CI;
- L3 Seven-Driver Lab;
- Interop Lab Smoke;
- affected C19 focused tests.

Diagnose any red before rerun. Do not weaken tests, security, lifecycle, identity or C14 semantics.

After isolated acceptance:

1. compose C19 into `wave14/corrections-integration` preserving history;
2. run five combined gates again on exact resulting product SHA;
3. rerun dedicated C03 DNP3 carry-forward on the same exact product SHA;
4. repeat real Preview/browser validation as required by C10 Cycle 2 policy;
5. only then declare new product freeze.

## 8. C11 state after C19

C11 remains **IMPLEMENTATION LOCKED** while C19 is incomplete.

Already revalidated as supported by C12–C18:

- Server Runtime automation;
- generic periodic simulation authoring;
- simulated non-Good quality and propagation;
- Internal Memory normal authoring/lifecycle;
- Multi-Pen Trend Screen/Popup;
- Operational Command HMI;
- Startup/Home;
- persisted Popup X/Y;
- Alarm Browser;
- Event Browser consumption;
- affected Historical/Browser i18n.

After C19 converges, still close browser evidence for the visual/runtime findings before C11 release, especially:

- Analog Fill visibly live;
- Dynamo operational/bad-quality state behavior;
- two independent Dynamo instances;
- four canonical Runtime resolutions/scaling;
- fullscreen/no-scroll/overlay without reflow;
- one integrated living chain:
  `automation -> TAG/quality -> alarm/event/history -> HMI objects -> command`.

Only after every blocker is cleared may the Coordinator explicitly record:

`RELEASE C11 IMPLEMENTATION`.

## 9. Hard coordination boundaries

- `#212` stays OPEN / DRAFT / DO NOT MERGE TO `main` without later Product Owner authorization.
- C19 PR targets only `wave14/corrections-integration`.
- C11 stays locked.
- Wave13 #205 / #207 stays paused until final accepted Wave14 bytes.
- Preview #208 / #210 is infrastructure/history, never product authority.
- Documentation commits do not redefine product-code authority.

## 10. Resume checklist for a new Coordinator chat

If coordination moves to another chat, do this before any action:

1. read `PROJECT GOAL.md`;
2. read `LAST CHANGE.md`;
3. read `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. read this C19 handoff completely;
5. inspect live issue #211;
6. inspect live draft PR #212;
7. inspect live C19 PR/branch and compare its HEAD with base `2e284606...`;
8. inspect any C19 workflow runs on the exact live HEAD;
9. never assume a copied SHA is current if GitHub changed;
10. continue from the first unchecked C19 implementation/test item, not from chat recollection.

GitHub is the official memory.
