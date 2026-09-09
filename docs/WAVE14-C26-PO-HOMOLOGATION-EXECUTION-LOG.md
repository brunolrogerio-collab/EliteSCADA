# Wave 14 C26 — PO Homologation Execution Log

Coordinator issue: #286  
Branch: `wave14/c26-po-homologation-corrections`  
Base authority at C26 start: `a724ece64a292aa1d1dedd886a72fb28ff8d90fe`

## Status

**EXECUTION ACTIVE — C26.1–C26.6 IMPLEMENTED / VALIDATED — C26.7 IMPLEMENTED BUT CI-BLOCKED — C26.8 NOT STARTED — NEW PREVIEW ONLY AFTER ACCEPTED C26 PRODUCT — POST-C26 WORK AUDIT #289 REQUIRED BEFORE FINAL PO HOMOLOGATION**

Current C26 product/test candidate:

`642e33c83d17e3c53beb588841626551e6ea305f`

`fix(w14-c26): let readonly Engineering styles win cascade`

The branch contains documentation/coordination commits above this product SHA. Revalidate the live branch HEAD before any action and keep branch-head evidence separate from exact product/test validation evidence.

Pre-C26 Preview #285 / `d92e81f821c1a9c376b39bc3684eead54b3f570e` remains preserved as Product Owner homologation evidence and must not be reused as the post-C26 Preview.

New binding post-C26 quality gate:

- issue #289;
- canonical directive: `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`;
- do not execute during C26;
- after the new post-C26 Preview is technically green and fully prepared, ChatGPT Work must perform a real browser UI/functional audit before final Product Owner homologation.

## Work order and current progress

1. C26.1 Runtime transient-failure resilience — **IMPLEMENTED / VALIDATED**
2. C26.2 Runtime logical viewport/layout — **IMPLEMENTED / VALIDATED**
3. C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED / VALIDATED**
4. C26.4 Runtime popup layout — **IMPLEMENTED / VALIDATED**
5. C26.5 Alarm/Event/Historian Runtime surface — **DIAGNOSED / REGRESSION LOCKED / VALIDATED**
6. C26.6 Engineering shell workspace usability — **IMPLEMENTED / VALIDATED**
7. C26.7 Engineering theme/contrast — **IMPLEMENTED BUT NOT VALIDATED / CURRENT CI BLOCKER**
8. C26.8 Screen editor functionality audit/fixes — **NOT STARTED / BLOCKED BY C26.7**
9. C26.9 Popup editor functionality audit/fixes — **PENDING**
10. C26.10 Canonical EEE residual cleanup — **PENDING**
11. exact-SHA full validation — **PENDING**
12. integrate only into canonical C11 when accepted — **PENDING**
13. regenerate canonical package/checksum/provenance — **PENDING**
14. create a new post-C26 Preview branch/PR — **PENDING**
15. make post-C26 Preview technically green and establish `READY FOR WORK AUDIT` prerequisites — **PENDING**
16. create the short candidate-specific `docs/WORK-UI-AUDIT-HANDOFF.md` and final Work prompt — **PENDING**
17. ChatGPT Work real browser UI/functional audit under #289 — **PENDING**
18. coordinator triage/reproduction/corrections + deterministic regressions + new exact candidate — **PENDING**
19. optional targeted Work recheck when justified — **PENDING**
20. final real Codespace/Product Owner homologation — **PENDING**

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

## C26.7 — implemented but CI-blocked

Live issue #286 defines C26.7 as Engineering theme/contrast.

Acceptance remains:

- explicit readable foreground/background tokens for editable, readonly, disabled and placeholder states;
- regression coverage for affected Engineering surfaces;
- presentation-only correction with no capability/security/lifecycle authority change.

### Initial implementation

SHA:

`a05d349a37a41629ee13b47b4652df74d287273d`

`fix(w14-c26): make Engineering field states readable`

Primary paths:

- `web/scada-web/src/engineering/engineering-control-states.css`;
- `web/scada-web/src/main.tsx`;
- `web/scada-web/tests-e2e/wave-14-c26-engineering-contrast.spec.ts`.

At `a05d349...`:

- Preview Licensing CI #394 / run `34241442573` — SUCCESS;
- Interop Lab Smoke #271 / run `34241442611` — SUCCESS;
- Wave 11 Active HMI Runtime #372 / run `34241442718` — SUCCESS;
- L3 Seven-Driver Lab #350 / run `34241442633` — SUCCESS;
- EliteSCADA CI #1446 / run `34241442557` — FAILURE.

The Chromium regression proved the Screens route field could remain visually identical after `readonly` was applied.

### Diagnosed specificity defect and generic correction

Diagnosis: the editable input selector in `engineering-control-states.css` accumulated higher specificity through repeated `:not([type=...])` clauses than the `[readonly]` selector. Therefore editable styling could win the cascade despite the intended readonly rule.

Correction SHA:

`642e33c83d17e3c53beb588841626551e6ea305f`

`fix(w14-c26): let readonly Engineering styles win cascade`

The correction wraps the type-exclusion filters in `:where(...)` so they do not add specificity. It is intentionally minimal and generic. No `!important` was introduced. The strict C26.7 Playwright regression was not changed or weakened.

### Exact CI state for current product/test candidate

At `642e33c83d17e3c53beb588841626551e6ea305f`:

- Preview Licensing CI #402 / run `34255187708` — SUCCESS;
- Interop Lab Smoke #279 / run `34255187740` — SUCCESS;
- Wave 11 Active HMI Runtime #380 / run `34255187706` — SUCCESS;
- L3 Seven-Driver Lab #358 / run `34255187748` — SUCCESS;
- EliteSCADA CI #1454 / run `34255187823` — **FAILURE**.

EliteSCADA CI #1454:

- Backend build, test and smoke — SUCCESS;
- Web build — SUCCESS;
- Chromium end-to-end job `102159657922` — **FAILURE** at `Run browser E2E tests`.

No blind rerun was performed.

### Transfer blocker

The current assertion-level Chromium failure at `642e33c...` was **not conclusively extracted into the outgoing chat context** before coordinator rotation. Repeated decoded-log fetch attempts did not surface the full log text through the connector response visible to the chat.

Therefore:

- do **not** assume the `642e33c...` failure is the same assertion as the earlier `a05d349...` failure;
- do **not** rerun unchanged merely to seek green;
- do **not** weaken `wave-14-c26-engineering-contrast.spec.ts`;
- re-fetch job `102159657922` evidence, including Playwright report/artifact if needed;
- compare the actual failure with exact `642e33c...` CSS and DOM/component sources;
- make only the smallest generic correction supported by evidence;
- require a fresh exact product/test SHA with all five normal gates green before C26.7 can be marked VALIDATED.

C26.8 remains **NOT STARTED** until that gate is satisfied.

### Coordination-only cleanup note

During GitHub write-tool discovery in the outgoing coordinator chat, a temporary file `dummy` was accidentally created and then removed immediately by a normal follow-up commit. No force push or destructive rebase was used and no intended product file remains from that incident. Those commits and subsequent handoff documentation commits are coordination-only and are not product/test validation evidence.

## Remaining C26 and post-C26 sequence

After C26.7 is genuinely validated, continue in the live #286 order:

- C26.8 Screen editor basic authoring functionality audit/fixes;
- C26.9 Popup editor basic authoring;
- C26.10 residual canonical EEE cleanup only after generic fixes;
- exact-SHA full validation;
- integrate only into canonical C11 when accepted;
- regenerate package/checksum/provenance;
- create a **new** post-C26 Preview, preserving #285;
- bring the Preview to full technical readiness, including HistoricalQuery, Active EEE and dynamic simulation;
- satisfy the explicit `READY FOR WORK AUDIT` checklist from `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`;
- execute ChatGPT Work issue #289 as an audit-only real browser pass;
- preserve/triage/reproduce findings and correct confirmed generic defects with deterministic regressions;
- prepare a new exact candidate and perform targeted Work recheck only when justified;
- only then perform fresh real Codespace/Product Owner final homologation.

The first Work pass is not a development pass: no code changes, patches, commits or merges. The coordinator must prepare the environment before consuming the approximately 40-minute Work window.

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
- #287 remains C26 -> canonical C11 only;
- #263 remains C11 -> integration only;
- #285 remains preserved as pre-C26 Preview evidence;
- issue #289 is a post-C26 audit gate and must not be executed early;
- never modify `main` directly;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose CI red before rerun;
- no validation/test/security/authentication/authorization/Identity/Engineering Lock/licensing/lifecycle/package/Runtime authority weakening;
- Runtime/Active must not depend on `.escadalib`;
- no EEE-specific workaround for a generic product defect;
- Alarm / Operational Event / Audit remain separate;
- Wave13 #205/#207 remains paused.

## Continuation checkpoint — 2026-09-09 coordinator rotation

GitHub live was revalidated before this append. The earlier C26.7 transfer blocker in this chronological log was subsequently resolved; the current state is below.

### C26.7 — implemented / validated

Validation SHA:

`b1bd1f664b06684afaecbbff2d27da10760cc1ec`

Five normal gates all succeeded:

- EliteSCADA CI #1466 / run `34286582875`;
- Preview Licensing CI #414 / run `34286582844`;
- Interop Lab Smoke #292 / run `34286582814`;
- Wave 11 Active HMI Runtime #392 / run `34286582873`;
- L3 Seven-Driver Lab #370 / run `34286582905`.

Additional natural Interop #291 / run `34286580927` also succeeded. No rerun.

### C26.8 — implemented / validated

Validation SHA:

`6de64ed4d11ac1e525f32d7071b9624e428c96ee`

`fix(w14-c26): keep canvas overlays off controls`

