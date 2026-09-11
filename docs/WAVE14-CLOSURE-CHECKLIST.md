# Wave 14 — Objective Closure Checklist

**Established:** 2026-09-11 BRT  
**Authority:** Product Owner direction + Main Coordinator execution  
**Scope:** close Wave 14 as a diagnostic/documentation wave, integrate the accepted Wave 14 baseline into `main`, then open Wave 15 for corrections.

> GitHub live is the official memory and sole authority. Revalidate relevant issues, PRs, branches, SHAs, files and CI immediately before every decision, write, rerun, integration or merge. Any checkpoint value in this file is descriptive only and may be superseded by live GitHub.

## Closure principle

Wave 14 does **not** need to implement every newly discovered correction. It must leave every material finding sufficiently understood that Wave 15 can start implementing instead of repeating the exploratory audit from zero.

A finding may exit Wave 14 as either:

- `CONFIRMED` — evidence and mechanism are sufficient to define a correction contract; or
- `UNCERTAIN — BOUNDED` — the available evidence does not justify a product correction yet, but the known facts, exclusions, missing observation and exact reproduction/capture method are documented.

`UNCERTAIN` is not acceptable when it only means “investigate more”.

---

## A. Minimum mandatory diagnostics

For each material finding below, preserve at minimum:

`reproduction/evidence -> classification -> responsible layer/subsystem -> concrete technical path when determinable -> causal mechanism or bounded hypothesis -> Wave 15 correction contract -> deterministic regression -> dependencies/boundaries`

### A1 — Engineering `Failed to fetch` / route latency

- [ ] Correlate forwarded browser, local Vite `5173`, local API `5080`, authentication/proxy/forwarding, process health and logs in the same reproduction window.
- [ ] Determine where the divergence first appears.
- [ ] Separate product recovery behavior from Codespaces/private-forwarding/environment behavior.
- [ ] Record exact evidence and timing.
- [ ] If still uncertain, record exactly what future observation is missing and how to capture it.
- [ ] Define Wave 15 correction scope only for product-owned behavior.

### A2 — Engineering Working `demo` vs Runtime Active `eee-demo`

- [ ] Identify bootstrap/checkout/persistence/public-model path responsible for the Working identity.
- [ ] Determine when and why Engineering can open the wrong Working project.
- [ ] Preserve evidence that lifecycle correctly rejects cross-project activation.
- [ ] Do **not** use Active mutation or activation of `demo` as a correction.
- [ ] Define exact Wave 15 correction contract and regression for bootstrap/reopen/persistence identity.

### A3 — persisted legacy visual schema crash

- [ ] Confirm persisted legacy identifiers involved, including `tank`, `value`, `dynamo` where applicable.
- [ ] Preserve the call path reaching `getBuiltinVisualObjectSchema(element.type)` through dynamic-property authoring.
- [ ] Identify where compatibility/normalization/migration/fallback belongs.
- [ ] Distinguish **known legacy type** from **truly unknown type**.
- [ ] Define generic Wave 15 compatibility/migration/degradation behavior.
- [ ] Require deterministic regressions for Screen Editor, Popup Editor, and a negative unknown-type case.
- [ ] Do not weaken validation for arbitrary unknown types.

### A4 — Runtime Trends silent return

- [ ] Reproduce `Conectando dados ao vivo…` followed by silent return when possible.
- [ ] Correlate Historian/no-data, realtime subscription, projection, route/subview state and error handling.
- [ ] Identify responsible layer or leave a bounded uncertainty contract.
- [ ] Define data / valid-no-data / backend-error / realtime-error / navigation regressions for Wave 15.

### A5 — Runtime Popup `—` / disappearance / auto-return

- [ ] Reproduce popup values as `—` while authoritative TAG surfaces show numeric `Good`, when possible.
- [ ] Correlate binding, realtime, projection, quality and navigation/re-render state.
- [ ] Determine whether popup disappearance/return shares the same mechanism.
- [ ] Define future regressions for stopped/running, zero, missing, non-Good quality and navigation persistence.

### A6 — Engineering fallback/recovery UX

- [ ] Preserve the `Demo Project` / missing-snapshot behavior observed during model-load failure.
- [ ] Separate initial transport cause from product-owned fallback/error UX.
- [ ] Define correct loading/unavailable/retry behavior without fictitious authoritative Working identity.
- [ ] Define Wave 15 recovery/error-state regression.

### A7 — Screen/Popup Editor functional inventory

Each item must end as `WORKS`, `DEFECT`, `ABSENT/UNSUPPORTED`, or `NOT VALIDATED`, with evidence where practical.

- [ ] object selection;
- [ ] structure-tree selection;
- [ ] move;
- [ ] resize;
- [ ] copy/paste;
- [ ] undo/redo;
- [ ] group/ungroup where exposed;
- [ ] lock;
- [ ] alignment/distribution where exposed;
- [ ] z-order;
- [ ] zoom/pan;
- [ ] grid/snap;
- [ ] Properties access/edit;
- [ ] text/content editing;
- [ ] bindings;
- [ ] preview;
- [ ] popup bounds and authoring composition.

