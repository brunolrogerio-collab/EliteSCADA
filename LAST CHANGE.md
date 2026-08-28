# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-27  
**Development state:** **INTERFACE-WAVE-04 FINAL INTEGRATION CI**

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

Coordinator PR #74 merged Wave 03. Final integration head `41d44d513d337e8ef6d3cc0e04ef0cf07a697b41`; CI #418 fully green; merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`. This is the immutable logical WaveBaseSHA for Wave 04.

## INTERFACE-WAVE-04 — FINAL INTEGRATION

**WaveBaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**IntegrationBranch:** `integration/interface-wave-04`  
**Integration PR:** #78 `Integrate Interface Wave 04` — DRAFT pending final exact-head CI.

### DEV 3 Administration — ACCEPTED / MERGED_TO_INTEGRATION

PR #75 final worker head `8cdf2dae80dee5d0a761d773b780bf8e33cdde00`; CI #436 SUCCESS; integration merge `467007c7471f042df7a94096a9639b72103e6c3d`. Existing Engineering Security section hosts Administration; backend remains authority.

### DEV 2 Basic Trend Viewer — ACCEPTED / MERGED_TO_INTEGRATION

PR #76 final worker head `0bae66f1b5a11d8bbccf95f6c308dbb5121c8751`; CI #432 SUCCESS; integrated through `c72ab5f703a6a54db1c5c177856442570d4fb969`. Coordinator mounts Basic Trend Viewer exactly once in Runtime after Alarm Center and TAG Inspector.

### DEV 1 Project Management / Portability — CORRECTED / ACCEPTED / MERGED_TO_INTEGRATION

PR #77 targeted CAS correction is complete.

Final corrected worker head: `e84a762b5c5f511448f5f23600e4b592abed203c`; exact-head CI #439 SUCCESS.

Coordinator verified:

- package Apply requires `x-elitescada-workspace-version`;
- missing header returns `400` before package mutation;
- invalid/multiple/negative header returns `400`;
- stale version remains `409` with expected/current versions;
- missing/invalid-header test proves Workspace `changeVersion` remains unchanged;
- authorization is evaluated before the version requirement and Audit remains backend-owned;
- package format/schema/persistence/lifecycle semantics are unchanged.

PR #77 was retargeted to `integration/interface-wave-04`, marked Ready by coordinator and merged with exact worker head. Integration merge commit: `6f0ee17f0faa25827d1f5ed03561724fefd1f909`.

Coordinator mounts `EngineeringProjectManagementWorkspace` in the Engineering project overview alongside `EngineeringLifecycleWorkspace`, keeping lifecycle and project portability together without creating a parallel project model.

## Coordinator PR #78 — CURRENT EXACT STATE

Current integration head: `4ccc84e4230b024f3a2cb9e13b9b4abcf282763b`.

Coordinator added integrated shell acceptance that explicitly verifies:

- Runtime Operations + Alarm Center + Basic Trend Viewer;
- Engineering Project Management visibility;
- existing TAG Engineering path;
- Administration through the Security section;
- Audit navigation;
- stored Engineering locale behavior.

Final CI #445 / run `33133437843` is running on exact head `4ccc84e4230b024f3a2cb9e13b9b4abcf282763b`.

Last observed state:

- Web build: SUCCESS;
- backend build/test/smoke: IN PROGRESS;
- Chromium: waits on backend.

Do not mark PR #78 Ready or merge until this exact-head matrix is fully green.

## Next coordinator sequence

1. inspect CI #445 final result;
2. if any job fails, inspect root cause and fix without weakening tests/security/CAS;
3. if Web + backend/full tests + Runtime smoke + Chromium all pass, mark PR #78 Ready;
4. merge PR #78 to `main` with exact-head guard;
5. verify post-merge main health;
6. update `docs/ROADMAP.md`, `docs/CHAT-WORK-ASSIGNMENTS.md` and this handoff to Wave 04 MERGED;
7. only then open architecture-first Wave 05 canonical Script Engineering.

## Wave 05 — QUEUED ONLY

Canonical Script Engineering is the next gate. Coordinator first stabilizes the shared canonical Script contract: Scripts collection/entity identity, scope/source/enabled state, events/references/dependencies, import/export/migration, Preview/Apply, revision/PostgreSQL and `.escadapkg` compatibility. Workers may not start until Wave 04 is merged and the board explicitly promotes assignments.

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