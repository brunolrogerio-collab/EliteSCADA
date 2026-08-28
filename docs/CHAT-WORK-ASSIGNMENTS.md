# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — Wave 06 merged after exact-final-head CI #487 green; Wave 07 promoted for development with GitHub Actions deferred until owner reports reset.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, and current wave-specific `MustReadSpecific`. Then verify real branch/head and execute only the current authorized assignment.

Workers never modify `main`, merge their own work, choose a new mission or broaden scope.

## Current product gate

`VISUAL-RUNTIME-WAVE-07` is **DEVELOPMENT ACTIVE / CI_DEFERRED**.

- Previous Wave 06: **MERGED** via PR #83
- Wave 06 final product head: `d665dc13b0922938a15252d9775ef6604e41bff4`
- Wave 06 final CI: #487 / run `33194041390` **SUCCESS**
- Wave 06 main merge: `cc79713434c1d7b5988158b843b137eaf488d923`
- Wave 07 Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- Wave 07 ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- Integration branch: `integration/visual-runtime-wave-07`
- Integration PR: **NOT OPEN — intentionally deferred to avoid Actions**
- CI mode: `CONSTRAINED`
- Owner-reported remaining allowance: approximately **19 included minutes**

### Temporary no-Actions rule

Until the product owner explicitly reports the Actions allowance reset:

1. **Do not open Wave 07 PRs.** The repository workflow runs a full matrix on `pull_request` to `main`.
2. **Do not manually dispatch or rerun Wave 07 GitHub Actions.**
3. Implement and commit on the assigned branch only.
4. Write required tests but do not use GitHub Actions to execute them.
5. Use static/source-level review and any non-Actions evidence available.
6. Delivery status is `IMPLEMENTED / CI_DEFERRED`, never fully validated, complete or merge-ready.
7. After delivery, stop at `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED`.
8. Do not weaken/delete tests, security, CAS, lifecycle, persistence or Python sandbox rules because validation is deferred.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `VISUAL-RUNTIME-WAVE-07`  
**Status:** `ACTIVE — DEVELOPMENT / CI_DEFERRED`  
**CurrentTask:** coordinate the architecture-first Visual Runtime Object Model wave, review branch deliveries without triggering Actions, reconcile shared contracts/composition on `integration/visual-runtime-wave-07`, and hold all formal CI/PR validation until owner reports reset.  
**WaveBaseSHA:** `cc79713434c1d7b5988158b843b137eaf488d923`  
**ContractSHA:** `06faf079bc5185689712bd2c9a225c2bb8d90999`  
**IntegrationBranch:** `integration/visual-runtime-wave-07`

**Objective:** establish stable visual definition identity, typed Visual Property Registry, Runtime Visual Instance lifecycle/state, deterministic property precedence `Animation > Script > Binding/Expression > Engineering Base`, project AssetReference semantics and renderer-independent Python-facing property boundaries.

**CoordinatorOwned:** cross-slice contract reconciliation; shared exports/composition; canonical Engineering decisions/schema changes if later proven necessary; central `EngineeringApp.tsx`, `main.tsx`, routing/shell; integration branch; final PR/CI/merge after reset; official docs.

**ForbiddenScope:** graphical editor/canvas; Screen/Popup/Dynamo authoring UI; asset binary importer/storage; image renderer; Server Python; new protocols; direct Python DOM/renderer/filesystem/network authority; weakening Wave 06 sandbox/security.

**ValidationMatrix:** **DEFERRED** until owner reports reset. Final Wave 07 still requires Web + backend Release/full tests incl. PostgreSQL + Runtime smoke + Chromium + Wave-specific visual/Python acceptance.

**CompletionCriteria:** all accepted worker slices integrated; deterministic visual model proven by tests; exact-final integration matrix green after reset; Wave 07 PR merged; docs synchronized.

**MustReadSpecific:** `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/VISUAL-ASSETS-AND-IMAGES.md`, current `web/scada-web/src/python-runtime/**`, current Engineering/public model projections relevant to visual scripting.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `VISUAL-RUNTIME-WAVE-07`  
**Status:** `ACTIVE — DEVELOPMENT / CI_DEFERRED`  
**CurrentTask:** implement the typed Visual Property Registry and a renderer-independent Engineering visual-definition projection using the locked Wave 07 contract.  
**Branch:** `feature/visual-wave-07-property-registry`  
**BaseSHA:** `cc79713434c1d7b5988158b843b137eaf488d923`  
**StartCondition:** satisfied — Wave 06 merged and Wave 07 explicitly promoted.  
**DependsOn:** locked Wave 07 contract in current `main`; no worker branch dependency.  
**ParallelSafeWith:** DEV 2 runtime instance and DEV 3 acceptance when file boundaries below are respected.

**Objective:** one typed registry/validation source for visual property definitions and definition-level base property projection, suitable for later Property Inspector and Python API consumption.

**AllowedScope:** new focused modules under `web/scada-web/src/visual-runtime/**` for property types, registry, validation and definition projection; focused non-central test files for this domain; minimal existing helper/type edits only when strictly required and not coordinator-reserved.

**RequiredContract:** support explicit `number`, `boolean`, `string`, `color`, `enum`, `assetRef`; common keys and constraints from `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`; no arbitrary `any` property bags; reject non-finite/invalid values; stable AssetReference identity only.

**ForbiddenScope:** runtime instance layering/lifecycle owned by DEV 2; Python bridge/runtime changes; `EngineeringApp.tsx`; `main.tsx`; canonical backend Engineering contracts/schema; routing/shell; workflows; graphical editor/canvas; asset binary import/storage.

