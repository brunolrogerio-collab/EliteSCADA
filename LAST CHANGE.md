# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-27  
**Development state:** **INTERFACE-WAVE-04 ACTIVE / INTEGRATION IN PROGRESS**

Repository truth remains separated into `MERGED`, `IMPLEMENTED IN PR`, `MERGED_TO_INTEGRATION`, `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED` and `SPECIFIED / NOT IMPLEMENTED`.

## Mandatory resume reading

Read current `main` before action:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/PARALLEL-WORK.md`;
5. `docs/DEVELOPMENT-WAVES.md`;
6. `docs/CHAT-WORK-ASSIGNMENTS.md`;
7. `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
8. current assignment `MustReadSpecific`.

GitHub branch/PR/head/CI state is operational truth.

## Wave 03 — MERGED

Coordinator PR #74 merged the Wave 03 composition. Final integration head `41d44d513d337e8ef6d3cc0e04ef0cf07a697b41`; CI #418 fully green; merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`.

This merge is the immutable logical WaveBaseSHA for Wave 04.

## INTERFACE-WAVE-04 — ACTIVE

**WaveBaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**IntegrationBranch:** `integration/interface-wave-04`  
**Integration PR:** #78 `Integrate Interface Wave 04` — **DRAFT**

Product objective: practical non-graphical platform through project portability/backup, basic Trends and coherent local Administration while preserving Wave 03 lifecycle/runtime acceptance.

### DEV 3 Administration — ACCEPTED / MERGED_TO_INTEGRATION

PR #75 `Improve Administration workspace ergonomics`:

- final worker head `8cdf2dae80dee5d0a761d773b780bf8e33cdde00`;
- exact-head CI #436 SUCCESS;
- scope limited to Administration UI/API/model/styles/tests;
- backend remains authority; no frontend role/capability authority or credential leakage;
- explicit session-invalidation consequences and supported account-level revocation semantics;
- coordinator Delivery Review accepted;
- retargeted to `integration/interface-wave-04` and merged;
- integration merge commit `467007c7471f042df7a94096a9639b72103e6c3d`.

The existing Engineering Security section already hosts `UserAdministration`; no duplicate central path was added.

### DEV 2 Basic Trend Viewer — ACCEPTED / MERGED_TO_INTEGRATION

PR #76 `Add Wave 04 Basic Trend Viewer`:

- final worker head `0bae66f1b5a11d8bbccf95f6c308dbb5121c8751`;
- exact-head CI #432 SUCCESS;
- one-Pen read-only bounded Trend over protected TAG/Historian APIs;
- windows 15m/1h/6h/24h, max 24h, max 1000 samples;
- explicit quality/timestamp/value semantics and live-vs-historical behavior;
- deliberately no premature multi-axis/multi-Pen contract;
- coordinator Delivery Review accepted;
- retargeted and merged into integration;
- integration lineage after merge `c72ab5f703a6a54db1c5c177856442570d4fb969`.

Coordinator then mounted `BasicTrendViewer` exactly once in Runtime after Alarm Center and Runtime TAG Inspector. Integration branch head after this central mount: `a2ab615d2eaf9a52f5c1d760a6c585c2e5225e7d`.

### DEV 1 Project Management — TARGETED CORRECTION REQUIRED

PR #77 `Add Engineering project management and portability workspace` remains Draft/Open on worker branch.

Worker delivery head before correction: `ccd3fe50887dbc54b8384e5df6afa7b76de84925`; CI #433 SUCCESS.

Most of the slice is valid: canonical JSON Export/Import, Preview/Apply, merge modes, `.escadapkg` backup/inspection/restore UI, package boundary copy and protected error handling.

Coordinator Delivery Review found one blocking semantic defect in the backend CAS correction:

- `/api/project-package/import/apply` accepts missing `x-elitescada-workspace-version`;
- missing header produces `expectedChangeVersion = null`;
- `EngineeringWorkspace.AcquireMutationAsync(null)` serializes the mutation but performs no version comparison;
- therefore package restore can still bypass CAS when the caller omits the header.

This conflicts with the Wave 04 requirement that Engineering import/restore mutation never bypass Preview/validation/CAS.

Coordinator comment on PR #77 requires a narrow correction:

1. make `x-elitescada-workspace-version` mandatory for package Apply;
2. reject missing or invalid header before final mutation while preserving authorization/Audit;
3. retain stale-version `409` behavior;
4. add explicit missing-header test proving rejection and no Workspace mutation;
5. rerun exact-head CI and return to `WAIT_FOR_COORDINATOR`.

**Only DEV 1 currently has executable worker work.** DEV 2/3 remain stopped.

## Coordinator PR #78 — CURRENT STATE

PR #78 `Integrate Interface Wave 04` is Draft and currently contains accepted Administration + Trend slices plus central Trend mounting.

Current integration head: `a2ab615d2eaf9a52f5c1d760a6c585c2e5225e7d`.

CI #437 / run `33130552147` was started on this head. At last check:

- Web build: SUCCESS;
- backend build/test/smoke: IN PROGRESS;
- Chromium: waits on backend job.

This partial CI is diagnostic only. Final Wave 04 gate must run again after corrected DEV 1 integration and Project Management central mounting.

## Next coordinator sequence

1. wait for DEV 1 targeted correction on PR #77;
2. verify new exact head, diff and CI;
3. require mandatory package-restore CAS semantics;
4. accept/retarget/merge PR #77 into `integration/interface-wave-04` only if correct;
5. mount `EngineeringProjectManagementWorkspace` in coordinator-owned Engineering composition;
6. add/adjust integrated Wave 04 browser acceptance for portability + Trend + Administration while preserving Wave 03 lifecycle acceptance;
7. run final exact integration Web + backend/full tests + Runtime smoke + Chromium;
8. mark PR #78 Ready and merge only if fully green;
9. update roadmap/board/handoff;
10. open architecture-first Wave 05 only after Wave 04 closes.

## Wave 05 — QUEUED ONLY

Canonical Script Engineering is the next gate, but workers may not start it yet. Coordinator must first stabilize the shared canonical Script contract in the Wave 05 integration branch: Scripts collection/entity identity, scope/source/enabled state, event/references/dependencies, import/export/migration, Preview/Apply, revision/PostgreSQL and `.escadapkg` compatibility.

## First owner validation gate — LOCKED

The first true owner-facing build remains **EliteSCADA v0.1 — Full Product Validation Preview** after functional Client Visual Python + graphical Engineering/Runtime. Modbus TCP is sufficient as the real industrial protocol. MQTT/OPC UA/BACnet/S7/Allen-Bradley production work remains post-v0.1 unless the product owner deliberately changes the gate.

## Permanent continuity rules

- workers never choose their own next task, modify `main`, merge their own PR or broaden scope;
- final wave quality is proven on the integrated composition, not worker CI alone;
- documentation-only `main` movement does not invalidate logical WaveBaseSHA;
- known failing work is never merged;
- tests/security/CAS/runtime guards are never weakened merely for green CI;
- research does not equal production implementation;
- canonical Engineering remains authority.