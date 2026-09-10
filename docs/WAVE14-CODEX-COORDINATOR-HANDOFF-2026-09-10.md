# Wave 14 — Codex Coordinator Handoff — 2026-09-10

> GitHub live is the official memory and sole authority for EliteSCADA. Revalidate live before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge. If this handoff differs from GitHub live, GitHub wins.

## Intended next coordinator

The Product Owner intends to continue coordination in a ChatGPT Codex session that can access the real Codespace and the running EliteSCADA application directly. That capability should be used to obtain dynamic evidence that this coordinator could not collect from the private Codespace.

This is an execution advantage, not a relaxation of governance. The new coordinator must still revalidate GitHub live first and must not assign a root cause without technical evidence.

## Live topology at transfer

Repository: `brunolrogerio-collab/EliteSCADA`

Primary coordination issue: #286

Post-C26 Work audit issue: #289

Post-C26 Preview PR: #290 — OPEN/DRAFT, not a route to `main`

Diagnostic PR: #296 — **DIAGNOSTIC ONLY / MUST NEVER MERGE**

Accepted C26 product SHA:

`08e2530671de10d48933c4b712a1a1abc9e41dce`

Corrected canonical C11 SHA:

`19d5257d970f53ae798c5fa53946fce07c586452`

Technically validated/audited Preview candidate:

`59e815eae524b9ff043ea6bf3f797f4c01ba9143`

Preview branch documentation-only HEAD at last revalidation:

`f6196dfc113322ec7c11471071508314d70cd900`

Frozen post-C26 package SHA-256:

`e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`

Second Work recheck artifact identity:

- `ELITESCADA-RECHECK-UI-POST-C26-2026-09-10.zip`
- 83 files
- SHA-256 `10c1e495d74a85f1f9b8571065e98220cd1baf3b163382536ef31f2176437a14`

Authoritative second-audit checkpoint:

- #289 comment `5619800919`
- mirrored coordinator checkpoint in #286 comment `5619803479`

## Current quality state

Second targeted Work audit was accepted as evidence, but the candidate is **NOT READY FOR FINAL PRODUCT OWNER HOMOLOGATION**.

Reproduced:

- `UIAUD-289-001`
- `UIAUD-289-002`
- `UIAUD-289-003`
- `UIAUD-289-007`
- `UIAUD-289-011`
- `UIAUD-289-017`

Partially reproduced:

- `UIAUD-289-004`

Insufficient data:

- `UIAUD-289-016`

New provisional P1 candidate:

`RECHECK-SIM-PUMP-LEVEL`

P01 remained visually RUNNING while the full process snapshot stayed frozen for more than 15 minutes; later values degraded to bad/unavailable quality. Browser access to `/api/diagnostics/runtime` was blocked by `net::ERR_BLOCKED_BY_CLIENT`, therefore the audit did not establish whether Server Script execution stopped/throttled or whether the failure was downstream in runtime/cache/realtime/proxy/UI transport.

Do **not** assign a final UIAUD ID or product root cause yet.

## Diagnostic PR #296 state

Branch:

`diagnostic/w14-post-c26-long-run-stability`

Current diagnostic HEAD at last revalidation:

`7738b568a5dd4259e958a2c5023c2bf6ca7e5acb`

Latest commit:

`ci(w14): pass ephemeral preview auth before diagnostic devcontainer attach`

Latest workflow:

- run `34505442984`
- job `102966371728`
- `Diagnostic only — correlated >15m stability probe`
- result: **FAILURE**
- classification recorded by workflow: `INFRASTRUCTURE_OR_BOOTSTRAP_FAILURE`
- observed duration: `0s`
- direct/proxy LevelPct and realtime metrics: `unknown`

The workflow failed in `Run correlated long-run probe in canonical devcontainer` before collecting the intended long-run evidence. This **does not reproduce `RECHECK-SIM-PUMP-LEVEL`** and does not authorize a product correction.

Do not blind-rerun this unchanged workflow. PR #296 remains evidence-only and **MUST NEVER MERGE**.

## Preferred next diagnostic route for Codex

