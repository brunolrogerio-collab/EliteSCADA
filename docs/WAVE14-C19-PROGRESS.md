# Wave 14 C19 — Live Progress / Coordinator Handoff

**Updated:** 2026-09-04 BRT  
**Branch:** `wave14/c19-operational-event-authoring-script-bridge`  
**PR:** #257 — OPEN / DRAFT -> `wave14/corrections-integration`  
**Exact accepted pre-C19 product base:** `2e284606b605a26bb9632eae5264de30bea0acde`  
**Latest C19 PRODUCT SHA before this docs-only handoff:** `537ce074466aaf34d98142309ed4fd71036cf050`  
**C19 status:** NOT ACCEPTED  
**C11:** IMPLEMENTATION LOCKED

> GitHub is the official memory. Revalidate live refs before acting. This documentation commit does not redefine product-code authority; the product SHA discussed here is `537ce074...`.

## 1. C19 purpose

C19 closes `C11-P2-EVT-01`, the last functional product gap found after C12–C18 convergence:

1. normal Engineering authoring of canonical C14 Operational Event definitions;
2. generic Server Script emission through canonical C14 Active Runtime authority.

Required generic chain:

`Engineering authoring -> Preview/Apply -> Save/Publish/Activate -> Server Script -> emit_operational_event -> C14 Active Runtime -> event bus/history -> C18 Event Browser`

No EEE-specific code, direct history insertion, parallel event model, Command->Event shortcut, client-side event authority or DEMO-only bypass is allowed.

## 2. Accepted base before C19

Exact C12–C18 accepted product checkpoint:

`2e284606b605a26bb9632eae5264de30bea0acde`

Evidence on that exact SHA:

- EliteSCADA CI #1360 / `33920467689` — SUCCESS;
- Wave11 Active HMI Runtime #288 / `33920467682` — SUCCESS;
- Preview Licensing CI #310 / `33920467789` — SUCCESS;
- L3 Seven-Driver Lab #265 / `33920467676` — SUCCESS;
- Interop Lab Smoke #187 / `33920467665` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #91 / `33921310281` — SUCCESS;
- Test Preview #16 / `33921526826` — SUCCESS on disposable infrastructure overlay based directly on the exact product SHA; PR #256 closed without merge.

## 3. C19 implementation completed so far

### Engineering authoring

Implemented on the C19 branch:

- first-class Operational Events Engineering navigation/editor;
- create/edit via canonical Working package, not manual JSON;
- protected Preview -> CAS recheck -> Apply flow;
- fields for stable ID, key, name, type, category, source, area, equipment, TAG association, message and enabled state;
- TAG selection through configured Engineering TAGs;
- pt-BR / en / es affected chrome;
- backend/CAS Apply errors remain visible.

Important identity correction: static review found the editor had recreated the same class of NEW-selection race previously fixed in C17. `Nova Evento` could enter `isNew=true` while still carrying the previous persisted draft/ID. The transition was corrected so a pristine draft is installed synchronously before new-mode identity, with deterministic Wave11 regression coverage. Commit `4556e30409e75c13d1446829ded9c6932a31c02f` contains the regression checkpoint after that correction.

### Server Script emission

Implemented:

- deterministic Python API `emit_operational_event(definition_id, message=None, context=None)`;
- bounded message/context validation aligned with C14 limits;
- Python process only returns requested occurrences and has no event-bus/history authority;
- C# executor validates stable definition GUID and replays through canonical runtime authority;
- TAG/Server Memory requests remain replayed before Operational Event requests;
- unresolved/disabled/stale definitions fail closed through Active C14 definition/revision authority;
- no new Script dependency kind was introduced because stable ID + exact Active revision resolution already provide authority and fail-closed semantics.

## 4. Important runtime-gate correction

The first C19 implementation introduced a separate bridge gate and then attempted re-entrancy with `AsyncLocal` during activation. Wave11 #291 proved that design incorrect: Active revision advanced, but an `Initialize` emission never reached history.

Root cause: the `AsyncLocal` value established inside `BindForActivationAsync(...)` did not become caller context after that async method returned. The `Initialize` handler therefore did not own the activation lease and waited on the same bridge gate until Script timeout.

That design is OBSOLETE and must not be restored.

Current architecture reuses the existing canonical `ServerScriptRuntimeManager._revisionGate`, already responsible for exact Active revision serialization of Server Script TAG/Server Memory access and runtime swap.

Relevant correction commits:

- `075904ff82b425ed20aed2d8ced83ab33b4a608e` — host exposes revision-gated execution helper;
- `48202f29a9f6ac2e52109c6a5421f7472c04a8c3` — bridge simplified, no `AsyncLocal`, no parallel gate;
- `33313f5d56c343d98c80c9fd790fb82e18db3384` — published activation binds canonical event authority then activates;
- `d989003efd9be364b8c333e749fbe3c033c47fb2` — persisted Active recovery follows the same model;
- `537ce074466aaf34d98142309ed4fd71036cf050` — Wave11 surfaces canonical Server Script diagnostics so faults cannot masquerade as generic history timeouts.

Why the current locking design is intended:

1. runtime swap is serialized under `_revisionGate`;
2. an old generation attempting event emission during swap waits;
3. after swap it fails exact project/revision identity instead of emitting into the new revision;
4. a new generation `Initialize` starts after the swap gate is released and may emit normally;
5. no second gate, reentrant token or pre-check TOCTOU shortcut is needed.

## 5. Validation history

Intermediate SHA `eef644b0a8c31e3392106f447d0cc1532c1eae76` passed six gates, proving initial authoring/bridge code compiled, but it predates later corrections and is NOT a candidate:

