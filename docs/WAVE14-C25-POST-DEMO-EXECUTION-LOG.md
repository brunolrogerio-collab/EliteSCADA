# Wave 14 C25 — Post-Demo Consolidated Corrections — Execution Log

**Date:** 2026-09-07  
**Status:** **C25.0-C25.10 COMPLETE / PRODUCT OWNER ACCEPTED / #283 MERGED TO INTEGRATION / POST-MERGE INTEGRATION VALIDATION RED**  
**Tracking issue:** #282  
**Implementation PR:** #283 — MERGED  
**Implementation branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub live state is the sole project authority. Revalidate live refs, PR state, issue comments and exact-SHA CI before every decision or mutation. Historical detail remains preserved in Git history; this file records the current resumable C25 closure/integration state.

## 1. Permanent governance

- #212 remains OPEN/DRAFT and MUST NOT merge to `main` without later separate explicit Product Owner authorization.
- Never alter `main` directly.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose every CI red before correction or rerun; no blind reruns.
- Never weaken tests, validation, authentication, authorization, Authority, Engineering Lock, licensing, lifecycle, package or Runtime authority for green CI.
- Backend Active Revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for a generic product gap.
- #266 remains validation-only and MUST NEVER MERGE.
- Wave 13 issue #205 and PR #207 remain paused until the approved Wave 14/main sequence reaches them.

## 2. Accepted exact C25 authority

Audited pre-C25 integration base:

`c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`

Accepted exact C25 product/test authority:

`5193b81220f499cb2039dc1146c6dd1a7f7b3dbd`

Exact product evidence:

- Wave 14 C25 Post-Demo #314 / `34072225644` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #301 / `34072225635` — **SUCCESS**.

Validated C25.10 coordination head:

`a781294ed2996888a8147c6f5fcaeb4eaddcf3c1`

C25.10 coordination evidence:

- C25 #318 / `34073401858` — **SUCCESS**;
- C03 #303 / `34073401801` — **SUCCESS**.

Product Owner explicitly accepted the exact product candidate above. C25 PR #283 was then merged only into `wave14/corrections-integration`.

C25 merge commit:

`f282758d47c0419f3534f948658701467807758b`

This acceptance and merge do **not** authorize #212 -> `main`.

## 3. C25 checkpoint matrix

- **C25.0 — Bootstrap + full architecture/code audit:** COMPLETE.
- **C25.1 — Engineering Lock domain/package/security:** COMPLETE.
- **C25.2 — Engineering Lock backend enforcement:** COMPLETE.
- **C25.3 — Engineering Lock UI/lifecycle/package:** COMPLETE.
- **C25.4 — Restore-first / System Recovery:** COMPLETE.
- **C25.5 — Runtime session UX:** COMPLETE.
- **C25.6 — Reusable Resource Libraries:** COMPLETE.
- **C25.7 — Distributed Runtime Foundation:** COMPLETE.
- **C25.8 — Contextual multilingual Help/manual:** COMPLETE.
- **C25.9 — Integrated regression/audit:** COMPLETE.
- **C25.10 — Exact final candidate matrix / explicit Product Owner acceptance:** COMPLETE / ACCEPTED.

Final candidate matrix remains:

`docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

## 4. Retained C25 invariants

The accepted C25 product preserves, among other binding contracts:

- Engineering Lock as an additional server-side application-content admission boundary without weakening Authority;
- durable Active-revision lifecycle and Runtime application authority;
- Restore-first/System Recovery and atomic Authority-store replacement semantics;
- Runtime session UX and Distributed Runtime foundation contracts;
- reusable Engineering libraries with association != import, selective incorporation, project-owned canonical content and no Runtime dependency on `.escadalib`;
- complete installed/local Help with stable language-neutral Topic IDs and pt-BR/en/es structural parity;
- exactly eight production communication Drivers in Help, with Simulation excluded from production and Internal Memory/TAG Gateway distinct;
- Server Script documentation restricted to shipped APIs/triggers/lifecycle/sandbox contracts;
- OpenDNP3 native/interoperability and commercial dependency boundary.

## 5. Post-merge integration changes

After merge commit `f282758...`, two commits were added to integration before the current handoff:

- `f7f7a92163486c9a2157a5958b160b21b964a4ac` — `test(realtime): harden async scheduling watchdogs`;
- `3e2f69447419a0852d8eaf9358969c0adc1da9ae` — `ci(w14): validate integration head on push`.

The compare `f282758...` -> `3e2f694...` changes only:

- `.github/workflows/dotnet-ci.yml`;
- `tests/Scada.Drivers.Tests/TagRealtimeHubTests.cs`.

## 6. Current post-merge validation blocker

Integration head immediately before this handoff refresh:

`3e2f69447419a0852d8eaf9358969c0adc1da9ae`

Latest completed EliteSCADA CI on PR #212:

- CI #1422 / run `34074801046` — **FAILURE**;
- Backend build, test and smoke — **SUCCESS**;
- Web build — **SUCCESS**;
- Chromium end-to-end — **FAILURE** at `Run browser E2E tests`;
- browser summary: **605 passed / 6 failed**.

Failing specs reported:

1. `tests-e2e/c04-i18n-browser.spec.ts:85:1` — Source/Address locale switching;
2. `tests-e2e/c04-i18n-browser.spec.ts:106:1` — Driver resource-key localization;
3. `tests-e2e/communication-diagnostics.spec.ts:8:1` — Engineering communication diagnostics;
4. `tests-e2e/engineering.spec.ts:16:1` — Engineering workspace/public model + locale switching;
5. `tests-e2e/report-designer-workspace.spec.ts:37:1` — Report Designer canonical report flow;
6. `tests-e2e/wave-14-c07-multilingual-authoring.spec.ts:6:1` — multilingual visual-authoring live locale changes.

Other workflows associated with `3e2f694...` were green: Preview Licensing CI, Wave 11 Active HMI Runtime, Interop Lab Smoke and L3 Seven-Driver Lab.

The exact detailed Playwright failure messages/traces have not yet been captured into this ledger, so a shared root cause is **not yet established**. Do not infer one merely because several failures touch Engineering/i18n. No blind rerun is authorized.

## 7. C11 / EEE gate

C11 canonical EEE branch remains frozen at:

`41d24d89c3b9d2b881215255e44023fabde262f3`

PR #263 remains OPEN/DRAFT -> integration. Although C25 is now accepted and merged, C11 must not be synchronized until the resulting integration is exact-SHA green. Current red #212 validation therefore keeps C11 frozen.

PR #266 remains OPEN/DRAFT validation-only against `main` and **MUST NEVER MERGE**.

## 8. Required next sequence

1. Revalidate GitHub live.
2. Extract the exact Playwright failure output/traces/report for run `34074801046` and diagnose all six failures before any rerun.
3. Correct the generic source cause(s) without weakening tests or product contracts.
4. Exact-SHA validate the corrected integration head.
5. Only after integration is green, synchronize/adapt C11 normally to the accepted C25 contracts.
6. Revalidate EEE through generic product paths; prove lifecycle and `.escadapkg` portability; freeze canonical `EliteSCADA-EEE-Demo.escadapkg`, checksum and provenance.
7. Run exact C11 gates. Use #266 only as a validation trigger and close it without merge when complete.
8. Complete Product Owner Preview/Codespace homologation.
9. Merge #212 to `main` only after a separate explicit Product Owner authorization; C25 acceptance is not that authorization.
10. Validate the resulting new `main`.
11. Only then resume Wave 13 release/signing.
