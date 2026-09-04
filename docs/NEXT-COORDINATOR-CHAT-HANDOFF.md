# EliteSCADA — Next Coordinator Chat Handoff

**Prepared:** 2026-09-04 BRT  
**Purpose:** authoritative resume point after the previous Coordinator chat reached its duration/context limit.

> GitHub is the official development memory. Revalidate live refs before acting. Do not continue from chat recollection when repository state disagrees.

## 1. High-level state

Repository: `brunolrogerio-collab/EliteSCADA`  
Wave14 issue: #211  
Integration branch: `wave14/corrections-integration`  
Integration PR: #212 OPEN/DRAFT  
C11: **IMPLEMENTATION LOCKED**  
Wave13 #205/#207: **PAUSED**

Critical permanent rule: #212 must NEVER merge to `main` without later explicit Product Owner authorization.

## 2. Accepted product before C19

Exact accepted C12–C18 combined product SHA:

`2e284606b605a26bb9632eae5264de30bea0acde`

Green evidence on exact SHA:

- EliteSCADA CI #1360 / `33920467689`;
- Wave11 #288 / `33920467682`;
- Preview Licensing #310 / `33920467789`;
- L3 #265 / `33920467676`;
- Interop #187 / `33920467665`;
- C03 DNP3 #91 / `33921310281`;
- Test Preview #16 / `33921526826` on a disposable infrastructure overlay based directly on that SHA; PR #256 closed without merge.

C18 isolated accepted candidate: `c6d7601d17737deeaf196fac9c4c00190089df6b`.

## 3. C19 purpose and branch

C19 closes `C11-P2-EVT-01`:

- normal Engineering authoring of C14 Operational Event definitions;
- generic Server Script emission through C14 Active Runtime authority.

Branch: `wave14/c19-operational-event-authoring-script-bridge`  
PR: #257 OPEN/DRAFT -> `wave14/corrections-integration`  
Exact C19 base: `2e284606...`

Latest C19 PRODUCT SHA before the final docs-only handoff:

`537ce074466aaf34d98142309ed4fd71036cf050`

C19 is **NOT ACCEPTED**.

Read on the live C19 branch:

1. `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`;
2. `docs/WAVE14-C19-PROGRESS.md`.

The latter contains the detailed implementation/debug chronology.

## 4. C19 implementation already completed

Engineering:

- ordinary Operational Events editor/navigation;
- Preview -> CAS -> Apply workflow;
- create/edit canonical fields;
- configured TAG selection;
- pt-BR/en/es;
- visible backend errors;
- atomic pristine NEW-event transition plus regression, preventing a C17-class stale stable-ID race.

Server Script:

- `emit_operational_event(definition_id, message=None, context=None)`;
- bounded request shape;
- Python child cannot publish event bus or write history;
- C# validates stable definition and routes through canonical C14 Active Runtime;
- unresolved/disabled/stale definitions fail closed;
- no parallel event model or new Script dependency kind.

## 5. Do not restore the obsolete bridge gate

An earlier C19 design used a separate gate plus `AsyncLocal` reentrant activation lease. Wave11 #291 proved it wrong.

Root cause: `AsyncLocal` established inside `BindForActivationAsync(...)` did not become the caller's context after the async call returned, so the new `Initialize` handler waited on its own gate and timed out.

Current architecture deliberately reuses `ServerScriptRuntimeManager._revisionGate`, the already canonical Active revision gate used by Script TAG/Server Memory access and runtime swap.

Correction commits:

- `075904ff82b425ed20aed2d8ced83ab33b4a608e`;
- `48202f29a9f6ac2e52109c6a5421f7472c04a8c3`;
- `33313f5d56c343d98c80c9fd790fb82e18db3384`;
- `d989003efd9be364b8c333e749fbe3c033c47fb2`;
- diagnostic E2E `537ce074466aaf34d98142309ed4fd71036cf050`.

Do not reintroduce an independent bridge gate or a pre-check TOCTOU shortcut.

## 6. Exact current validation

On exact C19 product SHA `537ce074466aaf34d98142309ed4fd71036cf050`:

