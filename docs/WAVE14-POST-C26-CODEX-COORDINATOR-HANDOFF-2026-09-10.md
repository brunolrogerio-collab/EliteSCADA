# Wave 14 — Post-C26 coordinator handoff to Codex

Date: 2026-09-10 (BRT)

## Fundamental rule

**GitHub live is the official memory and sole authority for project state.**

Before any decision, diagnosis, code/documentation change, PR action, workflow rerun or merge, revalidate the relevant live GitHub state. If this handoff conflicts with GitHub live, GitHub wins.

Repository: `brunolrogerio-collab/EliteSCADA`

Current audit/coordination surface: `preview/wave14-post-c26-work-audit`

Coordinator issue: #286

Post-C26 audit gate: #289

Post-C26 Preview PR: #290 — OPEN/DRAFT / Preview only

Diagnostic PR: #296 — OPEN/DRAFT / **DIAGNOSTIC ONLY / MUST NEVER MERGE**

## Why coordination is being transferred to Codex

The next coordinator should preferably operate from the Codex chat/session that already performed the real browser audit and has direct access to the active Codespace, local API and application. This is operationally preferable for the remaining diagnoses because several observations depend on correlating browser behavior with the live Codespace at the same instant.

This preference does not make Codex memory authoritative. **GitHub live remains authoritative.** The Codespace is a diagnostic surface, not a substitute for repository state.

## Current technical baseline

Corrected canonical C11: `19d5257d970f53ae798c5fa53946fce07c586452`

Accepted C26 product SHA: `08e2530671de10d48933c4b712a1a1abc9e41dce`

Technically validated post-C26 Preview candidate: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`

Frozen package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`

Exact-head technical evidence on `59e815e...`:

- Post-C26 Canonical Preview run `34403903462` — SUCCESS;
- Post-C26 Audit State Readiness run `34403903471` — SUCCESS.

The Preview branch contains later audit documentation/evidence commits above the technical candidate. Do not confuse the branch HEAD with the exact technically validated product candidate.

## Mandatory reading order for the next coordinator

1. live #286, including its latest comments;
2. live #289, especially checkpoint comment `5619800919`, but treat it as an earlier chronological checkpoint;
3. live PR #290 and its current head/base;
4. `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` at the current Preview HEAD;
5. `docs/WORK-UI-AUDIT-HANDOFF.md`;
6. this file;
7. `docs/CURRENT-COORDINATOR-HANDOFF.md` and `LAST CHANGE.md`;
8. live PR #296 and its latest workflow evidence.

### Chronology rule

The audit evolved after issue checkpoint `5619800919`. When a later section/commit in `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` contradicts an earlier issue checkpoint, use the later live evidence after revalidating it.

Finding numbers also evolved during the audit. **Do not create correction tasks based only on the numeric UIAUD ID. Reconcile ID + title + evidence + latest chronological section first.**

## Current diagnostic map

### A. RECHECK-SIM-PUMP-LEVEL — no longer a confirmed product freeze

Earlier Work recheck observed a full process snapshot frozen for >15 minutes and later bad quality, with browser diagnostics blocked. That justified a provisional P1 investigation.

Later direct Codespace evidence changed the picture: authenticated local API sampling showed `EEE.P01.LevelPct` continuing to change, API and Vite healthy, with local responses around milliseconds. The external browser path failed around GitHub Codespaces forwarding/authentication (`pf-signin`, `ERR_HTTP_RESPONSE_CODE_FAILURE`, and other client/proxy symptoms).

Current decision: **do not treat the simulation or Server Script as broken unless the freeze is reproduced again with correlated local evidence.** Do not blind-rerun #296. Park #296 as evidence-only unless a new reproduction justifies it.

If the symptom returns, capture before restart/reopen: local API 5080, Vite 5173, forwarded browser request, TAG value/timestamp/quality, realtime/WebSocket, process liveness and Server Script diagnostics if accessible.

