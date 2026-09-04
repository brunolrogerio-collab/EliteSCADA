# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C12–C18 CONVERGED / C19 5-of-6 GREEN BUT BLOCKED / C11 IMPLEMENTATION LOCKED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs and exact-SHA workflows before acting. Documentation-only commits do not redefine product authority.

## 1. Permanent gates

- backend is canonical authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- lifecycle remains `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- diagnose a red before rerun;
- never weaken tests/security/contracts/identity/lifecycle for green;
- package delivery is not Coordinator acceptance;
- PR #212 stays OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization;
- C11 stays IMPLEMENTATION LOCKED until explicit release;
- Wave13 #205/#207 stays paused until final accepted Wave14 bytes.

## 2. Accepted product authority before C19

Integration branch: `wave14/corrections-integration`  
Integration PR: #212 OPEN/DRAFT  
Exact accepted C12–C18 product checkpoint:

`2e284606b605a26bb9632eae5264de30bea0acde`

Exact evidence:

- EliteSCADA CI #1360 / `33920467689` — SUCCESS;
- Wave11 #288 / `33920467682` — SUCCESS;
- Preview Licensing #310 / `33920467789` — SUCCESS;
- L3 #265 / `33920467676` — SUCCESS;
- Interop #187 / `33920467665` — SUCCESS;
- C03 DNP3 #91 / `33921310281` — SUCCESS;
- Test Preview #16 / `33921526826` — SUCCESS on disposable overlay directly based on `2e284606...`; PR #256 closed without merge.

C18 isolated accepted SHA: `c6d7601d17737deeaf196fac9c4c00190089df6b`.

## 3. Why C19 exists

C11 Pass-2 revalidation left one functional blocker, `C11-P2-EVT-01`.

C14 already provides canonical Operational Event definition/runtime/history/query. C18 already provides Event Browser consumption. Missing ordinary product mechanisms were:

1. human Engineering authoring of Operational Event definitions;
2. generic Server Script emission through canonical C14 Active Runtime.

C19 closes only that gap.

Required generic chain:

`Engineering -> Preview/Apply -> Save/Publish/Activate -> Server Script emit -> C14 Active Runtime -> event bus/history -> C18 Event Browser`

No EEE-specific code is allowed.

## 4. Live C19 package

Branch: `wave14/c19-operational-event-authoring-script-bridge`  
PR: #257 OPEN/DRAFT -> `wave14/corrections-integration`  
Exact base: `2e284606b605a26bb9632eae5264de30bea0acde`  
Latest validated PRODUCT SHA before its docs-only handoff:

`537ce074466aaf34d98142309ed4fd71036cf050`

C19 is **NOT ACCEPTED**.

The live branch has a detailed operational record at:

`docs/WAVE14-C19-PROGRESS.md`

The C19 docs-only handoff commit after product SHA 537ce is `fb29ea0712695216aef2f538da2fb1c0f17ef2ef`; it does not redefine product bytes.

## 5. C19 implementation already present

### Engineering

- normal Operational Events navigation/editor;
- create/edit via protected Working package Preview -> CAS -> Apply;
- normal authoring of stable ID, key/name/type/category/source, area/equipment/TAG, message/enabled;
- pt-BR/en/es chrome;
- backend Apply errors preserved;
- C17-class NEW-selection stale-draft race discovered and corrected atomically;
- deterministic regression proves a pristine NEW Operational Event draft is installed before new-mode identity.

### Server Script / C14

- deterministic `emit_operational_event(definition_id, message=None, context=None)`;
- bounded message/context;
- Python child has no event bus or history authority;
- C# host validates stable definition ID and routes through canonical C14 Active Runtime;
- unknown/disabled/stale definition behavior fails closed;
- no new Script dependency kind;
- Alarm / Operational Event / Audit remain separate.

## 6. Important bridge architecture correction

Do not restore the former C19 separate gate/`AsyncLocal` activation-lease design.

Wave11 #291 demonstrated that `AsyncLocal` set inside `BindForActivationAsync` did not become the caller's context after method return. New-generation `Initialize` therefore waited on its own bridge gate until Script timeout.

Current intended design reuses `ServerScriptRuntimeManager._revisionGate`, the existing canonical gate for Active revision swap and Script TAG/Server Memory access.

Correction sequence:

- `075904ff82b425ed20aed2d8ced83ab33b4a608e` — revision-gated host helper;
- `48202f29a9f6ac2e52109c6a5421f7472c04a8c3` — bridge simplified, no `AsyncLocal`/parallel gate;
- `33313f5d56c343d98c80c9fd790fb82e18db3384` — published activation binding;
- `d989003efd9be364b8c333e749fbe3c033c47fb2` — persisted recovery binding;
- `537ce074466aaf34d98142309ed4fd71036cf050` — diagnostic E2E assertions.

This ensures an old generation cannot cross a revision swap, while new-generation Initialize runs after the canonical swap gate is released.

## 7. Exact current validation on 537ce

On exact product SHA `537ce074466aaf34d98142309ed4fd71036cf050`:

- EliteSCADA CI #1365 / `33928660073` — SUCCESS;
- Preview Licensing #315 / `33928660187` — SUCCESS;
- L3 #271 / `33928659951` — SUCCESS;
- Interop #192 / `33928660008` — SUCCESS;
- C03 DNP3 #108 / `33928648375` — SUCCESS;
- Wave11 #293 / `33928659977` — FAILURE.

So current status is **5/6 green, C19 NOT ACCEPTED**.

Wave11 #293 proves the NEW Operational Event transition regression is green and reaches the C19 integrated flow. The Server Script executes once, but canonical diagnostics report a real runtime fault, not timeout/deadlock:

`python3: can't open file '/home/runner/work/EliteSCADA/EliteSCADA/src/Scada.Api/bin/Debug/net10.0/ServerScriptRunner.py': [Errno 2] No such file or directory`

