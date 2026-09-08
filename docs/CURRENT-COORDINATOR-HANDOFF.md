# Current Coordinator Handoff

> GitHub live is the official memory and the sole authority for this project. This file is a transfer snapshot only. Revalidate the repository before every decision, diagnosis, write, PR action, rerun or merge. If this file diverges from GitHub live, GitHub wins.

## Current Wave 14 coordination state

- Repository: `brunolrogerio-collab/EliteSCADA`
- Integration branch: `wave14/corrections-integration`
- Integration PR: `#212 -> main` — **OPEN/DRAFT; DO NOT MERGE without later, explicit Product Owner authorization**
- Active correction branch: `wave14/c26-po-homologation-corrections`
- C26 coordinator issue: `#286`
- C26 implementation PR: `#287` (`C26 -> C11`)
- C26 validation PR: `#288` (`C26 -> main`) — **VALIDATION ONLY; MUST NEVER MERGE**
- C11 branch: `wave14/c11-remaining-corrections`
- C11 PR: `#263`
- C11 validation PR: `#266` — **VALIDATION ONLY; MUST NEVER MERGE**
- Preserved pre-C26 Preview PR: `#285` — keep untouched as pre-C26 homologation evidence.

## Exact transfer boundary

At the moment of this handoff, the live C26 branch HEAD is:

`ba901cbec8efece22ad9f2c0aa39330f98a5262e`

Commit message:

`docs(w14-c26): record C26.4 and C26.5 validation`

This HEAD is documentation-only. The last exact product/test SHA with all five normal gates green is:

`2a69605b746f8035a99cb7202e44329993272409`

C26 status:

- C26.1 Runtime transient-failure resilience — **IMPLEMENTED / VALIDATED**
- C26.2 Runtime logical viewport/layout — **IMPLEMENTED / VALIDATED**
- C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED / VALIDATED**
- C26.4 Runtime popup layout/composition — **IMPLEMENTED / VALIDATED**
- C26.5 Alarm/Event/Historian Runtime surface — **DIAGNOSED / REGRESSION LOCKED / VALIDATED**
- C26.6 Engineering shell/workspace usability — **ACTIVE; NO C26.6 PRODUCT COMMIT YET**
- C26.7+ — pending in the order defined by live issue `#286`.

## CI state of transfer HEAD

For exact HEAD `ba901cbe...`, the latest live workflow state at transfer is:

- EliteSCADA CI #1439 / run `34167675250` — **SUCCESS**
- Preview Licensing CI #387 / run `34167675246` — **SUCCESS**
- Wave 11 Active HMI Runtime #365 / run `34167675231` — **SUCCESS**
- L3 Seven-Driver Lab #343 / run `34167675235` — **SUCCESS**
- Interop Lab Smoke #264 / run `34167675264` — **FAILURE**

Interop #264 failed in job `101881863012` (`common-peer-stack`), specifically step 17:

`MQTT round-trip smoke`

The exact log-level root cause has **not yet been extracted and classified**. Therefore:

- do not call the failure transient yet;
- do not blind-rerun the workflow;
- do not start a C26.6 product write while this red remains unexplained, because that would destroy CI causality on a docs-only HEAD.

The next coordinator's first technical action is to fetch and inspect the exact failed-job log, establish the cause, and only then decide whether a targeted rerun of job `101881863012` is justified. If evidence proves an environmental/transient failure unrelated to the docs-only commit, rerun **only that failed job**, then revalidate the exact outcome live.

## C26.4 / C26.5 validated facts

C26.4 commits:

- `2a6f742d87e84e5dbe7fd04e48b2b4c708f6652d` — generic Runtime Popup authored-bounds/composition correction;
- `fb0a6c21740d5be0ea322bad9fe77dcff2de5725` — regression locking canonical Runtime Popup composition.

C26.5 commits:

- `cd2ace19004601430b49f922ca0a1be785a400f2` — regression locking Historian error-vs-empty semantics;
- `2a69605b746f8035a99cb7202e44329993272409` — locale-aligned assertions without weakening semantics.

