# LAST CHANGE — EliteSCADA

**Date:** 2026-09-10 BRT

**Operational state:** **WAVE 14 DIAGNOSTIC CLOSURE ACTIVE / NO NEW BROAD PRODUCT CORRECTIONS IN W14 / W15 WILL OWN CORRECTIONS / W13 PRESERVED AND PAUSED UNTIL PRODUCT MATURITY / #212 CONDITIONALLY AUTHORIZED ONLY AFTER W14 CLOSURE PACKAGE + EXACT-SHA GREEN**

> GitHub live is the official and sole project memory. Revalidate refs, PR/issue state, exact files and workflows before every decision, diagnosis, code/documentation write, PR action, rerun, integration or merge. If this file differs from GitHub live, GitHub wins.

## Product Owner strategic decision — 2026-09-10 BRT

Wave 14 grew beyond a useful execution boundary. Its remaining goal is now deliberately narrowed:

**finish diagnostics, identify what/where/why/how each material defect should be corrected, preserve the evidence and correction contract, then close Wave 14 and integrate the accepted Wave 14 baseline into `main`.**

Newly diagnosed product corrections move to **Wave 15** rather than continuing to expand Wave 14.

After Wave 15 corrections are complete, EliteSCADA will receive a fresh Codespace Preview, technical readiness checks, real browser audit and diagnostic/log review. That validation may produce targeted follow-up corrections before a new maturity decision.

Wave 13 #205/#207 remains preserved and paused. It will resume only when the SCADA is materially more mature and the Product Owner explicitly decides release/signing is worthwhile. Wave 15 completion does not automatically resume Wave 13.

Canonical new closure plan:

`docs/WAVE14-DIAGNOSTIC-CLOSURE-AND-WAVE15-TRANSFER.md`

## Current live topology at decision checkpoint

- Repository: `brunolrogerio-collab/EliteSCADA`
- `main`: `edbdf446ea657713bdc487be91bf10bfcd03c684`
- Wave 14 integration PR #212: OPEN/DRAFT, `wave14/corrections-integration` -> `main`, head `ff185ffd67fe4abc597af9184c21f86376ba6e17`
- Canonical C11 PR #263: OPEN/DRAFT, `wave14/c11-canonical-eee-demo` -> `wave14/corrections-integration`, head `19d5257d970f53ae798c5fa53946fce07c586452`
- C26 PR #287: MERGED into canonical C11; accepted C26 product head `08e2530671de10d48933c4b712a1a1abc9e41dce`
- Post-C26 audit/coordination PR #290: OPEN/DRAFT / Preview-only; current documentation head after the strategic decision begins with `d18cea9c2a955a63adb917febcd0914d7ca5b62f`
- Exact technically validated post-C26 Preview candidate remains `59e815eae524b9ff043ea6bf3f797f4c01ba9143`; later Preview commits are documentation/evidence and do not become product candidates automatically
- Frozen package SHA-256 remains `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`
- PR #296 remains OPEN/DRAFT / DIAGNOSTIC ONLY / MUST NEVER MERGE; no blind rerun
- validation-only PRs remain validation-only / MUST NEVER MERGE where marked
- #285 remains preserved historical pre-C26 Preview evidence

## Important branch-content distinction

Comparison of canonical C11 `19d5257...` to validated Preview candidate `59e815e...` shows the Preview candidate adds Preview/devcontainer/workflow/audit-harness material, not a new product correction baseline. Therefore Wave 14 closure must not merge PR #290 as a route to `main`.

The intended product route remains canonical C11 -> Wave 14 integration -> `main`, with selected final diagnostic/roadmap/handoff documentation propagated separately onto the integration route before the final merge.

## Current Wave 14 diagnostic closure work

The direct-Codespace Codex assignment remains useful but its purpose is now **diagnosis and documentation only**, not product correction.

Priority diagnostic package:

1. Engineering `Failed to fetch` / route latency: correlate forwarded browser vs Vite 5173 vs API 5080 vs auth/proxy/process/log evidence and distinguish environment transport from product recovery behavior.
2. Working `demo` vs Runtime Active `eee-demo`: identify exact bootstrap/checkout/persistence/public-model origin while preserving correct lifecycle/Active authority.
3. persisted legacy visual schema crash: identify exact compatibility/migration/recovery path for known legacy types and preserve Screen + Popup + truly-unknown future regression contract.
4. Runtime Trends silent return: identify Historian/no-data/realtime/projection/navigation/error-recovery mechanism.
5. Runtime Popup `—` / auto-return: identify binding/realtime/projection/navigation mechanism and future stopped/running/zero/missing/quality regressions.
6. Engineering fetch/recovery UX: separate external transport cause from product responsibility not to present fictitious Working identity.
7. deterministic UI/authoring backlog: preserve component/code location and future regression contract.
8. Script Engineering / PO-PRE-07: finish diagnosis of the gap between existing implementation and a discoverable developer-complete authoring/debugging workflow.

`RECHECK-SIM-PUMP-LEVEL` remains not confirmed as a product freeze. Reopen only on a natural reproduction with correlated local evidence before restart/reopen.

## Developer-functional priority transferred to Wave 15

The Product Owner explicitly considers the current Screen Editor and Script Engineering too far from functional for a developer.

Wave 15 must therefore treat developer usability as product correctness, not cosmetic polish.

A representative developer must be able to complete Screen/Popup authoring and Script Engineering end-to-end through the product UI without repository/database/manual API intervention. Visible supported controls must work; unsupported actions must not masquerade as functional. Script development must include discoverability, object/TAG selection, understandable validation/errors, binding/trigger/lifecycle integration, persistence and observable/debuggable runtime behavior.

## Wave 14 integration authorization

The Product Owner decision of 2026-09-10 supplies the previously required separate authorization for PR #212 -> `main`, but **only after** all of the following are true:

- the Wave 14 diagnostic closure package is complete enough to transfer work without rediscovery;
- canonical C11 is correctly integrated through #263 into `wave14/corrections-integration`;
- final Wave 14 diagnostic/roadmap/handoff documentation is propagated onto the integration route;
- the exact integration head is validated by universal and impact-required gates;
- any red gate is diagnosed rather than blindly rerun;
- the expected PR head is revalidated immediately before merge.

This is not authorization to merge #212 early, merge Preview #290, merge diagnostic #296, merge validation-only PRs, bypass CI, or mutate `main` directly.

## Immediate next safe sequence

1. keep Codex focused on diagnosis/evidence; supersede any assumption that Wave 14 should implement the newly diagnosed fixes;
2. complete a Wave 14 diagnostic transfer table with finding -> mechanism -> responsible layer/path -> correction contract -> deterministic regression -> dependency;
3. reconcile the latest audit chronology and close remaining high-value diagnostic gaps;
4. revalidate #263 and integrate canonical C11 into `wave14/corrections-integration` when the diagnostic closure is ready for final packaging;
5. propagate selected Wave 14 closure documentation from Preview/coordination into the integration route without merging Preview harness content;
6. exact-SHA validate the integration head;
7. merge #212 to `main` only if the conditional authorization contract above is satisfied;
8. validate exact new `main`;
9. close Wave 14 coordination/audit/obsolete diagnostic surfaces without merging MUST-NEVER-MERGE PRs and without deleting preserved branches;
10. only then open Wave 15 from exact validated new `main` and execute the corrections backlog;
11. after Wave 15 corrections, create a fresh Codespace Preview and repeat audit/diagnostic reading against the corrected baseline;
12. keep Wave 13 paused until a later explicit product-maturity decision.

## Permanent governance

- GitHub live wins over chat or stale docs for implemented/current state;
- no direct mutation of `main`; use the authorized PR route;
- no force push, destructive rebase or branch deletion;
- no blind workflow rerun;
- never weaken tests, validation, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics or Runtime Active authority;
- Runtime/Active remains independent of `.escadalib`;
- no EEE-specific workaround for a generic product defect;
- Alarm / Operational Event / Audit remain distinct;
- uncertain diagnostics remain explicitly uncertain rather than being relabeled solved;
- Wave 15 correction completion does not automatically resume Wave 13.
