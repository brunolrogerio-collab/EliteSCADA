# Current Coordinator Handoff

> GitHub live is the official memory and sole authority for EliteSCADA. Revalidate live before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge. Any divergence here is resolved in favor of GitHub.

## Current topology

- Repository: `brunolrogerio-collab/EliteSCADA`
- Integration: `wave14/corrections-integration`, PR #212 -> `main` — OPEN/DRAFT; **NO MERGE without later separate explicit Product Owner authorization**
- Active branch: `wave14/c26-po-homologation-corrections`
- Coordinator issue: #286
- Implementation PR #287: C26 -> C11, DRAFT
- Validation PR #288: C26 -> `main` — **VALIDATION ONLY / MUST NEVER MERGE**
- C11 validation PR #266 — **MUST NEVER MERGE**
- Pre-C26 Preview #285 — preserve untouched
- Wave13 #205/#207 — paused

## Current execution boundary

Current C26 product HEAD before the latest docs-only coordinator transfer:

`a05d349a37a41629ee13b47b4652df74d287273d`

`fix(w14-c26): make Engineering field states readable`

Last exact product/test SHA fully green across all five normal gates:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

C26 status:

- C26.1–C26.6 — **IMPLEMENTED / VALIDATED**
- C26.7 Engineering theme/contrast — **IMPLEMENTED BUT NOT VALIDATED / CURRENT BLOCKER**
- C26.8–C26.11 — pending per live issue #286

## Current blocker

At exact SHA `a05d349...`:

- Preview Licensing #394 — SUCCESS
- Interop #271 — SUCCESS
- Wave11 #372 — SUCCESS
- L3 #350 — SUCCESS
- EliteSCADA CI #1446 / run `34241442557` — **FAILURE**

Failed Chromium job `102112833905` in `Run browser E2E tests`.

The Playwright C26.7 regression shows a deterministic acceptance defect: in Screens, applying `readonly` to the route field leaves its computed background equal to the editable background (`rgb(16, 25, 35)`). Readonly and editable are therefore not visually distinct as required by #286.

**Do not rerun unchanged. Do not weaken the assertion.** Fix the generic readonly styling/scope/specificity cause, then require fresh exact-SHA 5/5 green before starting C26.8.

## Immediate next task

Read `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-08.md` first. Then revalidate all live refs and diagnose the exact Screens readonly selector path from current branch sources before editing.

## Permanent guardrails

- no direct `main` mutation;
- #212 not authorized to merge;
- #288 and #266 never merge;
- preserve #285;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose CI red before rerun;
- never weaken tests, security, Identity, authorization, Engineering Lock, licensing, lifecycle, package, drivers or Runtime Active Revision authority;
- Runtime/Active cannot depend on `.escadalib`;
- Alarm / Operational Event / Audit remain separate.

When Product Owner says `siga`, continue autonomously through subsequent safe work. It does not authorize protected merges.
