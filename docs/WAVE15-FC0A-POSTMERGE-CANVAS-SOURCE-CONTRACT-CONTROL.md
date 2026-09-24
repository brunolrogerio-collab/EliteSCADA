# Wave 15 — FC0-A Post-Merge Canvas Source-Contract Closeout

> GitHub live is the sole authority.
> This is a bounded test-contract correction discovered by the exact FC0-A broad post-merge gate.
> It does **not** authorize a Visual Editor product redesign.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`MAIN_ORDER_REV: 0002`

`STATE: MERGED / BROAD_GREEN / CLOSED_FOR_MUTATION`

`ORDER_ID: FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-V1`

`EXECUTOR: SEQUENTIAL CODEX`

`EXACT_BASE_SHA: 1ab3550e1afb258e38caaa6de3f6547f481bbef7`

`EXACT_BASE_TREE: 701f4591a294885b414284691b1051cf274c9707`

`WORK_BRANCH: work/w15-fc0a-postmerge-canvas-source-contract`

`TARGET_BRANCH: wave15/corrections-integration`

`VALIDATION_PROFILE: UI_EDITOR, RUNTIME_RENDERER`

## 1. Trigger

PR #342 closed the deterministic Contextual Help E2E load defect and merged at the exact base above.

Exact broad post-merge gate:

`EliteSCADA CI 36049229264 / #1568`

Attempt 1:
- Web build — SUCCESS;
- Backend build — SUCCESS;
- Backend test — one known IEC-104 T2 timing transient;
- Chromium not reached.

Main diagnosed the IEC-104 failure before any rerun:
- failing test blob unchanged from frozen base;
- IEC-104 adapter and sequence-state blobs unchanged from frozen base;
- Wave 14 canonical handoff documents the same T2 timing test as an accepted transient after one same-SHA diagnosed rerun.

Exactly one same-SHA failed-job rerun was authorized.

Attempt 2:
- Web build — SUCCESS;
- Backend build/test — SUCCESS;
- Runtime smoke — SUCCESS;
- Chromium — 654 passed / 1 failed.

The only Chromium failure:

`tests-e2e/visual-editor-canvas-source-contract.spec.ts`
`Canvas geometry projection consumes the public Visual Property Registry instead of duplicating defaults`

Exact assertion failure:

`Expected substring: "getBuiltinVisualObjectSchema"`

Current source correctly contains:

`getVisualSchemaForEngineering`

## 2. Classification

`STALE_SOURCE_CONTRACT_TEST / FC0A_INTENTIONAL_FND06_COMPATIBILITY_CHANGE / PRODUCT_BEHAVIOR_NOT_DEFECTIVE`

Main verified:

- failing source-contract test blob is unchanged from frozen FND-06 base:
  `f43f9d77c5a0bcfbb4d865c10aa7e9146ef4ea5c`;
- frozen `canvasInteractionModel.ts` used `getBuiltinVisualObjectSchema`;
- accepted FC0-A candidate intentionally changed that consumer to `getVisualSchemaForEngineering`;
- `getVisualSchemaForEngineering` is the frozen FND-06 Engineering compatibility seam:
  - built-in types delegate to `getBuiltinVisualObjectSchema`;
  - only known persisted legacy types `tank | value | dynamo | status` receive bounded compatibility schemas;
  - arbitrary unknown types still fail closed;
- `COMMON_VISUAL_PROPERTY_REGISTRY` remains the generic fallback for common geometry/visibility defaults;
- functional Canvas contract tests already pass in the same broad suite before this stale source-string assertion fails.

The old source-contract expectation now asks the Canvas to bypass the very FND-06 compatibility seam required by the accepted FC0-A package.

Do not revert the product from `getVisualSchemaForEngineering` back to `getBuiltinVisualObjectSchema`.

## 3. Authorized correction

Test-only unless a focused test reveals an unexpected contradiction.

Required minimal correction:

1. update `web/scada-web/tests-e2e/visual-editor-canvas-source-contract.spec.ts`;
2. replace the stale literal requirement for `getBuiltinVisualObjectSchema` with the correct frozen seam `getVisualSchemaForEngineering`;
3. assert the Canvas source does **not** regress to directly depending on `getBuiltinVisualObjectSchema` for this compatibility-sensitive path;
4. retain assertions that:
   - `COMMON_VISUAL_PROPERTY_REGISTRY` is used;
   - `VISUAL_PROPERTY_KEYS` is used;
   - no Canvas-private geometry defaults are introduced;
   - no canonical Screen mutation authority is introduced;
5. if useful, rename the test title so it describes the Engineering compatibility/public registry contract rather than one historical implementation symbol.

Do not copy the compatibility table into the test.

## 4. Validation

Required focused evidence:

- the corrected `visual-editor-canvas-source-contract.spec.ts` passes;
- `visual-editor-canvas-contract.spec.ts` passes;
- FND-06 legacy compatibility owner tests relevant to Canvas/selection remain green;
- Web production build passes.

Then run natural Wave 15 T1 on exact candidate.

After Main accepts and merges, a new exact broad `EliteSCADA CI` must be globally green:
- Web;
- Backend build/test/smoke;
- Chromium full suite.

A green focused/T1 run does not by itself release FC0-A.

## 5. Forbidden

Do not:
- modify `canvasInteractionModel.ts` merely to satisfy the stale string assertion;
- restore `getBuiltinVisualObjectSchema` as the Canvas Engineering authority;
- create another compatibility registry;
- weaken arbitrary-unknown fail-closed behavior;
- skip or exclude the source-contract test from broad CI;
- alter IEC-104 code/test as part of this order;
- rerun broad CI instead of correcting the deterministic stale expectation;
- broaden DEV-EDITOR product scope;
- self-merge;
- declare `FC0A_RELEASE_APPROVED`.

## 6. Return

Return exactly:

`FC0-A POSTMERGE CANVAS CONTRACT CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Include:
- base -> exact candidate SHA/tree;
- changed files;
- before/after source-contract assertion;
- focused commands/results;
- Web build;
- exact natural T1 run;
- confirmation that product Visual/FND-06 code was not changed;
- no merge/freeze/release.

This is intended to be the final deterministic test-contract closeout before another exact broad post-merge release gate.


## 7. Main closure

PR #343 candidate:
- head `1b02b9ca0e7d84eed71d01cce2776fc1ced64c3b`
- T1 `36053138551`: SUCCESS.

Protected merge:
- `e3ed5138369c576549cb58a7aff9783792f322d3`
- tree `4e7627774fbfc111344e3d80fcb9d921eed8377e`.

Exact broad post-merge:
- `EliteSCADA CI 36060017969 / #1569`: SUCCESS;
- Chromium: **655 passed / 0 failed**;
- corrected source-contract and functional Canvas cases executed and passed.

Disposition:
`FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-V1 -> CLOSED`.

No further mutation is authorized by this control.
