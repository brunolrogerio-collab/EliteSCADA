# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE-07 FOURTH STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED / NOT MERGE-READY**  
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

## Wave 07 — current exact state

Wave 07 remains **NOT MERGED**, **CI_DEFERRED** and **NOT MERGE-READY**.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- integration branch: `integration/visual-runtime-wave-07`
- exact current integration head: `e376b37d2772906dd667afa199a6d8882abd43ae`
- integration PR: intentionally not open
- latest known Actions run remains #488; no Wave 07 Actions have been intentionally triggered
- owner-reported remaining Actions allowance remains approximately 19 included minutes until explicitly superseded by a reset report.

Worker deliveries remain integrated and workers remain stopped:
- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

## Wave 07 implemented/converged foundation

The integration branch contains:

- typed Visual Property Registry and renderer-independent Runtime Visual Instance;
- stable visual definition/object identity, lifecycle and per-client isolation;
- precedence `Animation > Script > Binding/Expression > Engineering Base > Default`;
- runtime-readable/runtime-writable/animatable/binding authority checks;
- canonical `assetRef = null | { assetId }` with path/URL rejection;
- transitional Engineering Schema v11 with stable nested visual IDs and v1-v10 compatibility;
- canonical binding semantics (`Key` = destination visual property, `Target` = source reference);
- typed frontend visual Engineering projection;
- C#/TypeScript common property catalog and `core.*` built-in schema parity;
- schema-guided transitional string-property codecs;
- official frontend Engineering -> Runtime adapter;
- backend Preview enforcement for built-in schemas/properties/binding capabilities;
- Client Visual Python visual-property authority bound to the exact current Runtime Visual Instance;
- structured bridge hardening and bounded structured values.

## Static audit corrections already applied

Earlier audit corrected stale tests that still expected three-field AssetReferences or the removed `createDefaultBaseValues()` API. That sequence ended at `63878f6fe28a0a9ac101d622628f8b95658899a7`.

The third static audit then corrected:

1. explicit Python visual-property `clear` across dispatcher/provider/composition and the real `elite_scada` Pyodide Worker API;
2. malformed nested visual JSON fail-closed behavior;
3. TS integer validation parity with signed C# Int32;
4. a stale Wave 06 exact Python policy snapshot that would have failed Chromium.

That sequence ended at `d184fdd5b65f2ce0c0e6ca28cd092644be080555`.

## Fourth static audit — additional concrete blockers corrected

A further repository review found several deterministic or referential-integrity failures that were safe to correct without running Actions:

1. **Top-level malformed Screen/Popup JSON was still not fully fail-closed.** `screens:[null]`, `popups:[null]` and null/blank view keys could still reach duplicate/reference/existing-resolution code after validation. Preview now emits stable errors instead of throwing, with real JSON coverage.
2. **Current-schema Script tests were stale after the v11 bump.** Fresh current exports/project packages still had three assertions hard-coded to schema v10. Current-export assertions now use `EngineeringExchangeService.CurrentSchemaVersion`; historical v10 persistence/compatibility fixtures remain intentionally v10.
3. **Canonical `VisualObject` Script references were absent from the real Engineering reference catalog.** Wave 07 gives nested visual objects stable IDs, and Script Engineering already supports `definitionId/objectId`, but Import/Preview previously catalogued only visual-definition IDs. The real Preview path now resolves both `VisualObject` dependencies and object-scoped `ScriptVisualEventReference` entries.
4. **The public `ScriptEngineeringReferenceResolver.FromEngineeringPackage` had the same gap.** It now recursively catalogs Screen/Popup object IDs from v11 packages, including nested elements.
5. **Malformed Script collections could Preview safely but still crash in Apply.** Null Script definitions and null visual-event references now produce explicit Preview errors (`SCRIPT_NULL`, `SCRIPT_VISUAL_REFERENCE_NULL`) so Apply is never entered with that invalid input.
6. **Prospective validation errors on already-saved Scripts were being dropped.** The validator already checked the whole prospective Script model, but the handler only surfaced issues attached to incoming Scripts. Errors on existing Scripts invalidated by a TAG/Data Source/visual change now surface as `script-model` Preview errors and block Apply.
7. **Prospective visual identity did not fully mirror legacy v10 identity preservation.** Script validation now projects Screen/Popup definition/object identity using the same `Id`/`Key` resolution and child-key preservation semantics used by Apply/`VisualElementIdentity`. A v10 update that keeps an object by key preserves the Script reference; removing that object now fails Preview before Apply.
8. **Prospective definition identity now follows actual Apply precedence.** Existing resolved identity wins over incoming identity for Screen/Popup/Dynamo updates, matching the Import handlers instead of inventing a different preview-only identity model.

