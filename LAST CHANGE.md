# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 BRT  
**Operational state:** **WAVE 14 #211 ACTIVE / C12–C18 CONVERGED / C19 5-of-6 GREEN BUT NOT ACCEPTED / C11 IMPLEMENTATION LOCKED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only commits do not redefine product-code authority.

## Accepted authority before C19

Exact accepted C12–C18 product checkpoint:

`2e284606b605a26bb9632eae5264de30bea0acde`

C10 Cycle 2 evidence on that exact product was green across EliteSCADA CI, Wave11, Preview Licensing, L3, Interop, dedicated C03 DNP3 and real Preview/browser validation.

PR #212 remains OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization.

## C19

Package: **W14-C19 — Operational Event Authoring + Server Script Emission Bridge**  
Branch: `wave14/c19-operational-event-authoring-script-bridge`  
PR #257: OPEN/DRAFT -> `wave14/corrections-integration`  
Exact base: `2e284606...`  
Latest C19 PRODUCT SHA before docs-only handoff:

`537ce074466aaf34d98142309ed4fd71036cf050`

C19 has implemented:

- normal Operational Event Engineering authoring via protected Preview/CAS/Apply;
- pt-BR/en/es affected UI;
- atomic pristine NEW-event transition regression, avoiding a C17-class stale-ID race;
- deterministic `emit_operational_event(definition_id, message=None, context=None)`;
- bounded Python request shape with no direct event-bus/history authority;
- C# replay through canonical C14 Active Runtime;
- exact Active revision protection using the existing `ServerScriptRuntimeManager._revisionGate`.

The former separate bridge gate/`AsyncLocal` reentrancy design is obsolete and must not be restored.

## Exact current validation on 537ce

SUCCESS:

- EliteSCADA CI #1365 / `33928660073`;
- Preview Licensing #315 / `33928660187`;
- L3 #271 / `33928659951`;
- Interop #192 / `33928660008`;
- C03 DNP3 #108 / `33928648375`.

FAILURE:

- Wave11 #293 / `33928659977`.

Status: **5/6 green. C19 is NOT ACCEPTED.**

Wave11 diagnostics identify the exact current blocker:

`python3: can't open file '/home/runner/work/EliteSCADA/EliteSCADA/src/Scada.Api/bin/Debug/net10.0/ServerScriptRunner.py': [Errno 2] No such file or directory`

This is not the previous activation deadlock. The Script executes once and faults because `ServerScriptRunner.py` is not present in normal Debug output.

## Immediate next action

Inspect `src/Scada.Api/Scada.Api.csproj` and the runner content/copy rules. Fix normal product packaging so `ServerScriptRunner.py` is copied to Debug/build output and publish/runtime output consistently.

Do not hardcode repository paths, bypass Python in tests, increase timeouts, weaken isolation/security/lifecycle, or add EEE-specific behavior.

After the product fix, validate one new exact candidate SHA through:

1. Wave11;
2. EliteSCADA CI;
3. Preview Licensing;
4. L3 Seven-Driver Lab;
5. Interop Lab Smoke;
6. C03 DNP3;
7. exact-diff architecture review.

Diagnose any red before rerun.

## Route after C19 acceptance

1. compose C19 into integration preserving history;
2. five combined gates on exact C12–C19 integration SHA;
3. C03 DNP3 on same SHA;
4. real Preview/browser carry-forward;
5. new exact product freeze;
6. close remaining C11 browser evidence;
7. only then explicitly `RELEASE C11 IMPLEMENTATION` if all blockers are clear;
8. build living deterministic EEE Simulation using only ordinary generic EliteSCADA mechanisms;
9. final Wave14 acceptance;
10. resume Wave13 #205/#207 packaging/signing only on final accepted Wave14 bytes.

## Mandatory references

Read:

- `docs/CURRENT-COORDINATOR-HANDOFF.md`;
- `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
- live C19 `docs/WAVE14-C19-IMPLEMENTATION-HANDOFF.md`;
- live C19 `docs/WAVE14-C19-PROGRESS.md`;
- C11 pre-DEMO/audit/HMI clarification docs;
- `docs/CI-VALIDATION-POLICY.md`;
- live issue #211 and PRs #212/#257/#259.

C11 remains IMPLEMENTATION LOCKED. Wave13 remains paused. #259 is validation-only and must never merge.
