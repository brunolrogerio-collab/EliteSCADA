# Wave 15 — FC0-A Consolidated Correction Package

> GitHub live is the sole authority.
> Main Coordinator intentionally widened the pre-FC0A correction from two serialized blockers into one large sequential CODEX package before the next Main review.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`ORDER_ID: FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V6`

`ORDER_STATE: ACTIVE`

`EXECUTOR: SAME_SEQUENTIAL_CODEX_USED_FOR_FOUNDATIONS`

`EXECUTOR_MODE: LARGE_CONSOLIDATED_PACKAGE / SINGLE_FINAL_HANDOFF`

`EXACT_BASE_PRODUCT_SHA: 560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

`EXACT_BASE_TREE: 674019fbbc21001a2d68deb853c2c0b293e0a5cb`

`WORK_BRANCH: work/w15-fc0a-consolidated-corrections`

`TARGET_BRANCH: wave15/corrections-integration`

`MAIN_REVIEW_POLICY: V4_REVIEWED / TESTS_ACCEPTED / FINAL_EXECUTION_PROOF_ONLY`

`PR_POLICY: ONE_CONSOLIDATED_PR_AFTER_PACKAGE_COMPLETION`

`LEGACY_P101_BRANCH: work/w15-fc0a-p101-server-script-recovery / SUPERSEDED_UNUSED / IDENTICAL_TO_BASE_AT_SUPERSESSION`

## 0. Intent

Product Owner prefers known FC0-A findings to be corrected now, before release, rather than intentionally carrying avoidable defects into parallel DEV execution.

Therefore CODEX must execute the phases below sequentially on the single work branch and return one consolidated candidate only after:
- all safe authorized phases are implemented;
- focused tests are green;
- the combined natural Wave 15 validation profile is green on the exact final head;
- one PR to `wave15/corrections-integration` exists.

CODEX may make multiple coherent commits internally. Do **not** stop for Main review after each phase.

If a phase exposes a real frozen-contract contradiction, record it as `BLOCKED-CONTRACT`, do not weaken the contract, and continue other independent safe phases where doing so cannot conceal or compound the blocker. Final handoff must list any incomplete phase prominently.

No self-merge, no freeze declaration, no direct write to integration/main.

## 1. Non-negotiable frozen guards

Preserve:
- FND-01 Working / Revisions / Published / Active separation and fail-closed lifecycle;
- FND-02 backend Authority/capability/scope semantics;
- FND-03 machine-license trust, logical Runtime Session Lease, requested/granted class authority, shared quota semantics and transactional license lifecycle;
- FND-04 readable Script TAG references bound to stable TagId, stale/identity-drift fail-closed behavior, selector semantics and canonical registry authority;
- FND-06 canonical renderer/public visual model, centralized known-legacy compatibility seam, arbitrary-unknown fail-closed behavior and Working-vs-Active separation;
- FND-08 bounded timing/retry rules;
- Server Script sandbox/process isolation/allowlist, timeout/cancellation safety, bounded queue/coalescing and Active revision gating.

No EEE-only workaround, no timeout inflation, no second legacy registry, no client-owned authority, no fake project/runtime status, no blind write retry.

## 2. Phase A — mandatory FC0-A release blockers

### A1 — W15-P1-01 Server Script bounded recovery

Replace permanent failure-throttle latch semantics with bounded recovery.

Required:
1. repeated timeout/fault enters visible degraded/throttled state;
2. bounded cooldown;
3. at most one half-open/probe execution after cooldown;
4. successful probe restores healthy state and resets failure streak;
5. failed probe returns to bounded cooldown without busy loop;
6. bounded/coalesced queue remains bounded;
7. stale queued effects never replay across Active revision/authority change;
8. diagnostics expose recovery state, last useful execution, last failure/timeout and next probe/cooldown or equivalent;
9. preserve sandbox and FND-04 binding/Authority contracts.

Mandatory evidence:
- RED on exact base for permanent latch;
- GREEN transient recovery;
- GREEN persistent failure bounded probes;
- queue/coalescing invariant;
- stale-write negative after authority/revision transition;
- health/diagnostics assertions.

### A2 — W15-P1-06 truthful Engineering bootstrap/fallback

Required:
1. `snapshot=null` never synthesizes `Demo Project`;
2. no-model WorkspaceBar never presents `clean` or `unsaved` as authoritative facts;
3. loading/unavailable/error identity is neutral and truthful;
4. transport rejection is distinguished from actual HTTP response status;
5. real HTTP error exposes real status/context without fictitious transport status;
6. retry is bounded/read-only and can recover to a real snapshot;
7. a real authoritative project actually named `Demo Project` still displays normally;
8. navigation requiring snapshot remains contained/disabled while unavailable;
9. no blank SPA.

Mounted RED/GREEN matrix is mandatory.

## 3. Phase B — shared/cross-cutting gaps to close before FC0-A

### B1 — W15-P2-03 shared shell responsiveness

Correct privileged shell crowding/overflow at common notebook widths.

Acceptance:
- no horizontal document overflow across representative desktop/notebook widths including around 901-1180 px;
- Runtime/Engineering/Audit/Licensing/Help remain reachable according to Authority;
- account/theme controls remain usable;
- no hidden navigation caused only by width unless an explicit accessible compact pattern replaces it.

Add mounted viewport regressions.

### B2 — W15-P2-04 account accessibility regression closeout

Current behavior already has native details/summary, account accessible name, native buttons, focus-visible and Escape close logic.

Do not rewrite working behavior gratuitously.

Add focused browser regression proving:
- keyboard opens the account menu;
- Escape closes it;
- focus returns to trigger;
- switch/logout actions remain keyboard-operable;
- accessible name follows locale.

Production delta only if the regression exposes a real defect.

### B3 — W15-P2-05 Engineering viewport/scroll composition

Remove harmful dependence on document-level scrolling for desktop Engineering.

Acceptance:
- application frame is viewport-aware;
- desktop sidebar/workspace can scroll independently where appropriate;
- visual-editor canvas keeps priority;
- existing collapsible Screen/palette/Properties behavior remains;
- no new clipping at compact widths;
- no lifecycle/Authority semantics change.

### B4 — W15-P2-06 Engineering Lock footprint

Reduce unlocked Lock-management footprint without weakening protection.

Acceptance:
- normal unlocked Engineering does not permanently devote a large full-width multi-row block to Lock management;
- explicit status and management remain discoverable;
- configure / configure+lock / lock-now / clear operations remain explicit and backend-authorized;
- lifecycle hint remains available;
- locked surface behavior and protected-workspace non-mount remain unchanged;
- no credential leakage and no hidden bypass.

Prefer compact/disclosed management composition over semantic redesign.

### B5 — W15-P2-01 Trends observability

Close the confirmed bounded observability gap.

Expose truthful, testable state for:
- realtime connection state (connected/reconnecting/disconnected or equivalent);
- last request;
- last success;
- freshness age/reason;
- History error vs valid no-data vs realtime failure;
- same-Active retry/recovery.

Do not invent values or hide transport failure.

### B6 — W15-P2-02 shared live-value freshness

Preserve correct numeric zero and quality behavior, while adding explicit freshness diagnostics.

Expose/test:
- last request / last success;
- freshness age/threshold or equivalent;
- unavailable/stale reason;
- Good zero remains zero;
- null/non-Good/disconnected are distinguishable;
- open/close/reopen and reconnect do not falsify freshness.

Prefer one shared live-value freshness model rather than Popup-only special casing.

### B7 — contextual Help routing

Current Help catalog has section-specific topics but global Engineering routing always falls back to `engineering.overview`.

Make contextual Help resolve the current Engineering section to the nearest stable topic where one exists, with a safe `engineering.overview` fallback.

At minimum cover:
- Scripts -> `scripts.server`;
- Libraries -> relevant reusable-library topic if present;
- Security/users -> security topic if present;
- project/recovery surface -> recovery topic if present;
- other sections remain safely mapped/fallback.

Preserve stable language-neutral topic IDs and pt-BR/en/es behavior.

### B8 — canonical Runtime class wording

Replace legacy public/error wording that says `viewer` where canonical wire vocabulary is `viewOnly`, without changing accepted backward-compatible parser aliases unless a frozen contract explicitly permits removal.

Tests must preserve compatibility inputs while canonical output/error/help wording uses `viewOnly`.

## 4. Phase C — bounded downstream gaps brought forward into FC0-A

These are **not** the full future DEV missions. Fix only the already-confirmed contract-consumption/usability gaps so parallel DEVs start from a cleaner base.

### C1 — DEV-EDITOR known-legacy compatibility consumption

Advanced authoring paths that still use strict built-in lookup must consume the frozen FND-06 Engineering compatibility seam.

Cover known persisted `tank | value | dynamo | status` for semantically valid:
- marquee/topmost selection geometry;
- move/resize;
- z-order;
- align/distribute/size/multi-object operations where supported.

Rules:
- use `getVisualSchemaForEngineering` or the single frozen equivalent;
- no second alias/compatibility table;
- arbitrary unknown remains contained/fail-closed;
- preserve legacy-specific authored fields.

This does not require the full DEV-EDITOR single-canvas product mission.

### C2 — DEV-SCRIPT Script Assistant compatibility

Replace direct strict built-in schema lookup in Script Assistant/property discovery with the frozen FND-06 Engineering compatibility seam.

Acceptance:
- known legacy `tank | value | dynamo | status` expose shared compatible properties;
- arbitrary unknown remains unknown/contained;
- no duplicate registry.

### C3 — Script API Help maturity

Do not reimplement cursor-aware insertion or timer/tagChanged authoring; those already exist.

Close the confirmed Help/assistant gap by providing structured, testable API documentation for supported Script APIs:
- callable name;
- signature;
- parameters;
- return/result semantics;
- relevant failure/safety notes;
- concise validated examples.

Object/property addressing shown by Script Engineering must be compatible with the FND-06 Engineering schema seam.

Include one representative visible-UI recipe for reading/comparing two TAGs and conditionally changing an allowed visual property/state, using only shipped APIs and without bypassing Authority/Active boundaries.

### C4 — Runtime Session / Licensing UX consumption

Backend requested/granted/fallback authority already exists; consume it truthfully.

Add:
- explicit ViewOnly request UX;
- requested class display;
- server granted class display;
- fallback/rejection reason display;
- Interactive-quota fallback to ViewOnly messaging;
- useful current usage/capacity information where already exposed by the frozen backend contract.

Do not redesign FND-03 admission or quotas.

### C5 — W15-P2-07 Template/Equipment/Library inspection

Do not rebuild the existing Dynamo insertion palette; it already has preview/interface metadata.

Improve the residual surfaces:
- Templates: useful selected-item inspection/metadata before reuse;
- Equipment: useful selected-item inspection/metadata and template/binding context;
- reusable-library resources: preview/inspection before `Use`, including dependency closure and relevant resource metadata; visual preview where the canonical resource type supports it;
- Dynamos top-level listing may link/bridge to existing preview rather than duplicate another renderer.

Preserve association != import and Runtime independence from `.escadalib`.

## 5. Explicitly not part of this product mutation package

The following remain evidence-bounded or future-gated and must not receive speculative fixes:

- W15-U-01 forwarding/Codespaces latency without correlated product-owned evidence;
- W15-U-02 Alarm timestamp semantics without same-occurrence evidence;
- `RECHECK-SIM-PUMP-LEVEL` absent correlated reproduction;
- FND-05 HA implementation;
- FND-07 detach implementation itself;
- FND-07 Engineering Lock × detach acceptance guard is already recorded in its future control and becomes executable when FND-07 activates;
- protected whole-system backup/restore, Historian Administration, raw->engineering scaling, decimal-place authoring, real PLC variant and Wave13 signing unless separately activated.

## 6. Execution sequence

CODEX should execute in this order unless a dependency discovered in code makes a small reorder safer:

1. baseline revalidation + RED harnesses;
2. A1 Server Script recovery;
3. A2 Engineering truthful fallback;
4. B5/B6 runtime observability/freshness;
5. B1/B2/B3/B4 shared shell/Engineering UX;
6. B7/B8 Help + canonical wording;
7. C1/C2 compatibility-consumption fixes;
8. C3 Script API Help/recipe;
9. C4 Runtime Session/Licensing UX;
10. C5 Template/Equipment/Library inspection;
11. combined focused test matrix;
12. combined natural T1/profile validation selected from the live Wave 15 router for the **actual final diff**;
13. one PR to `wave15/corrections-integration`;
14. final candidate handoff to Main.

Do not request Main review between steps 2-10.

## 7. Validation requirements

At minimum, final candidate evidence must include:

### Server/runtime
- Server Script focused unit/integration tests;
- throttle/recovery RED/GREEN proof;
- stale-effect/authority/revision negatives;
- Trends + live-value observability tests.

### Web/Engineering
- mounted P1-06 loading/transport/HTTP/retry matrix;
- shell width/no-overflow browser matrix;
- account keyboard/focus accessibility test;
- Engineering scroll/layout test;
- Engineering Lock locked/unlocked regression;
- contextual Help mapping/locale regression;
- legacy Editor + Script Assistant compatibility regressions;
- Script API Help/recipe regression;
- Runtime session requested/granted/ViewOnly/fallback UX regression;
- Template/Equipment/Library inspection regression.

### Cross-contract
- FND-04 readable TAG binding regressions remain green;
- FND-06 mounted legacy selection regressions remain green;
- lifecycle/Authority/Licensing targeted tests remain green where touched;
- arbitrary unknown visual type remains contained;
- real authoritative `Demo Project` remains valid;
- zero/quality live-value behavior remains correct.

### CI
Use the live profile-aware Wave 15 router. Because this is intentionally cross-cutting, run every natural T1 profile selected by the final diff; do not borrow green evidence from earlier SHAs. If the router chooses the broad profile, that is acceptable.

## 8. Scope and file-collision rule

Because this package intentionally precedes parallel DEV release, CODEX is authorized to touch the bounded surfaces above even where they will later be primary-owned by a DEV lane.

However:
- do not implement unrelated future DEV backlog;
- do not refactor large unrelated areas;
- do not move shared contracts merely to make UI work easier;
- keep changes grouped by phase/commit where practical so Main can audit causality.

After this consolidated candidate is integrated, future DEV branches must start from the resulting exact FC0-A checkpoint and consume these corrections rather than reimplementing them.

## 9. Final handoff

Return only after the large package is complete or a real blocking condition prevents safe completion.

Required prefix:

`FC0-A CONSOLIDATED CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Include:
- exact base SHA/tree;
- exact head SHA/tree;
- branch and PR;
- commits grouped by phase;
- changed files grouped by phase;
- RED evidence;
- GREEN evidence per A/B/C item;
- tests and exact run IDs on exact final head;
- remaining incomplete items, if any;
- explicit confirmation that frozen FND-01/02/03/04/06/08 contracts were not redefined;
- explicit non-actions.