C26.5 diagnosis: the historical query backend route exists, but is feature-gated. Preserved Preview #285 starts the Timescale Historian without enabling the HistoricalQuery route/cursor-key configuration. The observed homologation HTTP 404 is therefore a **pre-C26 Preview harness configuration gap**, not a missing backend route and not a legitimate no-data result. Keep #285 untouched. The new post-C26 Preview must enable HistoricalQuery with safe preview/development configuration.

Exact product/test SHA `2a69605...` completed all normal gates green:

- EliteSCADA CI #1438 / run `34166715032`
- Preview Licensing CI #386 / run `34166715046`
- Interop Lab Smoke #263 / run `34166715034`
- Wave 11 Active HMI Runtime #364 / run `34166715044`
- L3 Seven-Driver Lab #342 / run `34166715029`

## C26.6 current diagnosis and acceptance

Product Owner acceptance requires:

- Engineering shell/workspace remains usable in a constrained viewport;
- navigation/editor panels can collapse and expand;
- long panels scroll independently where appropriate;
- canvas remains the dominant work area;
- Properties remains reachable;
- no capability or authority loss.

Relevant source paths already identified, but they must be fetched again from the live branch before editing:

- `web/scada-web/src/engineering/EngineeringApp.tsx`
- `web/scada-web/src/engineering/engineering.css`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspaceLegacy.tsx`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.css`

Preliminary diagnosis found cumulative horizontal pressure from the global Engineering sidebar plus Screen/Popup list, palette and inspector/Properties. Existing responsive rearrangement does not provide a complete user-controlled collapse/restore model. Do not solve this by merely shrinking widths, hiding Properties, or deleting capabilities.

Required generic direction:

- collapsible/expandable global Engineering navigation;
- collapsible/expandable editor-side panels with an obvious restore path;
- independent vertical scrolling for long navigation/list/palette/Properties surfaces;
- canvas prioritization without capability loss;
- meaningful constrained-viewport regression/E2E.

## Mandatory sequence after C26 product corrections

After the C26 sequence defined by live issue #286 is accepted:

1. integrate C26 only into C11 through `#287` when the sequence authorizes it;
2. regenerate package/checksum/provenance;
3. create a **NEW post-C26 Preview**;
4. do not reuse Preview `#285`;
5. run real Product Owner homologation in Codespace;
6. solve any remaining generic gaps;
7. only then proceed to the real EEE Modbus/PLC variant;
8. complete final Wave 14 acceptance.

Still mandatory in the overall Wave 14 backlog:

- protected full-system backup/restore separate from `.escadapkg`;
- secure Historian backup/export/import/restore administration;
- generic TAG raw -> engineering scaling, with HMI/Alarm/Historian/Trend consuming canonical engineering value and writes applying explicit inverse transform fail-closed;
- human decimal-place configuration persisted through Save -> Publish -> Activate -> Runtime -> package export/import;
- real EEE Modbus/PLC variant only after generic mechanisms, corrected package and fresh homologation.

## Non-negotiable guardrails

- do not modify `main` directly;
- do not merge `#212` without later, explicit Product Owner authorization;
- never merge validation PRs `#266` or `#288`;
- no force push, destructive rebase, branch deletion or cleanup outside scope;
- never weaken tests, validation, security, authentication, authorization, identity, Engineering Lock, licensing, lifecycle, package, drivers or Runtime Active Revision authority to obtain green CI;
- Runtime/Active must not depend on `.escadalib`;
- Alarm, Operational Event and Audit remain distinct concepts/authorities;
- diagnose every red CI before any rerun.

## Canonical reading order for the next coordinator

Read live from the C26 branch before acting:

1. `docs/CURRENT-COORDINATOR-HANDOFF.md`
2. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
3. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
5. `LAST CHANGE.md`
6. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
7. `docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

When the Product Owner says `siga`, continue autonomously across the next tasks. It never authorizes merge of `#212` or any validation-only PR.