- EliteSCADA CI #1365 / `33928660073` — SUCCESS;
- Preview Licensing #315 / `33928660187` — SUCCESS;
- L3 #271 / `33928659951` — SUCCESS;
- Interop #192 / `33928660008` — SUCCESS;
- C03 DNP3 #108 / `33928648375` — SUCCESS;
- Wave11 #293 / `33928659977` — FAILURE.

Status = **5/6 GREEN. DO NOT ACCEPT C19.**

## 7. Exact current blocker

Wave11 #293 now gives a precise diagnosis. The C19 NEW-event regression passes. The integrated Server Script is present in Active revision and executes once, but faults with:

`ScriptExecutionDiagnosticException: python3: can't open file '/home/runner/work/EliteSCADA/EliteSCADA/src/Scada.Api/bin/Debug/net10.0/ServerScriptRunner.py': [Errno 2] No such file or directory`

Diagnostics prove:

- executionCount 1;
- faultedCount 1;
- timeoutCount 0;
- cancelledCount 0;
- completedCount 0.

Therefore this is NOT the previous bridge deadlock. It is a real normal-build output defect: `ServerScriptRunner.py` is missing from the Debug output used by Wave11/dev runtime.

## 8. FIRST TASK

Inspect `src/Scada.Api/Scada.Api.csproj` and current runner content/copy rules.

Fix normal product packaging so `ServerScriptRunner.py` is copied to application output in Debug/build and to publish/runtime output consistently.

Do not:

- hardcode repository paths;
- bypass Python runner in the test;
- increase timeouts;
- create an EEE-only workaround;
- weaken script isolation or revision authority.

A product fix produces a new SHA. All acceptance evidence must then target that exact new SHA.

## 9. C19 validation after the fix

On one exact candidate SHA:

1. Wave11 including C19 atomic-new-event test and integrated author->activate->emit->Event Browser test;
2. EliteSCADA CI;
3. Preview Licensing CI;
4. L3 Seven-Driver Lab;
5. Interop Lab Smoke;
6. C03 DNP3 carry-forward;
7. Coordinator exact-diff architecture review.

Diagnose any red before rerun. Never weaken tests/security/lifecycle/identity to get green.

Only after coherent exact-head green evidence may C19 be isolated-accepted.

## 10. After isolated C19 acceptance

1. compose C19 into `wave14/corrections-integration` preserving history;
2. run five combined gates on exact C12–C19 integration SHA;
3. run C03 DNP3 on that same SHA;
4. repeat real Preview/browser evidence;
5. freeze new exact product authority;
6. finish C11 browser evidence:
   - Analog Fill visibly live;
   - Dynamo operational/bad-quality state;
   - two independent Dynamo instances;
   - four canonical Runtime resolutions/scaling;
   - fullscreen/no-scroll/overlay without reflow;
   - integrated living chain `automation -> TAG/quality -> alarm/event/history -> HMI objects -> command`;
7. only when every blocker is clear, explicitly record `RELEASE C11 IMPLEMENTATION`;
8. build the living deterministic EEE Simulation entirely through normal generic EliteSCADA mechanisms;
9. final Wave14 acceptance;
10. resume Wave13 packaging/signing only on final accepted bytes.

## 11. Branch/PR hygiene

- #212: OPEN/DRAFT, never merge main without Product Owner explicit authorization;
- #257: OPEN/DRAFT, target integration only;
- #259: validation-only DRAFT PR/CI trigger, never merge;
- docs commits do not redefine product authority;
- issue #208 / PR #210 remain Preview infrastructure/history only.

## 12. Non-negotiable architecture

- backend canonical authority;
- backend authorization;
- host-owned fail-closed licensing;
- canonical TAG/Data Source identity;
- no Driver-to-Driver coupling;
- no EEE-specific simulator service, Driver, hidden package, private host logic or DEMO-only route;
- no direct history insert;
- Alarm / Operational Event / Audit remain separate;
- C11 locked until explicit release.

## 13. Mandatory resume order

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. this file;
5. live C19 implementation handoff;
6. live C19 progress handoff;
7. C11 pre-DEMO requirements + Pass-2 audit + HMI clarification;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211;
10. live PRs #212, #257, #259;
11. revalidate current exact SHAs/workflow state;
12. start with the `ServerScriptRunner.py` output/publish defect.

Do not ask the Product Owner to reconstruct decisions already committed to GitHub. Human memory has enough hobbies already.