No self-merge, no freeze, no FC0-A release declaration.


## 10. Main review of first consolidated candidate — CHANGES_REQUIRED

First candidate reviewed by Main:

- PR: `#340`
- candidate head: `0591cf50d987219716d8aa054e4a1c0369ced1b5`
- candidate tree: `411267c65e2fac56aa59098287d2fe6443833d6d`
- base: `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- commits: 10
- changed files: 35
- natural Wave 15 T1: `36003179697` — **SUCCESS**
  - classification — SUCCESS
  - Focused Chromium evidence — SUCCESS
  - Web semantic build — SUCCESS
  - Common T1 sanity — SUCCESS
  - Focused .NET evidence — SUCCESS
  - final Wave 15 T1 gate — SUCCESS

Main accepts this as a strong first implementation but **not yet complete against the active package contract**.

### V1 areas accepted as directionally correct; preserve them

- A1 bounded Server Script cooldown/one-probe recovery state machine and focused tests;
- A2 truthful Engineering no-model/transport/HTTP/retry model;
- shared shell responsive CSS direction;
- desktop Engineering independent sidebar/workspace scrolling direction;
- compact/disclosed Engineering Lock management direction;
- canonical `viewOnly` wording changes;
- contextual Help route mapping foundation;
- FND-06 compatibility consumption in advanced Editor paths;
- Script Assistant consumption of the FND-06 compatibility seam;
- account Escape/focus regression;
- Template/Equipment binding inspection as a partial C5 improvement.

Do not regress or rewrite these without evidence.

### Required V2 residuals on the SAME branch and PR

#### R1 — B5 Trends observability is only partially implemented

Current V1 adds sample-age freshness, but the package required truthful observability of:
- realtime connection/reconnecting/disconnected state or an explicit equivalent;
- last request;
- last successful update;
- freshness age/reason;
- History error vs valid no-data vs realtime failure;
- retry/recovery behavior under unchanged Active authority.

A sample timestamp freshness badge alone does not close W15-P2-01.

If Basic Trend is intentionally historical-only, make that architecture explicit and surface the missing realtime/request/success truth in the shared live-data path rather than inventing a fake Trend socket.

Add mounted regression covering the chosen truthful model.

#### R2 — B6 shared live-value freshness is only partially implemented

Current `liveValueFreshness.ts` classifies timestamp age, but V2 must also expose/test:
- last request;
- last success;
- age value/threshold or equivalent diagnostic detail;
- stale/unavailable reason;
- reconnect/open/close/reopen behavior;
- distinction between no observation, transport failure and genuinely stale observation.

Preserve correct numeric zero and quality semantics.

Prefer one shared request/success/freshness model reused by Trend/TAG/Popup-compatible paths.

#### R3 — C3 Script API Help maturity was not delivered

The V1 Help delta documents the new Server Script cooldown policy, but does not implement the authorized structured Script API Help contract.

Complete:
- callable name;
- formal signature;
- parameters;
- return/result semantics;
- failure/safety notes;
- concise validated examples;
- one representative visible-UI recipe reading/comparing two TAGs and conditionally changing an allowed visual property/state using shipped APIs.

Do not reimplement cursor insertion or timer/tagChanged authoring.

Script/object-property examples must consume the frozen FND-06 Engineering schema seam.

#### R4 — C4 Runtime Session/Licensing UX is incomplete and current fallback handling needs correction

V1 correctly preserves server `requestedClass`, `grantedClass` and reason codes in the client helper, but does not yet provide the authorized user-facing flow:
- explicit ViewOnly request UX;
- requested class display;
- granted class display;
- fallback/rejection reason;
- current capacity/usage where already exposed by frozen backend contracts.

Additionally, current `admitInteractiveRuntimeSession` calls POST admission, then throws when the server grants `viewOnly`.

The backend has already created a valid lease before the helper throws. The V1 helper neither exposes that lease to a ViewOnly Runtime flow nor terminates it, so the client can abandon a valid server lease until expiry.

V2 must make fallback lifecycle truthful:
- either consume the granted ViewOnly lease in an explicit read-only flow;
- or explicitly terminate/release it before reporting rejection;
- never silently leave a server-created fallback lease orphaned;
- never treat ViewOnly as Interactive.

Add tests proving no orphaned/falsely-interactive fallback semantics.

#### R5 — C5 reusable-library preview/inspection remains incomplete

V1 improves Template/Equipment binding inspection, but `ReusableLibraryWorkspace` is unchanged.

Complete the residual authorized scope:
- inspect/preview a reusable-library resource before `Use`;
- show dependency closure and relevant canonical metadata;
- visual preview where the resource type already has a canonical renderer/preview path;
- preserve association != import and Runtime independence from `.escadalib`;
- do not create a second renderer.

Do not rebuild the existing Dynamo insertion palette.

#### R6 — close the missing explicit regressions for shared UX changes

The V1 product code changes are broader than its new browser regressions.

Add focused mounted evidence for:
- B1: no document-level horizontal overflow at representative widths around 901-1180 px and navigation/account/theme remain reachable;
- B2: account accessible name follows pt-BR/en/es in addition to Escape/focus restoration;
- B3: desktop Engineering uses bounded independent sidebar/workspace scrolling and preserves compact/mobile behavior;
- B4: collapsed Engineering Lock management remains discoverable and all configure/lock/clear operations still work under existing Authority;
- C1: known legacy `tank | value | dynamo | status` advanced authoring operations covered by the changed compatibility paths; arbitrary unknown remains contained.

Existing tests may be extended; do not duplicate expensive broad scenarios unnecessarily.

### V2 final evidence

After R1-R6:
1. update PR #340 body with exact completed matrix;
2. rerun natural Wave 15 T1 on the new exact head;
3. return one final handoff:
   `FC0-A CONSOLIDATED CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF V2`
4. include exact head/tree and T1 run ID;
5. no merge/freeze/release.

Main will not re-review intermediate commits. Continue until V2 is complete or a real frozen-contract/environment blocker prevents completion.


## 11. CODEX mandatory reconciliation checklist — preserve current execution flow

Checkpoint recorded when this checklist was created:

- PR: `#340`
- branch: `work/w15-fc0a-consolidated-corrections`
- checkpoint head: `6f19029b52641d5644fe0cf7365c119f7f87fa9a`
- checkpoint tree: `5120d7484d8b62dc9cf36880cf96b11566e58622`
- natural T1 on that checkpoint: `36005490966` — **SUCCESS**
- PR body state then: `CANDIDATE HANDOFF V2 (IN PROGRESS)`

