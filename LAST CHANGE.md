# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE-07 STATIC AUDIT CORRECTIONS COMPLETE / CI_DEFERRED / NOT MERGE-READY**  
**CI budget mode:** **CONSTRAINED until owner explicitly reports reset**

## Mandatory resume reading

Before any action read current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`, `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`, plus current source/tests.

GitHub branch/PR/head/CI is operational truth.

## Wave 06 — MERGED

- PR #83: MERGED
- final product head: `d665dc13b0922938a15252d9775ef6604e41bff4`
- final CI #487: fully green
- main merge: `cc79713434c1d7b5988158b843b137eaf488d923`
- automatic post-merge run #488 allocated no runner and executed no product steps; it remains infrastructure evidence, not a product regression.

## Wave 07 — static audit corrections complete, validation deferred

Wave 07 remains **NOT MERGED**, **CI_DEFERRED** and **NOT MERGE-READY**.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- integration branch: `integration/visual-runtime-wave-07`
- exact current integration head: `63878f6fe28a0a9ac101d622628f8b95658899a7`
- integration PR: intentionally not open
- latest Actions run remains #488; no Wave 07 Actions were triggered
- owner-reported remaining Actions allowance remains approximately 19 included minutes until an explicit reset report supersedes it.

Worker deliveries remain integrated and workers remain stopped:
- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

## Wave 07 implemented foundation

The integration branch contains the worker implementation plus coordinator hardening for:

- typed Visual Property Registry;
- Runtime Visual Instance identity/lifecycle/isolation;
- deterministic precedence `Animation > Script > Binding/Expression > Engineering Base > Default`;
- truthful distinction between explicitly engineered values and registry defaults;
- schema/definition identity checks and validated-value propagation;
- runtime-readable/runtime-writable enforcement;
- stable `assetRef` authority with path/URL denial;
- Client Visual Python visual-property provider bound to the exact current runtime instance;
- TAG + Client Memory + visual-property provider composition without weakening the Wave 06 Pyodide sandbox;
- structured bridge hardening and bounded structured values;
- visual/Python acceptance and adversarial tests committed but not yet executed on the final head.

## Coordinator canonical convergence

The coordinator no-Actions convergence established:

1. transitional Engineering Schema v11 on the integration branch with stable nested visual IDs and v1-v10 compatibility;
2. canonical visual binding semantics: `EngineeringBindingDto.Key` = destination visual property and `Target` = source reference;
3. typed frontend visual-element Engineering projection;
4. distinct registry Default versus explicit Engineering Base;
5. C#/TypeScript common visual property catalog parity;
6. renderer-independent built-in object schemas for `core.group`, `core.rectangle`, `core.ellipse`, `core.line`, `core.text`, `core.image`, `core.valueDisplay`, `core.button`;
7. canonical `assetRef = null | { assetId }` authority with descriptive metadata reserved to the future first-class asset entity;
8. schema-guided transitional string-property codecs in C# and TypeScript;
9. official frontend Engineering -> Runtime adapter;
10. backend Preview enforcement for built-in schemas/properties/binding capabilities.

## Newly discovered deterministic test blockers — CORRECTED / CI_DEFERRED

A repository-wide static audit after the earlier stop point found three deterministic Chromium/Wave-07 test inconsistencies against the already-settled canonical contract. These were concrete correctness blockers, so coordinator correction was authorized even during the no-CI interval.

Corrections on `integration/visual-runtime-wave-07`:

- `cd2753f64a8191df3d2861871bb53077b74cc7a2` — removes obsolete source-contract expectation that `AssetReference` contains `assetId/name/mediaType`; the test now enforces identity-only authority.
- `c3f9cc15a6715bf6434b1553878ba7c6121e0783` — fixes Wave 07 browser acceptance so valid `assetRef` is `{ assetId }` and name/mediaType/path/URL extras are explicitly rejected.
- `63878f6fe28a0a9ac101d622628f8b95658899a7` — replaces obsolete `createDefaultBaseValues()` test calls with the actual `createDefaultValues()` API and aligns Engineering projection asset tests with identity-only references.

Static compare/search after the correction found no remaining `createDefaultBaseValues` reference and no remaining obsolete three-field AssetReference expectation in the Wave 07 delta. This remains source evidence only, not execution proof.

The main Wave-07 implementation decision was also clarified so the earlier permissive wording cannot recreate the same drift: canonical visual references contain only `assetId`; asset name/MIME/dimensions/hash belong to the future asset entity.

## Repository-wide audit findings — tracked, not silently fixed during CI freeze

The same audit found several project-quality issues outside the immediate Wave 07 correctness blockers:

- frontend reproducibility is weak: several dependencies use `latest`, there is no committed `package-lock.json`, and CI uses `npm install` rather than `npm ci`;
- backend SDK/compiler reproducibility is not fully pinned: no `global.json` is present while `LangVersion` is `latest` and CI selects `10.0.x`;
- `main` is currently unprotected at GitHub level, so the green-CI-before-merge rule is procedural rather than enforced by branch protection;
- API CORS currently allows any origin/header/method globally, acceptable as a development convenience but too permissive as a final industrial-product default;
- `tests-e2e` are outside the frontend `tsconfig` build include, so test TypeScript/API drift can escape the Web build and surface only in the later Chromium job;
- many source/contract tests run under the full Playwright/WebServer path, increasing CI cost and delaying cheap failures.

These items are real hardening/reproducibility debt. They are **not authorization to broaden Wave 07 during the current no-CI interval**. Address them deliberately when CI budget permits and before the v0.1 packaging/hardening gates where applicable.

## Deliberately unresolved before Wave 08 activation

### 1. Canonical typed visual property persistence

`VisualElementEngineeringDto.Properties` is still `Dictionary<string,string>`. Schema v11 is therefore a transitional identity/convergence schema, not the final typed visual persistence model.

The legacy codec is containment for this mismatch, not permission for the graphical editor to persist a private/stringly model. Before Wave 08 becomes ACTIVE, the canonical JSON-native typed property representation and migration strategy must be deliberately settled and validated.

### 2. Wave 07 final validation and merge

The exact final integration head still requires full integrated CI. Wave 08 cannot start merely because static review and deterministic test corrections are complete.

## Current coordinator state

**WAIT_FOR_OWNER_CI_RESET / NO FURTHER SPECULATIVE CODE.**

Until the owner explicitly reports Actions reset:
- do not open the Wave 07 PR;
- do not dispatch/rerun Actions;
- do not merge Wave 07 into `main`;
- do not activate Wave 08 workers;
- do not implement Canvas, Property Inspector, Object Palette, image import/storage/renderer or other Wave 08 UI;
- only resume product-code changes if repository review exposes another concrete correctness blocker that can safely be addressed without CI.

## When Actions reset

1. Re-read `main` and verify exact integration head/state.
2. Reconcile `integration/visual-runtime-wave-07` with current `main` if required.
3. Resolve/finalize canonical typed visual property persistence deliberately, with migration/compatibility coverage, before Wave 08 activation.
4. Resolve any remaining reproducibility/CI blocker necessary to make the final run trustworthy; do not casually broaden functional scope.
5. Open one Wave 07 integration PR only when ready to spend CI.
6. Require exact-final-head Web build + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + Wave-specific visual/Python acceptance.
7. Fix concrete failures without weakening tests/security/contracts.
8. Merge Wave 07 only fully green.
9. Activate/freeze Wave 08 assignments only after the Wave 08 Definition of Ready is satisfied.