### B. UIAUD-289-001 — P1 generic legacy visual schema crash — confirmed

Real UI reproduction exists in both Screen Editor and Popup Editor. Selecting persisted legacy visual objects can blank the entire application.

Confirmed mechanism: `listDynamicPropertyDestinations(element)` reaches `getBuiltinVisualObjectSchema(element.type)` directly, while persisted legacy identifiers such as `tank`, `value` and `dynamo` are not represented in the current built-in schema catalog. No local normalization/recovery exists in that path.

Classification: **GENERIC PRODUCT / P1**.

Required diagnosis/correction direction: establish the correct generic compatibility/migration/degradation contract for persisted legacy visual types. Do not weaken unknown-type validation merely to hide the crash. Add deterministic regressions for Screen + Popup and representative legacy types.

### C. UIAUD-289-002 — P1 Working/Runtime project mismatch — confirmed in later direct UI evidence

The audit initially appeared to refute the mismatch using `.preview` artifacts. Later live UI evidence superseded that conclusion: Engineering loaded Working `Demo Project` / key `demo`, version 0, while Runtime remained `eee-demo`, Active revision 2. The lifecycle UI itself reported the project mismatch and correctly blocked cross-project activation.

Classification in latest audit evidence: **EEE-SPECIFIC / P1**, with bootstrap/persistence/checkout cause still to diagnose.

Do not activate `demo`, mutate Active authority or weaken lifecycle protection merely to make the two surfaces match. Determine why the real Engineering Working state does not land on the intended EEE candidate even though preview artifacts indicated `eee-demo`.

### D. Engineering fetch/recovery UX — current UIAUD-289-004 path

Real browser observations show transient `Failed to fetch` while loading the public Engineering model. The shell can display fallback `Demo Project`, missing schema/revision data and block module navigation. A later recheck showed `Tentar novamente` can recover after a long delay.

The initiating transport/proxy cause remains separable from the product UX. The misleading fallback identity, blocked modules and weak recovery guidance are generic UI concerns.

Correlate local API vs Vite proxy vs private forwarded Codespaces route before assigning network cause.

### E. Route/navigation latency — current UIAUD-289-005 path

Engineering → Runtime, Runtime → Engineering/Historian/Audit and Manual transitions were observed taking roughly tens of seconds, while local Codespace HTTP responses were milliseconds.

Classification remains **P2 / UNCERTAIN** because proxy/authentication/forwarding has not been separated from application behavior.

Use Codex's direct Codespace access to measure local API, local Vite and forwarded browser timing for the same navigation before proposing product optimization.

### F. Deterministic generic Engineering/UI findings

The current audit record contains confirmed P2 product findings that do not require guessing about the transport root cause, including:

- Engineering shell navigation depends on global page scrolling instead of a stable independent/collapsible navigation model;
- Templates / Equipamentos / Dínamos / Bibliotecas lack useful resource previews in their own catalogs;
- Engineering Lock band consumes excessive viewport space across modules;
- shared Runtime/Engineering header overlaps around notebook-width viewports;
- account/session menu exposes controls without accessible names.

Treat each by its current title/evidence in the live audit file, not by remembered numbering alone.

### G. Runtime Trends and Popup behavior

Current audit evidence reports:

- Runtime Trends enters `Conectando dados ao vivo…` and returns silently to the operational view; reproduced in the audited session;
- pump detail popups can show `—` while main cards and Engineering TAG Monitor show Good numeric values, and the popup may close/return to the screen after several seconds.

Popup data evidence makes a general TAG bridge outage less likely, but cause remains uncertain. Correlate bindings, projection/re-render, navigation state and realtime before correcting.

### H. Script authoring / PO-PRE-07

Engineering instability blocked the complete practical script-authoring test. Static inspection confirms `PythonScriptAssistant` exposes project objects and snippets for TAG and visual-property operations, but the audit did not find a complete discoverable example for the requested compound workflow (compare TAGs and conditionally alter visual state), and Monaco autocomplete did not obviously provide TAG/API-specific completion.

