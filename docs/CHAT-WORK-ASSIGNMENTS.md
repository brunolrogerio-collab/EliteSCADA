# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — Wave 07 worker slices remain integrated; no-Actions hardening advanced the integration head; product owner authorized coordinator-only canonical visual convergence/readiness review while workers remain stopped.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, and current MustReadSpecific documents. Then verify real branch/head/CI and execute only the current authorized assignment.

## Current product gate

`VISUAL-RUNTIME-WAVE-07` is **HARDENED INTEGRATION / CANONICAL CONVERGENCE REVIEW / CI_DEFERRED / NOT MERGE-READY**.

- Wave 06: MERGED through PR #83; final CI #487 SUCCESS
- Wave 07 Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- Integration branch: `integration/visual-runtime-wave-07`
- Real current integration head: `9c01f7e1815d76cea18728d7a753ddfd078613b7`
- Integration PR: NOT OPEN
- CI mode: CONSTRAINED
- Owner-reported remaining allowance: approximately 19 included minutes
- Workers: stopped

Integrated worker heads:
- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

Coordinator hardening on the integration branch additionally covers default-vs-Engineering semantics, typed schema authority, stable identities, validated-value propagation, runtime-readable enforcement, `assetRef` hardening, Python visual provider composition, prototype-safe structured bridge cloning and bounded structured bridge values.

### Temporary no-Actions rule

Until the owner explicitly reports reset:
1. do not open Wave 07 PR;
2. do not manually dispatch/rerun Actions;
3. do not merge Wave 07 into `main`;
4. do not activate Wave 08 workers;
5. preserve all final CI requirements.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `VISUAL-RUNTIME-WAVE-07 / WAVE-08 READINESS`  
**Status:** `ACTIVE — CANONICAL VISUAL CONVERGENCE REVIEW / CI_DEFERRED`  
**IntegrationBranch:** `integration/visual-runtime-wave-07`  
**CurrentIntegrationHead:** `9c01f7e1815d76cea18728d7a753ddfd078613b7`

**CurrentTask:** review and converge the canonical visual boundary required for Wave 08 without starting graphical editor functionality. Continue only while a concrete integration defect, ambiguous contract or indispensable dependency is found.

**MustReadSpecific:**
- `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`
- `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/VISUAL-ASSETS-AND-IMAGES.md`
- canonical Engineering contracts/import-export/validation/views/scripts
- existing C# `VisualScripting` foundation
- integrated TypeScript `web/scada-web/src/visual-runtime/**`

**Current findings to resolve before Wave 08:**
1. canonical visual property bags are string-valued while Wave 07 is typed;
2. canonical nested visual objects lack stable object IDs although Script event references require them;
3. visual binding target-property semantics are ambiguous;
4. old C# visual property/runtime names/defaults diverge from Wave 07 TypeScript contract;
5. frontend Screen/Popup elements are still `unknown[]`;
6. first-class canonical visual asset identity/metadata is not yet present in `EngineeringPackage`.

**AllowedScope:** architecture/code review; canonical contract/migration design; frontend/backend parity design/tests; small indispensable convergence corrections; official documentation.

**ForbiddenScope:** Canvas; graphical editor; zoom/pan/grid/snap; selection/drag/resize/rotation UI; Property Inspector UI; Object Palette UI; image importer/storage/renderer; Screen/Popup/Dynamo graphical authoring; production animation/tween engine; Wave 09/10 functionality.

**ValidationMatrix:** DEFERRED. Final exact Wave 07 head still requires Web + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + visual/Python acceptance before merge.

**StopCondition:** pause when remaining useful work requires substantial functional UI/runtime implementation that cannot be meaningfully validated without CI.

---

# DEV 1 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `ed8d9af027173e6d265ff382dbc9c115bcd2e284`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

---

# DEV 2 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `d6c1e997178e0ce525233079effd442f59743386`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

---

# DEV 3 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

## Coordinator note

Do not create work merely to keep workers busy. The current useful work is central contract convergence owned by the coordinator. Wave 08 becomes executable only after Wave 07 final validation/merge and the readiness conditions in `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md` are satisfied.