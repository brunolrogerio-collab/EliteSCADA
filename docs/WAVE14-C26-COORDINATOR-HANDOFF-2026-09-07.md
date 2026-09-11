# Wave 14 C26 Coordinator Handoff — 2026-09-07

## Authority

**GitHub live is the official memory and sole authority for EliteSCADA.**

This document is an operational handoff snapshot. The next coordinator must revalidate live state before every decision, diagnosis, code/documentation change, PR action, workflow rerun or merge. Any divergence between this document and GitHub live is resolved in favor of GitHub.

Repository: `brunolrogerio-collab/EliteSCADA`

## Protected topology

| Purpose | Branch / PR | Rule |
|---|---|---|
| Wave 14 integration | `wave14/corrections-integration`, PR `#212 -> main` | OPEN/DRAFT. **No merge without later explicit Product Owner authorization.** |
| C26 correction branch | `wave14/c26-po-homologation-corrections` | Active working branch. |
| C26 coordinator issue | `#286` | Live work-order authority for C26. |
| C26 implementation | PR `#287`, C26 -> C11 | Use only when C26 sequence authorizes integration. |
| C26 validation | PR `#288`, C26 -> main | **VALIDATION ONLY. MUST NEVER MERGE.** |
| C11 correction branch | `wave14/c11-remaining-corrections` | Downstream target for C26. |
| C11 implementation | PR `#263` | C11 -> Wave14 integration. |
| C11 validation | PR `#266` | **VALIDATION ONLY. MUST NEVER MERGE.** |
| Pre-C26 Preview | PR `#285` | Preserve untouched as historical homologation evidence. |

Wave13 PRs `#205/#207` remain paused unless GitHub live explicitly establishes otherwise.

## Exact transfer snapshot

Live C26 branch HEAD at transfer:

`ba901cbec8efece22ad9f2c0aa39330f98a5262e`

`docs(w14-c26): record C26.4 and C26.5 validation`

This commit changes documentation only.

Last exact product/test SHA validated green across all five normal gates:

`2a69605b746f8035a99cb7202e44329993272409`

C26 work-order progress:

1. C26.1 Runtime transient-failure resilience — **IMPLEMENTED / VALIDATED**
2. C26.2 Runtime logical viewport/layout — **IMPLEMENTED / VALIDATED**
3. C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED / VALIDATED**
4. C26.4 Runtime popup layout/composition — **IMPLEMENTED / VALIDATED**
5. C26.5 Alarm/Event/Historian Runtime surface — **DIAGNOSED / REGRESSION LOCKED / VALIDATED**
6. C26.6 Engineering shell/workspace usability — **ACTIVE; NO PRODUCT COMMIT YET**
7. C26.7 Engineering theme/contrast — **PENDING**
8. C26.8 Screen editor functionality audit/fixes — **PENDING**
9. C26.9 Popup editor functionality audit/fixes — **PENDING**
10. C26.10 residual EEE cleanup after generic corrections — **PENDING**

Always fetch live issue `#286` before using the wording/order above as an execution decision.

## CI boundary requiring immediate attention

For exact documentation-only HEAD `ba901cbe...`, live workflow state at transfer:

- EliteSCADA CI #1439 / run `34167675250` — **SUCCESS**
- Preview Licensing CI #387 / run `34167675246` — **SUCCESS**
- Wave 11 Active HMI Runtime #365 / run `34167675231` — **SUCCESS**
- L3 Seven-Driver Lab #343 / run `34167675235` — **SUCCESS**
- Interop Lab Smoke #264 / run `34167675264` — **FAILURE**

Interop #264:

- failed job: `101881863012`
- job name: `common-peer-stack`
- failed step: `MQTT round-trip smoke`
- all setup, model-validation, peer startup/readiness steps before it completed successfully;
- later OPC UA smoke was skipped because MQTT failed;
- cleanup/status/log-dump steps completed.

**The exact log-level root cause is still pending extraction.** Do not convert that absence of evidence into a comforting story about a transient failure. Humans invented CI specifically to prevent that kind of optimism from becoming production state.

Rules for the next action:

