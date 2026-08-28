# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — all three Wave 07 worker slices reviewed and integrated; formal validation remains deferred until the product owner reports GitHub Actions reset.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, and current wave-specific `MustReadSpecific`. Then verify real branch/head/CI and execute only the current authorized assignment.

Workers never modify `main`, merge their own work, choose a new mission or broaden scope.

## Current product gate

`VISUAL-RUNTIME-WAVE-07` is **IMPLEMENTED ON INTEGRATION / CI_DEFERRED / NOT MERGE-READY**.

- Previous Wave 06: **MERGED** via PR #83
- Wave 06 final CI: #487 / run `33194041390` **SUCCESS**
- Wave 06 main merge: `cc79713434c1d7b5988158b843b137eaf488d923`
- Wave 07 Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- Wave 07 ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- Integration branch: `integration/visual-runtime-wave-07`
- Current integrated head: `ffde3b03a3b647acb2f0c484c11b956c602237d6`
- Integration PR: **NOT OPEN — intentionally deferred to avoid Actions**
- CI mode: `CONSTRAINED`
- Owner-reported remaining allowance: approximately **19 included minutes**

### Integrated worker evidence

- DEV 1 head `ed8d9af027173e6d265ff382dbc9c115bcd2e284`: typed Visual Property Registry, property validation, Engineering visual-definition projection and focused tests.
- DEV 2 head `d6c1e997178e0ce525233079effd442f59743386`: Runtime Visual Instance identity/state/layers/resolution/disposal and focused isolation tests.
- DEV 3 head `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`: Python ↔ Visual capability/acceptance/adversarial coverage.
- Coordinator integration commit `295bdabba5c25b2e4a729228130185976735d939` preserved all three worker heads as merge parents.
- Coordinator reconciliation then added the typed Registry → Runtime consumer adapter and unified the public Runtime Visual Instance surface expected by acceptance tests, ending at `ffde3b03a3b647acb2f0c484c11b956c602237d6`.

The integrated diff remains confined to new `web/scada-web/src/visual-runtime/**` modules and Wave 07 test files. No graphical editor, backend schema, shell/routing, workflow, renderer, asset binary importer or Server Python implementation was introduced.

### Temporary no-Actions rule

Until the product owner explicitly reports the Actions allowance reset:

1. **Do not open a Wave 07 PR.**
2. **Do not manually dispatch or rerun Wave 07 GitHub Actions.**
3. Do not merge Wave 07 into `main`.
4. Preserve required tests exactly; do not weaken security/sandbox/CAS/lifecycle/persistence rules.
5. Current state is `IMPLEMENTED / CI_DEFERRED`, never validated/complete/merge-ready.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `VISUAL-RUNTIME-WAVE-07`  
**Status:** `WAITING_FOR_CI_RESET — INTEGRATED / CI_DEFERRED`  
**CurrentTask:** preserve the integrated Wave 07 head and avoid Actions/PR churn. When the owner reports reset, reconcile the integration branch with current `main`, open the integration PR, run the required exact-head matrix, correct only concrete failures, and merge only when fully green.  
**WaveBaseSHA:** `cc79713434c1d7b5988158b843b137eaf488d923`  
**ContractSHA:** `06faf079bc5185689712bd2c9a225c2bb8d90999`  
**IntegrationBranch:** `integration/visual-runtime-wave-07`  
**CurrentIntegrationHead:** `ffde3b03a3b647acb2f0c484c11b956c602237d6`

**IntegratedObjective:** stable visual definition identity; typed Visual Property Registry; Runtime Visual Instance lifecycle/state; deterministic precedence `Animation > Script > Binding/Expression > Engineering Base > Default`; runtime override isolation from Engineering; stable project-owned AssetReference validation; renderer-independent Python-facing visual property boundary.

**CoordinatorOwned:** final main reconciliation, central exports/composition, canonical Engineering decisions if proven necessary, integration PR, CI, corrections, merge and official docs.

**ForbiddenScope:** graphical editor/canvas; Screen/Popup/Dynamo authoring UI; asset binary importer/storage; image renderer; production animation engine; Server Python; new protocols; direct Python DOM/renderer/filesystem/network authority; weakening Wave 06 sandbox/security.

**ValidationMatrix:** **DEFERRED** until owner reports reset. Final Wave 07 requires Web + backend Release/full tests incl. PostgreSQL + Runtime smoke + Chromium + Wave-specific visual/Python acceptance on the exact final integration head.

**CompletionCriteria:** exact-final integrated CI fully green; integration PR merged to `main`; docs synchronized. Until then Wave 07 is not complete.

**MustReadSpecific:** `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/VISUAL-ASSETS-AND-IMAGES.md`, integrated `web/scada-web/src/visual-runtime/**`, Wave 07 tests and current Python runtime capability boundary.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `VISUAL-RUNTIME-WAVE-07`  
**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `ed8d9af027173e6d265ff382dbc9c115bcd2e284`  
**Branch:** `feature/visual-wave-07-property-registry`  
**Delivery:** typed Visual Property Registry, explicit property value types/constraints, AssetReference validation, Engineering visual-definition projection and focused tests.  
**AfterCompletion:** stop. No Wave 08/editor work is authorized.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `VISUAL-RUNTIME-WAVE-07`  
**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `d6c1e997178e0ce525233079effd442f59743386`  
**Branch:** `feature/visual-wave-07-runtime-instance`  
**Delivery:** Runtime Visual Instance identity, client-local Engineering/binding/script/animation layers, deterministic resolution/source diagnostics, instance isolation and disposal semantics with focused tests.  
**AfterCompletion:** stop. No renderer/canvas/animation-engine work is authorized.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `VISUAL-RUNTIME-WAVE-07`  
**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / CI_DEFERRED / INTEGRATED`  
**DeliveryHead:** `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`  
**Branch:** `test/visual-wave-07-python-api-acceptance`  
**Delivery:** Python ↔ Visual capability boundary tests, integrated Runtime Visual Instance acceptance, AssetReference/path denial, instance isolation, disposal and DOM/renderer-private authority denial coverage.  
**AfterCompletion:** stop and wait for coordinator/reset.

---

## Coordinator note

All three workers have reached the maximum useful Wave 07 work allowed under the current no-Actions policy. Do not assign Wave 08 early. The next executable coordinator event is the owner's explicit Actions-reset notice, followed by reconciliation, PR creation and exact-head validation of Wave 07.