### 11.A Binding interpretation — checklist is mandatory to REVIEW, not a forced execution sequence

The CODEX executor was already making good progress before this checklist was written. This section must **not** cause a task switch, loss of in-chat/runtime context, forced reordering, or abandonment of work already in progress.

Binding rules:

1. continue the technical sequence and implementation plan the CODEX was already following when its session stopped, unless live GitHub evidence requires a change;
2. treat the detailed items below as a **mandatory reconciliation checklist before final handoff**, not as an instruction to stop current work and execute them mechanically in numbered order;
3. the headings `Step 1`, `Step 2`, etc. below are suggested review grouping only; CODEX may implement/test in a different order when that is technically safer or preserves work already underway;
4. do not discard useful context, partial analysis, local test intent, or implementation decisions already present in the CODEX chat/runtime merely because they were not yet written to GitHub;
5. before final handoff, reconcile any such useful context with GitHub live and persist the relevant durable evidence/code/tests/documentation; **GitHub remains the final authority if there is any conflict**;
6. if CODEX discovers an additional gap while continuing its existing work, it may close that gap when it is inside the authorized FC0-A package and does not redefine a frozen contract;
7. do not skip an already-started safe piece of work solely because it is not named in this checklist; finish it if it belongs to the authorized package and prevents a later gap;
8. before declaring V2 complete, explicitly review every R2/R4/R5/R6 checklist item below and mark it `COVERED`, `NOT_APPLICABLE_WITH_EVIDENCE`, or `BLOCKED-CONTRACT`.

