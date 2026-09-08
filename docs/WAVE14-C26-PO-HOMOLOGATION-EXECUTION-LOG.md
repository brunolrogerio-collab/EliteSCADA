# Wave 14 C26 — PO Homologation Execution Log

Coordinator issue: #286  
Branch: `wave14/c26-po-homologation-corrections`  
Base authority at C26 start: `a724ece64a292aa1d1dedd886a72fb28ff8d90fe`

## Status

**EXECUTION ACTIVE — C26.1–C26.6 IMPLEMENTED / VALIDATED — C26.7 ACTIVE — NEW PREVIEW ONLY AFTER ACCEPTED C26 PRODUCT**

Pre-C26 Preview #285 / `d92e81f821c1a9c376b39bc3684eead54b3f570e` remains preserved as Product Owner homologation evidence and must not be reused as the post-C26 Preview.

## Work order and current progress

1. C26.1 Runtime transient-failure resilience — **IMPLEMENTED / VALIDATED**
2. C26.2 Runtime logical viewport/layout — **IMPLEMENTED / VALIDATED**
3. C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED / VALIDATED**
4. C26.4 Runtime popup layout — **IMPLEMENTED / VALIDATED**
5. C26.5 Alarm/Event/Historian Runtime surface — **DIAGNOSED / REGRESSION LOCKED / VALIDATED**
6. C26.6 Engineering shell workspace usability — **IMPLEMENTED / VALIDATED**
7. C26.7 Engineering theme/contrast — **ACTIVE**
8. C26.8 Screen editor functionality audit/fixes — **PENDING**
9. C26.9 Popup editor functionality audit/fixes — **PENDING**
10. C26.10 Canonical EEE residual cleanup — **PENDING**
11. exact-SHA full validation — **PENDING**
12. integrate only into canonical C11 when accepted — **PENDING**
13. regenerate canonical package/checksum/provenance — **PENDING**
14. create a new post-C26 Preview branch/PR — **PENDING**
15. new real Codespace/Product Owner homologation — **PENDING**

## C26.1–C26.5 retained validated authority

C26.1: `2cfdee7b943e35d8ad04ba77e20b89da93a48113`.

C26.2: `a918ed4dcca2a6f5fa2657d0c7108508ead91543`, refined by `3e312bd29a8d9109a9ec354e7f579d9bbc4aef3d`.

C26.3 final product/test head before popup work: `b7f0566f9e30f3e4c6dee3d020710b23923b6db2`.

C26.4:

- `2a6f742d87e84e5dbe7fd04e48b2b4c708f6652d` — generic authored Popup bounds/composition correction;
- `fb0a6c21740d5be0ea322bad9fe77dcff2de5725` — canonical Runtime Popup regression.

C26.5:

- `cd2ace19004601430b49f922ca0a1be785a400f2` — regression locking Historian error-vs-empty semantics;
- `2a69605b746f8035a99cb7202e44329993272409` — locale-aligned assertions without weakening semantics.

At `2a69605...` all five normal gates were green: EliteSCADA #1438, Preview #386, Interop #263, Wave11 #364 and L3 #342.

C26.5 diagnosis remains binding: `/api/historical/query` exists but is feature-gated. Preserved Preview #285 did not enable HistoricalQuery/cursor-key configuration; its 404 is a pre-C26 Preview harness configuration gap, not absence of backend route and not valid no-data. Failure remains failure; valid zero-row response remains no-data; raw transport text is not operational content; Alarm / Operational Event / Audit remain distinct.

## C26.6 — implemented / validated

Acceptance:

- user-controlled collapse/expand for Engineering navigation and editor panels;
- independent scrolling for long side content where appropriate;
- canvas remains usable/dominant in constrained viewport;
- Properties remains reachable/restorable;
- no authority or capability loss.

Implementation sequence from handoff SHA `11ef0f786952bd6dda4a313062e083bd414149f3`:

- `4286f2409f30492387f764b192acede6d26bd3aa` — `fix(w14-c26): make Engineering workspace collapsible`;
- `4b0e46d96510c6a637e12dc83c6d56dd8b9cc592` — `fix(w14-c26): keep collapsed Properties responsive on mobile`;
- `c1ae4bd311334835961bf4bbdcd6bd46b4acfa36` — `fix(w14-c26): keep workspace placed when navigation collapses`;
- `7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11` — `fix(w14-c26): keep editor main placed when screens collapse`.

