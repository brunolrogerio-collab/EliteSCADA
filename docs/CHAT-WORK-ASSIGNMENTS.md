# CHAT WORK ASSIGNMENTS — EliteSCADA

Date: 2026-08-29
Stage: **Wave 08 FOLLOW-B — ACTIVE**
Integration owner: **Coordinator**
Integration branch: `integration/visual-expressions-wave-08-follow-b`

All workers must start from the approved FOLLOW-B baseline and must not edit `main` or the integration branch directly.

## DEV 1 — Typed Expression Core

Branch: `dev1/follow-b-expression-core`
Status: **ACTIVE**

Ownership:

- typed parser/evaluator and constrained expression representation;
- deterministic type rules, operators, comparisons and whitelisted pure helpers;
- explicit `bool(number)` / `number(boolean)` conversion semantics;
- dependency representation sufficient to consume canonical TAG identity and integer TAG selectors;
- unavailable/bad-quality evaluation behavior and diagnostics contracts;
- bounded evaluation limits and rejection of unsupported/arbitrary execution;
- focused unit tests for parsing, typing, precedence, arithmetic, comparisons, bit dependencies and failure cases.

Must not:

- create a second TAG-bit identity/parser independent of FOLLOW-A;
- implement persistence as opaque metadata;
- use JavaScript `eval`/`Function`, Python or arbitrary dynamic invocation;
- take ownership of Property Inspector/rendering UX except where a minimal public contract is required.

## DEV 2 — Engineering and Persistence

Branch: `dev2/follow-b-engineering`
Status: **ACTIVE**

Ownership:

- public/versioned Engineering representation for expressions, Boolean Conditions and Analog Fill;
- public `visible: boolean` property registration/default semantics for renderable objects;
- validation and deterministic dependency persistence using stable identities;
- JSON/CSV where applicable, Preview/Apply, revision and project-package round-trip fidelity;
- compatibility/migration behavior for existing Engineering payloads;
- tests proving canonical serialization, validation, import/export and persistence behavior.

Must not:

- invent renderer-only state or undocumented `metadata` conventions;
- change FOLLOW-A bit semantics;
- implement a separate evaluator or web-only persistence format.

## DEV 3 — Web Editor and Visual Runtime

Branch: `dev3/follow-b-web-visuals`
Status: **ACTIVE**

Ownership:

- Property Inspector authoring modes for constants, direct bindings, integer TAG bit selectors, Boolean Conditions and typed expressions;
- source insertion/autocomplete and validation UX using canonical source identity;
- application of Binding/Expression results to public visual properties, including `visible`;
- Analog Fill rendering for eligible objects, scaling, clamping and required directions;
- user-facing diagnostics/effective-source visibility where practical;
- browser E2E coverage for representative FOLLOW-B acceptance scenarios.

Must not:

- invent a local `.NN` identity model or duplicate evaluator semantics;
- save canonical configuration only in React state/CSS/renderer internals;
- silently coerce bad/unavailable values to `false` or `0`.

## Shared integration constraints

- Canonical contract: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.
- TAG-bit dependency contract: `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.
- Property precedence remains `Animation > Script > Binding/Expression > Engineering Base > Default`.
- Coordinate shared DTO/interface edits before touching the same file from multiple worker branches.
- Keep commits narrow and attributable to the assigned slice.
- Do not run reassurance CI after every small commit. CI policy is **NORMAL**.

## Required worker handoff

Each worker handoff must report:

1. exact branch and head SHA;
2. concise delivered scope;
3. exact changed-file list;
4. tests executed and results;
5. known limitations/risks;
6. confirmation that no unassigned files were changed;
7. any shared contract decision that the coordinator must reconcile.

Wave 09 remains **STOPPED**. No Wave 09 implementation branch is authorized until FOLLOW-B is merged and post-merge green.