The purpose of this checklist is to prevent omissions at the end, not to micromanage the executor.

### 11.0 Do-not-repeat / preserve list

Do **not** reimplement, rewrite or reopen without a new failing proof:

- R1/B5 Historian polling truth + last request/last success/freshness diagnostics already added;
- R3/C3 structured Server Script API Help + signatures/parameters/result/safety/examples + TAG recipe already added;
- R4 critical orphan-lease fix already added: Interactive request that receives ViewOnly must terminate the unused fallback lease before rejection;
- R5 resource inspection already added: resource identity, payload path, dependency closure and metadata/canonical-renderer boundary;
- V1 accepted A1/A2, shell responsive direction, Engineering scroll direction, compact Lock direction, viewOnly wording, Help routing, FND-06 compatibility consumption, account Escape/focus, Template/Equipment binding inspection;
- do not create a second visual renderer, second compatibility registry, second Authority path or new FND-03 quota/admission contract.

### 11.1 R2 review group — shared live-value diagnostics

Goal: finish the **shared** diagnostics contract before adding more mounted tests.

Current implementation already has:
- `describeLiveValueDiagnostics`;
- `lastRequestAt`;
- `lastSuccessAt`;
- reasons `no-observation | invalid-observation | request-failed | fresh | aging | stale`;
- Trend and TAG Inspector initial consumption.

Complete only the residuals:

1. ensure the shared diagnostics presentation exposes:
   - observation age in useful human-readable form or the exact age value;
   - stale threshold / equivalent diagnostic threshold;
   - explicit reason when unavailable/stale;
2. preserve:
   - numeric zero as a valid value;
   - quality independently from value/freshness;
   - null/no-sample as distinct from zero;
   - transport failure as distinct from stale authoritative observation;