This is the current blocker.

## 8. First mandatory next action

Inspect `src/Scada.Api/Scada.Api.csproj` and the existing `ServerScriptRunner.py` content/copy rules. Fix normal product output packaging so the runner is present in Debug output and publish/runtime output consistently.

Do not:

- hardcode repository paths;
- bypass the Python runner in tests;
- increase timeout to hide the failure;
- create EEE-specific handling;
- weaken isolation/revision authority.

Any product fix creates a new candidate SHA. Revalidate the new exact SHA.

## 9. C19 acceptance sequence after packaging fix

1. inspect exact diff/output behavior;
2. Wave11 including both C19 tests;
3. EliteSCADA CI;
4. Preview Licensing;
5. L3 Seven-Driver Lab;
6. Interop Lab Smoke;
7. C03 DNP3 on the same exact candidate SHA;
8. diagnose every red before rerun;
9. architecture review;
10. only then isolated C19 acceptance.

## 10. After C19 acceptance

1. compose C19 into integration preserving history;
2. five combined gates on exact C12–C19 integration SHA;
3. C03 DNP3 on the same SHA;
4. real Preview/browser carry-forward;
5. declare new exact product freeze only after coherent green evidence;
6. finish C11 browser evidence: Analog Fill live behavior, Dynamo operational/bad-quality states, two independent Dynamo instances, four canonical Runtime resolutions/scaling, fullscreen/no-scroll/overlay without reflow, integrated living chain;
7. only when every blocker is clear, explicitly record `RELEASE C11 IMPLEMENTATION`;
8. construct living deterministic EEE Simulation exclusively through ordinary generic EliteSCADA mechanisms;
9. final Wave14 acceptance;
10. resume Wave13 packaging/signing only on final accepted Wave14 bytes.

## 11. Hard boundaries

- #212 never merges to `main` without later explicit Product Owner authorization;
- #257 stays DRAFT until accepted and never targets main;
- #259 is a validation-only DRAFT PR/CI trigger and must never merge;
- C11 remains locked;
- Wave13 remains paused;
- no EEE-specific simulator service/Driver/hidden DEMO package/private host bypass;
- no direct history insert;
- no Alarm/Event/Audit conflation;
- backend Active revision remains authority;
- no weakening security/tests/licensing/identity/lifecycle.

## 12. Mandatory resume order

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. live C19 `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`;
6. live C19 `docs/WAVE14-C19-PROGRESS.md`;
7. C11 pre-DEMO requirements/audit/HMI clarification;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211;
10. live PRs #212, #257, #259;
11. revalidate exact refs/workflows;
12. start with the missing `ServerScriptRunner.py` output/publish defect.

Do not ask the Product Owner to repeat decisions already recorded unambiguously in GitHub.
