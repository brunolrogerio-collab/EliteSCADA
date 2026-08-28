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

## Wave 07 — third static audit hardening complete, validation deferred

Wave 07 remains **NOT MERGED**, **CI_DEFERRED** and **NOT MERGE-READY**.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- integration branch: `integration/visual-runtime-wave-07`
- exact current integration head: `d184fdd5b65f2ce0c0e6ca28cd092644be080555`
- integration PR: intentionally not open
- latest Actions run remains #488; no Wave 07 Actions were triggered
- owner-reported remaining Actions allowance remains approximately 19 included minutes until an explicit reset report supersedes it.

Worker deliveries remain integrated and workers remain stopped:
- DEV 1 `ed8d9af027173e6d265ff382dbc9c115bcd2e284`
- DEV 2 `d6c1e997178e0ce525233079effd442f59743386`
- DEV 3 `25ebac63a957c1c0c5b8e2557caec152d9d36bfc`

## Wave 07 implemented foundation

The integration branch contains the worker implementation plus coordinator convergence/hardening for:

- typed Visual Property Registry;
- Runtime Visual Instance identity/lifecycle/isolation;
- deterministic precedence `Animation > Script > Binding/Expression > Engineering Base > Default`;
- truthful distinction between explicitly engineered values and registry defaults;
- schema/definition identity checks and validated-value propagation;
- runtime-readable/runtime-writable enforcement;
- stable `assetRef` authority with path/URL denial;
- Client Visual Python property authority bound to the exact current Runtime Visual Instance;
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

## Earlier deterministic test drift — CORRECTED / CI_DEFERRED

Repository audit previously found stale Wave 07 tests that contradicted the settled asset/property API. Corrections ended at:

- `cd2753f64a8191df3d2861871bb53077b74cc7a2` — AssetReference source contract aligned to identity-only authority;
- `c3f9cc15a6715bf6434b1553878ba7c6121e0783` — browser acceptance aligned to `assetRef = { assetId }`;
- `63878f6fe28a0a9ac101d622628f8b95658899a7` — projection tests aligned to `createDefaultValues()` and identity-only assets.

The implementation decision in `main` was clarified so visual references contain only `assetId`; asset name/MIME/dimensions/hash belong to the future asset entity.

## Third static audit — concrete blockers corrected / CI_DEFERRED

A subsequent full-project/Wave-07 audit found three product-contract blockers plus one deterministic legacy test failure. These were safe, concrete correctness corrections and therefore authorized during the no-CI interval.

### 1. Client Visual Python Script override clear

The Wave 07 contract requires Python to set **and clear** a runtime-writable Script override. Runtime already had `clearScriptOverride`, but the public Python path only exposed write.

The integration branch now keeps bridge v1 and the existing `visualProperty.write` capability while adding explicit operation `clear`:

- dispatcher/provider interface includes `clearVisualProperty`;
- concrete visual provider maps clear to `RuntimeVisualInstance.clearScriptOverride`;
- official composed Client Visual provider forwards the same authority;
- the actual Pyodide Worker module now exposes `elite_scada.visual_property_clear(target, property)` and sends `visualProperty.write` + operation `clear` with no value/null sentinel;
- tests cover set -> clear -> fallback to Engineering Base, current-target/current-instance authority, and the real Worker source surface;
- Python editor help now states that the capability can set or explicitly clear Script overrides.

Key commits in this slice include `815259ceb1e7ce0e58bfa6ed10099377fd0c3b85`, `e25aa5dcbcf20371e9ffa9bb0614f5810dd41f5a`, `42e641cea77e61edb813701bab0f595b93c369e5`, `f0ef61b96ed4461eb5225903647bfc6d16806702` and associated tests.

### 2. Malformed visual Engineering fails closed in Preview

Static review found that JSON-deserialized null visual nodes/bindings/keys/targets could be diagnosed by generic validation and then dereferenced by later built-in/reference validation, turning invalid user input into an exception instead of `ImportIssue` output.

The integration branch now:

- reports `VISUAL_ELEMENT_NULL` and `BINDING_NULL` rather than dereferencing malformed collection entries;
- avoids placeholder checks on null targets;
- keeps built-in schema validation from dereferencing null/blank binding keys;
- keeps `ViewEngineeringHandler` recursive reference traversal null-safe;
- keeps `EngineeringHandlerSupport.ValidateConcreteTagBindings` null-safe;
- includes direct validator tests and a full `EngineeringExchangeService.Preview` malformed-tree test proving the expected fail-closed path structurally.

