# Current Coordinator Handoff

> **GitHub live is the official memory and sole authority for EliteSCADA.** Revalidate live before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge. Any divergence here is resolved in favor of GitHub.

## Coordination model

Wave 14 has a **Main Coordinator + diagnostic co-coordinator** model.

- **Main Coordinator:** the ChatGPT coordination session designated by the Product Owner. It retains principal Wave 14 coordination authority, system-level triage, sequencing, guardrails and correction-route decisions.
- **GPT Codex:** diagnostic co-coordinator with direct Codespace/application/browser/local-port access. It is delegated correlated live diagnosis and evidence capture, but it does not replace the Main Coordinator and its chat/Codespace memory is never authoritative over GitHub live.
- **Product Owner:** retains protected future authorizations and final homologation authority; the Product Owner is not intended to manually relay operational findings between the two coordinators after Codex is bootstrapped.

Canonical collaboration protocol:

`docs/WAVE14-MAIN-COORDINATOR-CODEX-COLLABORATION-PROTOCOL.md`

Issue **#286** is the primary coordination ledger. Use `MAIN COORDINATOR -> CODEX` and `CODEX -> MAIN COORDINATOR` markers for operational exchanges.

The earlier file `docs/WAVE14-POST-C26-CODEX-COORDINATOR-HANDOFF-2026-09-10.md` remains valuable chronological/audit context, but any wording there suggesting transfer of principal coordination authority to Codex is superseded by this coordination model.

## Current phase

Wave 14 is **post-C26, after real Work/browser audit and targeted rechecks, with technical diagnosis/corrections still required before final Product Owner homologation**.

Repository: `brunolrogerio-collab/EliteSCADA`

Current audit/coordination surface: `preview/wave14-post-c26-work-audit`

Coordinator issue: #286

Audit gate: #289

Preview PR: #290 - OPEN/DRAFT / Preview only

Diagnostic PR: #296 - OPEN/DRAFT / DIAGNOSTIC ONLY / **MUST NEVER MERGE**

## Technical baseline

- corrected canonical C11: `19d5257d970f53ae798c5fa53946fce07c586452`;
- accepted C26 product: `08e2530671de10d48933c4b712a1a1abc9e41dce`;
- exact technically validated Preview candidate: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`;
- frozen package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`;
- exact-head technical gates on `59e815e...`: Preview run `34403903462` SUCCESS and readiness run `34403903471` SUCCESS.

The live Preview branch contains later audit documentation/evidence above `59e815e...`. Do not use a documentation HEAD as if it were a newly validated product candidate.

## Read this first

1. `docs/WAVE14-MAIN-COORDINATOR-CODEX-COLLABORATION-PROTOCOL.md`;
2. live #286, including the latest coordinator exchange;
3. live #290 and any PR/issue relevant to the current assignment;
4. this file;
5. `docs/WAVE14-AUDIT-PARTIAL-2026-09-10.md` latest relevant chronological section;
6. `docs/WAVE14-POST-C26-CODEX-COORDINATOR-HANDOFF-2026-09-10.md` for detailed audit history;
7. live #296 when the simulation/long-run diagnostic path is relevant.

Earlier issue checkpoint `#289 / comment 5619800919` remains valuable historical evidence, but later audit sections/commits supersede it where they conflict.

## Current diagnostic priorities delegated to GPT Codex

1. Correlate Engineering `Failed to fetch` / route latency using direct Codespace API 5080, local Vite 5173 and forwarded browser path before assigning transport cause.
2. Diagnose confirmed Working `demo` vs Runtime `eee-demo` mismatch without weakening lifecycle/Active authority.
3. Reproduce/mechanically diagnose confirmed P1 generic legacy visual schema crash in Screen + Popup editor. Persisted legacy types including `tank`, `value` and `dynamo` can reach current schema lookup and blank the application. A product correction requires live-authorized correction routing plus deterministic regression.
4. Correlate Runtime Trends silent return and popup `-`/auto-return behavior against TAG Monitor, realtime and projection/navigation state.
5. Then gather/validate evidence for deterministic generic P2 UI findings: Engineering navigation scrolling/collapse, resource previews, Engineering Lock footprint, shared responsive header and account-menu accessibility, plus error/fallback UX.
6. Re-run practical script-authoring/PO-PRE-07 after Engineering is stable.

`RECHECK-SIM-PUMP-LEVEL` is no longer a confirmed product freeze: later authenticated local Codespace evidence showed dynamic LevelPct with healthy local API/Vite while the public forwarded path failed. Reopen the simulation/Server Script hypothesis only if a freeze is reproduced with correlated local diagnostics captured before restart/reopen.

PR #296 remains evidence-only. Latest recorded diagnostic run `34505442984` / job `102966371728` failed as `INFRASTRUCTURE_OR_BOOTSTRAP_FAILURE` with `0s` effective observation and did not reproduce the freeze. **Do not blind-rerun it.**

## Important finding-ID rule

The audit evolved and some numeric UIAUD references in older comments do not map cleanly to the latest audit-file titles. Before opening or implementing any correction, reconcile **ID + current title + evidence + chronological latest section**. Do not act on a remembered number alone.

## Permanent guardrails

- never mutate `main` directly;
- #212 requires a later separate explicit Product Owner authorization before merge to `main`;
- `siga`, coordinator agreement, green CI or successful audit do not authorize that merge;
- #266 / #288 / #292 / #293 remain validation-only / MUST NEVER MERGE where applicable;
- #296 MUST NEVER MERGE;
- preserve #285 as historical pre-C26 Preview evidence;
- #290 is Preview-only and not a route to `main`;
- no force push, destructive rebase, branch deletion or blind rerun;
- never weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics or drivers;
- Runtime/Active remains independent of `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- no EEE-specific workaround for a generic platform defect;
- Wave13 #205/#207 remains paused.

Current decision:

`MAIN COORDINATOR RETAINS WAVE14 COORDINATION -> GPT CODEX IS DIRECT-CODESPACE DIAGNOSTIC CO-COORDINATOR -> EXCHANGE THROUGH #286 + VERSIONED EVIDENCE -> P1 DIAGNOSIS/CORRECTIONS REQUIRED -> UNCERTAIN TRANSPORT FINDINGS REQUIRE CORRELATED LIVE EVIDENCE -> NOT READY FOR FINAL PO HOMOLOGATION`
