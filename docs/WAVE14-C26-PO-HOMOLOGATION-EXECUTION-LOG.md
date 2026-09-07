# Wave 14 C26 — PO Homologation Execution Log

Coordinator issue: #286

Branch:

`wave14/c26-po-homologation-corrections`

Base authority at C26 start:

`a724ece64a292aa1d1dedd886a72fb28ff8d90fe`

## Status

**EXECUTION ACTIVE — C26.1–C26.3 IMPLEMENTED — C26.4 DIAGNOSED / NOT IMPLEMENTED — C26.5+ PENDING — NEW PREVIEW ONLY AFTER ACCEPTED C26 PRODUCT**

Pre-C26 Preview #285 / `d92e81f821c1a9c376b39bc3684eead54b3f570e` remains preserved as Product Owner homologation evidence. It must not be reused as the post-C26 Preview.

Detailed coordinator handoff:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`

## Work order and current progress

1. C26.1 Runtime transient-failure resilience — **IMPLEMENTED**
2. C26.2 Runtime logical viewport/layout — **IMPLEMENTED**
3. C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED**
4. C26.4 Runtime popup layout — **DIAGNOSED / BLOCKING / NOT IMPLEMENTED**
5. C26.5 Alarm/Event/Historian Runtime surface — **PENDING**
6. C26.6 Engineering shell workspace usability — **PENDING**
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

Final product/test head:

`b7f0566f9e30f3e4c6dee3d020710b23923b6db2`

The final correction suppresses operator-visible technical fallback text without mutating visual keys/identity. Engineering/DOM/debug semantics remain available.

## Validation at `b7f0566...`

SUCCESS:

- EliteSCADA CI #1433 / `34154058083`
- Preview Licensing CI #381 / `34154058077`
- Interop Lab Smoke #258 / `34154058109`
- L3 Seven-Driver Lab #337 / `34154058154`

FAILURE:

- Wave 11 Active HMI Runtime #359 / `34154058296`

The Wave11 red is diagnosed. Do not blind-rerun.

## C26.4 — diagnosed / no code committed

Playwright evidence shows the canonical EEE Popup exists in the DOM but is visually clipped/misaligned and can be effectively hidden/overlapped by the Screen.

Root cause is broader than z-index:

C26.2's Screen-specific Runtime reset correctly uses:

`.runtime-visual-screen > .runtime-visual-definition`

The Popup therefore still inherits Engineering renderer chrome, including approximately:

- `min-height: 450px`
- `margin: 18px`
- `overflow: auto`

This produces internal Popup renderer scroll offsets and leaves no deterministic Runtime Popup bounding box.

Canonical EEE Popup authored geometry already provides an approximately 880×600 content envelope at `(0,0)` with logical placement around `(520,210)`. Do not invent C11-only persisted dimensions merely for the test.

Required generic correction:

- derive Popup logical bounds from authored content geometry;
- preserve/extend existing `resolvePopupLogicalPosition` semantics;
- add Popup-specific Runtime renderer reset;
- explicit sane overlay stacking context;
- Popup open/close cannot resize/corrupt the Screen viewport;
- deterministic scale/position in Runtime logical coordinates;
- regression for canonical EEE Popup proving visible/reachable controls, no unintended internal editor scrollbars/clipping and stable underlying Screen.

No C26.4 product commit exists at handoff.

## C26.5+ pending acceptance

C26.5 must diagnose the real Preview `Historical query failed with HTTP 404` instead of hiding it as no-data.

C26.6–C26.9 cover Engineering shell usability, contrast/theme, functional Screen editor audit and Popup editor authoring.

C26.10 is residual EEE cleanup only after generic fixes.

After full accepted C26 exact-SHA validation: integrate only into C11, regenerate package/checksum/provenance, create a **new** Preview and perform a fresh Product Owner Codespace homologation.

## Binding follow-on backlog not absorbed into C26

Still mandatory in the overall Wave14 sequence:

- separate protected whole-system backup/restore;
- safe Historian Administration backup/export/import/restore;
- generic TAG raw-to-engineering scaling with explicit inverse write semantics;
- normal human decimal-place authoring and Save/Publish/Activate/package persistence;
- later real Modbus/PLC EEE variant only after current generic gates and fresh homologation.

Do not silently drop these requirements and do not mix them into an unrelated C26.4 commit.

## Guardrails

- #212 remains OPEN/DRAFT and has no authorization to merge to `main`;
- #266 and #288 MUST NEVER MERGE;
- #263 remains C11 -> integration only;
- never modify `main` directly;
- no force push/destructive rebase/branch deletion;
- diagnose CI red before rerun;
- no validation/test/security/Identity/authorization/Engineering Lock/licensing/lifecycle/package/Runtime authority weakening;
- Runtime/Active must not depend on `.escadalib`;
- Alarm / Operational Event / Audit remain separate;
- Wave13 #205/#207 remains paused.
