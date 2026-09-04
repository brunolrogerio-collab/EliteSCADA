# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C11 IMPLEMENTATION LOCKED / C12+C13+C14+C15+C17 ACCEPTED+INTEGRATED / C16 ISOLATED ACCEPTED BUT COMBINED WAVE11 RED / C18 HOLD UNTIL C12–C17 CONVERGENCE / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. DEV delivery is not Coordinator acceptance. Diagnose a red gate before rerun. Documentation-only commits do not redefine product-code authority.

## 1. Integration authority

Coordinator branch:

`wave14/corrections-integration`

Draft integration PR:

`#212` — **DRAFT / DO NOT MERGE TO main**

Last full C10 converged freeze before this correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Last fully combined-green pre-DEMO correction checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That product checkpoint contains accepted C12+C13+C14+C15+C17 and passed all five gates:

- EliteSCADA CI #1337 / `33838725814` — SUCCESS;
- Wave 11 Active HMI Runtime #265 / `33838725796` — SUCCESS;
- Preview Licensing CI #287 / `33838725824` — SUCCESS;
- L3 Seven-Driver Lab #242 / `33838725850` — SUCCESS;
- Interop Lab Smoke #164 / `33838725805` — SUCCESS.

No new Wave14 product freeze has been declared. First C12–C17 must converge, then C18 may be released/accepted, then C10 convergence cycle 2 must establish a new exact product-code SHA.

## 2. C11 remains locked

C11 Pass 2 remains:

**KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions remain:

1. persisted authorable Popup X/Y is mandatory before C11 release; centered/shell placement is not an accepted substitute;
2. the living deterministic EEE Simulation must be buildable solely through normal generic EliteSCADA tools;
3. no EEE-specific simulator service, special Driver, historical/hidden DEMO runtime/package, private host hook or DEMO-only bypass is acceptable;
4. if normal product tools cannot build the EEE Simulation, that is a PRODUCT GAP to correct before C11 release;
5. Trend, Alarm Browser and Event Browser must be first-class authored HMI objects insertable into Screen/Popup;
6. Trend must support persisted Multi-Pen authoring;
7. C11 is not released for implementation.

## 3. Accepted package state before C16 composition

- C13 — Canonical Simulation Quality: `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`;
- C14 — First-Class Operational Events: `70e311d3a359e7b00e8f0ed035478d51bb6ee001`;
- C12 — Server Runtime Automation / Generic Simulation Authoring: `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`;
- C17 — Internal Memory Authoring UX + Full Lifecycle E2E: `6db4fb33f06159f108ca17ceca23a35ee158b228`;
- C15 — Embeddable Multi-Pen Trend HMI Object: corrected candidate `3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`.

C17's earlier rerun completed successfully. The old C15 candidate `2abee30a...` remains rejected historical evidence.

The current fully green combined authority for these packages is `1dcd80a4...`.

## 4. C16 — isolated accepted, composed product currently NOT accepted

C16 package:

**HMI Operational Command + Startup/Home Screen + Persisted Popup X/Y**

Accepted isolated candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

Its branch still points to that SHA. PR #245 is already closed/merged into integration; do not integrate it again.

Five isolated gates were green:

- EliteSCADA CI `33834427366`;
- Wave 11 Active HMI Runtime `33834427376`;
- Preview Licensing `33834427368`;
- L3 Seven-Driver Lab `33834427362`;
- Interop Lab Smoke `33834427361`.

Coordinator composition product-code SHA:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Combined result:

- EliteSCADA CI #1338 / `33869678384` — SUCCESS;
- Preview Licensing CI #288 / `33869678597` — SUCCESS;
- L3 Seven-Driver Lab #243 / `33869678581` — SUCCESS;
- Interop Lab Smoke #165 / `33869678547` — SUCCESS;
- Wave 11 Active HMI Runtime #266 / `33869678407` — **FAILURE**.

### Refined Wave11 diagnosis

Failure location:

`tests-wave11/c17-memory-lifecycle.spec.ts`

The C17 TAG Source selector cannot find `memory.server.c17`.

Retained Playwright trace/report proves this is not just a slow UI selector:

- Working begins with Built-in Simulation using stable id `40000000-0000-0000-0000-000000000001` plus the Wave11 Server Memory Source;
- normal creation of `Server Memory C17` submits a package where the new Source reuses `40000000-...0001`, so Apply replaces Built-in Simulation;
- normal creation of `Client Memory C17` again reuses `40000000-...0001`, so Apply replaces `Server Memory C17`;
- the TAG Source selector then cannot list `memory.server.c17` because canonical Working state genuinely lost that Source.

The last green Wave11 #265 and red Wave11 #266 both used the exact same PR #212 `main` base:

`edbdf446ea657713bdc487be91bf10bfcd03c684`

Therefore main drift is excluded.

C16 does not directly change the generic Data Source editor, so final code ownership is still under root-cause isolation. Treat the failure as a **C16×C17 composition blocker exposing a generic authoring-state/identity defect**.

Do not:

- blindly rerun as acceptance;
- weaken the C17 real-Engineering lifecycle path;
- bypass normal UI by injecting hidden package JSON;
- loosen canonical Source identity/import semantics merely to obtain green.

Last combined-green product authority remains `1dcd80a4...` until a new exact integration product head passes all five gates.

Issue #211 diagnostic/decision record: comment `5541091621`.

## 5. C18 — HOLD / NOT RELEASED

Product Owner / Coordinator decision on 2026-09-04 supersedes the earlier administrative release:

**C18 must not be released for full implementation until C12–C17 are converged on one exact combined-green integration checkpoint.**

C18 will be handled by a parallel DEV chat, but implementation authorization is currently inactive.

Existing branch:

`wave14/c18-hmi-alarm-event-browsers`

It may remain parked at:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That SHA is a historical parked base, **not an active implementation authorization**.

Do not advance or rebase C18 onto `607a60d0...` or later coordinator/docs commits. After C12–C17 convergence, the Coordinator will explicitly declare a new exact C18 development base and release marker.

Binding handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

Current marker there is **HOLD / IMPLEMENTATION NOT AUTHORIZED**.

## 6. Immediate route

1. keep C18 on HOLD;
2. keep PR #212 DRAFT;
3. isolate root cause/ownership of the duplicate Data Source identity defect exposed by C16×C17 composition;
4. require a bounded correction preserving normal Engineering authoring and canonical identity contracts;
5. validate the correction on its exact candidate SHA under the owning package policy;
6. compose without rewriting history;
7. require all five combined gates green on one new exact product-code head;
8. declare C12–C17 converged only then;
9. explicitly release C18 with a new exact base only after that convergence;
10. after C18 acceptance/integration, run full combined validation;
11. execute C10 convergence cycle 2 and freeze a new exact product SHA;
12. revalidate affected C11 findings;
13. only then consider explicit `RELEASE C11 IMPLEMENTATION`;
14. Wave13 issue #205 / PR #207 remain paused until final Wave14 acceptance.

## 7. Boundaries still in force

- PR #212 remains DRAFT and must not be merged to `main` without later Product Owner authorization.
- C11 implementation remains LOCKED.
- C18 is HOLD despite the pre-existing branch.
- Issue #208 / PR #210 are Preview infrastructure/history, not product authority.
- Wave13 issue #205 / PR #207 remain paused.
- Do not package/sign stale pre-Wave14 product bytes.