New coverage includes:

- malformed top-level view JSON;
- malformed Script arrays plus Apply guard;
- real `EngineeringExchangeService.Preview` acceptance/rejection for Script dependencies on canonical visual object IDs;
- object-scoped visual event association validation;
- public package-derived nested `VisualObject` resolution;
- v10 Screen update preserving an existing child ID by key;
- v10 Screen update removing a referenced child and invalidating an existing Script.

The exact integration head after the fourth static audit is:

`e376b37d2772906dd667afa199a6d8882abd43ae`

All of this remains **static/source evidence only** until CI reset and exact-head execution.

## Reviewed hypotheses deliberately not changed

- Bare historical visual `Type` strings such as `group`/`text` were **not** automatically remapped to `core.group`/`core.text`; old `Type` was extensible and such an alias would risk reclassifying custom types without evidence.
- Script-only mutation packages remain schema v10 intentionally. They do not contain the v11 visual-ID surface, the backend deliberately supports v10, and existing Script E2E coverage depends on that compatibility boundary.
- C# Runtime visual `Clear*` methods are less strict than the TS implementation about re-checking write/binding/animation flags. No current public path can create a forbidden layer and then exploit clear as stronger authority, so this is tracked as semantic convergence debt rather than a current security blocker.

## Deliberately unresolved before Wave 08 activation

### Canonical typed visual persistence

`VisualElementEngineeringDto.Properties` still persists as `Dictionary<string,string>`. Schema v11 is transitional identity/convergence, not final JSON-native typed persistence. The codec contains this mismatch; it does not authorize editor-private/stringly persistence.

Before Wave 08 becomes ACTIVE, settle and validate the canonical typed representation and migration strategy.

### Lower-priority convergence / hardening items

- C# `double.ToString("R")` and JavaScript `String(number)` can spell some exponent values differently, so the transitional string codec is not guaranteed byte-canonical across runtimes;
- canonical `Tag` and `Property` binding kinds currently collapse into a generic Runtime `binding` source and should retain enough discrimination before the graphical binding engine resolves them itself;
- explicit `Guid.Empty` and null-entry robustness remains inconsistent in older non-visual Engineering entity collections; Screen/Popup top-level empty-ID handling is part of this broader import-hardening class and should be handled deliberately rather than expanding the frozen Wave 07 audit into a rewrite of every legacy handler.

### Final validation and merge

Wave 07 still requires exact-final-head Web build, backend Release/full PostgreSQL tests, Runtime smoke, Chromium and Wave-specific visual/Python acceptance before merge.

## Repository-wide audit debt tracked separately

- frontend floating `latest` dependencies, no committed `package-lock.json`, CI using `npm install` rather than `npm ci`;
- no `global.json` while `.NET` language version is `latest`;
- `main` currently lacks required branch protection;
- production CORS default is too permissive;
- `tests-e2e` sit outside normal frontend typecheck;
- cheap source/contract tests run through expensive Playwright/WebServer infrastructure.

These are hardening/reproducibility debt, not authorization to broaden Wave 07 now.

## Main/integration fact

All functional Wave 07 code remains on `integration/visual-runtime-wave-07`; current `main` changes after the Wave 06 merge are coordination/documentation only. Historical reconciliation is still required before final exact-head CI.

## Current coordinator state

**WAIT_FOR_OWNER_CI_RESET / NO FURTHER SPECULATIVE CODE.**

Until the owner explicitly reports Actions reset:
- do not open the Wave 07 PR;
- do not dispatch/rerun Actions;
- do not merge Wave 07 into `main`;
- do not activate Wave 08 workers;
- do not implement Canvas, Property Inspector, Object Palette, image import/storage/renderer or other Wave 08 UI;
- only resume code if another concrete, safe correctness blocker is demonstrated.

## When Actions reset

1. Re-read `main` and verify exact integration state.
2. Reconcile `integration/visual-runtime-wave-07` with current `main`.
3. Finalize canonical typed visual property persistence/migration with compatibility coverage.
4. Resolve any reproducibility blocker required for trustworthy final validation.
5. Open one Wave 07 PR only when ready to spend CI.
6. Validate the exact final head across Web, backend Release/full PostgreSQL tests, Runtime smoke, Chromium and Wave-specific visual/Python acceptance, preserving Wave 06 sandbox/native-escape/cancellation regressions.
7. Fix root causes only.
8. Merge Wave 07 only fully green.
9. Activate Wave 08 only after its Definition of Ready is satisfied.
