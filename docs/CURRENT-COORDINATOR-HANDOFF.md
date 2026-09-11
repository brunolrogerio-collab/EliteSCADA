# Current Coordinator Handoff

> **GitHub live is the official memory and sole authority for EliteSCADA.** Revalidate live before every decision, diagnosis, documentation/code write, PR action, workflow rerun, integration or merge. Any divergence here is resolved in favor of GitHub.

## Coordination model

Wave 14 keeps the **Main Coordinator + diagnostic co-coordinator** model.

- **Main Coordinator:** the ChatGPT coordination session designated by the Product Owner. It owns classification, sequencing, closure criteria, integration and guardrails.
- **GPT Codex:** diagnostic co-coordinator with direct Codespace/application/browser/local-port access. It owns correlated live diagnosis/evidence capture where that access is necessary.
- **Product Owner:** retains product direction and maturity decisions.

Primary coordination ledger: issue #286.

Canonical collaboration protocol:

`docs/WAVE14-MAIN-COORDINATOR-CODEX-COLLABORATION-PROTOCOL.md`

Canonical current strategic closure plan:

`docs/WAVE14-DIAGNOSTIC-CLOSURE-AND-WAVE15-TRANSFER.md`

## Current phase — Wave 14 diagnostic closure

Product Owner direction on 2026-09-10 BRT changes the remaining Wave 14 objective.

Wave 14 is no longer the home for a growing set of newly discovered product corrections. Its remaining job is:

**finish diagnosis -> identify what/where/why/how to correct -> document evidence, mechanism, correction contract and deterministic regression -> package the transfer -> integrate accepted Wave 14 baseline to `main` -> close Wave 14.**

The actual corrections then belong to **Wave 15**.

Wave 13 remains preserved and paused until EliteSCADA is materially more mature and the Product Owner explicitly decides release/signing should resume.

## Live topology checkpoint

Repository: `brunolrogerio-collab/EliteSCADA`

At the strategic-pivot checkpoint:

- `main`: `edbdf446ea657713bdc487be91bf10bfcd03c684`;
- Wave 14 integration PR #212: OPEN/DRAFT, head `ff185ffd67fe4abc597af9184c21f86376ba6e17`;
- canonical C11 PR #263: OPEN/DRAFT, head `19d5257d970f53ae798c5fa53946fce07c586452`;
- accepted C26 PR #287: MERGED; accepted C26 product `08e2530671de10d48933c4b712a1a1abc9e41dce`;
- post-C26 audit/coordination PR #290: OPEN/DRAFT / Preview-only; documentation HEAD moves as closure evidence is recorded;
- exact technically validated post-C26 Preview candidate remains `59e815eae524b9ff043ea6bf3f797f4c01ba9143`;
- frozen package SHA-256 remains `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`;
- PR #296 remains DIAGNOSTIC ONLY / MUST NEVER MERGE / no blind rerun;
- #285 remains historical pre-C26 Preview evidence;
- Wave 13 #205/#207 remains paused.

Always revalidate these refs live; this file is a checkpoint, not authority over later GitHub state.

## Important product-route distinction

Comparison of canonical C11 `19d5257...` to technically validated Preview candidate `59e815e...` shows Preview/devcontainer/workflow/audit-harness additions rather than a successor product correction baseline.

Therefore:

- PR #290 remains evidence/Preview only and is not the Wave 14 route to `main`;
- canonical C11 must flow through #263 into `wave14/corrections-integration`;
- final selected closure/diagnostic/roadmap docs must be propagated onto the integration route separately;
- #212 remains the only intended Wave 14 route to `main`.

## Product Owner merge authorization update

The Product Owner's 2026-09-10 direction — close diagnostics, close Wave 14, send it to `main`, then open Wave 15 for corrections — supplies the previously required separate authorization for #212 -> `main`, **subject to completion of the Wave 14 closure package and exact-SHA validation**.

This authorization is conditional, not immediate. Do not merge #212 until:

1. the diagnostic transfer is complete enough that Wave 15 does not need to rediscover the known defects merely to locate the work;
2. #263/C11 has been correctly integrated into the Wave 14 integration branch;
3. selected final closure documentation is on the integration route;
4. exact integration-head universal and impact-required gates are green;
5. any red gate has been diagnosed, not blindly rerun;
6. #212 head/base and expected head SHA have been revalidated immediately before merge.

No authorization exists to merge #290, #296 or any validation-only / MUST-NEVER-MERGE PR.

## Current diagnostic priorities — diagnosis/documentation only

### 1. Engineering transport / `Failed to fetch` / latency

Use the direct Codespace lane to correlate forwarded browser, local Vite 5173, local API 5080, auth/proxy/forwarding, process health and logs in the same reproduction window. Record where the divergence begins and separate external environment behavior from product recovery UX. Do not implement a Wave 14 product patch from uncertain transport evidence.

