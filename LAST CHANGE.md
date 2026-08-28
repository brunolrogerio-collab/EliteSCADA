# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE-07 THIRD STATIC AUDIT HARDENING COMPLETE / CI_DEFERRED / NOT MERGE-READY**  
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
- exact current integration head: `d184fdd5b65f2ce0c0e6ca28cd092644be080555`
- integration PR: intentionally not open
- latest Actions run remains #488; no Wave 07 Actions were triggered
- owner-reported remaining Actions allowance remains approximately 19 included minutes until explicitly superseded by a reset report.

Worker deliveries remain integrated and workers remain stopped:
- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

## Wave 07 implemented/converged foundation

The integration branch contains:

- typed Visual Property Registry and renderer-independent Runtime Visual Instance;
- stable object identity/lifecycle/isolation;
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

Earlier audit corrected stale tests that still expected three-field AssetReferences or the removed `createDefaultBaseValues()` API. That correction sequence ended at `63878f6fe28a0a9ac101d622628f8b95658899a7`.

The third static audit then found and corrected four additional concrete blockers:

1. **Python visual clear was incomplete.** Runtime had `clearScriptOverride`, but the public Python route could only set. Bridge v1 now keeps the existing `visualProperty.write` capability with explicit operation `clear`; provider composition and the actual Pyodide Worker expose `elite_scada.visual_property_clear(target, property)` without using `null` as a sentinel.
2. **Malformed visual JSON could escape validation and throw.** Null visual nodes/bindings/keys/targets are now kept on the `parse -> validate -> preview` path through null-safe generic, built-in, recursive-reference and concrete-TAG validation. Direct and full `EngineeringExchangeService.Preview` tests are committed.
3. **TS integer validation was wider than C# Int32.** Visual integer values now share signed-Int32 limits in both languages.
4. **A Wave 06 policy snapshot would deterministically fail Chromium.** `python-sandbox-foundation.spec.ts` still expected the old exact policy object and omitted Wave 07 `maxBridgeDepth`, `maxBridgeNodes` and `maxBridgeStringLength`; the expectation was updated without removing any security bound.

The exact head after these corrections is `d184fdd5b65f2ce0c0e6ca28cd092644be080555`.

All of the above remains **static/source evidence only** until CI reset and exact-head execution.

## Deliberately unresolved before Wave 08 activation

### Canonical typed visual persistence

`VisualElementEngineeringDto.Properties` still persists as `Dictionary<string,string>`. Schema v11 is transitional identity/convergence, not final JSON-native typed persistence. The codec contains this mismatch; it does not authorize editor-private/stringly persistence.

Before Wave 08 becomes ACTIVE, settle and validate the canonical typed representation and migration strategy.

### Lower-priority convergence items

Resolve with typed-persistence/readiness work rather than stretching the no-CI interval indefinitely:

- C# `double.ToString("R")` and JavaScript `String(number)` can spell some exponent values differently, so the transitional string codec is not guaranteed byte-canonical across runtimes;
- canonical `Tag` and `Property` binding kinds currently collapse into a generic Runtime `binding` source and should retain enough discrimination before the graphical binding engine resolves them itself.

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

Every current `main` change after the Wave 06 merge is documentation-only. No functional `main` code delta is hidden from the Wave 07 integration branch. Historical reconciliation is still required before final exact-head CI.

## Current coordinator state

**WAIT_FOR_OWNER_CI_RESET / NO FURTHER SPECULATIVE CODE.**

Until the owner explicitly reports Actions reset:
- do not open the Wave 07 PR;
- do not dispatch/rerun Actions;
- do not merge Wave 07 into `main`;
- do not activate Wave 08 workers;
- do not implement Canvas, Property Inspector, Object Palette, image import/storage/renderer or other Wave 08 UI;
- only resume code if repository review demonstrates another concrete safe correctness blocker.

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
