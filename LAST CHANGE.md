# LAST CHANGE — EliteSCADA

**Date:** 2026-09-10 BRT

**Operational state:** **WAVE14 POST-C26 / REAL AUDIT EVIDENCE PRESERVED / CODEX DIRECT-CODESPACE COORDINATION PREFERRED / P1 DIAGNOSIS + CORRECTIONS REQUIRED / FINAL PO HOMOLOGATION BLOCKED / #212 NOT AUTHORIZED / VALIDATION-ONLY PRs MUST NEVER MERGE / WAVE13 PAUSED**

> GitHub live is the official and sole project memory. Revalidate refs, PR/issue state, exact files and workflows before every decision, diagnosis, code/documentation write, PR action, rerun or merge. If this file differs from GitHub live, GitHub wins.

## What changed in this rotation

Coordination is being transferred preferably to the Codex chat/session that performed the real browser audit and has direct access to the Codespace and application. This is intended to improve diagnosis of findings where the browser, private Codespaces forwarding, local Vite/API, realtime and product state must be correlated in the same instant.

A new canonical handoff was added:

`docs/WAVE14-POST-C26-CODEX-COORDINATOR-HANDOFF-2026-09-10.md`

`docs/CURRENT-COORDINATOR-HANDOFF.md` and `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md` were refreshed from their stale pre-C26 state.

No product code, package, Runtime configuration, security/lifecycle authority or `main` state was changed by this coordinator rotation.

## Current topology

- Repository: `brunolrogerio-collab/EliteSCADA`
- Audit/coordination surface: `preview/wave14-post-c26-work-audit`
- Coordinator issue: #286
- Post-C26 audit gate: #289
- PR #290: post-C26 Preview — OPEN/DRAFT / Preview only
- PR #296: diagnostic-only long-run probe — OPEN/DRAFT / **MUST NEVER MERGE**
- PR #212: Wave14 integration -> `main` — **NO MERGE without later, separate and explicit Product Owner authorization**
- #266 / #288 / #292 / #293: validation-only / **MUST NEVER MERGE** where applicable
- #285: preserve as historical pre-C26 Preview evidence
- Wave13 #205/#207: paused

## Technical baseline preserved

- corrected canonical C11: `19d5257d970f53ae798c5fa53946fce07c586452`;
- accepted C26 product: `08e2530671de10d48933c4b712a1a1abc9e41dce`;
- exact technically validated post-C26 Preview candidate: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`;
- frozen package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`;
- Post-C26 Canonical Preview run `34403903462` — SUCCESS;
- Post-C26 Audit State Readiness run `34403903471` — SUCCESS.

Later commits on the Preview branch preserve audit documentation/evidence. Do not treat the documentation HEAD as a newly validated product SHA.

## Important evidence update

Earlier checkpoint `#289 / 5619800919` classified `RECHECK-SIM-PUMP-LEVEL` as a provisional P1 candidate because the browser audit saw a process snapshot frozen for >15 minutes.

Later direct Codespace evidence recorded in `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` showed authenticated local API values continuing to change, including `EEE.P01.LevelPct`, while local API/Vite stayed healthy and the public browser path encountered Codespaces forwarding/authentication failures.

Therefore **simulation/Server Script freeze is not currently a confirmed product defect**. Reopen that hypothesis only if reproduced again with correlated local diagnostics.

PR #296 remains diagnostic-only. Latest recorded head at handoff: `7738b568a5dd4259e958a2c5023c2bf6ca7e5acb`. Latest recorded run `34505442984` / check `102966371728` failed `INFRASTRUCTURE_OR_BOOTSTRAP_FAILURE` with `0s` effective product observation. Do not blind-rerun.

## Current high-priority diagnostic/correction map

### P1 — legacy visual schema crash — confirmed generic defect

Screen Editor and Popup Editor can blank the entire application when persisted legacy visual types are selected. Reproductions include legacy `tank`, `value` and `dynamo` paths.

