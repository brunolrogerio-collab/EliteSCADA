# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Merged product state:** **WAVE 07 CLOSED / WAVE 08 NOT MERGED**  
**Active development state:** **WAVE 08 ACTIVE — CENTRAL FOUNDATION VALIDATED IN DRAFT PR #90 / DEV 1-2-3 ACTIVE**  
**Next mandatory visual follow-up:** **TYPED VISUAL EXPRESSIONS + BOOLEAN CONDITIONS + ANALOG FILL — SPECIFIED / NOT IMPLEMENTED**  
**CI mode:** **NORMAL — Actions authorized with conservative usage**

## Mandatory resume reading

Before any action read current `main`:
- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `docs/DEVELOPMENT-WAVES.md`
- `docs/CHAT-WORK-ASSIGNMENTS.md`
- `docs/CI-USAGE-POLICY.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/COORDINATOR-HANDOFF.md`
- `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`
- current assignment `MustReadSpecific`.

Then verify live GitHub branch/PR/head/CI. GitHub is operational truth.

## Wave 07 — MERGED / CLOSED

- final integration product head: `6d869109af23b25d1ae95cd35610e1930a16791c`
- exact-head CI #508 / run `33217787482`: **SUCCESS**
- merge PR #89
- main merge: `8de706882ba20afedd666532ac41ae11115d06b3`
- post-merge CI #510 / run `33218282760`: **SUCCESS**

## Wave 08 — IMPLEMENTED IN PR / ACTIVE / NOT MERGED

Frozen identity:

- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- ContractSHA / logical worker base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration: `integration/graphical-editor-wave-08`
- Draft integration PR: **#90 — `Wave 08: graphical editor foundation integration`**
- exact validated central-foundation product head: **`b48b489660ae953029fd2416aa18b149eaa18258`**
- validation: **CI #515 / run `33225051402` — SUCCESS**

CI #515 proved on that exact product head:

- Web React/Vite/TypeScript build: **SUCCESS**;
- backend Release build: **SUCCESS**;
- full backend tests including PostgreSQL/Timescale coverage: **SUCCESS**;
- Runtime smoke: **SUCCESS**;
- Chromium E2E: **SUCCESS**;
- `visual-editor-workspace.spec.ts`: **SUCCESS**;
- existing Wave 06/07 Python/visual regressions remained green.

Documentation-only successors use `[skip ci]`; they do not invalidate unchanged product evidence from `b48b489...`.

PR #90 remains Draft and must not merge until the complete Wave 08 interaction gate is implemented and the final integrated head is green.

## Validated coordinator foundation in PR #90

Implemented in the Wave 08 integration branch, not yet merged product state:

- Engineering Schema v13 first-class Visual Asset metadata;
- stable Visual Asset `Id` reference identity separated from developer `Key` and content SHA-256;
- bounded PNG/JPEG/BMP validation;
- canonical Working asset authority;
- PostgreSQL asset blobs + immutable revision links;
- asset-aware revision save/load/checkout/rollback;
- `.escadapkg` v2 asset sidecars with v1 asset-free compatibility;
- protected Visual Asset API with CAS/authorization/Audit;
- frontend asset catalog/import/content seams;
- `Engineering -> Telas` central `VisualEditorWorkspace`;
- Screen draft Preview/Apply through canonical Engineering + Workspace CAS;
- canonical snapshot reload after Apply;
- renderer consuming shared `core.*` Visual Property Registry and stable `assetRef`;
- explicit non-authoritative compatibility placeholders for historical demo visual types;
- `visualEditorCanonicalModel.ts` Screen mutation helpers;
- shared `visualEditorContracts.ts` separating transient UI state from canonical object/property/binding mutation intents;
- PT-BR/EN/ES editor composition;
- Screen Preview -> Apply -> canonical export -> reopen E2E.

## CI defects corrected before green checkpoint

The first meaningful PR validation found and corrected three deterministic issues:

1. TypeScript readonly/mutable mismatch in the Screen element clone path;
2. forbidden `Clone` helper names on C# records, renamed to `Copy`;
3. stale broad Runtime E2E expecting `.escadapkg` v1 after current Wave 08 export advanced to v2; current expectation updated while dedicated v1 compatibility coverage remains.

No unchanged-head reassurance reruns were used.

## Worker state — ACTIVE / AUTHORIZED

The previous owner stop has been deliberately lifted by the coordinator after CI #515 proved the central foundation and after the shared intent contract was seeded into each worker branch.

The logical worker Base remains `7a445d3dd94cabd09807291a0ee94276559fcb0e`. Before worker implementation, each branch is exactly one coordinator dependency-seed commit ahead of that base:

- DEV 1 `feature/graphical-editor-wave-08-canvas` authorization head `57521312914e21e303976a81bc81c84ad5aa9cbb` — **ACTIVE / Canvas + Selection**;
- DEV 2 `feature/graphical-editor-wave-08-property-inspector` authorization head `3e942ed641d96afe848966f123fb10eaeaa99ed7` — **ACTIVE / Property Inspector**;
- DEV 3 `feature/graphical-editor-wave-08-palette-bindings` authorization head `df3cd6c332a19bb3011373c95d010f33754c0c12` — **ACTIVE / Object Palette + Binding Foundation**.