Key commits include `59953a34ff5178138a5f2cc59d65a311769f2180`, `edc01655c311483c8996de104c856c414a67e452`, `d687599ef0909a1363a3d6eac121f7c1dd63c29a`, `a69446f0497d1c0dca1bca0dcb791312335b7102`, `92c0ec2a188c157db8154477c5e742b239b674fb` and `f89ce79cbd7ec791d8c329e80091b2623103dc3b`.

### 3. TypeScript integer domain aligned with C# Int32

C# visual integer values use `int`; the TypeScript legacy codec already enforced signed Int32, but the general Visual Property Registry previously accepted any JavaScript integer.

The registry now rejects integer values outside `-2147483648..2147483647` using the existing `number.integer` validation code, with boundary tests committed. Commit: `231eed9f63d2d953d541fd348a4f584ef9379b1b` plus test `d8b09a925a6e30d012cd5d9d8d17c822bd0ce93b`.

### 4. Wave 06 sandbox policy snapshot drift

`python-sandbox-foundation.spec.ts` still compared `CLIENT_VISUAL_PYTHON_POLICY` to the old Wave 06 exact object, while Wave 07 structured-value hardening had already added `maxBridgeDepth`, `maxBridgeNodes` and `maxBridgeStringLength`. That test would deterministically fail in Chromium.

The expectation was reconciled without removing any security bound. Final correction commit: `d184fdd5b65f2ce0c0e6ca28cd092644be080555`.

All corrections remain **static/source evidence only** until CI reset and exact-head execution.

## Repository-wide audit findings — tracked, not silently broadened

The audits also identified project-quality debt outside the immediate Wave 07 correctness blockers:

- frontend dependencies use floating `latest` versions, no committed `package-lock.json`, and CI uses `npm install` rather than `npm ci`;
- no `global.json` pins the .NET SDK while `LangVersion` is `latest`;
- `main` is not protected by required branch checks;
- API CORS is globally permissive;
- `tests-e2e` are outside the normal frontend TypeScript build include;
- many cheap source/contract tests execute only through the full Playwright/WebServer path.

These are tracked hardening/reproducibility issues, not authorization to expand Wave 07 while CI is frozen.

## Deliberately unresolved before Wave 08 activation

### 1. Canonical typed visual property persistence

`VisualElementEngineeringDto.Properties` is still `Dictionary<string,string>`. Schema v11 is therefore a transitional identity/convergence schema, not the final typed visual persistence model.

The codec contains the mismatch; it does not authorize an editor-private/stringly persistence model. Before Wave 08 becomes ACTIVE, canonical JSON-native typed property representation and migration strategy must be deliberately settled and validated.

### 2. Known lower-priority convergence items

Static review also noted two items to resolve deliberately with the typed-persistence/readiness work rather than stretching Wave 07 indefinitely:

- C# `double.ToString("R")` and JavaScript `String(number)` can produce different textual spellings for some exponent values, so the transitional codec should not be treated as byte-canonical across runtimes;
- the frontend Engineering -> Runtime projection currently collapses canonical `Tag` and `Property` binding kinds into a generic Runtime `binding` kind, which is acceptable while values are externally resolved but should retain sufficient source discrimination before the graphical binding engine depends on it.

### 3. Wave 07 final validation and merge

The exact final integration head still requires full integrated CI. Wave 08 cannot start merely because static review is clean.

## Current coordinator state

**WAIT_FOR_OWNER_CI_RESET / NO FURTHER SPECULATIVE CODE.**

Until the owner explicitly reports Actions reset:
- do not open the Wave 07 PR;
- do not dispatch/rerun Actions;
- do not merge Wave 07 into `main`;
- do not activate Wave 08 workers;
- do not implement Canvas, Property Inspector, Object Palette, image import/storage/renderer or other Wave 08 UI;
- only resume product-code changes if another concrete correctness blocker is demonstrated safely by repository review.

The 24 commits currently present on `main` after Wave 06 modify documentation only, so no unmerged functional main change is currently hidden from the integration branch. Reconciliation with current `main` remains required before final exact-head CI, but it is not a source-code conflict at this stop point.

## When Actions reset

1. Re-read `main` and verify exact integration head/state.
2. Reconcile `integration/visual-runtime-wave-07` with current `main`.
3. Resolve/finalize canonical typed visual property persistence deliberately, with migration/compatibility coverage, before Wave 08 activation.
4. Resolve any remaining reproducibility/CI blocker necessary to make the final run trustworthy; do not casually broaden functional scope.
5. Open one Wave 07 integration PR only when ready to spend CI.
6. Require exact-final-head Web build + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + Wave-specific visual/Python acceptance.
7. Fix concrete failures without weakening tests/security/contracts.
8. Merge Wave 07 only fully green.
9. Activate/freeze Wave 08 assignments only after the Wave 08 Definition of Ready is satisfied.
