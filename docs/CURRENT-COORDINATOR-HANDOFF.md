# Current Coordinator Handoff

> GitHub live is the official memory and sole authority for EliteSCADA. Revalidate live before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge. Any divergence here is resolved in favor of GitHub.

## Coordinator transfer — 2026-09-10

The Product Owner intends to continue Wave 14 coordination in a ChatGPT Codex session with direct access to the real Codespace and running application.

Canonical detailed transfer document:

`docs/WAVE14-CODEX-COORDINATOR-HANDOFF-2026-09-10.md`

Use that capability to obtain live dynamic evidence. It does not authorize assumptions, fixes or protected merges.

## Current live technical boundary at last revalidation

- repository: `brunolrogerio-collab/EliteSCADA`
- coordinator issue: #286 — OPEN
- Work audit issue: #289 — OPEN
- Preview PR #290 — OPEN/DRAFT/not merged
- diagnostic PR #296 — OPEN/DRAFT, **DIAGNOSTIC ONLY / MUST NEVER MERGE**
- accepted C26 product SHA: `08e2530671de10d48933c4b712a1a1abc9e41dce`
- corrected canonical C11 SHA: `19d5257d970f53ae798c5fa53946fce07c586452`
- technically validated/audited Preview candidate: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`
- Preview docs-only HEAD: `f6196dfc113322ec7c11471071508314d70cd900`
- frozen package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`

Second Work recheck authority:

- #289 comment `5619800919`
- #286 comment `5619803479`

Current decision:

`SECOND AUDIT ACCEPTED AS EVIDENCE -> P1s REPRODUCIBLE -> NEW RUNTIME/SIMULATION P1 CANDIDATE -> TECHNICAL REPRODUCTION/DIAGNOSIS REQUIRED -> NOT READY FOR FINAL PO HOMOLOGATION`

## Highest-priority blocker

`RECHECK-SIM-PUMP-LEVEL` remains provisional P1 / UNCERTAIN.

Observed in the second real-browser audit: P01 remained visually RUNNING while the full process snapshot stayed frozen for >15 minutes, then values degraded to bad/unavailable quality. Browser-side `/api/diagnostics/runtime` was blocked by `net::ERR_BLOCKED_BY_CLIENT`, so no Server Script stop/throttle root cause was established.

Do not assign a final UIAUD ID or product root cause yet.

## Diagnostic PR #296

Current diagnostic branch HEAD at last revalidation:

`7738b568a5dd4259e958a2c5023c2bf6ca7e5acb`

Latest run:

- workflow run `34505442984`
- job `102966371728`
- conclusion: FAILURE
- classification: `INFRASTRUCTURE_OR_BOOTSTRAP_FAILURE`
- observed duration: `0s`
- simulation/realtime evidence: unknown

This run failed before the intended >15-minute probe and therefore **does not reproduce the simulation freeze**. Do not blind-rerun unchanged. PR #296 remains evidence-only / MUST NEVER MERGE.

## Immediate next task for the Codex coordinator

1. Revalidate #286, #289, #290, #296, relevant branch heads and latest workflow/comments live.
2. Use direct Codespace/application access to diagnose `RECHECK-SIM-PUMP-LEVEL` first.
3. During a freeze, before restart/reopen, capture Server Script counters/status/timestamps/errors, current TAG values/timestamps/quality, event progression, direct internal API behavior and browser/Vite/realtime behavior.
4. Correlate `UIAUD-289-003` using direct API 5080 versus forwarded/Vite 5173. Do not equate browser text `(500) Failed to fetch` with a proven backend HTTP 500.
5. Then diagnose/correct `UIAUD-289-001` generically with Screen + Popup regressions.
6. Continue the ordered triage in #289 comment `5619800919`.

Useful static diagnostic split, not a root-cause conclusion:

- Server Script counters/`lastCompletedAt` stop -> investigate timer/script/sandbox;
- diagnostics advance but `/api/tags` stops -> investigate bridge/cache/event publication;
- `/api/tags` advances but browser freezes -> investigate realtime/WebSocket/proxy/projection/UI;
- internal API healthy while forwarded browser path fails -> Codespaces/Vite/proxy transport gains weight.

## Permanent guardrails

- never modify `main` directly;
- #212 remains OPEN/DRAFT and requires later, separate and explicit Product Owner authorization before merge;
- `siga`, green CI, Work completion, Preview or homologation do not authorize #212;
- #266, #288, #292 and #293 are validation-only / MUST NEVER MERGE where applicable;
- #296 is diagnostic-only / MUST NEVER MERGE;
- preserve #285 as historical pre-C26 evidence;
- #290 is Preview-only and not a route to `main`;
- no force push, destructive rebase, branch deletion, blind rerun or unrelated cleanup;
- never weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics or backend Active Runtime authority;
- Runtime/Active cannot depend on `.escadalib`;
- Alarm, Operational Event and Audit remain distinct;
- no EEE-specific workaround for a generic platform defect;
- Wave13 #205/#207 remains paused.