Each worker starts on `siga` after rereading current `main`, then uses the seeded `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts` from its own branch. The contract seed is not worker implementation and is not merged product state.

Workers must preserve their fixed AllowedScope/ForbiddenScope, use focused validation, report exact delivery head and stop at `WAIT_FOR_COORDINATOR` after delivery.

## Newly locked visual behavior — SPECIFIED / NOT IMPLEMENTED

The owner expanded the mandatory visual-engineering follow-up while the three current worker slices were already active. Their missions remain **unchanged mid-delivery**.

Canonical contract:

`docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`

This follow-up must be implemented after the current Wave 08 worker deliveries are reviewed/integrated and before Wave 09 is activated.

Locked behavior now includes:

- visual properties declaring Binding/Expression support accept typed, side-effect-free expressions over canonical TAGs and Client Memory;
- boolean expressions support `and`, `or`, `not`, comparisons and parentheses, e.g. `falha_inversor1 or falha_bomba1`;
- numeric expressions support `+`, `-`, `*`, `/`, `%`, unary sign and parentheses, e.g. `(nivel1 + nivel2) * 3`;
- boolean/numeric destination type compatibility is enforced and conversions are explicit, e.g. `bool(falha_inversor1 + falha_bomba1)` or `(falha_inversor1 + falha_bomba1) > 0` when source TAGs are numeric;
- a small whitelist of deterministic pure helpers such as `abs`, `min`, `max`, `clamp`, `round`, `floor`, `ceil`, `bool` and `number` is allowed;
- expression dependencies are canonical and validated rather than relying only on ambiguous display labels;
- evaluation is reactive to source changes, bounded and cannot execute arbitrary JavaScript/Python/user code;
- every renderable Screen/Popup/Dynamo object exposes public `visible: boolean`;
- every public boolean visual property can be driven by a direct boolean source, numeric interval evaluation or compatible typed boolean expression;
- interval conditions support lower/upper bounds, inclusive/exclusive edges, one-sided intervals and inside/outside semantics;
- bad/unavailable/wrong-type dependencies do not silently coerce to false/zero; the Binding/Expression layer becomes unavailable and normal precedence falls back with diagnostics;
- all of this remains inside the existing `Animation > Script > Binding/Expression > Engineering Base > Default` resolution model;
- closed fill-capable shapes such as rectangle and ellipse/circle support Analog Fill;
- Analog Fill accepts a compatible numeric binding/expression, then scales configured engineering min/max to a clamped 0..100% filled region;
- first fill directions are bottom->top, top->bottom, left->right and right->left;
- filled-region color is explicit while unfilled geometry keeps normal base/background appearance;
- expression/condition/fill configuration is first-class canonical Engineering and must round-trip through JSON, Preview/Apply, revisions and `.escadapkg`;
- runtime expression results, condition results and calculated fill percentages are presentation state and are not persisted as Engineering base values;
- visual expressions/conditions must never be treated as safety/interlock/permissive process authority.

The first follow-up is intentionally an expression language, not a general-purpose programming language: no assignments, loops, arbitrary functions, JavaScript/Python evaluation or direct driver/database/network/DOM access.

## Actions rule

Actions is authorized, but use remains conservative:

- use static/diff/contract review first when sufficient;
- workers batch coherent changes and use focused validation in owned scope;
- do not rerun unchanged heads for reassurance;
- diagnose and fix localized failures before another expensive run;
- coordinator reuses CI #515 while validated central product code is unchanged;
- full integration matrices are reserved for meaningful composed/final checkpoints;
- final Wave Definition of Done is never weakened to save minutes.

## Still pending for current Wave 08 delivery

- DEV 1 Canvas/selection, zoom/pan/grid/snap and move/resize/rotate/duplicate/delete/z-order intents;
- DEV 2 schema-driven typed Property Inspector;
- DEV 3 registered Object Palette + canonical Binding authoring;
- coordinator canonical application of worker mutation intents into Screen drafts;
- cross-slice UI composition;
- practical Image workflow through stable `assetRef`;
- complete product gate:
  `Create Screen -> add objects -> move/resize/rotate -> edit properties -> canonical binding -> image asset -> save -> reopen -> export/import`;
- proof that transient Canvas selection/viewport/adornment state is not persisted;
- final exact-head full Wave 08 CI after worker integration;
- PR #90 readiness/merge;
- post-merge `main` health confirmation.

Wave 09/10 remain NOT ACTIVE.

## Next coordinator execution

1. verify actual `main`, PR #90, integration and all worker heads;
2. monitor worker Draft PRs/diffs at Early Contract Review and Integration Review checkpoints;
3. do not duplicate worker UI scopes in the integration branch;
4. implement coordinator-owned canonical intent application/composition hooks as needed;
5. integrate only reviewed worker deliveries;
6. use focused validation during composition and preserve CI #515 evidence only for unchanged validated code;
7. run the next full matrix on a meaningful integrated product checkpoint;
8. finish/merge the current Wave 08 interaction work only when exact-head CI is green;
9. before activating Wave 09, execute the mandatory Typed Visual Expressions + Boolean Conditions + Analog Fill follow-up under `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`, with its own assignments and validation;
10. only after that follow-up is green and documentation is synchronized may Wave 09 activate.