The audited path reaches `getBuiltinVisualObjectSchema(element.type)` without adequate compatibility/recovery for those persisted identifiers. Diagnose a generic compatibility/migration/degradation contract and require deterministic Screen + Popup regressions. Do not weaken unknown-type validation as a workaround.

### P1 — Working `demo` vs Runtime `eee-demo` — confirmed in later live evidence

Later direct UI evidence superseded the earlier artifact-only impression of alignment. Engineering Working can load `Demo Project` / `demo` while Runtime remains `eee-demo`, Active revision 2, with lifecycle correctly blocking cross-project activation.

Diagnose bootstrap/checkout/persistence. Do not change Active or activate `demo` merely to make the surfaces match.

### Engineering fetch/recovery and route latency

Transient `Failed to fetch`, fallback project identity, blocked modules and long route transitions were reproduced. Local API endpoints were much faster than the forwarded browser path, so initiating cause remains partly UNCERTAIN.

Preferred next diagnosis is direct correlation from the Codespace: API 5080 vs local Vite 5173 vs forwarded browser, captured before restart/reopen.

### Runtime Trends / Popups

Current audit records Runtime Trends returning silently to the operational screen after `Conectando dados ao vivo…`, and pump popups showing `—` despite Good values elsewhere and sometimes auto-returning to the screen.

Correlate TAG/realtime/projection/navigation state before correction.

### Deterministic generic P2 backlog

The latest audit also preserves generic UI findings around Engineering navigation scrolling/collapse, missing catalog previews, excessive Engineering Lock footprint, shared responsive header overlap, account-menu accessible names and misleading Engineering fallback/error UX.

Practical script-authoring/PO-PRE-07 remains to be completed after Engineering is stable.

Alarm / Operational Event / Audit remain distinct. Do not manufacture a Historian defect from zero data where the loaded Working reports zero historian policies.

## Finding chronology rule

The audit file evolved after issue checkpoint `5619800919`, and some numeric UIAUD mappings changed during the audit. Before creating a correction task, reconcile **ID + current title + evidence + latest chronological section**. Later revalidated evidence wins over an older narrative when they conflict.

## Immediate next safe sequence

1. New Codex coordinator revalidates live #286, #289, #290, #296 and branch SHAs.
2. Reads `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` and `docs/WAVE14-POST-C26-CODEX-COORDINATOR-HANDOFF-2026-09-10.md`.
3. Uses direct Codespace access to correlate Engineering transport/latency and confirm bootstrap/Working state before restarting anything.
4. Diagnoses/corrects confirmed P1 legacy-schema crash with deterministic regressions.
5. Diagnoses Runtime Trends/Popup behavior with local TAG/realtime/projection evidence.
6. Handles deterministic generic P2 findings after P1s and uncertain transport-dependent diagnoses.
7. Produces a new exact candidate only through the live-authorized correction route, with regression coverage and exact-head validation.
8. Uses targeted Work recheck only where justified.
9. Final Product Owner homologation remains blocked until correction/revalidation is complete.

## Permanent governance

- never modify `main` directly;
- #212 must not merge without later separate explicit Product Owner authorization;
- validation-only PRs stay validation-only / MUST NEVER MERGE;
- #296 MUST NEVER MERGE;
- preserve #285;
- #290 is Preview-only and not a route to `main`;
- no force push, destructive rebase, branch deletion, blind rerun or unrelated cleanup;
- never weaken tests, validation, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics or Runtime Active authority;
- Runtime/Active remains independent of `.escadalib`;
- no EEE-specific workaround for a generic product defect;
- Alarm / Operational Event / Audit remain distinct;
- Wave13 remains paused.

Canonical current handoff:

`docs/WAVE14-POST-C26-CODEX-COORDINATOR-HANDOFF-2026-09-10.md`

Copy-ready next coordinator prompt:

`docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