1. revalidate C26 HEAD and current workflow state;
2. fetch the exact log for job `101881863012`;
3. identify the precise MQTT round-trip failure line/context and classify it;
4. do not rerun until diagnosis exists;
5. if evidence establishes an environmental/transient failure unrelated to the docs-only commit, rerun **only job `101881863012`**, not the whole workflow;
6. poll/revalidate the rerun outcome live;
7. only after CI causality is understood should C26.6 product code move.

The deliberate pause before C26.6 protects causality: adding a product commit on top of an unexplained red from a docs-only HEAD would make subsequent diagnosis needlessly ambiguous.

## C26.1–C26.3

These corrections were implemented and validated before the current transfer. Detailed commits, regressions and gate history remain in `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`.

Do not reconstruct their state from memory. Fetch the execution log and exact commits if touching those areas.

## C26.4 — Runtime Popup composition

Functional commit:

`2a6f742d87e84e5dbe7fd04e48b2b4c708f6652d`

Regression commit:

`fb0a6c21740d5be0ea322bad9fe77dcff2de5725`

Root cause established during C26.4:

- Screen Runtime reset had already been correctly scoped by C26.2;
- Popup rendering still inherited Engineering renderer layout/chrome such as minimum height, margin and overflow behavior;
- authored Popup geometry therefore lacked a deterministic Runtime composition box and could become clipped/misaligned relative to the Screen.

Generic correction:

- derives Popup logical bounds from authored content geometry;
- extends existing popup-position resolution while preserving legacy semantics when bounds are absent;
- composes Popup in the same logical coordinate system as the Screen;
- adds Runtime Popup-specific renderer reset and explicit overlay stacking;
- prevents Popup open/close from resizing or corrupting the underlying Screen;
- preserves authored IDs, bindings, Commands and backend Active Revision authority;
- avoids C11/EEE-specific hardcoded dimensions.

Regression:

`web/scada-web/tests-wave11/c26-popup-composition.spec.ts`

The canonical EEE P01 Popup is required to remain visible, correctly bounded and operable, without unintended editor chrome/scroll behavior, while preserving the Screen layout across open/close.

## C26.5 — Historian Runtime semantics and Preview diagnosis

Observed Product Owner homologation symptom:

`Historical query failed with HTTP 404`

Diagnosis established from source/configuration:

- backend historical query route exists;
- route mapping is feature-gated by HistoricalQuery configuration;
- Wave11 canonical harness enables HistoricalQuery and supplies its cursor key;
- preserved pre-C26 Preview #285 starts TimescaleDB Historian but does not enable the HistoricalQuery route/cursor-key configuration;
- the 404 is therefore a **pre-C26 Preview harness configuration gap**, not a missing backend API route and not a valid zero-row/no-data result.

Do not modify #285 retroactively. It is evidence of what the PO actually homologated before C26.

Regression commits:

- `cd2ace19004601430b49f922ca0a1be785a400f2`
- `2a69605b746f8035a99cb7202e44329993272409`

Locked semantics:

- HTTP/backend failure remains an error state;
- successful valid query with zero rows renders normal no-data state;
- raw transport/HTTP technical text is not presented as chart/operator content;
- Alarm, Operational Event and Audit remain distinct authorities/concepts.

The future **new post-C26 Preview** must enable HistoricalQuery with safe preview/development configuration, including the required cursor-key setup.

### Exact-SHA green evidence

At `2a69605b746f8035a99cb7202e44329993272409`:

- EliteSCADA CI #1438 / `34166715032` — SUCCESS
- Preview Licensing CI #386 / `34166715046` — SUCCESS
- Interop Lab Smoke #263 / `34166715034` — SUCCESS
- Wave 11 Active HMI Runtime #364 / `34166715044` — SUCCESS
- L3 Seven-Driver Lab #342 / `34166715029` — SUCCESS

## C26.6 — current active scope

No C26.6 product commit exists at transfer.

Product Owner acceptance:

- Engineering shell/workspace usable in limited viewport;
- navigation/editor panels can collapse/expand;
- long panels scroll independently where appropriate;
- canvas remains dominant;
- Properties remains reachable;
- no capability loss.

Relevant paths identified during preliminary diagnosis:

- `web/scada-web/src/engineering/EngineeringApp.tsx`
- `web/scada-web/src/engineering/engineering.css`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspaceLegacy.tsx`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.css`

These paths must be fetched again from the live branch before any edit.

### Preliminary structural diagnosis