3. wire the same shared model to any existing Popup/live-value consumer **only if that consumer already exposes live-value status**; do not create a Popup-only parallel model;
4. verify realtime reconnect/open/close/reopen transitions do not:
   - invent observation timestamps;
   - reset freshness to fresh without a new authoritative observation;
   - discard the last successful observation merely because transport disconnected;
5. add/extend focused tests:
   - `web/scada-web/tests-e2e/live-value-freshness-contract.spec.ts`;
   - `web/scada-web/tests-e2e/runtime-tag-inspector-contract.spec.ts`;
   - the existing mounted Runtime/Popup spec that owns the live-value transport if applicable.

R2 DONE only when tests prove:
- Good zero remains zero;
- null/no-sample remains unavailable;
- non-Good quality remains visible independently;
- request failure != stale != no observation;
- reconnect/open/close/reopen preserves truthful last request / last success / observation state.

Commit suggestion:
`test/fix: close shared live value diagnostics contract`

### 11.2 R4 review group — Runtime Session / Licensing UX without backend redesign

Preserve the already-completed fallback lease termination.

Do not add new admission/quota semantics.

Backend facts already available and authoritative:
- `POST /api/runtime/sessions` returns `requestedClass`, `grantedClass`, `admissionReasonCode`, `capacityReasonCode`, `capacityReserved`;
- `GET /api/runtime/contract` exposes supported connection classes and admission reason-code vocabulary;
- terminate/heartbeat endpoints already exist;
- current backend does **not** expose an authoritative active-session count/usage counter in this contract.

Therefore:

1. add explicit user-facing Runtime Session control/status surface using the existing client helper:
   - selector/action for `viewOnly` vs `interactive`;
   - display requested class;
   - display server granted class;
   - display admission reason;
   - display capacity reason when present;
   - display whether the admission was accepted, downscoped or rejected;
2. on `viewOnly` request:
   - consume the granted ViewOnly lease as read-only;
   - never route it through Interactive-only mutation helpers;
3. on `interactive -> viewOnly` fallback:
   - keep current termination-before-rejection behavior unless the UI intentionally chooses to accept the ViewOnly fallback;
   - if UI accepts fallback, make that an explicit user-visible state/action, not silent automatic mutation;
4. capacity/usage:
   - show only facts already provided by frozen contracts;
   - do **not** invent active-seat counts or add a new backend capacity endpoint merely to satisfy the UI;
   - `capacityReasonCode`, supported classes, lease duration and reserved state are acceptable truthful facts;
5. place the UX in the smallest existing shared Runtime/Licensing surface that avoids duplicating session logic; prefer reuse of existing Runtime session helpers over a second client API;
6. add mounted regression proving:
   - explicit ViewOnly request;
   - Interactive request granted Interactive;
   - Interactive request downscoped to ViewOnly;
   - fallback reason shown;
   - no mutation path uses ViewOnly as Interactive;
   - rejected/unconsumed fallback lease is terminated;
   - termination failure is surfaced;
   - pt-BR/en/es labels remain coherent.

Recommended files to inspect/reuse:
- `web/scada-web/src/runtime/runtimeSessionAdmissionApi.ts`;
- `web/scada-web/src/licensing/LicensingApp.tsx`;
- existing Runtime shell/session surface;
- `web/scada-web/tests-e2e/runtime-session-admission-contract.spec.ts`;
- `web/scada-web/tests-e2e/wave-14-c25-runtime-session.spec.ts`.

R4 DONE only when the user can see requested/granted/reason truth and ViewOnly cannot be mistaken for Interactive.

Commit suggestion:
`feat: complete runtime session admission UX`

### 11.3 R5 review group — reusable-library preview with an explicit capability decision

Current inspection is already valid and must be preserved.

Before writing more production code, perform one bounded capability check:

1. inspect the current reusable-library resource DTO/API:
   - if the associated catalog already exposes enough canonical typed payload to feed an existing canonical preview/renderer **without incorporation**, reuse that exact renderer;
   - if it exposes only manifest metadata / payload path / dependency closure, do not invent a second parser or renderer;
2. for `screen | popup | dynamo`:
   - reuse an existing canonical preview component only if it can consume the existing canonical resource model directly;
   - otherwise retain metadata/payload/dependency inspection and explicitly label visual preview as unavailable before incorporation because the catalog does not expose renderable canonical content;
3. for non-visual resources:
   - metadata/package inspection is sufficient;
4. never:
   - import/incorporate merely to preview;
   - mutate Working during preview;
   - make Runtime depend on `.escadalib`;
   - duplicate FND-06 renderer/compatibility logic;
5. extend the existing reusable-library mounted test:
   - `web/scada-web/tests-e2e/wave-14-c25-reusable-libraries.spec.ts`;
6. prove:
   - inspection opens before Use;
   - resource ID/payload/dependencies visible;
   - preview either uses canonical renderer or explicitly reports metadata-only capability;
   - preview causes zero Working mutation;
   - Use still performs the existing validated incorporation path;
   - disassociation preserves incorporated content.

R5 DONE when the capability boundary is truthful and tested, even if the correct result for some associated resource kinds is “metadata-only until incorporation”.

Commit suggestion:
`test/fix: close reusable library preview boundary`

### 11.4 R6 review group — mounted regressions, grouped by existing owner spec

Do this only after R2/R4/R5 product deltas are stable.

#### R6-A — shell overflow / reachability

Extend `web/scada-web/tests-e2e/app-shell.spec.ts`.

Test representative widths:
- 1180;
- 1024;
- 901;
- optionally 900/760 as breakpoint guards.

Assertions:
- `document.documentElement.scrollWidth <= clientWidth`;
- Runtime/Engineering/Audit/Licensing/Help remain reachable according to current Authority;
- theme control remains reachable;
- account trigger remains reachable;
- no navigation item becomes unreachable solely because of overflow.

#### R6-B — localized account accessibility

Extend `wave-14-c25-runtime-session.spec.ts` or `session-menu-contract.spec.ts`.

For pt-BR / en / es:
- account trigger has expected accessible name;
- keyboard Enter/Space opens;
- Escape closes;
- focus returns to trigger;
- logout/switch actions remain keyboard-operable where authorized.

Do not rewrite the account component unless a test fails.

#### R6-C — Engineering scroll composition