**ReservedFiles:** coordination docs; `.github/workflows/**`; central app composition; `src/Scada.Api/Program.cs`; canonical Engineering contracts; Python runtime bridge central files; DEV 2 runtime-instance modules.

**IntegrationRequired:** coordinator may reconcile shared exports/naming after delivery.

**ValidationMatrix:** write focused registry/projection tests; **do not run GitHub Actions or open a PR until owner reset**.

**CompletionCriteria:** registry/types/defaults/constraints and Engineering projection implemented; required tests committed; diff stays in scope; handoff names exact head and files; status becomes `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED`.

**AfterCompletion:** stop. Do not begin graphical editor work.

**MustReadSpecific:** `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/VISUAL-ASSETS-AND-IMAGES.md`.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `VISUAL-RUNTIME-WAVE-07`  
**Status:** `ACTIVE — DEVELOPMENT / CI_DEFERRED`  
**CurrentTask:** implement Runtime Visual Instance identity, client-local property layers, deterministic resolution and disposal semantics against the locked Wave 07 property contract.  
**Branch:** `feature/visual-wave-07-runtime-instance`  
**BaseSHA:** `cc79713434c1d7b5988158b843b137eaf488d923`  
**StartCondition:** satisfied.  
**DependsOn:** semantic property contract in `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`; coordinator will reconcile concrete registry imports with DEV 1 at integration.  
**ParallelSafeWith:** DEV 1 and DEV 3 while runtime-instance files remain separate.

**Objective:** per-client/per-instance visual presentation state with source diagnostics and no mutation of Engineering base values.

**AllowedScope:** new focused modules under `web/scada-web/src/visual-runtime/**` dedicated to runtime instance identity/state/property layers/resolution/disposal; focused tests for these modules; minimal local types required to compile the isolated slice.

**RequiredContract:** effective precedence exactly `animation > script > binding > engineering > default`; source diagnostics; script writes only to runtime-writable properties; animation writes only to animatable properties; invalid layer values fail closed; disposal prevents further writes and clears runtime layers; separate instances remain isolated.

**ForbiddenScope:** redefine DEV 1 registry semantics; Python worker/bridge changes; central Engineering/shell/routing; canonical backend schema; graphical editor; renderer/DOM implementation; asset file loading/import.

**ReservedFiles:** coordination docs; workflows; central app composition; canonical Engineering contracts; DEV 1 registry implementation files; DEV 3 acceptance-only files.

**IntegrationRequired:** coordinator reconciles concrete property registry interface/imports and shared exports.

**ValidationMatrix:** write focused runtime-instance tests; **no GitHub Actions and no PR until reset**.

**CompletionCriteria:** runtime instance model/layers/resolution/disposal implemented; isolation tests committed; exact head/files recorded; status becomes `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED`.

**AfterCompletion:** stop. Do not implement renderer/canvas/animation engine.

**MustReadSpecific:** `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, current Wave 06 Python runtime identity/isolation code for boundary consistency.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `VISUAL-RUNTIME-WAVE-07`  
**Status:** `ACTIVE — DEVELOPMENT / CI_DEFERRED`  
**CurrentTask:** implement Wave 07 Python ↔ Visual API acceptance/contract tests and adversarial coverage without granting direct DOM/renderer authority.  
**Branch:** `test/visual-wave-07-python-api-acceptance`  
**BaseSHA:** `cc79713434c1d7b5988158b843b137eaf488d923`  
**StartCondition:** satisfied.  
**DependsOn:** locked semantic contract; tests may target stable public boundary shapes and be reconciled by coordinator with DEV 1/DEV 2 implementation during integration.  
**ParallelSafeWith:** DEV 1 and DEV 2 because this slice is acceptance/test-focused.

**Objective:** preserve Wave 06 sandbox authority while defining proof that future Client Visual Python can read/write only registered permitted visual properties on the current visual instance.

**AllowedScope:** Wave 07-focused tests/fixtures/helpers, primarily `web/scada-web/tests-e2e/**` and isolated test-support modules; source-level contract assertions where appropriate.

**RequiredAcceptance:** unregistered property denied; non-runtime-readable/writable access denied; Script override does not mutate Engineering base; precedence/source deterministic; two runtime instances isolated; disposed instance rejects writes; AssetReference cannot become filesystem/arbitrary URL; no DOM/React/renderer-private authority leaks through visual capability.

**ForbiddenScope:** product runtime implementation owned by DEV 1/2/coordinator; weaken existing Python sandbox/native escape tests; central Engineering/shell/backend; graphical editor; Server Python; new protocols.

**ReservedFiles:** product visual-runtime implementation modules unless coordinator explicitly requests a tiny testability hook; Python sandbox central runtime files; coordination docs/workflows.

**IntegrationRequired:** coordinator maps tests to final integrated public boundary after DEV 1/DEV 2 delivery.

**ValidationMatrix:** write tests now; **do not execute GitHub Actions and do not open a PR until owner reset**.

**CompletionCriteria:** required acceptance/adversarial coverage committed, structurally consistent with locked contract, exact head/files recorded; status becomes `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED`.

**AfterCompletion:** stop and wait for coordinator/reset.

**MustReadSpecific:** `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, Wave 06 sandbox/bridge tests and current `web/scada-web/src/python-runtime/**`.

---

## Coordinator note

All three Wave 07 worker tasks are now explicitly authorized for **development only**. No Wave 07 PR or Actions validation is authorized until the owner reports the allowance reset. Branch creation itself is complete and does not change the product `main` source.
