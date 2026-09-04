# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C11 IMPLEMENTATION LOCKED / C12+C13+C14+C15+C17 ACCEPTED+INTEGRATED / C16 EXACT CANDIDATE ACCEPTED AND COMPOSED BUT COMBINED WAVE11 RED / C18 RELEASED FOR IMPLEMENTATION FROM LAST GREEN BASE / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. DEV delivery is not Coordinator acceptance. Diagnose a red gate before rerun. Documentation-only `[skip ci]` commits do not redefine product-code authority.

## 1. Integration authority

Coordinator integration branch:

`wave14/corrections-integration`

Draft integration PR:

`#212` — **DRAFT / DO NOT MERGE TO main**

Last full C10 converged freeze before this correction round:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Last fully combined-green pre-DEMO correction checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That checkpoint contains accepted C12+C13+C14+C15+C17 and passed all five gates:

- EliteSCADA CI #1337 / `33838725814` — SUCCESS;
- Wave 11 Active HMI Runtime #265 / `33838725796` — SUCCESS;
- Preview Licensing CI #287 / `33838725824` — SUCCESS;
- L3 Seven-Driver Lab #242 / `33838725850` — SUCCESS;
- Interop Lab Smoke #164 / `33838725805` — SUCCESS.

No new Wave14 product freeze has been declared. C12-C18 must converge, then C10 convergence cycle 2 must establish a new exact product-code SHA.

## 2. C11 remains locked

C11 Pass 2 is consolidated and still says:

**KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions remain:

1. persisted authorable Popup X/Y is mandatory before C11 release;
2. the living deterministic EEE Simulation must be buildable solely through normal generic EliteSCADA tools;
3. no EEE-specific simulator service, historical DEMO runtime, hidden package manipulation, private Driver/host hook, DOM/React bypass, authorization bypass or licensing bypass is acceptable;
4. Trend, Alarm Browser and Event Browser must be first-class authored HMI objects insertable into Screen/Popup;
5. Trend must support persisted Multi-Pen authoring.

Canonical correction plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

## 3. Accepted package state

Accepted/integrated packages before C16 composition:

- C13 — Canonical Simulation Quality: candidate `b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`;
- C14 — First-Class Operational Events: candidate `70e311d3a359e7b00e8f0ed035478d51bb6ee001`;
- C12 — Server Runtime Automation / Generic Simulation Authoring: candidate `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`;
- C17 — Internal Memory Authoring UX + Full Lifecycle E2E: candidate `6db4fb33f06159f108ca17ceca23a35ee158b228`;
- C15 — Embeddable Multi-Pen Trend HMI Object: corrected accepted candidate `3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`.

The current fully green combined authority for those packages is `1dcd80a4...` above.

## 4. C16 — accepted candidate, composed candidate currently NOT accepted

C16 package:

**HMI Operational Command + Startup/Home Screen + Persisted Popup X/Y**

Accepted isolated candidate:

`6d9f971eb469d931ca56becff4d240088725f37a`

Its five exact-candidate gates were green:

- EliteSCADA CI `33834427366`;
- Wave 11 Active HMI Runtime `33834427376`;
- Preview Licensing `33834427368`;
- L3 Seven-Driver Lab `33834427362`;
- Interop Lab Smoke `33834427361`.

Coordinator composed C16 into the accepted post-C15 integration tree at product-code commit:

`607a60d0e930fc7080e09c0689c306c040c4ace6`

Commit message:

`merge(w14): compose accepted C16 into post-C15 integration`

Combined exact-head result on `607a60d0...`:

- EliteSCADA CI #1338 / `33869678384` — SUCCESS;
- Preview Licensing CI #288 / `33869678597` — SUCCESS;
- L3 Seven-Driver Lab #243 / `33869678581` — SUCCESS;
- Interop Lab Smoke #165 / `33869678547` — SUCCESS;
- Wave 11 Active HMI Runtime #266 / `33869678407` — **FAILURE**.

The Wave11 failure is concrete and must be diagnosed before any rerun or C16 acceptance in composition:

`tests-wave11/c17-memory-lifecycle.spec.ts`

failed in `createMemoryTag()` because the Source selector did not contain `memory.server.c17` after the preceding C16 startup bootstrap/lifecycle sequence:

`expect(sourceOption).toBeTruthy()` -> `Received: null` at line 191.

Four earlier Wave11 tests passed, including C16 startup bootstrap and the generic Active lifecycle, before C17 Memory failed. Six later tests did not run. Do not classify this as a flake without evidence. Determine whether C16 composition changed Working package/source visibility, test isolation/order, or UI refresh semantics.

Therefore:

- isolated C16 candidate remains accepted;
- composed `607a60d0...` is **NOT YET an accepted combined product checkpoint**;
- last combined-green authority remains `1dcd80a4...` until this failure is resolved and five gates are green on a new exact head.

## 5. C18 — released and ready for DEV

C18 package:

**Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 Event and C15 visual-object prerequisites are satisfied.

Authorized development base remains the last combined-green authority:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Package branch already exists at that exact base:

`wave14/c18-hmi-alarm-event-browsers`

Binding DEV handoff:

`docs/WAVE14-C18-DEV-HANDOFF.md`

C18 must **not** rebase onto the current red C16 composition head merely because it is newer. Develop from the authorized green base and target `wave14/corrections-integration`. Coordinator will resolve later composition with C16.

## 6. Immediate next-coordinator route

1. revalidate live `wave14/corrections-integration`, PR #212 and C18 branch before acting;
2. diagnose Wave11 #266 on composed C16 `607a60d0...`; do not rerun blindly;
3. correct only the actual composition/product/test-isolation defect, preserving C12-C17 and C16 contracts;
4. obtain all five exact-head combined gates green and declare the resulting combined checkpoint;
5. in parallel receive/review C18 from frozen base `1dcd80a4...`;
6. require C18 exact-candidate five-gate green plus package-specific browser evidence;
7. integrate accepted C18 and resolve C16/C18 composition without losing C14/C15/C17/C16 contracts;
8. rerun complete combined exact-head validation;
9. execute C10 convergence cycle 2 and freeze a new exact product-code SHA;
10. revalidate affected C11 findings plus remaining real-browser Memory/visual/resolution items;
11. only then issue explicit `RELEASE C11 IMPLEMENTATION`;
12. finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md` before/with canonical EEE implementation;
13. build the living EEE Simulation only through corrected normal product mechanisms;
14. later validate the conceptual HMI/TAG architecture against physical Modbus PLC;
15. Wave13 issue #205 / PR #207 remain paused until final Wave14 acceptance.

## 7. Boundaries still in force

- PR #212 remains DRAFT.
- C11 implementation remains LOCKED.
- C18 is authorized from `1dcd80a4...` despite the newer red integration composition.
- Issue #208 / PR #210 are Preview infrastructure/history, not product authority.
- Wave13 issue #205 / PR #207 remain paused.
- Do not package/sign stale pre-Wave14 product bytes.