Because the intended new coordinator can directly access the real Codespace and application, prefer live correlated diagnosis there before spending more time repairing the GitHub Actions harness.

During an actual freeze, and before restarting/reopening the environment, capture read-only evidence for:

- `runtime.serverScripts.executionCount` and `completedCount`;
- `faultedCount`, `timeoutCount`, `consecutiveFailures`, `isThrottled`;
- `lastStatus`, `lastCompletedAt`, `lastSanitizedError`;
- current value/timestamp/quality of `EEE.Process.LevelPct`, inflow, total flow, P01 running/flow/current/frequency/pressure;
- Operational Event progression around the same time;
- direct internal API behavior versus the browser/Vite path;
- direct and proxied realtime/WebSocket behavior when possible.

Useful diagnostic split from static analysis, **not a root-cause conclusion**:

- if Server Script execution counters and `lastCompletedAt` stop advancing, investigate timer/script/sandbox execution first;
- if Server Script diagnostics advance but `/api/tags` stops changing, investigate script bridge/cache/event publication path;
- if `/api/tags` continues changing while the browser freezes, investigate realtime/WebSocket/proxy/projection/UI path;
- if local/internal API remains healthy while only the forwarded browser path fails, Codespaces/Vite/proxy transport gains weight.

The current code path is capable of surfacing Server Script diagnostics, so direct Codespace access should provide substantially better evidence than the browser audit alone.

## Required technical order

1. Revalidate #286, #289, #290, #296, branch heads and latest workflow/comments live.
2. Diagnose `RECHECK-SIM-PUMP-LEVEL` first in the real Codespace. Capture evidence before any restart.
3. Correlate `UIAUD-289-003` with direct API 5080 versus Vite/forwarded 5173 behavior. Preserve the distinction between browser text `(500) Failed to fetch` and an actually proven backend HTTP 500.
4. Diagnose and correct `UIAUD-289-001` generically with deterministic Screen Editor and Popup Editor regressions. Do not weaken unknown visual-type validation.
5. Resolve project selection/checkout/workspace semantics for `UIAUD-289-002` before changing lifecycle or Active authority.
6. Split technical work under `UIAUD-289-007` without renumbering the audit finding.
7. Then address 011 and 017; revisit 004 after simulation health; leave 016 pending evidence.
8. For every confirmed defect: implement generically, add deterministic regressions, validate a new exact SHA, and use targeted Work recheck only where justified.
9. No final Product Owner homologation until the post-Work correction/revalidation sequence is complete.

## Permanent guardrails

- GitHub live always wins over this handoff.
- Never modify `main` directly.
- PR #212 remains OPEN/DRAFT and **MUST NOT MERGE** without later, separate and explicit Product Owner authorization.
- `siga`, green CI, Work completion, Preview success or homologation do not authorize #212.
- PRs #266, #288, #292 and #293 are validation-only / **MUST NEVER MERGE** where applicable.
- PR #296 is diagnostic-only / **MUST NEVER MERGE**.
- Preserve #285 as historical PRE-C26 Preview evidence.
- PR #290 remains Preview-only and is not a route to `main`.
- No force push, destructive rebase, branch deletion, blind rerun or unrelated cleanup.
- Never weaken tests, validation, security, authentication, authorization, Identity, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics or backend Active Runtime authority.
- Runtime/Active remains self-contained and cannot depend on `.escadalib`.
- Alarm, Operational Event and Audit remain distinct authorities.
- Do not use EEE-specific workarounds for generic platform defects.
- Wave13 #205/#207 remains paused.

## Mandatory reading for the next coordinator

Read live, in this order:

1. `docs/WAVE14-CODEX-COORDINATOR-HANDOFF-2026-09-10.md`
2. `docs/CURRENT-COORDINATOR-HANDOFF.md`
3. `LAST CHANGE.md`
4. #289 comment `5619800919`
5. #286 comment `5619803479`
6. PR #290 body/state/commits
7. PR #296 body/state/commits and latest workflow result
8. `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`
9. `docs/WORK-UI-AUDIT-HANDOFF.md` on the Preview branch

Then revalidate everything live before acting.