Five normal gates all succeeded:

- EliteSCADA CI #1468 / run `34305175515`;
- Preview Licensing CI #416 / run `34305175507`;
- Interop Lab Smoke #295 / run `34305175549`;
- Wave 11 Active HMI Runtime #394 / run `34305175519`;
- L3 Seven-Driver Lab #372 / run `34305175521`.

Additional natural Interop #296 / run `34305175891` also succeeded. No rerun.

### C26.9 — implemented / validated

Validation SHA:

`55f292359af86f5f28d90cc578d4ac93ccc1f19c`

`fix(w14-c26): align Popup authoring with Runtime`

Five normal gates all succeeded:

- EliteSCADA CI #1469 / run `34309372353`;
- Preview Licensing CI #417 / run `34309372378`;
- Interop Lab Smoke #297 / run `34309372338`;
- Wave 11 Active HMI Runtime #395 / run `34309372309`;
- L3 Seven-Driver Lab #373 / run `34309372297`.

Additional natural Interop #298 / run `34309372823` also succeeded. No rerun.

This remains the last exact product/test SHA with all five normal gates green.

### C26.10 — initial EEE cleanup candidate

Structural audit scope:

- 38 TAGs;
- 13 Commands;
- 6 Screens;
- 2 Popups;
- 1 Dynamo;
- 190 visual IDs.

All references and logical geometry checks passed. The residual project-specific defect was overlapping pump state text: `PARADA` remained visible beneath `OPERANDO` and `FALHA`.

Initial candidate:

`50363bcc50037cc6932a1282e58e3b6d75fda9f2`

`fix(w14-c26): make EEE pump state labels legible`

The candidate added opaque `backgroundColor` and `cornerRadius` properties to the three `core.text` labels and strict Runtime computed-color assertions.

At that SHA, Preview, Interop and L3 succeeded. EliteSCADA CI #1470 failed on a generic PostgreSQL shared-schema race. Wave11 #396 independently failed on one Server Script timeout but recorded recovery and otherwise healthy diagnostics. Neither failure was blindly rerun.

### Generic PostgreSQL concurrency correction

Exact product/test SHA:

`e7c8a8bf3954890a1ca841498222bf6094952baf`

`fix(w14-c26): serialize shared PostgreSQL schema setup`

The correction:

- changed `PostgreSqlOperationalEventHistoryStore` infrastructure advisory lock from `4993446713136202562` to shared-schema lock `4993446713136202561`;
- added real Operational Event initialization/query to `PostgreSqlConcurrentInitializationTests`.

EliteSCADA CI #1471 passed, including backend build/test/smoke.

### Current exact gate state

At `e7c8a8bf3954890a1ca841498222bf6094952baf`:

- EliteSCADA CI #1471 / run `34347157464` — SUCCESS;
- Preview Licensing CI #419 / run `34347157585` — SUCCESS;
- Interop Lab Smoke #302 / run `34347159685` — SUCCESS;
- L3 Seven-Driver Lab #375 / run `34347157398` — SUCCESS;
- Wave 11 Active HMI Runtime #397 / run `34347157735` — **FAILURE**;
- additional natural Interop #301 / run `34347157333` — SUCCESS.

No rerun was requested.

### Current deterministic Wave11 blocker

Failed job:

`102451385709`

The normal browser lifecycle completed with 17 passed, 2 failed and 4 not run. Both failures occurred during package Preview:

- `tests-wave11/c11-eee-demo-hmi.spec.ts`;
- `tests-wave11/c26-popup-composition.spec.ts`.

`preview.canApply` was false because Dynamo `eee.dynamo.pump` contained three invalid `core.text` elements:

- `pump-stopped-label`;
- `pump-running-label`;
- `pump-fault-label`.

Each reported `VISUAL_PROPERTY_INVALID`: `core.text` does not declare `backgroundColor`.

Exact backend/browser schema inspection also confirms `core.text` does not declare `cornerRadius`, although the current EEE helper authors both properties. Renderer tolerance is not package schema authority; Preview correctly rejected the package.

Evidence artifact:

- `playwright-report-wave11`;
- artifact ID `10102316731`;
- run `34347157735`.

### Current classification and next step

- C26.1–C26.9 — **IMPLEMENTED / VALIDATED**;
- C26.10 — **IMPLEMENTED / NOT VALIDATED / CURRENT BLOCKER**;
- C26.11 — **NOT STARTED / BLOCKED**.

The next product action is not a rerun. Revalidate live, inspect the exact Wave11 evidence and schemas, then replace the invalid text-background representation with the smallest schema-valid EEE state-plate composition. Preserve opaque state coverage, z-order, strict Preview/package validation and Runtime regressions. Do not add an EEE validation exception or widen `core.text` solely for this fixture.

Require all five normal gates green on one new exact product/test SHA before validating C26.10 or starting C26.11.

Canonical detailed transfer:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-09.md`