Current Engineering width pressure can stack several panels simultaneously:

- global Engineering sidebar around 236 px;
- Screen/Popup list around 210–260 px;
- visual palette around 135–170 px;
- inspector/Properties around 160–210 px;
- plus gaps/borders.

Existing responsive behavior rearranges some surfaces at narrower viewport widths but does not provide a complete, user-controlled collapse/expand model. This matches the PO symptom that the canvas becomes squeezed and Properties can become awkward to reach.

### Generic implementation direction

Do not treat this as a cosmetic width tweak.

Expected correction shape:

- a collapse/expand affordance for global Engineering navigation;
- collapse/expand affordances for relevant visual-editor side panels, preserving obvious restoration;
- independent vertical scrolling for long nav/list/palette/Properties areas;
- canvas takes the remaining workspace and stays the primary authoring surface;
- Properties never disappears without a user-reachable restore path;
- capability projection/backend authority remains intact;
- no feature removal merely to make a screenshot fit;
- add a constrained-viewport regression/E2E that proves panels can be collapsed/restored, canvas remains usable and Properties remains reachable.

Before implementation, locate existing Engineering visual-editor tests on the **exact current branch** and extend the closest canonical regression rather than creating a parallel test universe without need.

## C26 execution behavior after C26.6

After C26.6 is implemented:

1. let natural workflows run on the exact new SHA;
2. diagnose any red before rerun;
3. never weaken tests/contracts/security/authority for green CI;
4. when exact-SHA validation is established, update `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md` with factual evidence;
5. continue C26.7 onward in the sequence defined by **live issue #286**;
6. do not create a new Preview prematurely.

## Post-C26 Preview and homologation sequence

Only after the C26 product sequence is accepted:

1. integrate C26 into C11 through PR #287 when authorized by the sequence;
2. regenerate the canonical package, checksum and provenance;
3. create a **new** post-C26 Preview;
4. do not reuse or rewrite #285;
5. run real Product Owner homologation in Codespace;
6. correct any remaining generic findings;
7. only after generic mechanisms and fresh homologation proceed to the real EEE Modbus/PLC variant;
8. complete final Wave14 acceptance.

## Mandatory follow-on Wave14 obligations

These requirements remain in scope even if they do not belong inside C26.6:

1. protected full-system backup/restore separate from `.escadapkg`;
2. secure Historian backup/export/import/restore administration;
3. generic TAG raw -> engineering scaling:
   - HMI, Alarm, Historian and Trend consume one canonical engineering value;
   - writes apply inverse transform explicitly and fail closed;
4. human decimal-place authoring/configuration persisted through:
   - Save -> Publish -> Activate -> Runtime -> package export/import;
5. real EEE Modbus/PLC variant only after generic mechanisms, corrected package and new homologation.

## Hard guardrails

- `main` is never edited directly.
- PR #212 stays OPEN/DRAFT and cannot merge without later explicit Product Owner authorization.
- PR #288 is validation-only and MUST NEVER MERGE.
- PR #266 is validation-only and MUST NEVER MERGE.
- no force push;
- no destructive rebase;
- no branch deletion;
- no cleanup outside scope;
- no weakening tests or validation to obtain green CI;
- no weakening security, authentication, authorization, identity, Engineering Lock, licensing, lifecycle, package, driver or Runtime authority;
- Runtime/Active cannot depend on `.escadalib`;
- backend remains authority for capabilities where designed;
- Runtime Active Revision authority remains intact;
- Alarm / Operational Event / Audit are not to be collapsed into one concept;
- diagnose red CI before rerun. Never blind-rerun.

## Transfer reading order

Before doing any work, fetch and read live from `wave14/c26-po-homologation-corrections`:

1. `docs/CURRENT-COORDINATOR-HANDOFF.md`
2. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
3. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
5. `LAST CHANGE.md`
6. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
7. `docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

Then revalidate:

- branch HEAD;
- live issue #286;
- PRs #287, #288 and #212;
- current workflows/checks for exact HEAD.

## Coordinator operating convention

When the Product Owner says **`siga`**, advance autonomously through subsequent safe tasks. Do not stop after each micro-step merely to announce that work continues. Stop only for a real blocker, an indispensable Product Owner decision, or a substantive completed milestone.

`Siga` never authorizes merge of #212, #266 or #288.
