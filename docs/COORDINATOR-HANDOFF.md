# COORDINATOR HANDOFF — EliteSCADA

Date: 2026-08-29
Status: **FOLLOW-B ACTIVE / FOLLOW-A CLOSED / WAVE 09 BLOCKED**

## Operational truth

- Current validated product baseline on `main`: `bb0186cddc54946e8cc829c04a04b99495462304`.
- PR #105 (FOLLOW-A) is merged.
- CI #541 passed on the exact pre-merge product head `4e8a3c76753c1ead815c790407601852c6f888e3`.
- CI #543 passed on exact post-merge `main` head `bb0186cddc54946e8cc829c04a04b99495462304`.
- CI policy remains **NORMAL**.
- GitHub state is the operational source of truth for branches, PRs and CI.

## Closed gate: Wave 08 FOLLOW-A

FOLLOW-A is complete. Do not rebuild or re-integrate its prior worker slices.

The canonical integer TAG bit contract is now available to downstream work:

- stable identity is `TagId + selector`;
- `.NN` is friendly authoring/display syntax, not persisted identity by itself;
- quality and type semantics remain those defined by `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.

## Active gate: Wave 08 FOLLOW-B

Canonical contract: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.

FOLLOW-B must deliver the visual Binding/Expression layer without creating competing semantics. Required direction includes:

- typed, side-effect-free visual expressions;
- universal public `visible` property for renderable visual objects;
- Boolean Condition authoring and runtime behavior;
- canonical integer TAG bit dependencies consuming FOLLOW-A identity;
- numeric expressions and deterministic pure helper functions;
- Analog Fill on explicitly eligible closed visual objects;
- deterministic quality/unavailable behavior and diagnostics;
- canonical Engineering persistence, validation, import/export, revisions and project-package fidelity;
- Property Inspector/editor support and runtime rendering using the same public contract.

Forbidden shortcuts include renderer-private saved state, undocumented `metadata`, JavaScript `eval`/`Function`, Python evaluation, implicit truthiness/coercion, or a second TAG-bit parser/identity model.

The existing property precedence remains authoritative:

`Animation > Script > Binding/Expression > Engineering Base > Default`

## Execution order

1. Coordinator owns and stabilizes worker boundaries and the integration branch.
2. Workers branch only from the current approved baseline and edit only their assigned ownership.
3. Integration branch: `integration/visual-expressions-wave-08-follow-b`.
4. Worker handoff must include exact SHA, changed files, tests executed/results, risks/known limitations and confirmation that no out-of-scope files were changed.
5. Coordinator reviews/integrates worker heads, performs contract acceptance, reconciles current `main` if necessary, then requires exact final integration CI before merge.
6. After merge, exact post-merge `main` CI must be green before the next product stage is activated.

## Gate after FOLLOW-B

Wave 09 remains **BLOCKED** until FOLLOW-B is implemented, accepted, merged and post-merge green.

The Wave 09 historical/alarm historian and reporting/report-designer documents remain planning contracts only until that gate opens.
