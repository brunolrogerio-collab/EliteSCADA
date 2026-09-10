# LAST CHANGE — EliteSCADA

**Date:** 2026-09-10 BRT

**Operational state:** **POST-C26 WORK RECHECK TRIAGED / FINAL PO HOMOLOGATION BLOCKED / `RECHECK-SIM-PUMP-LEVEL` IS HIGHEST-PRIORITY TECHNICAL INVESTIGATION / PR #296 DIAGNOSTIC HARNESS FAILED BEFORE LONG-RUN EVIDENCE / NEXT COORDINATOR INTENDED TO USE CODEX DIRECT CODESPACE ACCESS / #212 NOT AUTHORIZED / #266+#288+#292+#293 NEVER MERGE / #296 NEVER MERGE / WAVE13 PAUSED**

> GitHub live is the official and sole project memory. Revalidate refs, PR/issue state, exact files and workflows before every decision, diagnosis, code/documentation write, PR action, rerun or merge. If this file differs from GitHub live, GitHub wins.

## Canonical handoff

`docs/WAVE14-CODEX-COORDINATOR-HANDOFF-2026-09-10.md`

The Product Owner intends to move coordination to a ChatGPT Codex session with direct access to the real Codespace and running EliteSCADA application. Prefer that live route for dynamic diagnosis before spending more time repairing the diagnostic CI harness.

## Current live technical boundary at last revalidation

- coordinator issue #286: OPEN
- Work audit issue #289: OPEN
- Preview PR #290: OPEN/DRAFT/not merged
- diagnostic PR #296: OPEN/DRAFT, **DIAGNOSTIC ONLY / MUST NEVER MERGE**
- accepted C26 product SHA: `08e2530671de10d48933c4b712a1a1abc9e41dce`
- corrected canonical C11 SHA: `19d5257d970f53ae798c5fa53946fce07c586452`
- technically validated/audited Preview candidate: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`
- Preview docs-only HEAD: `f6196dfc113322ec7c11471071508314d70cd900`
- frozen package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`

Second audit authority:

- #289 comment `5619800919`
- #286 comment `5619803479`

Decision:

`SECOND AUDIT ACCEPTED AS EVIDENCE -> P1s REPRODUCIBLE -> NEW RUNTIME/SIMULATION P1 CANDIDATE -> TECHNICAL REPRODUCTION/DIAGNOSIS REQUIRED -> NOT READY FOR FINAL PO HOMOLOGATION`

## Highest-priority investigation

`RECHECK-SIM-PUMP-LEVEL` remains provisional P1 / UNCERTAIN.

Observed: P01 visually RUNNING while the full process snapshot stayed frozen >15 minutes, later degrading to bad/unavailable quality. The browser audit could not read `/api/diagnostics/runtime` because of `net::ERR_BLOCKED_BY_CLIENT`, so Server Script stop/throttle was **not** established.

No final UIAUD ID and no product root cause may be assigned yet.

## PR #296 diagnostic state

Current diagnostic HEAD at last revalidation:

`7738b568a5dd4259e958a2c5023c2bf6ca7e5acb`

Latest workflow:

- run `34505442984`
- job `102966371728`
- result: FAILURE
- classification: `INFRASTRUCTURE_OR_BOOTSTRAP_FAILURE`
- observed diagnostic duration: `0s`
- LevelPct/realtime metrics: unknown

The run failed in the canonical-devcontainer probe before long-run collection. It **did not reproduce the simulation freeze**. Do not blind-rerun unchanged and do not treat this as product evidence.

## Immediate next action

The next coordinator should revalidate GitHub live, then use direct Codespace/application access to reproduce `RECHECK-SIM-PUMP-LEVEL` and capture read-only Server Script counters/status/timestamps/errors, TAG values/timestamps/quality, event progression, internal API behavior, forwarded/Vite behavior and realtime/WebSocket evidence **before any restart/reopen**.

After simulation diagnosis, follow the technical sequence in #289 comment `5619800919`: correlate `UIAUD-289-003`, then diagnose/correct `UIAUD-289-001`, then continue the remaining ordered findings.

## Permanent governance

- never modify `main` directly;
- PR #212 remains OPEN/DRAFT and **MUST NOT MERGE** without later, separate and explicit Product Owner authorization;
- `siga`, green CI, Work audit, Preview or homologation do not authorize #212;
- #266, #288, #292 and #293 are validation-only / **MUST NEVER MERGE** where applicable;
- #296 is diagnostic-only / **MUST NEVER MERGE**;
- preserve #285 as historical PRE-C26 evidence;
- #290 remains Preview-only and is not a route to `main`;
- no force push, destructive rebase, branch deletion, blind rerun or unrelated cleanup;
- never weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics or backend Active Runtime authority;
- Runtime/Active cannot depend on `.escadalib`;
- Alarm, Operational Event and Audit remain distinct;
- no EEE-specific workaround for generic platform defects;
- Wave13 #205/#207 remains paused.

Copy-ready next-chat handoff:

`docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
