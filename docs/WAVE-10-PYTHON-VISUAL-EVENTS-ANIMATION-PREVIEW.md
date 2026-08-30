# WAVE 10 — Python Visual Events, Animation and Preview/Test

Date: 2026-08-30
Status: **ACTIVE**

## Product base

Wave 10 product `WaveBaseSHA`:

`bbfd730e404b0dee2c05e0ec0afb979b1b14ea35`

This SHA had normal CI green before Wave 10 activation. Later docs-only coordination commits with `[skip ci]` do not change the Wave 10 product base.

Integration branch:

`integration/wave-10-python-visual-events-animation-preview`

Worker branches:

- DEV 1: `dev1/wave-10-event-editor` — issue #149
- DEV 2: `dev2/wave-10-runtime-animation-tween` — issue #150
- DEV 3: `dev3/wave-10-python-preview-test` — issue #151

Coordinator ownership: issue #152.

## Goal

Complete the roadmap slice for Python-authored visual behavior:

- visual event association;
- lifecycle, value-change and timer associations;
- deterministic runtime animation/tween behavior;
- mounted Python Preview/Test with safe, actionable traceback diagnostics.

Wave exit must prove this complete path:

`click -> canonical event binding -> Python entry point -> script visual command -> animated public visual property -> deterministic stable final result`

## Canonical contracts

### Python boundary

Reuse `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

- Python execution uses the accepted sandbox/capability/proxy path.
- `ScriptContext` is the only implicit scripting context.
- `asset`, `parameters` and `user` remain plain bounded value graphs.
- `scada.*` capabilities remain explicit proxy services.
- No arbitrary `eval`, `exec`, imports, filesystem/network access, reflection escape or alternate Python evaluator/host.
- Execution remains bounded, cancellable and deny-by-default.

### Engineering authority

Event/script configuration persists through canonical Engineering only:

- Preview/Apply;
- revisions/CAS behavior;
- project package;
- import/export when the entity is public Engineering data;
- runtime snapshot derived from accepted Engineering state.

No worker may create a private frontend/runtime persistence path as competing truth.

### Stable identities

- Script/event references use canonical stable identities.
- TAG value-change references use canonical stable TAG identity and selectors.
- Client Memory change references use the real stable memory-definition ID; friendly path is authoring/display only.
- Display names are never substituted for stable identity.

### Visual property precedence

The locked precedence remains:

`Animation > Script > Binding/Expression > Engineering Base > Default`

Animation is transient runtime behavior over canonical public visual properties. It is not renderer-private persisted truth.

## DEV 1 — Events editor

Issue #149 is authoritative for worker scope.

Deliver:

- mounted Events editor;
- object/visual event associations, beginning with click;
- lifecycle associations;
- TAG value-change associations;
- Client Memory change associations;
- timer associations;
- validation and Engineering round-trip fidelity.

If the current canonical schema is insufficient, DEV 1 reports the smallest schema gap to the Coordinator instead of inventing a second event model.

## DEV 2 — Runtime animation/tween

Issue #150 is authoritative for worker scope.

Deliver:

- explicit bounded duration/transition semantics;
- deterministic start/intermediate/completion behavior;
- deterministic cancellation/replacement behavior;
- public visual property execution;
- stable final value and diagnostics;
- preservation of the locked property precedence.

No private DOM/renderer state may become persistence authority.

## DEV 3 — Python Preview/Test

Issue #151 is authoritative for worker scope.

Deliver:

- mounted Preview/Test in the canonical Python authoring flow;
- execution through the accepted sandbox/runtime path;
- deterministic structured result/output;
- loading/success/validation/runtime-error/timeout/cancel states;
- actionable traceback and failing source line;
- secret/protected-context redaction.

Preview/Test is derived execution and does not persist alternate runtime truth.

## Coordinator ownership

Issue #152 is authoritative.

Coordinator owns:

- shared event-binding schema changes when genuinely required;
- central DI/runtime bridge composition;
- shared event dispatcher wiring;
- cross-component animation arbitration changes;
- integration of DEV 1/2/3;
- reconciliation of shared files;
- final Wave 10 end-to-end acceptance.

Workers must not independently create competing central dispatchers, Python hosts, event contracts or visual state models.

## Driver isolation

Wave 10 has priority over parallel Driver work.

Driver branches remain isolated/parked unless a shared canonical contract explicitly requires Coordinator reconciliation. Driver CI or interoperability gaps do not block Wave 10 by themselves.

At activation, OPC UA interoperability work remained isolated from the Wave 10 product base.

## CI and closure

CI policy remains NORMAL.

- Do not run reassurance CI on unchanged product trees.
- Worker PRs report their exact tested SHA and evidence.
- Coordinator integrates only reviewed worker heads into the Wave 10 integration branch.
- The exact final integration head must pass normal CI before any transition to `main`.
- The exact resulting `main` product head must pass post-main CI before Wave 10 is closed.

Minimum final acceptance includes:

1. persisted visual click event resolves a canonical Python entry point;
2. script executes through the accepted sandbox/runtime path;
3. script requests a visual-property change with animation;
4. animation visibly and deterministically reaches the stable final value;
5. Preview/Test can reproduce a successful script result;
6. a failing script produces actionable traceback/failing-line diagnostics without protected-data leakage;
7. Engineering round-trip preserves canonical identities and configuration.