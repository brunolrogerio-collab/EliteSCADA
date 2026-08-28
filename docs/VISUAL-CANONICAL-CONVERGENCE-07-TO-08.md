# EliteSCADA — Visual Canonical Convergence 07 -> 08

Status: **WAVE 07 CLOSED / WAVE 08 DEFINITION OF READY SATISFIED**  
Date: 2026-08-28

This document records the canonical convergence completed before the graphical editor wave. It is historical authority for the 07 -> 08 boundary; Wave 08 must not reopen these decisions casually.

## Wave 07 closure evidence

- Final integration product head: `6d869109af23b25d1ae95cd35610e1930a16791c`
- Final exact-head CI: #508 / run `33217787482` — **SUCCESS**
- Merge PR: #89
- Main merge commit: `8de706882ba20afedd666532ac41ae11115d06b3`
- Post-merge main CI: #510 / run `33218282760` — **SUCCESS**
- Web build: SUCCESS
- backend Release build + full PostgreSQL/Timescale tests: SUCCESS
- Runtime smoke: SUCCESS
- Chromium E2E: SUCCESS
- Wave 07 visual/Python acceptance: SUCCESS
- Wave 06 Pyodide sandbox/native-escape/timeout/cancellation regressions: SUCCESS

PR #88 was the original Draft integration PR. It was closed unmerged only because the available connector could not transition Draft -> Ready after validation. PR #89 replaced it on the **same exact product head**, with no product commit between #508 and the replacement PR.

## Canonical convergence settled

### Engineering Schema v12 typed visual persistence

Wave 07 replaced the transitional string property bag for canonical built-in visual properties with JSON-native typed visual properties in Engineering Schema v12.

Compatibility is explicit:
- historical v10/v11 string properties remain readable through schema-guided migration for registered built-ins;
- v12 built-in visual properties must use native JSON types;
- custom legacy types are not guessed/coerced into invented semantics;
- revision/package/import-export paths preserve the canonical representation.

The graphical editor must edit this canonical model. It must not create a second persisted canvas/property JSON authority.

### Stable visual identity

Nested visual elements have stable IDs. Legacy missing IDs are materialized/preserved under explicit rules; empty/duplicate IDs fail closed. Script `VisualObject` references resolve against the same stable identity model.

### Visual binding semantics

For a visual binding:
- `EngineeringBindingDto.Key` = destination visual property/slot;
- `EngineeringBindingDto.Target` = source TAG/property/expression reference;
- `Kind` identifies source interpretation.

No editor-private competing destination field is authorized.

### Runtime source semantics

C# and TypeScript preserve:

`Animation > Script > Binding/Expression > Engineering Base > Default`.

Registry defaults remain distinct from explicitly engineered values. Runtime writes never silently mutate Engineering.

### Frontend Engineering -> Runtime seam

The official adapter consumes canonical typed visual Engineering, requires stable identity and declared schemas, preserves hierarchy and does not establish private persisted editor state.

### Property registry and built-ins

C# and TypeScript align the common public visual-property family and renderer-independent built-in object types:
- `core.group`
- `core.rectangle`
- `core.ellipse`
- `core.line`
- `core.text`
- `core.image`
- `core.valueDisplay`
- `core.button`

Integer properties share signed Int32 semantics. Duplicate definition-level binding writers and duplicate Script event references fail closed.

### AssetReference

Canonical visual reference semantics remain:

`assetRef = null | { assetId }`

The reference contains stable project-owned identity only. Paths/URLs and duplicated asset metadata are rejected. First-class asset entity/import/storage/payload/preview are Wave 08 functionality, not a reason to change the reference contract.

### Client Visual Python property authority

Client Visual Python can read permitted registered properties and set/clear Script overrides on the exact current Runtime Visual Instance through capability boundaries. `null` is not a clear sentinel. DOM/React/renderer/storage/private JavaScript authority remains inaccessible.

### Fail-closed Engineering Preview

Demonstrated malformed visual/Script paths are rejected before Apply, including null/malformed view nodes, bindings, Script definitions/references and prospective removal of visual objects still referenced by saved Scripts.

## Reproducibility settled for active development

Final Wave 07 validation used:
- .NET SDK `10.0.400` pinned by repository configuration;
- Node `24.19.0` in CI;
- pinned direct frontend dependencies;
- committed `package-lock.json`;
- `npm ci` in CI.

## Lower-priority debt that does not block Wave 08

- broader legacy non-visual import null/empty-ID hardening;
- production CORS hardening;
- repository branch protection policy;
- further CI cost optimization / separation of cheap contract tests from full browser acceptance;
- later binding-engine semantics beyond the canonical source/destination contract already frozen.

These items do not authorize Wave 08 workers to broaden scope.

## Wave 08 Definition of Ready — FINAL

1. Wave 07 final integration green and merged — **SATISFIED**.
2. Canonical typed visual property persistence/migration settled — **SATISFIED, Schema v12**.
3. Stable visual definition/object identity settled — **SATISFIED**.
4. Visual-property binding target semantics settled — **SATISFIED**.
5. Frontend/backend property-registry parity validated — **SATISFIED by CI #508/#510**.
6. Asset-reference identity sufficient for Image/import — **SATISFIED**.
7. Editor can consume canonical visual definitions without private persistence — **SATISFIED by canonical typed Engineering + official adapter**.
8. Worker scopes and reserved central files frozen — **SATISFIED by `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`**.

Wave 08 is therefore eligible for explicit coordinator activation. Functional Wave 09/10 scope remains forbidden until its own gate.
