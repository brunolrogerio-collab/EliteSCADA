# LAST CHANGE — EliteSCADA

**Date:** 2026-09-05 BRT  
**Operational state:** **WAVE 14 #211 ACTIVE / C11 IMPLEMENTATION ACTIVE / C20+C21 INTEGRATED / C11 HMI NEAR-GREEN / POST-DEMO PRODUCT GAPS RECORDED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only commits after the product/test head do not redefine product bytes.

## Current branches / PRs

- integration: `wave14/corrections-integration` @ `cac8b6d58a4969a4aa6369590f6bd32fc2fae3d2`;
- integration PR #212: **OPEN/DRAFT**, base `main`, **DO NOT MERGE without later explicit Product Owner authorization**;
- C11 branch: `wave14/c11-canonical-eee-demo`;
- C11 implementation PR #263: **OPEN/DRAFT** -> integration only;
- validation-only PR #266: **OPEN/DRAFT / NEVER MERGE**, exists only to trigger main-scoped workflows;
- `main` remains `edbdf446ea657713bdc487be91bf10bfcd03c684` at the last live revalidation;
- Wave13 #205 / PR #207 remain paused.

## Product authority carried into C11

Pre-DEMO exact combined C12–C19 product authority:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

C20 visual dynamic wire contract was accepted and integrated. C21 Dynamo TagReference runtime was later accepted on exact PRODUCT SHA:

`6d0d71bc91b08114f4c3d3238b56e4ca225b76bd`

C21 integrated into `wave14/corrections-integration` through merge commit:

`cac8b6d58a4969a4aa6369590f6bd32fc2fae3d2`

C11 consumed C21 through a real two-parent merge and now builds/tests against that product.

## Current C11 product/test head before this documentation handoff

Exact C11 product/test head:

`043ee86dd4815a253978b032750a1498a6c89df9`

Commit message:

`test(c11): sequence one-shot HMI command acknowledgements`

The branch may now be ahead by documentation-only handoff commits. Do not treat those later docs SHAs as new product authority.

## Exact validation on 043ee86

SUCCESS:

- EliteSCADA CI #1392 / `33949584309`;
- Preview Licensing #342 / `33949584221`;
- L3 Seven-Driver Lab #298 / `33949584220`;
- Interop Lab Smoke #219 / `33949584204`.

FAILURE:

- Wave11 Active HMI Runtime #320 / `33949584209`.

Wave11 #320 is **diagnosed**. Do not rerun it unchanged and do not classify it as a PRODUCT GAP.

Result before failure:

- C16/C17/C15/C18/C19/C20 passed;
- C21 pure + Active two-instance TagReference tests passed;
- C11 canonical EEE foundation passed;
- C11 HMI reached the navigation phase after exercising lifecycle, Analog Fill, Dynamo instances, Commands, faults and quality scenario;
- 16 tests passed; 1 failed; 4 downstream tests did not run due project dependency.

Exact failure:

`getByRole('button', { name: 'TENDÊNCIAS' })` resolved to two legitimate buttons named `TENDÊNCIAS` on the overview (top navigation + quick-access button).

First mandatory action for the next coordinator:

1. inspect `web/scada-web/tests-wave11/c11-eee-demo-hmi.spec.ts` around line 146;
2. replace the ambiguous role/name selector with a deterministic selector/scoped locator for the intended Trends navigation control;
3. do not change product code for this failure;
4. create a new exact C11 candidate SHA and run the normal five-gate validation through #266;
5. diagnose any new red before rerun.

## C11 DEMO state

The canonical EEE Simulation is already constructed through normal generic product surfaces:

- `builtin.memory.server` process authority;
- deterministic Server Script;
- conceptual EEE TAG model;
- Alarm + Operational Event separation;
- Historian/Trend;
- Commands using one-shot request TAGs;
- live Analog Fill;
- one reusable pump Dynamo with two independent P01/P02 instances;
- Screens, pump Popups, Trend, Alarm Browser and Event Browser;
- `eee.overview` startup;
- normal `Preview -> Apply -> Save -> Publish -> Activate -> HMI Runtime` lifecycle.

Real EEE references are captured in `docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`. Original material supplied by Product Owner included `tags(1).csv`, `alarm.csv` and 14 unique HMI screens. Do not ask for this material again unless a specific raw artifact is genuinely unavailable after Library/repository search.

## Newly fixed post-DEMO gate

Read:

`docs/WAVE14-C11-POST-DEMO-SYSTEM-RECOVERY-SCALING-GAPS.md`

Product Owner sequence is binding:

1. finish canonical Simulation DEMO;
2. close application-level repository validation and canonical `eee-demo` package exportability;
3. resolve generic system backup/restore and Historian administration/recovery requirements;
4. resolve generic TAG engineering scaling;
5. resolve analog display decimal-place authoring/runtime persistence;
6. only then update/test Preview in fresh Codespace;
7. after Codespace findings are resolved, build the second EEE package using real Modbus addresses for the existing PLC.

The `.escadapkg` remains an application package, not a container for local users/passwords/history/system secrets.

Scaling/decimals are confirmed PRODUCT GAP items but do not need to stop finishing the current Simulation DEMO unless the current DEMO itself cannot proceed without them. They **do block** Codespace homologation and the real Modbus/PLC variant.

## Mandatory references

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-HANDOFF.md`;
7. `docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`;
8. `docs/WAVE14-C11-POST-DEMO-SYSTEM-RECOVERY-SCALING-GAPS.md`;
9. `docs/CI-VALIDATION-POLICY.md`;
10. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
11. live issue #211;
12. live PRs #212, #263 and #266;
13. exact workflow history for current C11 head.

Do not merge #212 to `main`. Do not resume Wave13 yet.