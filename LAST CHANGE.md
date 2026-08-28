# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE-07 CANONICAL CONVERGENCE REVIEW COMPLETE / CI_DEFERRED / NOT MERGE-READY**  
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

## Wave 07 — canonical convergence review complete, validation deferred

Wave 07 remains **NOT MERGED**, **CI_DEFERRED** and **NOT MERGE-READY**.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- ContractSHA: `06faf079bc5185689712bd2c9a225c2bb8d90999`
- integration branch: `integration/visual-runtime-wave-07`
- exact current integration head after coordinator convergence review: `0c00413e2dc96d770a905cf0a416833764af59e7`
- integration PR: intentionally not open
- latest Actions run remains #488; no Wave 07 convergence Actions were triggered
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

## Coordinator-only canonical convergence result

The no-Actions review defined by `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md` is now **COMPLETE FOR THE CURRENT NO-CI INTERVAL**. The stop condition was reached: no further speculative product-code changes are authorized until CI can validate the accumulated head or a new concrete blocker is discovered.

The convergence work advanced the integration branch from `9c01f7e1815d76cea18728d7a753ddfd078613b7` to `0c00413e2dc96d770a905cf0a416833764af59e7` and established:

1. **Transitional Engineering Schema v11 on the integration branch.** `main` remains Schema v10 until Wave 07 merges. v11 adds stable nested visual object identity while continuing to accept older v1-v10 packages.
2. **Stable visual object IDs.** `VisualElementEngineeringDto.Id` is optional on input, appended compatibly to the DTO, generated for legacy materialization, preserved by prior key-path matching when legacy updates omit IDs, exported once materialized, and validated for empty/duplicate IDs across a visual tree.
3. **Binding target semantics settled without duplicating fields.** For a visual element, `EngineeringBindingDto.Key` is the destination visual property/slot and `Target` is the TAG/property/expression source reference.
4. **Frontend Engineering projection is typed.** Screen/Popup `elements` no longer remain an `unknown[]` boundary; the frontend has an explicit `VisualElementEngineering` projection.
5. **Default versus Engineering Base is aligned in C# and TypeScript.** Registry default is a distinct lower-priority source and is not falsely reported as user-authored Engineering Base.
6. **Canonical visual property catalog parity was established.** C# and TypeScript now share the intended common keys/defaults/constraints for geometry, appearance, stroke, text and image/resource properties.
7. **Built-in renderer-independent object schemas were established** for `core.group`, `core.rectangle`, `core.ellipse`, `core.line`, `core.text`, `core.image`, `core.valueDisplay` and `core.button`. No Canvas, palette UI or renderer was implemented.
8. **`assetRef` identity semantics were aligned.** The visual reference is null or `{ assetId }`, carries no copied asset name/MIME authority, and cannot be a filesystem path/arbitrary URL. First-class asset import/storage/payload remains Wave 08 work.
9. **A schema-guided transitional property codec exists in both C# and TypeScript.** It is the single bridge from the current string-valued visual Engineering property bag to typed runtime values; it does not guess types from content. Final review also aligned canonical numeric text acceptance between C# and TypeScript.
10. **An official frontend Engineering -> Runtime adapter exists.** It requires stable materialized IDs, uses the declared visual schema, interprets `binding.key` as the destination property and projects visual trees parent-before-child without editor-private persistence.
11. **Backend Preview enforces built-in `core.*` schemas.** Unknown built-in types, unknown properties and bindings targeting unsupported built-in properties fail closed while non-core future custom/Dynamo types are not prematurely prohibited.

All code/tests above remain **CI_DEFERRED**. Static review found no further concrete blocker at head `0c00413e...`, but this is not a substitute for compilation or execution.

## Deliberately unresolved before Wave 08 activation

### 1. Canonical typed visual property persistence

`VisualElementEngineeringDto.Properties` is still `Dictionary<string,string>`. Schema v11 is therefore a **transitional identity/convergence schema**, not the final typed visual persistence model.

The legacy codec is containment for this mismatch, not permission for the graphical editor to persist a private/stringly model. Before Wave 08 becomes ACTIVE, the canonical JSON-native typed property representation and migration strategy must be deliberately settled and validated.

### 2. Wave 07 final validation and merge

The exact final integration head still requires full integrated CI. Wave 08 cannot start merely because the static convergence review is complete.

The stable visual `assetRef` identity is sufficiently defined for the future Image/import slice. The first-class project asset entity, binary import/storage/serving and renderer remain intentional Wave 08 functionality, not work to smuggle into Wave 07 during the no-CI interval.

## Current coordinator state

**WAIT_FOR_OWNER_CI_RESET / NO FURTHER SPECULATIVE CODE.**

Until the owner explicitly reports Actions reset:
- do not open the Wave 07 PR;
- do not dispatch/rerun Actions;
- do not merge Wave 07 into `main`;
- do not activate Wave 08 workers;
- do not implement Canvas, Property Inspector, Object Palette, image import/storage/renderer or other Wave 08 UI;
- only resume product-code changes if repository review exposes a new concrete correctness blocker that can safely be addressed without CI.

## When Actions reset

1. Re-read `main` and verify exact integration head/state.
2. Reconcile `integration/visual-runtime-wave-07` with current `main` if required.
3. Resolve/finalize canonical typed visual property persistence deliberately, with migration/compatibility coverage, before Wave 08 activation.
4. Open one Wave 07 integration PR only when ready to spend CI.
5. Require exact-final-head Web build + backend Release/full PostgreSQL tests + Runtime smoke + Chromium + Wave-specific visual/Python acceptance.
6. Fix concrete failures without weakening tests/security/contracts.
7. Merge Wave 07 only fully green.
8. Activate/freeze Wave 08 assignments only after the Wave 08 Definition of Ready is satisfied.
