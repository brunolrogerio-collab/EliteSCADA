# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md` and `docs/PARALLEL-WORK.md`.

**Last coordinator synchronization:** 2026-08-27

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, current `MustReadSpecific` and `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

`NextQueuedTask` is planning only. Workers never modify `main`, merge their own PR, work another DEV branch or broaden their mission.

## Current product gate

`INTERFACE-WAVE-04` is **ACTIVE / INTEGRATION IN PROGRESS**.

**WaveBaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**IntegrationBranch:** `integration/interface-wave-04`  
**Integration PR:** #78 `Integrate Interface Wave 04` — DRAFT  
**Product objective:** practical non-graphical platform through project portability/backup, basic Trends and stronger local Administration while preserving Wave 03 lifecycle/runtime acceptance.

Documentation-only coordination commits after WaveBaseSHA do not invalidate worker evidence. Unrelated product-code `main` remains frozen until the wave closes.

Previous Wave 03 is **MERGED** through PR #74, integration CI #418 green, merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `INTERFACE-WAVE-04`  
**CurrentTask:** integrate accepted Wave 04 slices, enforce portability CAS, mount central surfaces and prove final composition  
**Branch:** `integration/interface-wave-04` plus `main` for coordination docs  
**Status:** `ACTIVE`  
**BaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**StartCondition:** `TRUE`

**DependsOn:** Wave 03 merged/green; DEV 2 and DEV 3 accepted; DEV 1 targeted correction outstanding.

**ParallelSafeWith:** DEV 1 targeted correction.

**Objective:** preserve product-code `main`, integrate only accepted slices, own central mounts/composition, require package restore CAS, add integrated Wave 04 acceptance, run final Web/backend/tests/Runtime-smoke/Chromium and merge only exact-head green PR #78.

**ReservedFiles:** `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, coordination docs, `.github/workflows/**`, `src/Scada.Api/Program.cs`, `web/scada-web/src/engineering/EngineeringApp.tsx`, `web/scada-web/src/main.tsx`, `web/scada-web/src/AppNavigation.tsx`, central routing/shell/composition.

**ForbiddenScope:** protocol expansion; Wave 05 Python/Script production work before Wave 04 closes; graphical editor; weakening security/CAS/lifecycle/runtime authority.

**IntegrationRequired:** yes.  
**IntegrationTarget:** `integration/interface-wave-04` / PR #78.  
**ValidationMatrix:** Web build + backend Release/full tests + Runtime smoke + Chromium cross-product acceptance, preserving Wave 03 lifecycle acceptance.  
**CompletionCriteria:** DEV 1 corrected/accepted; all three slices integrated; no duplicate paths; portability Preview/Apply/CAS usable; Trend mounted; Administration coherent; final exact integration CI green.  
**AfterCompletion:** `CONTINUE_COORDINATION` to architecture-first Wave 05.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `INTERFACE-WAVE-04`  
**CurrentTask:** `Project Management / Portability Workspace — targeted CAS correction`  
**Branch:** `feature/interface-wave-04-project-management`  
**PR:** #77  
**Status:** `ACTIVE — TARGETED_CORRECTION_REQUIRED`  
**BaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**StartCondition:** `TRUE — coordinator Delivery Review comment on PR #77`

**DependsOn:** existing canonical JSON Import/Export, Preview/Apply/CAS and `.escadapkg` contracts.

**ParallelSafeWith:** coordinator integration work. DEV 2/3 are already integrated.

**Objective:** complete the already implemented Project Management surface and close one blocking CAS hole in package restore.

**RequiredCorrection:** `/api/project-package/import/apply` must require `x-elitescada-workspace-version`. Missing or invalid header must be rejected before final mutation; stale version remains `409`; authorization/Audit authority remains backend-owned. Add explicit test that Apply without header is rejected and Working is not mutated.

**AllowedScope:** existing PR #77 files only, especially `src/Scada.Api/ProjectPackages/ProjectPackageEndpoints.cs` and focused package-CAS tests.

**ForbiddenScope:** no package-format/schema/persistence redesign; no central `EngineeringApp.tsx`/routing; no Trend/Admin; no protocols/Python/graphical editor; do not weaken Preview/CAS/security.

**ReservedFiles:** existing PR #77 Project Management/package files. Coordinator owns central mounts.

**IntegrationRequired:** coordinator retargets/merges accepted corrected PR into `integration/interface-wave-04` and mounts `EngineeringProjectManagementWorkspace`.

**IntegrationTarget:** `integration/interface-wave-04`.

**ValidationMatrix:** missing-header rejection/no mutation; invalid-header rejection; stale-version `409`; existing Preview/Apply/package tests; exact new head CI green.

**CompletionCriteria:** mandatory CAS cannot be omitted; existing portability scope remains green; PR body/evidence updated; stop at `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**NextQueuedTask:** `Wave 05 Script Engineering Workspace foundation`.  
**NextStartCondition:** `QUEUED_ONLY — Wave 04 merged, coordinator central Script contract stabilized, board promotes task`.

**MustReadSpecific:** `docs/DEVELOPMENT-WAVES.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, PR #77 coordinator comment, package endpoints/tests and current portability files.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `INTERFACE-WAVE-04`  
**CurrentTask:** `Basic Trend Viewer`  
**Branch:** `feature/interface-wave-04-trend-viewer`  
**PR:** #76  
**Status:** `MERGED_TO_INTEGRATION / WAIT_FOR_COORDINATOR`  
**BaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`

**Evidence:** final worker head `0bae66f1b5a11d8bbccf95f6c308dbb5121c8751`; CI #432 SUCCESS; coordinator Delivery Review accepted; PR #76 merged into `integration/interface-wave-04` as `c72ab5f703a6a54db1c5c177856442570d4fb969` lineage after DEV 3 integration. Basic Trend Viewer is mounted by coordinator exactly once in Runtime.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**NextQueuedTask:** `Wave 05 Script Reference Runtime Adapter`.  
**NextStartCondition:** `QUEUED_ONLY — Wave 04 merged and central Script contract stabilized/promoted`.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `INTERFACE-WAVE-04`  
**CurrentTask:** `Administration Workspace ergonomics`  
**Branch:** `feature/interface-wave-04-administration-workspace`  
**PR:** #75  
**Status:** `MERGED_TO_INTEGRATION / WAIT_FOR_COORDINATOR`  
**BaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`

**Evidence:** final worker head `8cdf2dae80dee5d0a761d773b780bf8e33cdde00`; CI #436 SUCCESS; coordinator Delivery Review accepted; PR #75 merged into `integration/interface-wave-04` as merge `467007c7471f042df7a94096a9639b72103e6c3d`. Existing Engineering Security section continues to host Administration; no duplicate central path is required.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**NextQueuedTask:** `Wave 05 Script validation / compatibility`.  
**NextStartCondition:** `QUEUED_ONLY — Wave 04 merged and central Script contract stabilized/promoted`.

---

## Coordinator note for future chats

Current action boundary: **only DEV 1 has executable worker work**. `siga` in DEV 1 means perform the targeted mandatory-CAS correction recorded above on PR #77. DEV 2 and DEV 3 must remain stopped. Coordinator continues PR #78 integration and final validation once DEV 1 returns green.