Extend `app-shell.spec.ts`.

Desktop:
- `.eng-shell` is viewport-bounded;
- `.eng-body` does not force document scrolling;
- sidebar has independent vertical scroll;
- workspace has independent vertical scroll;
- visual editor canvas/panels remain usable.

Compact/mobile:
- breakpoint intentionally returns to document-flow behavior;
- no clipping of sidebar/workspace controls.

#### R6-D — Engineering Lock compact management

Extend `wave-14-c25-engineering-lock.spec.ts`.

Configured + unlocked:
- management is collapsed/compact by default;
- summary/status remains discoverable;
- expand and perform:
  - configure/replace secret;
  - configure + lock;
  - lock now;
  - clear;
- existing backend Authority behavior remains the source of truth.

Locked:
- protected workspace still never mounts;
- no regression to restricted administration/recovery surface.

#### R6-E — legacy advanced authoring + arbitrary unknown containment

Do **not** add a new compatibility registry.

Extend existing owner-model tests:
- `visual-editor-selection-model.spec.ts`;
- `visual-editor-authoring-model.spec.ts`;
- `visual-editor-z-order-model.spec.ts`;
- existing resize/move/toolbar spec where needed.

Create fixtures using known persisted legacy types:
- `tank`;
- `value`;
- `dynamo`;
- `status`.

Prove, where semantically supported:
- point/topmost selection;
- marquee selection;
- move;
- resize;
- z-order;
- align/distribute/size operations.

Also prove:
- arbitrary unknown type remains contained/fail-closed;
- no crash;
- no fabricated schema;
- legacy-specific authored fields survive operations.

### 11.5 Focused validation review before final T1

Run the smallest focused set first:

- live-value diagnostics specs;
- Runtime session/admission specs;
- reusable-library mounted spec;
- app-shell;
- Engineering Lock;
- legacy visual-editor model specs.

Then:
- Web production build;
- owning .NET tests if any backend/UI contract touched.

Do not rerun unrelated broad suites manually if natural T1 will own them.

### 11.6 Final reconciliation + PR update + exact-head natural T1

Only after the executor's current work is complete and the mandatory checklist reconciliation shows the required coverage:

1. update PR #340 body from `V2 (IN PROGRESS)` to final candidate handoff;
2. include an explicit matrix:
   - R2 COMPLETE;
   - R4 COMPLETE;
   - R5 COMPLETE;
   - R6 COMPLETE;
3. include any R5 metadata-only preview decision and why it preserves the canonical renderer boundary;
4. push final head;
5. let the natural Wave 15 T1 run on that exact head;
6. require all selected jobs + final gate SUCCESS;
7. return:

`FC0-A CONSOLIDATED CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF V2`

Include:
- exact final SHA/tree;
- commits added after `6f19029b...`;
- changed files grouped R2/R4/R5/R6;
- focused test results;
- exact T1 run ID;
- confirmation that no FND-01/02/03/04/06/08 contract was redefined;
- no merge/freeze/release.

### 11.7 Stop conditions

Stop and report `BLOCKED-CONTRACT` only if completing an item requires:
- a new FND-03 admission/quota contract;
- a second FND-06 renderer/compatibility registry;
- preview by mutating Working;
- weakening Engineering Lock/Authority;
- inventing unavailable Runtime/capacity facts.

Environment-only local Playwright identity setup is **not** a product blocker if the natural GitHub T1 provides the required mounted evidence on the exact head.


## 12. Main review of final V2 handoff — CHANGES_REQUIRED / narrow V3 closeout

Main reviewed the exact V2 candidate:

- PR: `#340`
- head: `150b808140a5fdf80afd0c88d46ea80f83f630b2`
- tree: `c2bfbbfe705be64e5c1aac314f96bf8851a913b0`
- commits: 15
- changed files: 41
- final natural T1: `36033152318` — **SUCCESS**
  - classification — SUCCESS
  - Common T1 sanity — SUCCESS
  - Focused .NET evidence — SUCCESS
  - Focused Chromium evidence — SUCCESS
  - Web semantic build — SUCCESS
  - Wave 15 T1 gate — SUCCESS
- integration branch remains product/infra-identical to frozen checkpoint outside the PR; its current divergence from `560ac9d...` is documentation only.

Main accepts the following V2 closures and they must not be reopened without new failing evidence:

- A1/P1-01 bounded Server Script recovery;
- A2/P1-06 truthful Engineering fallback;
- R1/B5 Trend polling/request-success/freshness truth;
- R2 shared freshness model and final TAG realtime reconnect state;
- R3 structured Script API Help + representative recipe;
- R4 orphan fallback-lease termination and mutation-path requested/granted/reason feedback;
- R5 metadata-only pre-Use library inspection boundary as `NOT_APPLICABLE_WITH_EVIDENCE` for rendered pixels because the associated catalog does not expose canonical renderable content;
- contextual Help routing, canonical `viewOnly` wording and accepted V1 shared UX directions.

However V2 does **not** fully satisfy the binding reconciliation checklist. Do not merge yet.

### V3-R1 — complete the actual user-facing Runtime Session Class surface

The V2 implementation exposes `admitRuntimeSession(requestedClass)` and requested/granted/reason data in the client API, and the mutation helper truthfully reports Interactive -> ViewOnly fallback.

But the current product candidate still lacks the user-facing control/status surface required by R4:

- `web/scada-web/src/licensing/LicensingApp.tsx` remains a license/status/install/remove UI and has no Runtime Session Class request/status control;
- `web/scada-web/src/runtime/application/RuntimeApplicationMount.tsx` has no ViewOnly/Interactive request/status surface;
- no changed product UI in PR #340 provides an explicit user action to request `viewOnly` and then displays the resulting requested class, granted class and reason codes.

Required bounded closeout:

1. add one minimal user-facing Runtime Session access surface in the most appropriate existing Runtime/Licensing UI;
2. allow an explicit `viewOnly` request and an explicit `interactive` request without inventing new backend authority;
3. show requested class, server granted class, admission reason and capacity reason when present;
4. make accepted/downscoped/rejected truth visible;
5. never route a ViewOnly grant into mutation helpers;
6. if the UI creates a lease that it will not actually consume, terminate it explicitly; do not add a second orphan-lease path;
7. do not invent active-seat counts/capacity facts not exposed by frozen FND-03;
8. add a mounted product-level regression for this UI.

