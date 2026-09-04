# EliteSCADA — Next Coordinator Chat Handoff

**Prepared:** 2026-09-04 BRT  
**Purpose:** authoritative resume point if the current ChatGPT Coordinator conversation reaches context/session limits.

> GitHub is the official development memory. Revalidate live refs before acting. This document records the current intended state, but live GitHub always wins if a ref moved after this commit.

## 1. Current high-level state

Repository:

`brunolrogerio-collab/EliteSCADA`

Wave 14 coordination issue:

`#211`

Coordinator integration branch:

`wave14/corrections-integration`

Coordinator integration PR:

`#212` — **OPEN / DRAFT / DO NOT MERGE TO main WITHOUT LATER PRODUCT OWNER AUTHORIZATION**

C11:

**IMPLEMENTATION LOCKED**

Wave13 packaging/signing:

**PAUSED** — issue #205 / PR #207 must not package stale pre-Wave14 product bytes.

## 2. Exact accepted product checkpoint before C19

C18 isolated accepted candidate:

`c6d7601d17737deeaf196fac9c4c00190089df6b`

C18 was composed into integration preserving history.

Exact C12–C18 combined product checkpoint:

`2e284606b605a26bb9632eae5264de30bea0acde`

Five combined gates on exact `2e284606...`, all SUCCESS:

- EliteSCADA CI #1360 / `33920467689`;
- Wave11 Active HMI Runtime #288 / `33920467682`;
- Preview Licensing CI #310 / `33920467789`;
- L3 Seven-Driver Lab #265 / `33920467676`;
- Interop Lab Smoke #187 / `33920467665`.

Additional C10 Convergence Cycle 2 carry-forward evidence:

- Wave 14 C03 DNP3 Adapter #91 / `33921310281` — SUCCESS on exact product SHA `2e284606...`;
- Test Preview #16 / `33921526826` — SUCCESS using a disposable Preview infrastructure overlay whose direct product parent was `2e284606...`;
- disposable Preview PR #256 was closed without merge.

No product change was required to obtain those greens.

**C18 = ACCEPTED / CONVERGED.**  
**C12–C18 = CONVERGED.**

Do not reinterpret later documentation-only commits as replacing `2e284606...` product bytes.

## 3. Why a C19 correction was opened

After C10 Cycle 2 evidence closed, C11 Pass-2 findings were revalidated against `2e284606...`.

The major product gaps delivered by C12–C18 are now supportable:

- Server Runtime automation;
- generic periodic simulation;
- non-Good simulated quality + propagation;
- Internal Memory authoring/lifecycle;
- Multi-Pen Trend as Screen/Popup object;
- Operational Command HMI;
- Startup/Home;
- persisted Popup X/Y;
- Alarm Browser as Screen/Popup object;
- Event Browser as Screen/Popup object;
- affected Historical/Browser i18n.

One functional blocker remained:

`C11-P2-EVT-01`

C14 already supplies canonical Operational Event definitions/runtime/history/query, and C18 supplies Event Browser consumption, but ordinary integrator workflow still lacks:

1. normal Engineering UI authoring of Operational Event definitions;
2. generic Server Script emission through the canonical C14 Active Runtime authority.

Without those, the EEE Simulation cannot generate its operational events using only ordinary product mechanisms. Therefore C11 cannot be released yet.

## 4. C19 current state

Package:

**W14-C19 — Operational Event Authoring + Server Script Emission Bridge**

Exact base:

`2e284606b605a26bb9632eae5264de30bea0acde`

Branch:

`wave14/c19-operational-event-authoring-script-bridge`

PR:

`#257` — **OPEN / DRAFT / target wave14/corrections-integration / DO NOT RETARGET main**

First C19 commit:

`68ac7ed1c019954f88671a983ee328dc9a7ceb47`

That first commit is documentation only:

`docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`

Read that document completely before continuing C19. It contains the technical contract, relevant existing architecture, candidate acceptance gates and next-chat resume checklist.

## 5. C19 confirmed architecture

Do not rediscover or redesign these without evidence of a defect.

### Existing canonical Engineering model

`OperationalEventEngineeringDto` already exists.

`EngineeringExchangeService` already owns/exports an `IOperationalEventEngineeringRegistry` and includes `operationalEvents` in the canonical Engineering package.

`EngineeringWorkspace.Scripts` is `InMemoryScriptEngineeringRegistry`, which also implements `IOperationalEventEngineeringRegistry`; Operational Event definitions therefore already share ordinary Working workspace dirty/Clear lifecycle.

This means normal package Preview/Apply/CAS can be reused for Operational Event UI authoring. Do not create a second registry or direct unprotected mutation endpoint unless a real defect proves it necessary.

