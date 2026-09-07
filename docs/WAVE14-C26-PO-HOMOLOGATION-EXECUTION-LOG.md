# Wave 14 C26 — PO Homologation Execution Log

Coordinator issue: #286

Branch:

`wave14/c26-po-homologation-corrections`

Base authority at C26 start:

`a724ece64a292aa1d1dedd886a72fb28ff8d90fe`

## Status

**EXECUTION ACTIVE — C26.1–C26.5 IMPLEMENTED / VALIDATED — C26.6 ACTIVE — NEW PREVIEW ONLY AFTER ACCEPTED C26 PRODUCT**

Pre-C26 Preview #285 / `d92e81f821c1a9c376b39bc3684eead54b3f570e` remains preserved as Product Owner homologation evidence. It must not be reused as the post-C26 Preview.

Detailed coordinator handoff:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`

## Work order and current progress

1. C26.1 Runtime transient-failure resilience — **IMPLEMENTED / VALIDATED**
2. C26.2 Runtime logical viewport/layout — **IMPLEMENTED / VALIDATED**
3. C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED / VALIDATED**
4. C26.4 Runtime popup layout — **IMPLEMENTED / VALIDATED**
5. C26.5 Alarm/Event/Historian Runtime surface — **DIAGNOSED / REGRESSION LOCKED / VALIDATED**
6. C26.6 Engineering shell workspace usability — **ACTIVE**
7. C26.7 Engineering theme/contrast — **PENDING**
8. C26.8 Screen editor functionality audit/fixes — **PENDING**
9. C26.9 Popup editor functionality audit/fixes — **PENDING**
10. C26.10 Canonical EEE residual cleanup — **PENDING**
11. exact-SHA full validation — **PENDING**
12. integrate only into canonical C11 when accepted — **PENDING**
13. regenerate canonical package/checksum/provenance — **PENDING**
14. create a new post-C26 Preview branch/PR — **PENDING**
15. new real Codespace/Product Owner homologation — **PENDING**

## C26.1 — implemented

Commit:

`2cfdee7b943e35d8ad04ba77e20b89da93a48113`

Preserves the last valid Active Runtime projection and selected Screen across a transient transport/no-response failure, while still failing closed for first-load failure and authoritative HTTP conflict.

Regression:

`web/scada-web/tests-e2e/wave-14-c26-runtime-resilience.spec.ts`

## C26.2 — implemented

Commits:

- `a918ed4dcca2a6f5fa2657d0c7108508ead91543`
- `3e312bd29a8d9109a9ec354e7f579d9bbc4aef3d`

Fits the full logical HMI into the Runtime viewport, removes Engineering editor scroll chrome from Runtime Screens, and deliberately scopes the CSS reset to `.runtime-visual-screen`.

Regression:

`web/scada-web/tests-e2e/wave-14-c26-runtime-viewport.spec.ts`

## C26.3 — implemented

Final product/test head before C26.4:

`b7f0566f9e30f3e4c6dee3d020710b23923b6db2`

The final correction suppresses operator-visible technical fallback text without mutating visual keys/identity. Engineering/DOM/debug semantics remain available.

## Historical validation at `b7f0566...`

SUCCESS:

- EliteSCADA CI #1433 / `34154058083`
- Preview Licensing CI #381 / `34154058077`
- Interop Lab Smoke #258 / `34154058109`
- L3 Seven-Driver Lab #337 / `34154058154`

FAILURE, later resolved by C26.4:

- Wave 11 Active HMI Runtime #359 / `34154058296`

The Wave11 red was diagnosed before correction; it was not blind-rerun.

## C26.4 — implemented / validated

Commits:

- `2a6f742d87e84e5dbe7fd04e48b2b4c708f6652d` — generic authored Popup bounds/composition correction;
- `fb0a6c21740d5be0ea322bad9fe77dcff2de5725` — canonical Runtime Popup regression and redundant Runtime wrapper-chrome removal.

Root cause was broader than z-index. C26.2 correctly scoped the Screen reset to `.runtime-visual-screen`, while Popup rendering still inherited Engineering renderer `min-height`, margin and `overflow:auto`, leaving no deterministic Runtime Popup box.

The generic correction:

- derives Popup logical bounds from authored content geometry;
- extends `resolvePopupLogicalPosition` while preserving legacy behavior when bounds are absent;
- composes Popup in the same logical coordinate system as the Screen;
- adds Runtime Popup-specific renderer reset and explicit overlay stacking;
- prevents Popup open/close from resizing or corrupting the underlying Screen;
- preserves IDs, bindings, Commands and backend Active Revision authority;
- does not hardcode EEE-specific dimensions.

Regression:

`web/scada-web/tests-wave11/c26-popup-composition.spec.ts`

It proves the canonical EEE P01 Popup is visible, correctly bounded, has accessible authored controls, has no unintended editor scroll/chrome and leaves the Screen layout stable while open and after close.

## C26.5 — diagnosed / regression locked / validated

Observed homologation failure:

`Historical query failed with HTTP 404`

Live diagnosis established that the backend route `/api/historical/query` is intentionally mapped only when Historical Query is enabled. The Wave11 harness enables the feature and supplies its cursor key. Preserved pre-C26 Preview #285 starts the TimescaleDB Historian but does not set the Historical Query feature gate/cursor-key configuration, therefore the backend correctly leaves the route unmapped and returns 404.

This is a **pre-C26 Preview harness configuration gap**, not a missing backend API route and not a valid no-data result. PR #285 remains untouched as historical evidence.

Product semantics were also revalidated:

- a failed HTTP query remains an error state and must not be disguised as no-data;
- a successful valid query with zero rows renders the normal no-data state;
- raw transport text is not rendered as operator-visible chart content;
- Alarm / Operational Event / Audit authority remains separate.

Regression commits:

- `cd2ace19004601430b49f922ca0a1be785a400f2` — locks error-vs-empty Historian semantics in the Active Runtime Trend flow;
- `2a69605b746f8035a99cb7202e44329993272409` — aligns text assertions with the actual Wave11 runtime locale without weakening state assertions.

The future **new post-C26 Preview** must enable Historical Query with the required development-preview configuration. Do not retrofit or repurpose #285.

## Exact-SHA validation at `2a69605...`

All normal gates completed SUCCESS on exact product/test head `2a69605b746f8035a99cb7202e44329993272409`:

- EliteSCADA CI #1438 / run `34166715032`
- Preview Licensing CI #386 / run `34166715046`
- Interop Lab Smoke #263 / run `34166715034`
- Wave 11 Active HMI Runtime #364 / run `34166715044`
- L3 Seven-Driver Lab #342 / run `34166715029`

An earlier EliteSCADA CI #1437 backend advisory-lock test failure was diagnosed before rerun and was unrelated to the C26.5 Playwright-only change. The same backend test passed naturally on #1438.

## C26.6 active

Product Owner acceptance remains:

- Engineering navigation panels can collapse/expand;
- long panels scroll independently where appropriate;
- canvas remains the dominant work area;
- Properties remains reachable;
- no loss of existing authority/capability projection.

Preliminary live code diagnosis already shows cumulative horizontal pressure from the global Engineering sidebar plus visual-editor navigation, palette and Properties panels. Implementation must solve the generic workspace model, not merely hide Properties or remove capabilities.

C26.7–C26.9 remain Engineering contrast/theme, functional Screen editor audit and Popup editor authoring.

C26.10 is residual EEE cleanup only after generic fixes.

After full accepted C26 exact-SHA validation: integrate only into C11, regenerate package/checksum/provenance, create a **new** Preview and perform a fresh Product Owner Codespace homologation.

## Binding follow-on backlog not absorbed into C26

Still mandatory in the overall Wave14 sequence:

- separate protected whole-system backup/restore;
- safe Historian Administration backup/export/import/restore;
- generic TAG raw-to-engineering scaling with explicit inverse write semantics;
- normal human decimal-place authoring and Save/Publish/Activate/package persistence;
- later real Modbus/PLC EEE variant only after current generic gates and fresh homologation.

Do not silently drop these requirements or mix them into unrelated C26 commits.

## Guardrails

- #212 remains OPEN/DRAFT and has no authorization to merge to `main`;
- #266 and #288 MUST NEVER MERGE;
- #263 remains C11 -> integration only;
- #285 remains preserved as pre-C26 Preview evidence;
- never modify `main` directly;
- no force push/destructive rebase/branch deletion;
- diagnose CI red before rerun;
- no validation/test/security/Identity/authorization/Engineering Lock/licensing/lifecycle/package/Runtime authority weakening;
- Runtime/Active must not depend on `.escadalib`;
- Alarm / Operational Event / Audit remain separate;
- Wave13 #205/#207 remains paused.