Keep the scope bounded: do not absorb the full future DEV-LICENSING-UX mission or redesign FND-03.

### V3-R2 — close only the missing R6 evidence, not more product scope

Main re-read the exact-head owner tests.

The following checklist rows are not proven by the current PR:

#### Shell width/no-overflow
`app-shell.spec.ts` still contains the historical 1024x720 visual-editor viewport case but no explicit `scrollWidth <= clientWidth` matrix at approximately 1180 / 1024 / 901 px.

Add the bounded assertions required by R6-A. Production code changes only if the new test exposes a real regression.

#### Engineering scroll composition
The current `app-shell.spec.ts` does not explicitly prove the R6-C contract:
- desktop viewport-bounded shell;
- independent sidebar/workspace scrolling;
- compact/mobile return to intentional document-flow behavior without clipping.

Add focused mounted evidence. Product changes only on a real failure.

#### Engineering Lock compact management
Existing `wave-14-c25-engineering-lock.spec.ts` proves core lock/unlock/configure behavior, but does not explicitly prove the new compact/collapsed discoverability and the full configure/lock/clear lifecycle through that composition.

Extend the owner spec only as needed to cover the changed compact UI. Do not redesign Lock semantics.

#### Legacy advanced authoring containment
The current selection/authoring/z-order owner specs do not explicitly exercise known persisted legacy `tank | value | dynamo | status` through the advanced paths changed by this package, nor the arbitrary-unknown negative across those paths.

Add focused model/mounted regressions for the operations actually touched by PR #340:
- selection/topmost/marquee as applicable;
- move/resize/z-order/align-distribute-size where the changed model participates;
- arbitrary unknown remains contained/fail-closed;
- legacy-specific authored fields survive.

Do not broaden into the full future DEV-EDITOR mission.

### V3 completion rule

Continue on the SAME branch and PR:

- branch: `work/w15-fc0a-consolidated-corrections`
- PR: `#340`

Preserve all accepted V2 work.

No intermediate Main review is required.

After V3-R1/R2:
1. run the smallest focused specs;
2. run Web build;
3. update PR body with exact V3 reconciliation;
4. run natural Wave 15 T1 on the exact new head;
5. return:

`FC0-A CONSOLIDATED CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF V3`

Include exact SHA/tree, changed files for this narrow closeout and exact T1 run ID.

No merge/freeze/release.


## 13. Main review of V3 handoff — product delta accepted / FINAL TEST-ONLY closeout

Main reviewed exact V3 candidate:

- PR: `#340`
- head: `1efc16994ea7b11857b0a1aac7da1690276e6d07`
- tree: `da022f95a0c2342188a34ffe6dc830acf84d873a`
- commits: 17
- changed files: 44
- natural T1: `36036316797` — SUCCESS on exact head.

### Accepted V3 product delta

V3-R1 is accepted.

`RuntimeSessionClassPanel.tsx` now provides:
- explicit ViewOnly request;
- explicit Interactive request;
- requested class display;
- granted class display;
- admission reason display;
- capacity reason display;
- explicit session end;
- disposal-time release of a user-owned lease;
- no reuse of this user-owned ViewOnly grant as mutation admission.

The mounted Runtime session spec includes a bounded requested/granted/end regression.

The shell reachability/no-horizontal-overflow matrix at 1180 / 1024 / 901 is also present.

Do not redesign or reopen these product changes without new failing evidence.

### Why merge remains blocked

The V3 handoff stated that remaining R6 Engineering scroll, compact Engineering Lock lifecycle and legacy advanced-authoring/unknown containment were covered by existing owner specs.

Main independently read the exact V3 tree and that statement is not sufficiently supported.

#### Missing evidence A — Engineering scroll composition

Current `app-shell.spec.ts` proves visual-editor panel internal overflow and panel collapse/reclaim behavior, but does not explicitly prove the binding R6-C contract:
- desktop `.eng-shell` is viewport-bounded;
- Engineering document/body does not become the unintended primary scroll surface;
- Engineering sidebar has independent vertical scrolling;
- Engineering workspace has independent vertical scrolling;
- compact/mobile intentionally returns to document-flow behavior without clipping.

#### Missing evidence B — compact Engineering Lock lifecycle

Current `wave-14-c25-engineering-lock.spec.ts` proves:
- locked workspace does not mount;
- wrong/correct unlock;
- configure-and-lock.

It does not explicitly prove the changed compact-management composition required by R6-D:
- compact/collapsed-by-default state for configured + unlocked;
- status/summary remains discoverable;
- expanding management exposes the lifecycle;
- lock-now path;
- clear path;
- backend Authority remains source of truth.

#### Missing evidence C — legacy advanced authoring

Current legacy coverage is concentrated in Property Inspector / Binding / Dynamic Property compatibility.

The exact current:
- `visual-editor-selection-model.spec.ts`;
- `visual-editor-authoring-model.spec.ts`;
- `visual-editor-z-order-model.spec.ts`

do not exercise persisted legacy `tank | value | dynamo | status` across the advanced authoring models changed by this package, and do not carry the arbitrary `vendor.unknown-x` negative through those paths.

The old tests are useful but are not the explicit R6-E proof Main ordered.

### Validation-routing finding

Natural T1 `36036316797` is green, but its Chromium job selected only:

- `python-runtime-host.spec.ts`;
- `runtime.spec.ts`;
- `script-engineering-workspace-contract.spec.ts`;
- `visual-editor-workspace.spec.ts`;
- plus local-auth bootstrap.

It did **not** execute:
- `wave-14-c25-runtime-session.spec.ts`;
- `app-shell.spec.ts`;
- `wave-14-c25-engineering-lock.spec.ts`;
- `visual-editor-selection-model.spec.ts`;
- `visual-editor-authoring-model.spec.ts`;
- `visual-editor-z-order-model.spec.ts`.

Therefore T1 green is valid for the selected profile-owned suite, but it is not evidence for these closeout rows.

### FINAL closeout scope — tests/validation only

Continue SAME branch and SAME PR.

No new product feature is authorized.