Changed product/test paths across C26.6:

- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx`;
- `web/scada-web/src/engineering/visual-editor/VisualEditorLayoutControls.css`;
- `web/scada-web/tests-e2e/app-shell.spec.ts`.

The constrained-viewport Playwright regression deliberately exercises 1024x720 Engineering visual authoring. It collapses global navigation, Screens and Palette, verifies that the canvas gains usable width and remains above the minimum usability threshold, then collapses/restores Properties and proves it remains reachable.

### Browser findings retained, not hidden

The first browser validation exposed a real outer-grid defect: when Engineering navigation became `display:none`, the grid still had a zero-width first track and `.eng-workspace` auto-placed into it, collapsing the effective editor area. `c1ae4bd...` fixes placement explicitly.

The next Chromium validation exposed the same structural error one layer lower: after Screens became hidden, `.visual-editor-main` auto-placed into the zero-width first track. `7d9d977...` fixes the inner placement explicitly and resets it to column 1 in the mobile single-column breakpoint.

The test expectation was never relaxed. Both failures were corrected in product CSS.

A Wave11 failure on the intermediate `c1ae4bd...` SHA was separately diagnosed as a browser-navigation timeout in the C17 memory lifecycle flow, with no assertion/product failure and no executable relation to the C26.6 CSS delta. Only that failed job was rerun after diagnosis and succeeded. The final `7d9d977...` SHA then passed Wave11 naturally, so final C26.6 authority does not depend on the intermediate rerun.

### Exact-SHA C26.6 validation

At exact product/test SHA:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

all five normal gates completed SUCCESS:

- EliteSCADA CI #1444 / run `34237046911`;
- Preview Licensing CI #392 / run `34237046935`;
- Interop Lab Smoke #269 / run `34237046897`;
- Wave 11 Active HMI Runtime #370 / run `34237046873`;
- L3 Seven-Driver Lab #348 / run `34237046980`.

C26.6 is therefore **IMPLEMENTED / VALIDATED**.

## C26.7 — active

Live issue #286 defines C26.7 as Engineering theme/contrast.

Observed class of defect:

- white/light fields with light text have insufficient readability;
- readonly/disabled/editable states can be visually ambiguous in Screens and Script Engineering.

Acceptance:

- explicit readable foreground/background tokens for editable, readonly, disabled and placeholder states;
- regression coverage for affected Engineering surfaces.

Implementation must remain presentation-only. Do not change capability projection, authentication, authorization, Engineering Lock, lifecycle, package semantics or backend authority to solve contrast.

Before writing, fetch the exact current Screens and Script Engineering components/CSS and extend the closest canonical regression instead of creating parallel behavior.

## Remaining C26 sequence

After C26.7, continue in the live #286 order:

- C26.8 Screen editor basic authoring functionality audit/fixes;
- C26.9 Popup editor basic authoring;
- C26.10 residual canonical EEE cleanup only after generic fixes;
- exact-SHA full validation;
- integrate only into canonical C11 when accepted;
- regenerate package/checksum/provenance;
- create a **new** post-C26 Preview, preserving #285;
- fresh real Codespace/Product Owner homologation.

## Binding follow-on Wave14 backlog

Still mandatory outside this immediate C26 checkpoint sequence:

- separate protected whole-system backup/restore;
- safe Historian Administration backup/export/import/restore;
- generic TAG raw-to-engineering scaling with explicit inverse-write semantics;
- normal human decimal-place authoring and lifecycle/package persistence;
- real EEE Modbus/PLC variant only after generic mechanisms, corrected package and fresh homologation.

## Guardrails

- #212 remains OPEN/DRAFT and has no authorization to merge to `main`;
- #266 and #288 MUST NEVER MERGE;
- #263 remains C11 -> integration only;
- #285 remains preserved as pre-C26 Preview evidence;
- never modify `main` directly;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose CI red before rerun;
- no validation/test/security/Identity/authorization/Engineering Lock/licensing/lifecycle/package/Runtime authority weakening;
- Runtime/Active must not depend on `.escadalib`;
- Alarm / Operational Event / Audit remain separate;
- Wave13 #205/#207 remains paused.