Re-run this as a real user flow only after Engineering is stable enough to keep the Scripts workspace open.

### I. Alarm / Event / Historian evidence

Do not collapse these domains. Alarm, Operational Event and Audit remain separate authorities.

The later audit observed stable global Alarm UI and a working Operational Event table after a transient delay. Historian one-hour queries returned zero clearly, consistent with the loaded Working reporting zero historian policies. Do not invent a Historian defect from absence of data alone.

Any earlier `UIAUD-289-016` timing/occurrence concern remains evidence-insufficient until a naturally comparable active occurrence is captured.

## Recommended next technical order for Codex

1. Revalidate GitHub live and confirm the current Preview/Codespace association and clean state.
2. Use direct Codespace access to correlate local API 5080, local Vite 5173 and forwarded browser behavior for Engineering `Failed to fetch` / latency, without restarting before evidence capture.
3. Reproduce and diagnose the `demo` Working vs `eee-demo` Runtime state and determine the bootstrap/checkout/persistence cause while preserving lifecycle/Active authority.
4. Diagnose and correct UIAUD-289-001 generically, with deterministic Screen + Popup regressions for persisted legacy visual types.
5. Diagnose Runtime Trends and popup auto-return/data mismatch using local TAG/realtime/projection evidence.
6. Then address deterministic generic P2 UI defects (shared header responsive layout, Engineering navigation scrolling/collapse, Engineering Lock footprint, accessibility labels, catalog previews, error/fallback UX).
7. Re-run PO-PRE-07 script-authoring flow after Engineering stability is restored.
8. For every UNCERTAIN finding, obtain technical reproduction before product correction.
9. For every confirmed product correction, use the authorized correction route established live at that time, add deterministic regressions, validate an exact new SHA, and only then prepare targeted Work recheck where justified.
10. Final Product Owner homologation remains blocked until audit findings are corrected/revalidated.

## PR #296 status

PR #296 is diagnostic-only and **MUST NEVER MERGE**.

Current diagnostic head at handoff: `7738b568a5dd4259e958a2c5023c2bf6ca7e5acb`.

Latest recorded diagnostic run: `34505442984`, job/check `102966371728` — FAILURE classified `INFRASTRUCTURE_OR_BOOTSTRAP_FAILURE`, effective observation `0s`. It did not reproduce the >15-minute freeze and does not authorize a product correction.

Do not blind-rerun it. Prefer the real Codespace for correlated read-only diagnosis unless new evidence makes the isolated workflow useful again.

## Permanent boundaries

- never mutate `main` directly;
- #212 remains OPEN/DRAFT and requires a future, separate, explicit Product Owner authorization before merge to `main`;
- #266 / #288 / #292 / #293 remain validation-only / MUST NEVER MERGE where applicable;
- #296 MUST NEVER MERGE;
- preserve #285 as historical pre-C26 Preview evidence;
- #290 is Preview-only and is not a route to `main`;
- no force push, destructive rebase, branch deletion or blind workflow rerun;
- never weaken security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics, drivers or tests;
- Runtime/Active must remain independent of `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- do not use EEE-specific workarounds to mask generic product defects;
- Wave13 #205/#207 remains paused.

## Handoff decision

`POST-C26 AUDIT EVIDENCE PRESERVED -> CODEX DIRECT-CODESPACE COORDINATION PREFERRED -> RECHECK FREEZE NOT CURRENTLY A CONFIRMED PRODUCT DEFECT -> P1 LEGACY SCHEMA CRASH + WORKING/RUNTIME MISMATCH REQUIRE ACTION -> TRANSPORT/LATENCY/POPUP/TRENDS REQUIRE CORRELATED DIAGNOSIS -> P2 GENERIC UI BACKLOG PRESERVED -> NOT READY FOR FINAL PO HOMOLOGATION`
