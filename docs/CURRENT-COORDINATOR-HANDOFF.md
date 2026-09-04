# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C12–C18 CONVERGED / C10 CYCLE 2 GREEN / C19 ACTIVE / C11 IMPLEMENTATION LOCKED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs before acting. `PROJECT GOAL.md` governs permanent intent. `LAST CHANGE.md` is the fastest current-state summary. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md` is the explicit resume document for a new Coordinator chat.

## 1. Permanent Coordinator gates

The Coordinator owns integration, architecture preservation, exact-head validation sequencing and acceptance.

Permanent rules:

- backend is canonical authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- no Preview-only bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identity remains authoritative;
- lifecycle remains `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- diagnose a red before rerun;
- never weaken tests/security/contracts/lifecycle merely to obtain green;
- package delivery is not Coordinator acceptance;
- PR #212 remains DRAFT and must not merge to `main` without later Product Owner authorization;
- C11 remains locked until explicit Coordinator release after every product blocker is cleared.

## 2. Current accepted product checkpoint before C19

Repository:

`brunolrogerio-collab/EliteSCADA`

Coordinator branch:

`wave14/corrections-integration`

Integration PR:

`#212` — **OPEN / DRAFT / DO NOT MERGE TO main WITHOUT LATER PRODUCT OWNER AUTHORIZATION**

Exact C12–C18 combined accepted product checkpoint:

`2e284606b605a26bb9632eae5264de30bea0acde`

C18 isolated accepted candidate:

`c6d7601d17737deeaf196fac9c4c00190089df6b`

Five combined gates on exact `2e284606...`, all SUCCESS:

- EliteSCADA CI #1360 / `33920467689`;
- Wave11 Active HMI Runtime #288 / `33920467682`;
- Preview Licensing CI #310 / `33920467789`;
- L3 Seven-Driver Lab #265 / `33920467676`;
- Interop Lab Smoke #187 / `33920467665`.

Additional C10 Convergence Cycle 2 evidence:

- Wave 14 C03 DNP3 Adapter #91 / `33921310281` — SUCCESS on exact product SHA;
- Test Preview #16 / `33921526826` — SUCCESS with disposable Preview infrastructure overlay based directly on exact product SHA;
- overlay PR #256 closed without merge.

**C18 = ACCEPTED / CONVERGED.**  
**C12–C18 = CONVERGED.**

Later docs-only integration commits do not redefine these product bytes.

## 3. C11 revalidation after C12–C18

C11 Pass 2 remains **IMPLEMENTATION LOCKED**.

The post-C18 revalidation confirmed support for the major correction packages:

- C12 Server Runtime automation / generic simulation authoring;
- C13 canonical non-Good simulation quality + propagation;
- C15 first-class Multi-Pen Trend in Screen/Popup;
- C16 Operational Command HMI + Startup/Home + persisted Popup X/Y;
- C17 Internal Memory ordinary authoring/lifecycle;
- C18 first-class Alarm Browser and Event Browser + affected i18n.

One functional blocker remains before the EEE Simulation can be constructed entirely with ordinary product mechanisms:

`C11-P2-EVT-01`

C14 already has canonical Operational Event model/runtime/history/query. C18 already consumes it. The remaining missing normal product surfaces are:

1. human Engineering authoring for Operational Event definitions;
2. generic Server Script emission through canonical C14 Active Runtime authority.

That gap is C19.

## 4. C19 active correction

Package:

**W14-C19 — Operational Event Authoring + Server Script Emission Bridge**

Exact product base:

`2e284606b605a26bb9632eae5264de30bea0acde`

Branch:

`wave14/c19-operational-event-authoring-script-bridge`

PR:

`#257` — **OPEN / DRAFT / target wave14/corrections-integration / DO NOT RETARGET main**

First C19 commit:

`68ac7ed1c019954f88671a983ee328dc9a7ceb47`

Binding implementation handoff on the C19 branch:

`docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`

C19 target chain:

`Engineering authoring -> Preview/Apply -> Save/Publish/Activate -> Server Script emit -> C14 Active Runtime -> event bus/history -> C18 Event Browser`

No EEE-specific code is accepted.

### Confirmed existing architecture to preserve

- `OperationalEventEngineeringDto` already exists;
- `EngineeringExchangeService` already exports `operationalEvents`;
- `EngineeringWorkspace.Scripts` is `InMemoryScriptEngineeringRegistry`, which also implements `IOperationalEventEngineeringRegistry`, so Working lifecycle/persistence already exists;
- `ScadaRuntimeFacade.EmitOperationalEventAsync(...)` already delegates to Active `IOperationalEventRuntime`;
- `OperationalEventContract.CreateOccurrence(...)` is canonical;
- Alarm / Operational Event / Audit remain distinct;
- Command execution must not be changed to implicitly generate Operational Events.

## 5. C19 implementation route

1. add `OperationalEventEngineering` to web types and `operationalEvents` package view;
2. add normal Engineering navigation/authoring surface in pt-BR/en/es;
3. reuse protected package Preview -> Apply/CAS, not an unprotected direct mutation shortcut;
4. add generic deterministic Server Script API `emit_operational_event(...)`;
5. Python child returns bounded emission requests, never publishes/writes history itself;
6. C# host validates/replays emission under existing Active Revision lifecycle protection through C14 authority;
7. add focused backend tests for success, unknown definition, stale revision, disabled/unresolved definitions and bounded payloads;
8. add integrated browser E2E proving ordinary authoring -> Save/Publish/Activate -> Script emit -> Event Browser observation;
9. exact C19 candidate: five required gates + focused tests;
10. diagnose red before rerun;
11. compose only accepted C19 into integration preserving history;
12. five combined gates again;
13. dedicated C03 DNP3 again on exact combined SHA;
14. real Preview/browser carry-forward again;
15. only then freeze new product bytes.

## 6. C11 work after C19

Even after C19 converges, close remaining browser evidence before C11 release:

- Analog Fill visibly live;
- Dynamo operational/bad-quality visual states;
- two independent Dynamo instances;
- four canonical Runtime resolution/scaling behaviors;
- fullscreen/no-scroll/overlay without reflow;
- one integrated living chain:
  `automation -> TAG/quality -> alarm/event/history -> HMI objects -> command`.

Only after every blocker is cleared may the Coordinator explicitly record:

`RELEASE C11 IMPLEMENTATION`

## 7. Preview / Wave13 boundaries

- Issue #208 / PR #210 are Preview infrastructure/history only, never product authority.
- Wave13 issue #205 / PR #207 remains paused until final Wave14 accepted bytes.
- do not package/sign stale pre-Wave14 product bytes.

## 8. Mandatory next-chat resume protocol

Read/revalidate in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. live `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md` from C19 branch;
6. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
7. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
8. C11 Pass-2 audit + HMI clarification;
9. `docs/CI-VALIDATION-POLICY.md`;
10. live issue #211;
11. live PR #212;
12. live PR #257;
13. inspect exact C19 HEAD and its workflow runs;
14. continue from the first unfinished C19 checklist item.

Do not ask the Product Owner to repeat decisions that are already unambiguously recorded in GitHub.