### Existing canonical runtime model

`ScadaRuntimeFacade.EmitOperationalEventAsync(...)` delegates to the Active `IOperationalEventRuntime` and fails closed when Engineering runtime/event support is absent.

`OperationalEventContract.CreateOccurrence(...)` is canonical.

C19 must not:

- write history directly;
- publish a parallel event occurrence type;
- make Command execution automatically create Operational Events;
- conflate Alarm, Operational Event and Audit.

## 6. C19 intended implementation

### Engineering UI

Add a normal Operational Events section to Engineering.

Use existing protected package Preview -> Apply/CAS workflow.

Author fields from canonical DTO:

- id;
- key;
- name;
- type;
- category;
- source;
- area;
- equipmentPath;
- tagId/tagPath where applicable;
- message;
- enabled;
- metadata as supported.

Add `OperationalEventEngineering` to web types and `operationalEvents?: OperationalEventEngineering[]` to `EngineeringPackageView`.

Affected chrome must support pt-BR / en / es.

### Server Script bridge

Working API name:

`emit_operational_event(definition_id, message=None, context=None)`

The isolated Python runner should return requested events separately from existing TAG writes.

C# executor/host then replays the request through the canonical C14 Active Runtime authority under the existing Server Script revision gate.

Binding behavior:

- Server scope only;
- stable definition ID;
- unknown/unresolved definition fails closed;
- stale script generation cannot emit into newer revision;
- disabled definition fails closed according to existing runtime contract;
- Python child never writes history or publishes directly to event bus;
- canonical C14 field/context limits remain authoritative.

Before changing `ServerScriptRuntimeManager.GetShared(...)`, locate all call sites and activation paths so shared-host identity is not accidentally split.

## 7. C19 tests required

Focused backend:

- successful configured Active event emission from Server Script;
- canonical occurrence identity/context;
- unknown definition rejected;
- stale revision rejected;
- disabled definition fail-closed;
- forged/unbounded payload rejected;
- existing TAG/Memory Script behavior unchanged.

Browser/E2E:

- normal Engineering navigation exposes Operational Events;
- create/edit via UI, not JSON;
- Preview before Apply;
- Save -> Publish -> Activate persists definition;
- normal Server Script emits occurrence;
- C18 Event Browser observes occurrence through protected `operational.events`;
- pt-BR/en/es affected chrome.

## 8. C19 validation route

On exact C19 candidate HEAD:

1. focused tests;
2. EliteSCADA CI;
3. Wave11 Active HMI Runtime;
4. Preview Licensing CI;
5. L3 Seven-Driver Lab;
6. Interop Lab Smoke.

Diagnose a red before any rerun.

If isolated accepted:

1. compose C19 into integration preserving history;
2. five combined gates on exact combined SHA;
3. dedicated C03 DNP3 on that same exact SHA;
4. real Preview/browser evidence again as required;
5. new product freeze only after those gates.

## 9. C11 work remaining after C19

Even after C19 converges, do not release C11 until browser evidence closes remaining visual/runtime validation items:

- Analog Fill visibly tracks live data;
- Dynamo operational/bad-quality visual state semantics;
- two independent Dynamo instances;
- canonical four-resolution Runtime/scaling behavior;
- fullscreen/no-scroll/overlay without layout reflow;
- integrated living chain:
  `automation -> TAG/quality -> alarm/event/history -> HMI objects -> command`.

Only after all blockers are clear may the Coordinator explicitly record:

`RELEASE C11 IMPLEMENTATION`

## 10. Never violate these boundaries

- #212 remains DRAFT and never merges to `main` without later Product Owner authorization.
- C11 remains locked until explicit release.
- no EEE-specific simulator service, Driver, package, hidden route, private host logic or DEMO-only bypass.
- backend remains canonical authority.
- authorization remains backend enforced.
- licensing remains host-owned and fail-closed.
- no direct Driver-to-Driver coupling.
- do not weaken tests/security/identity/lifecycle to obtain green.
- diagnose before rerun.
- documentation-only commits never redefine product-code authority.

## 11. Mandatory resume order for the next Coordinator chat

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. this document;
5. `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md` from live C19 branch;
6. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
7. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
8. C11 Pass-2 consolidated audit + HMI clarification;
9. `docs/CI-VALIDATION-POLICY.md`;
10. live issue #211;
11. live PR #212;
12. live C19 PR #257;
13. inspect current C19 HEAD and exact-head workflow runs;
14. continue from the first unfinished C19 checklist item.

Do not ask the Product Owner to repeat decisions already recorded here unless GitHub contains a genuine contradiction that cannot be resolved by live state.