1. Add explicit Engineering scroll-composition evidence to the existing owner spec.
2. Add explicit compact Engineering Lock lifecycle evidence, including lock-now and clear.
3. Add explicit known-legacy advanced authoring + arbitrary-unknown containment evidence to the existing owner-model specs.
4. Re-run the new Runtime Session Class mounted test plus shell-width test, because natural T1 did not execute that spec.
5. Run the smallest focused Playwright/model set covering:
   - Runtime Session Class + shell width;
   - Engineering scroll;
   - Engineering Lock;
   - legacy selection/authoring/z-order.
6. Persist exact focused command/result in the PR handoff.

Production code may change only if one of these newly required tests exposes a real defect directly inside the already-authorized FC0-A correction package. No scope expansion.

### T1 proof rule

After focused evidence is green:
- rerun natural T1 on the exact final head;
- T1 must again be SUCCESS;
- do not claim that T1 itself executed the owner specs unless the router actually selected them.

If the local CODEX environment cannot execute the required focused Playwright specs because of the known first-run identity environment limitation, CODEX may make the smallest validation-router change necessary to run these exact owner specs in natural T1, with router tests, rather than weakening or skipping the evidence.

Do not broaden the router beyond the exact profiles/specs needed for this closeout.

### Required final handoff

Return:

`FC0-A CONSOLIDATED CODEX -> MAIN COORDINATOR — FINAL CANDIDATE HANDOFF V4`

Include:
- exact final SHA/tree;
- tests added/changed;
- exact focused commands;
- exact focused results;
- natural T1 run ID;
- whether owner specs ran locally or via T1 routing;
- confirmation that no new feature scope was added;
- no merge/freeze/release.

This is intended to be the final evidence-only closeout before Main integration decision.


## 14. Main review of V4 handoff — tests accepted / FINAL EXECUTION PROOF ONLY

Main reviewed exact V4 candidate:

- PR: `#340`
- head: `87eafb68e4fea7815a26ccffeb8a070fae6564c8`
- commit: `test: complete FC0-A V4 closeout coverage`
- V4 delta: test-only;
- natural T1: `36037962003` — SUCCESS.

The V4 test additions are directionally accepted:

- `app-shell.spec.ts` now has explicit desktop/compact Engineering scroll-composition assertions;
- `wave-14-c25-engineering-lock.spec.ts` now covers compact management discovery and lock/clear request paths;
- `visual-editor-authoring-model.spec.ts` now carries known legacy `tank | value | dynamo | status` through align/size/z-order while preserving legacy-specific fields.

No V4 production code changed.

### Remaining blocker is execution proof, not product code

Main inspected the exact T1 Chromium log for `36037962003`.

It selected only:
- `python-runtime-host.spec.ts`;
- `runtime.spec.ts`;
- `script-engineering-workspace-contract.spec.ts`;
- `visual-editor-workspace.spec.ts`;
- local-auth bootstrap.

It did **not** execute:
- `wave-14-c25-runtime-session.spec.ts`;
- `app-shell.spec.ts`;
- `wave-14-c25-engineering-lock.spec.ts`;
- `visual-editor-authoring-model.spec.ts`;
- `visual-editor-selection-model.spec.ts`;
- `visual-editor-z-order-model.spec.ts`.

Therefore V4 T1 is green but still does not prove the newly added closeout specs.

### FINAL required action — smallest CI routing correction

No new product feature is authorized.

Use the existing Wave 15 profile router so the natural T1 itself runs the owner specs needed for this package.

Preferred bounded implementation:

1. change `scripts/ci/wave15_profile_router.py` so one validation profile may own **multiple** E2E specs instead of exactly one;
2. preserve all current mappings;
3. extend only the relevant mappings:
   - `UI_EDITOR` must include:
     - `tests-e2e/visual-editor-workspace.spec.ts`;
     - `tests-e2e/app-shell.spec.ts`;
     - `tests-e2e/wave-14-c25-engineering-lock.spec.ts`;
     - `tests-e2e/visual-editor-authoring-model.spec.ts`;
     - `tests-e2e/visual-editor-selection-model.spec.ts`;
     - `tests-e2e/visual-editor-z-order-model.spec.ts`;
   - `RUNTIME_RENDERER` must include:
     - `tests-e2e/runtime.spec.ts`;
     - `tests-e2e/wave-14-c25-runtime-session.spec.ts`;
4. add/update `tests/ci/test_wave15_profile_router.py` so the expanded deterministic spec set is itself unit-tested;
5. do not add a one-off FC0-A-only validation profile and do not weaken existing profile inference.

This is CI/validation infrastructure only.

### Two test-strengthening requirements before final T1

Because this should be the last closeout cycle, strengthen two assertions now:

#### Lock lifecycle

The compact Lock test must prove backend-returned state, not only that requests were sent.

- assert management is collapsed by default and summary/status is visible;
- for lock-now, return truthful `lockedStatus` and assert the protected workspace becomes restricted/unmounted;
- prove clear separately from a configured+unlocked state, returning `configured:false, locked:false`, and assert configuration UI returns.

Do not change Lock product semantics unless the stronger test exposes a real defect.

#### Unknown legacy containment

The known legacy positive remains good.

Also add an explicit negative using `vendor.unknown-x` through strict advanced consumers:
- strict selection/schema-dependent path must reject with a contained diagnostic/error rather than fabricate a schema;
- z-order/authoring must not mutate an arbitrary unknown as if it were a known legacy type;
- the test must assert rejection/containment, not merely leave the unknown unselected.

Use existing canonical behavior; no new compatibility registry.

### Final proof

Push the final validation-only head and let natural T1 run.

The Chromium log must visibly include the owner specs above.

Require:
- Common sanity SUCCESS;
- Web build SUCCESS;
- .NET selected evidence SUCCESS;
- Chromium owner specs SUCCESS;
- final T1 gate SUCCESS.

Then return:

`FC0-A CONSOLIDATED CODEX -> MAIN COORDINATOR — FINAL INTEGRATION HANDOFF V5`

Include:
- exact SHA/tree;
- router/test files changed;
- exact T1 run ID;
- Chromium spec list from the log;
- total pass/fail result;
- confirmation of no production feature change;
- no merge/freeze/release.

If this passes, Main may proceed directly to protected merge and post-merge FC0-A gate without another feature review.