A visible enabled control that does not perform its documented action is a **functional defect**, not cosmetic feedback.

### A8 — Script Engineering functional inventory

Each item must end as `WORKS`, `DEFECT`, `ABSENT/UNSUPPORTED`, or `NOT VALIDATED`.

- [ ] create/open/edit a script;
- [ ] discover project TAGs;
- [ ] discover screens/popups/visual objects/properties;
- [ ] discover available script APIs;
- [ ] autocomplete/snippet behavior;
- [ ] understandable syntax/validation diagnostics;
- [ ] bind/associate intended event, trigger or execution scope;
- [ ] test/execute through the product-supported path;
- [ ] save into authoritative Engineering state;
- [ ] revision/publish/activate behavior where applicable;
- [ ] observe/debug runtime result;
- [ ] demonstrate at least one representative compound flow rather than only isolated snippets.

Static existence of Monaco, Python, snippets or APIs does not satisfy the developer-functional bar by itself.

---

## B. Additional backlog that must not be lost

Before Wave 14 closes, explicitly preserve or transfer:

- [ ] responsive shared header overlap;
- [ ] account-menu accessible names;
- [ ] Engineering navigation / scrolling / collapse behavior;
- [ ] Engineering Lock footprint;
- [ ] Templates / Equipment / Dynamos / Libraries useful-preview gaps;
- [ ] residual theme/readability/state issues;
- [ ] Alarm timestamp issue, if still uncertain, as a bounded future diagnostic;
- [ ] `RECHECK-SIM-PUMP-LEVEL` as **not currently confirmed as a product defect**, unless new correlated evidence supersedes that classification;
- [ ] protected whole-system backup/restore distinct from `.escadapkg`;
- [ ] Historian Administration backup/export/import/restore;
- [ ] generic TAG raw -> engineering scaling with explicit inverse-write semantics;
- [ ] human decimal-place authoring persisted through lifecycle/package;
- [ ] real Modbus/PLC EEE variant for its later appropriate stage.

---

## C. Canonical Wave 14 -> Wave 15 transfer backlog

Create and finalize:

`docs/WAVE15-INITIAL-CORRECTION-BACKLOG.md`

Every transferred item must contain:

`ID | priority | area | symptom | reproduction | evidence | classification | responsible subsystem/layer | concrete code/API/schema/lifecycle path | cause/mechanism | Wave 15 correction contract | deterministic regression | dependencies | protected boundaries`

Acceptance:

- [ ] all material P1 findings transferred;
- [ ] relevant P2 findings transferred;
- [ ] uncertain findings explicitly marked and bounded;
- [ ] no hypothesis presented as proven fact;
- [ ] no EEE-specific workaround proposed for a generic defect;
- [ ] Screen/Popup Editor and Script Engineering explicitly prioritized as **developer-functional product correctness**.

---

## D. Mandatory closure documentation

- [ ] Finalize `docs/WAVE14-DIAGNOSTIC-CLOSURE-AND-WAVE15-TRANSFER.md`.
- [ ] Maintain/finalize the post-C26 audit record with the latest chronological evidence.
- [ ] Create/finalize `docs/WAVE15-INITIAL-CORRECTION-BACKLOG.md`.
- [ ] Keep this `docs/WAVE14-CLOSURE-CHECKLIST.md` synchronized with the actual exit state.
- [ ] Update `docs/CURRENT-COORDINATOR-HANDOFF.md`.
- [ ] Update `LAST CHANGE.md`.
- [ ] Update `docs/ROADMAP.md`.
- [ ] Synchronize the obsolete Wave14/Wave13 sequencing statement in `PROJECT GOAL.md` while preserving stable architectural intent.
- [ ] Record a Wave 14 diagnostic-closure checkpoint in issue #286.
- [ ] Record conclusion/transfer state in issue #211.
- [ ] Preserve Wave 13 #205 / PR #207 as paused; no automatic resumption after Wave 15.

---

## E. Logical cleanup of the Wave 14 graph

This is **not** destructive branch cleanup. Historical evidence remains preserved.

- [ ] PR #263 is confirmed as the canonical C11 -> `wave14/corrections-integration` route.
- [ ] PR #212 is confirmed as the only Wave 14 integration -> `main` route.
- [ ] PR #290 is closed/preserved as Preview/audit evidence and **not merged**.
- [ ] PR #296 is closed/preserved as diagnostic evidence and **MUST NEVER MERGE**.
- [ ] PR #285 remains preserved as pre-C26 historical evidence.
- [ ] #266 / #288 / #292 / #293 remain validation-only / MUST NEVER MERGE where marked.
- [ ] No preserved evidence branch is deleted as routine cleanup.
- [ ] No force push or destructive rebase is used.

Checkpoint topology at checklist creation, subject to live revalidation:

- `main`: `edbdf446ea657713bdc487be91bf10bfcd03c684`;
- PR #212 head: `wave14/corrections-integration` at `ff185ffd67fe4abc597af9184c21f86376ba6e17`;
- PR #263 head: canonical C11 `19d5257d970f53ae798c5fa53946fce07c586452`;
- accepted C26 product: `08e2530671de10d48933c4b712a1a1abc9e41dce`;
- technically validated post-C26 Preview candidate: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`;
- PR #290 is Preview-only;
- PR #296 is diagnostic-only / MUST NEVER MERGE.

---

## F. Wave 14 integration into `main`

This block technically closes the Wave.

- [ ] Revalidate canonical C11 live.
- [ ] Confirm current C11 exact SHA or its legitimate successor.
- [ ] Revalidate PR #263 head/base/mergeability.
- [ ] Integrate **#263 -> `wave14/corrections-integration`** only through the authorized route.
- [ ] Confirm #263 does not accidentally bring Preview-only/diagnostic harness content.
- [ ] Propagate only the selected final Wave14/Wave15 closure, diagnostic, roadmap and handoff documentation onto the integration branch through an explicit non-Preview route.
- [ ] Do **not** merge #290.
- [ ] Do **not** merge #296.
- [ ] Do **not** merge any validation-only / MUST-NEVER-MERGE PR.
- [ ] Record exact final `wave14/corrections-integration` SHA.
- [ ] Run universal `EliteSCADA CI` on the exact integration SHA.
- [ ] Run specialized gates required by actual impact.
- [ ] If a gate is red, diagnose before rerun; no blind rerun.
- [ ] Record exact accepted integration SHA and evidence.
- [ ] Revalidate PR #212 exact head/base/mergeability immediately before merge.
- [ ] Confirm #212 remains `wave14/corrections-integration -> main`.
- [ ] Merge #212 using `expected_head_sha` protection only after every exit gate is satisfied.
- [ ] Record exact new `main` SHA.
- [ ] Validate the exact new `main`.
- [ ] Record post-merge validation evidence.

Only after this block can the repository state be declared:

`WAVE 14 = CLOSED / MERGED TO MAIN / DIAGNOSTIC TRANSFER COMPLETE`

---

## G. Close Wave 14 coordination surfaces

Only after exact new `main` validation:

- [ ] Close #286 as `COMPLETED — diagnostic transfer to Wave 15`.
- [ ] Close #211 as `COMPLETED — owner validation / diagnostic wave`.
- [ ] Close #289 as completed audit/transfer evidence, preserving its history.
- [ ] Close #290 without merge; preserve as Wave 14 Preview evidence.
- [ ] Close #296 without merge; preserve as diagnostic evidence.
- [ ] Close obsolete auxiliary Wave 14 PRs/issues when their historical role is explicit.
- [ ] Do not delete evidence merely to make the repository look cleaner.
- [ ] Update `LAST CHANGE.md` to point to the exact validated `main` and Wave 15 opening state.

---

## H. Gate to open Wave 15

Wave 15 must **not** be opened as an active correction wave until all of the following are true:

- [ ] Wave 14 diagnostic package is complete.
- [ ] `docs/WAVE15-INITIAL-CORRECTION-BACKLOG.md` is complete enough to implement from.
- [ ] Canonical C11 has been integrated into the Wave 14 integration branch.
- [ ] PR #212 has merged through the authorized route.
- [ ] Exact new `main` is validated.
- [ ] Wave 14 is formally marked closed.
- [ ] No material P1 exists only in chat memory.
- [ ] Every material transferred finding has an objective correction/test contract.
- [ ] Wave 13 remains formally paused/preserved.

Then:

- [ ] Create principal issue **Wave 15 — Product Functional Maturity & Corrections**.
- [ ] Create Wave 15 correction/integration branch from the exact validated new `main`.
- [ ] Import the Wave 14 transfer backlog as the initial Wave 15 execution ledger.
- [ ] Define dependency order before creating broad correction branches.

---

## I. Initial Wave 15 priority order

Unless later live evidence changes dependency/severity:

1. **P0/P1 authority/state** — Working/Runtime/lifecycle/bootstrap defects that can invalidate Engineering truth.
2. **P1 Screen/Popup Editor functional maturity** — including legacy-schema compatibility and real developer authoring flow.
3. **P1 Script Engineering functional maturity** — discoverability, authoring, diagnostics, trigger/binding/lifecycle, observable debugging.
4. **P1 Runtime behavior** — Trends, Popup bindings/realtime/navigation and recovery behavior.
5. **P2 Engineering structural UX** — fallback/error state, navigation/layout, Lock footprint, resource previews.
6. **P2 shell/accessibility/responsiveness**.

Wave 15 endgame:

`Wave 15 corrections -> exact-SHA green -> fresh Codespace Preview -> technical readiness -> real browser audit -> diagnostic/log reading -> targeted residual corrections/recheck if necessary -> Product Owner maturity decision`

Wave 13 does not automatically resume after Wave 15. It requires a later explicit Product Owner maturity decision.

---

## Definition of Done — Wave 14

Wave 14 is closed only when:

> **All material observed problems are either confirmed or bounded-uncertain, have preserved evidence and an implementable Wave 15 correction/test contract; the transfer backlog is consolidated; the accepted Wave 14 baseline and required closure documentation are integrated and validated in `main`; temporary Wave 14 surfaces are closed without forbidden merges or evidence destruction; and Wave 15 can begin without reconstructing Wave 14 from chat history.**
