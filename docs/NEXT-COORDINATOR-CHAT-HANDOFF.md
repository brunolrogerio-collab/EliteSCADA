# EliteSCADA — Next Coordinator Chat Handoff

**Prepared:** 2026-09-04 BRT  
**Purpose:** authoritative resume point after C19 isolated acceptance, composition and combined automated validation.

> GitHub is the official development memory. Revalidate live refs before acting. Documentation-only and validation-overlay commits do not redefine product authority.

## 1. High-level state

Repository: `brunolrogerio-collab/EliteSCADA`  
Wave14 issue: #211 ACTIVE  
Integration branch: `wave14/corrections-integration`  
Integration PR: #212 OPEN/DRAFT  
C11: **IMPLEMENTATION LOCKED**  
Wave13 #205/#207: **PAUSED**

Critical permanent rule: #212 must NEVER merge to `main` without later explicit Product Owner authorization.

## 2. Product authority

C19 isolated accepted PRODUCT SHA:

`43d2734b18dc8b78caaa917b9bf6a381ca47b202`

C19 was composed into integration via history-preserving merge of PR #257.

Exact combined C12–C19 PRODUCT CANDIDATE:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

Tree:

`da6b406ac111cb40b99e5b13031601eb71606ddd`

The integration branch now has later documentation-only synchronization commits. Do not treat those branch-head SHAs as product bytes.

`3fda880...` is automated-green but **not yet declared the new final product freeze**, because fresh-Codespace visual/Product Owner browser homologation is still pending.

## 3. Combined automated validation is complete

Direct on exact candidate `3fda880...`:

- EliteSCADA CI #1370 / `33934982242` — SUCCESS;
- Wave11 Active HMI Runtime #298 / `33934982300` — SUCCESS;
- Preview Licensing CI #320 / `33934982254` — SUCCESS;
- L3 Seven-Driver Lab #276 / `33934982215` — SUCCESS;
- Interop Lab Smoke #197 / `33934982216` — SUCCESS.

Dedicated C03 carry-forward:

- Wave 14 C03 DNP3 Adapter #113 / `33935067545` — SUCCESS;
- validation-only overlay PR #260, direct product parent `3fda880...`, documentation-only trigger;
- #260 closed without merge.

So the required six automated carry-forward gates are green.

## 4. Test Preview automated validation is also green

Prepared validation branch from direct product parent `3fda880...` with only the seven proven Preview infrastructure files reapplied.

Harness SHA:

`086b5d6c92ccfa4f5dc7e947c787ffd953acc5e2`

Validation-only PR #262 was opened to main solely because `test-preview.yml` is PR-to-main triggered, then closed without merge after evidence collection.

- Test Preview run `33935493882` — SUCCESS.

It validated:

- exact devcontainer SDK;
- disposable stable machine identity for normal fail-closed licensing;
- Compose/TimescaleDB setup;
- automatic Preview launch contract;
- backend/frontend setup;
- ephemeral admin credential;
- normal Preview launcher execution;
- browser entry;
- Pyodide static asset.

This is automated harness evidence only.

## 5. FIRST TASK: fresh Codespace / real browser homologation

Do not redo the C19 packaging fix. Do not rerun already-green CI without a reason.

The immediate required action is the runbook-required **fresh Codespace visual/Product Owner browser carry-forward** against the current product + Preview harness composition.

Use:

`docs/CODESPACES-PREVIEW-RUNBOOK.md`

Core evidence to record:

1. fresh Codespace created from the current validation composition;
2. exact running SHA/provenance recorded;
3. automatic attach startup succeeds;
4. Web 5173 remains Private; API 5080 and DB 5432 remain internal;
5. real login/first-run succeeds;
6. Engineering Dark/Light readability/usability;
7. representative pt-BR/en/es changed surfaces;
8. current Engineering forms and assistants;
9. Property Inspector / Screens / Popups / Dynamos / Script Assistant as relevant;
10. Python/Pyodide functional with normal security boundaries;
11. Save -> Publish -> Activate -> real Active Runtime;
12. operator-only Runtime shell/capabilities;
13. scaling at 1280×720, 1920×1080, 2560×1440, 3840×2160 plus mismatched aspect ratio;
14. no document scroll/reflow and aligned hit targets;
15. Screen navigation and Popups under the same transform;
16. representative alarm/event/trend/history behavior;
17. visibly live simulation behavior for the current validation fixture;
18. exact-head CI remains green.

The current coordination chat does not expose Codespaces creation or an interactive browser, so this evidence requires an actual human/Product Owner browser session. Do not substitute CI screenshots, HTTP curl or Playwright for the required visual acceptance.

## 6. Freeze rule

If fresh browser carry-forward is coherent and finds no product defect, record the new exact product freeze as `3fda880...`.

If it finds a product defect:

1. capture exact evidence;
2. classify product vs harness/environment;
3. fix the narrow responsible layer;
4. add regression where practical;
5. form a new PRODUCT SHA if product bytes changed;
6. rerun all required affected/universal gates;
7. repeat the failed browser evidence;
8. only then freeze.

## 7. C19 architecture that must remain intact

- Operational Events are ordinary Engineering objects through protected Preview/CAS/Apply;
- `emit_operational_event(definition_id, message=None, context=None)` is generic;
- Python has no event-bus/history authority;
- C14 Active Runtime resolves canonical Operational Event identity;
- unknown/disabled/stale definitions fail closed;
- `ServerScriptRuntimeManager._revisionGate` is the single revision gate for Script TAG/Server Memory/Operational Event access and Active swap;
- never restore the obsolete separate gate/`AsyncLocal` reentrancy scheme;
- `ServerScriptRunner.py` is a normal Debug/publish application asset.

## 8. C11 remains locked after freeze

Even after a successful product freeze, do not automatically release C11.

Remaining browser evidence includes:

- Analog Fill visibly live;
- Dynamo operational/bad-quality state;
- two independent Dynamo instances;
- canonical Runtime resolutions/scaling;
- fullscreen/no-scroll/overlay without reflow;
- integrated living chain `automation -> TAG/quality -> alarm/event/history -> HMI objects -> command`.

Only after every blocker is clear record exactly:

`RELEASE C11 IMPLEMENTATION`

## 9. After C11 release

Build the canonical living deterministic EEE Simulation using only normal generic product mechanisms: Drivers, TAGs, Internal Memory, Server Scripts, Operational Events, Alarms, Historian, Trend, Alarm Browser, Event Browser, Commands, Screens, Popups, Dynamo and Startup/Home.

No EEE-specific service, Driver, hidden package, private runtime or DEMO-only wiring.

Then final Wave14 acceptance. Only after final accepted Wave14 bytes resume Wave13 #205/#207 packaging/signing.

## 10. PR/issue hygiene

- #212: OPEN/DRAFT; NEVER MERGE MAIN without later explicit Product Owner authorization;
- #257: C19 already composed into integration;
- #259: historical validation-only DRAFT CI trigger; NEVER MERGE;
- #260: closed without merge after C03 #113 SUCCESS;
- #262: closed without merge after Test Preview `33935493882` SUCCESS;
- #208/#210: Preview harness/history;
- #211: active Product Owner validation ledger.

## 11. Mandatory resume order

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. this file;
5. C19 implementation/progress docs;
6. C11 pre-DEMO / Pass-2 audit / HMI clarification / canonical requirements;
7. `docs/CI-VALIDATION-POLICY.md`;
8. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
9. live issue #211;
10. live PR #212 plus validation PR history;
11. revalidate current exact refs/workflows;
12. continue with fresh Codespace / real browser homologation.

Do not ask the Product Owner to repeat decisions already committed to GitHub.
