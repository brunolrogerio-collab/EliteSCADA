# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — fourth Wave 07 static audit remains the current functional head `e376b37d2772906dd667afa199a6d8882abd43ae`; owner reported expanded GitHub Actions allowance and a controlled Web-build probe successfully received a hosted runner and completed green; CI mode is now NORMAL; workers remain stopped; Wave 08 is not active.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, and current MustReadSpecific documents. Then verify real branch/head/CI and execute only the current authorized assignment.

## Current product gate

`VISUAL-RUNTIME-WAVE-07` is **FOURTH STATIC AUDIT COMPLETE / CI AVAILABLE / PRE-FINALIZATION / NOT MERGE-READY**.

- Wave 06: MERGED through PR #83; final CI #487 SUCCESS
- Wave 07 Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- Integration branch: `integration/visual-runtime-wave-07`
- Exact current integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`
- Integration PR: NOT OPEN
- CI mode: NORMAL
- Availability probe: run #488 attempt 2, Web build job `98990802140`, SUCCESS
- Probe target: historical Wave 06 merge head only; it is not Wave 07 validation
- Workers: stopped
- Wave 08: NOT ACTIVE

Integrated worker heads:
- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

Coordinator convergence/hardening already covers:
- stable nested visual object identity through transitional Schema v11;
- typed Visual Property Registry and `core.*` parity;
- `Default` versus Engineering Base semantics;
- canonical `assetRef = null | { assetId }`;
- canonical binding target semantics;
- Engineeering -> Runtime adapter and schema-guided transitional codecs;
- runtime property layering/isolation/disposal;
- Client Visual Python visual read/write/explicit-clear on the exact current instance;
- bounded/prototype-safe Python bridge values;
- malformed nested/top-level visual Engineering fail-closed Preview paths;
- signed-Int32 TS/C# parity;
- canonical nested `VisualObject` Script dependencies and object-scoped event references;
- prospective validation of existing Scripts against incoming Engineering changes;
- legacy v10 visual identity projection aligned with Apply semantics;
- deterministic stale tests found during audits corrected on the integration branch.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `VISUAL-RUNTIME-WAVE-07 / WAVE-08 READINESS`  
**Status:** `READY_FOR_WAVE07_FINALIZATION — CI AVAILABLE`  
**IntegrationBranch:** `integration/visual-runtime-wave-07`  
**CurrentIntegrationHead:** `e376b37d2772906dd667afa199a6d8882abd43ae`

**CurrentTask:** finalize Wave 07 deliberately now that CI is available. Do not spend the final full matrix until the candidate head is ready.

**MustReadSpecific:**
- `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`
- `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/VISUAL-ASSETS-AND-IMAGES.md`
- canonical Engineering contracts/import-export/validation/views/scripts
- existing C# `VisualScripting` foundation
- integrated TypeScript `web/scada-web/src/visual-runtime/**`
- Client Visual Python dispatcher/provider/Worker tests.

**Remaining readiness blockers before Wave 08 activation:**
1. canonical JSON-native typed visual property persistence/migration is not yet settled; `VisualElementEngineeringDto.Properties` remains `Dictionary<string,string>` and the codec is transitional only;
2. Wave 07 exact-final-head full CI/merge remains mandatory;
3. reconcile integration with current documentation-only `main` history before final candidate validation;
4. settle the minimum reproducibility blockers needed for trustworthy final CI.

**Repository-wide audit debt, not automatic scope expansion:** frontend dependency/lockfile reproducibility; .NET SDK/compiler pinning; branch protection for `main`; production CORS hardening; earlier typecheck for `tests-e2e`; cheaper separation of contract/unit checks from full Playwright.

**AllowedScope now:** coordinator-owned Wave 07 finalization, canonical typed visual persistence/migration, compatibility coverage, minimal reproducibility fixes required for trustworthy validation, branch reconciliation, integration PR and exact-head validation.

**ForbiddenScope until Wave 07 is merged:** Canvas; graphical editor; zoom/pan/grid/snap; Property Inspector UI; Object Palette UI; image importer/storage/renderer; Screen/Popup/Dynamo graphical authoring; production animation/tween engine; Wave 09/10 functionality.

**ValidationMatrix:** final exact head requires Web + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + visual/Python acceptance, preserving Wave 06 sandbox/native-escape/cancellation regressions.

**CompletionCriteria:** final integrated Wave 07 head fully green and merged; docs synchronized; Wave 08 Definition of Ready then evaluated.

---

# DEV 1 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / INTEGRATED`  
**DeliveryHead:** `ed8d9af027173e6d265ff382dbc9c115bcd2e284`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

---

# DEV 2 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / INTEGRATED`  
**DeliveryHead:** `d6c1e997178e0ce525233079effd442f59743386`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

---

# DEV 3 - EliteSCADA

**Status:** `WAIT_FOR_COORDINATOR — IMPLEMENTED / INTEGRATED`  
**DeliveryHead:** `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`  
**AfterCompletion:** remain stopped. No Wave 08 work authorized.

## Coordinator note

Do not create work merely to keep workers busy. CI capacity does not itself authorize Wave 08. Wave 08 becomes executable only after Wave 07 final validation/merge and the readiness conditions in `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md` are satisfied.