### 2. Working `demo` vs Runtime Active `eee-demo`

Identify the exact bootstrap/checkout/persistence/public-model origin that leaves Engineering on `demo`, while preserving that lifecycle/Active authority correctly rejects cross-project activation. Record responsible path and Wave 15 correction contract.

### 3. persisted legacy visual schema crash

Close the generic mechanism for persisted legacy visual identifiers (`tank`, `value`, `dynamo` observed). Identify lookup/migration/recovery path, known-legacy contract and truly-unknown behavior. Future Wave 15 regression must cover Screen + Popup + unknown-type negative case. No new Wave 14 product fix.

### 4. Runtime Trends silent return

Locate the failure among Historian/no-data handling, realtime subscription, projection, route/subview state or error recovery. Define future data/no-data/error regressions.

### 5. Runtime Popup `—` / auto-return

Locate binding/realtime/projection/navigation cause and preserve stopped/running, zero/missing, quality and navigation regressions.

### 6. Engineering recovery/fallback identity

Even when initiating transport failure is external, diagnose the product path that can present fallback/fictitious Working identity. Preserve the Wave 15 UX/state correction contract.

### 7. deterministic UI/editor backlog

Preserve code/component location and future acceptance for responsive header, account accessibility, Engineering navigation/scroll, Engineering Lock footprint, resource previews, residual theme/state defects, TAG/source identity coherence and remaining accessible-name defects.

### 8. Script Engineering / PO-PRE-07

Finish diagnosis of the gap between implemented internals/snippets and a developer-complete workflow: project object/TAG discovery, authoring, validation/errors, trigger/binding/lifecycle integration, persistence and observable/debuggable runtime behavior.

## Developer-functional priority for Wave 15

The Product Owner explicitly considers Screen Editor and Script Engineering currently too far from functional for a developer. Treat this as a correctness/maturity problem, not polish.

Wave 15 must make representative Screen/Popup authoring and Script Engineering possible end-to-end through the product UI without repository/database/manual API intervention. Supported visible controls must work; unsupported actions must not masquerade as functional. Script authoring must be discoverable, diagnosable and observable at runtime.

## RECHECK-SIM-PUMP-LEVEL

Not currently confirmed as a product freeze. Later local evidence showed dynamic LevelPct while public forwarding failed. PR #296's last known diagnostic run failed before effective observation. Reopen only if the freeze occurs naturally and local/browser/WS/TAG/process evidence can be captured before restart/reopen. No blind rerun.

## Immediate next sequence

1. update #286 so Codex/current coordinators know Wave 14 is diagnosis/documentation only;
2. complete the finding transfer table: finding -> evidence -> classification -> mechanism -> responsible layer/path -> Wave 15 correction contract -> deterministic regression -> dependency;
3. close remaining high-value diagnostic gaps using live Codespace access where necessary;
4. when the transfer is complete, revalidate and integrate #263 into `wave14/corrections-integration`;
5. propagate selected closure/roadmap/handoff documentation to the integration route without merging Preview harness content;
6. exact-SHA validate integration;
7. conditionally merge #212 to `main` using expected-head protection;
8. validate exact new `main`;
9. close Wave 14 issue/audit/diagnostic surfaces as appropriate without merging MUST-NEVER-MERGE PRs or deleting evidence branches;
10. create Wave 15 issue/branch from exact validated new `main` and execute corrections;
11. after Wave 15 corrections, create a fresh Codespace Preview and run technical readiness + real-browser audit + diagnostic/log review;
12. keep Wave 13 paused until a later explicit maturity decision.

## Read first

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/WAVE14-DIAGNOSTIC-CLOSURE-AND-WAVE15-TRANSFER.md`;
5. live #286 and latest comments;
6. live #211, #212, #263, #289, #290 and #296 as relevant;
7. `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` latest chronological sections;
8. `docs/WAVE14-POST-C26-CODEX-COORDINATOR-HANDOFF-2026-09-10.md` for historical audit detail.

## Permanent guardrails

- GitHub live wins over stale docs/chat for current state;
- no direct `main` mutation; use the authorized PR route;
- no force push, destructive rebase or branch deletion;
- no blind workflow rerun;
- never weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics or drivers;
- Runtime/Active remains independent of `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- no EEE-specific workaround for a generic platform defect;
- uncertain diagnostics must remain explicit rather than being falsely closed;
- Wave 15 completion does not automatically resume Wave 13.

Current decision:

`WAVE14 = FINISH DIAGNOSIS + DOCUMENT + TRANSFER -> INTEGRATE ACCEPTED BASELINE TO MAIN -> CLOSE -> WAVE15 = CORRECTIONS + DEVELOPER-FUNCTIONAL MATURITY -> FRESH PREVIEW + AUDIT -> PRODUCT OWNER MATURITY DECISION -> WAVE13 REMAINS PAUSED UNTIL SEPARATELY RESUMED`