- EliteSCADA CI #1362 / `33926163307` — SUCCESS;
- Wave11 #290 / `33926163231` — SUCCESS;
- Preview #312 / `33926163221` — SUCCESS;
- L3 #268 / `33926163254` — SUCCESS;
- Interop #189 / `33926163315` — SUCCESS;
- C03 #97 / `33925884987` — SUCCESS.

On intermediate `3b943e36...`, Elite CI #1363 had one IEC104 failure in `Adapter_OutOfOrderIFrameFaultsBeforePublishingAsdu`. The IEC104 test bytes were unchanged from the accepted base; no C19 IEC104 code was touched. No blind rerun or product weakening was performed. Current exact-head Elite CI later passed, so no IEC104 product correction was introduced.

Wave11 #291 on that intermediate product exposed the obsolete `AsyncLocal` bridge-gate defect described above and led to the canonical `_revisionGate` redesign.

## 6. Exact current product validation: `537ce074...`

Six relevant runs exist on exact product SHA `537ce074466aaf34d98142309ed4fd71036cf050`:

- EliteSCADA CI #1365 / `33928660073` — SUCCESS;
- Preview Licensing CI #315 / `33928660187` — SUCCESS;
- L3 Seven-Driver Lab #271 / `33928659951` — SUCCESS;
- Interop Lab Smoke #192 / `33928660008` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #108 / `33928648375` — SUCCESS;
- Wave11 Active HMI Runtime #293 / `33928659977` — FAILURE.

Therefore C19 is **5/6 green and NOT ACCEPTED**.

### Wave11 #293 exact failure

The deterministic NEW-event regression passes. The integrated C19 test reaches Active revision and executes the Server Script once, but canonical diagnostics report:

- `executionCount = 1`;
- `faultedCount = 1`;
- `timeoutCount = 0`;
- `cancelledCount = 0`;
- `completedCount = 0`.

Exact sanitized error:

`ScriptExecutionDiagnosticException: python3: can't open file '/home/runner/work/EliteSCADA/EliteSCADA/src/Scada.Api/bin/Debug/net10.0/ServerScriptRunner.py': [Errno 2] No such file or directory`

This is NOT the previous bridge deadlock. The current blocker is a build/output packaging defect: `ServerScriptRunner.py` is absent from the Debug output used by the Wave11 dev/webServer path.

## 7. FIRST TASK FOR THE NEXT COORDINATOR

Inspect `src/Scada.Api/Scada.Api.csproj` and the existing Server Script runner content/copy rules. Correct product packaging so `ServerScriptRunner.py` is available in normal Debug output and publish/runtime output consistently.

Constraints:

- do not hardcode a repository path;
- do not alter the Wave11 test to bypass the runner;
- do not increase timeout to hide the fault;
- do not embed EEE-specific behavior;
- preserve isolated Python execution and canonical Active revision authority;
- make the runner a normal application output/publish asset.

After any product correction, the candidate SHA changes. Validate the new exact SHA, not `537ce074...`.

## 8. Required C19 acceptance sequence after packaging fix

1. inspect exact diff and build/output behavior;
2. run Wave11 including both C19 tests;
3. run EliteSCADA CI;
4. run Preview Licensing CI;
5. run L3 Seven-Driver Lab;
6. run Interop Lab Smoke;
7. run Wave 14 C03 DNP3 carry-forward on the exact same candidate SHA;
8. diagnose every red before rerun;
9. perform Coordinator architecture review;
10. only then declare isolated C19 accepted.

Do not weaken tests, security, identity, lifecycle, authorization or C14 semantics to obtain green.

## 9. Route after isolated C19 acceptance

1. compose accepted C19 into `wave14/corrections-integration` preserving history;
2. run five combined gates again on exact C12–C19 combined product SHA;
3. run dedicated C03 DNP3 on that exact combined SHA;
4. repeat real Preview/browser validation;
5. declare a new exact product freeze only after coherent green evidence;
6. finish remaining C11 browser evidence: Analog Fill live behavior, Dynamo operational/bad-quality state, two independent Dynamo instances, canonical Runtime resolutions/scaling, fullscreen/no-scroll/overlay without reflow, and one integrated living chain;
7. only if every C11 blocker is cleared, explicitly record `RELEASE C11 IMPLEMENTATION`;
8. construct the canonical living EEE Simulation entirely through ordinary generic EliteSCADA mechanisms;
9. final Wave14 acceptance;
10. resume Wave13 issue #205 / PR #207 packaging/signing only on final accepted Wave14 bytes.

## 10. Hard boundaries

- PR #212 remains OPEN/DRAFT and must NEVER merge to `main` without later explicit Product Owner authorization;
- PR #257 remains DRAFT and targets only `wave14/corrections-integration` until Coordinator acceptance;
- PR #259 is validation-only, DRAFT, targets `main` only as a CI trigger and must NEVER be merged;
- C11 remains IMPLEMENTATION LOCKED;
- Wave13 remains paused;
- Alarm, Operational Event and Audit remain distinct;
- backend Active revision remains canonical authority;
- no EEE-specific simulator service/Driver/hidden package/private host/DEMO-only route;
- no direct historical insert;
- no test/security/identity/licensing weakening;
- diagnose before rerun.

## 11. Mandatory resume order

1. `PROJECT GOAL.md`;
2. integration `LAST CHANGE.md`;
3. integration `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. integration `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. C19 `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`;
6. this file;
7. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
8. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
9. C11 Pass-2 audit + HMI clarification;
10. `docs/CI-VALIDATION-POLICY.md`;
11. live issue #211;
12. live PRs #212, #257 and #259;
13. revalidate exact branch/product SHA and workflow runs;
14. start with the missing `ServerScriptRunner.py` output/publish defect.
