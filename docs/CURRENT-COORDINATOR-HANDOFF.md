# Current Coordinator Handoff

> GitHub live is the official memory and sole authority for EliteSCADA. Revalidate live before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge. Any divergence here is resolved in favor of GitHub.

## Current topology

- Repository: `brunolrogerio-collab/EliteSCADA`
- Integration: `wave14/corrections-integration`, PR #212 -> `main` — OPEN/DRAFT; **NO MERGE without later separate explicit Product Owner authorization**
- Active branch: `wave14/c26-po-homologation-corrections`
- Coordinator issue: #286
- Implementation PR #287: C26 -> canonical C11, DRAFT
- Validation PR #288: C26 -> `main` — **VALIDATION ONLY / MUST NEVER MERGE**
- C11 validation PR #266 — **MUST NEVER MERGE**
- C11 -> integration PR #263 — integration route only
- Pre-C26 Preview #285 — preserve untouched as historical evidence
- Post-C26 real-use audit gate: issue #289 — **SPECIFIED / DO NOT EXECUTE UNTIL C26 + corrected C11 + new Preview are ready**
- Wave13 #205/#207 — paused

## Current execution boundary

Current C26 **product/test** candidate:

`642e33c83d17e3c53beb588841626551e6ea305f`

`fix(w14-c26): let readonly Engineering styles win cascade`

The branch has coordination-only commits above this SHA. Revalidate the live branch HEAD before every action; do not mistake a docs-only HEAD for a validated product SHA.

Last exact product/test SHA fully green across all five normal gates:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

C26 status:

- C26.1–C26.6 — **IMPLEMENTED / VALIDATED**
- C26.7 Engineering theme/contrast — **IMPLEMENTED BUT NOT VALIDATED / CURRENT BLOCKER**
- C26.8 Screen editor functionality audit/fixes — **NOT STARTED / BLOCKED BY C26.7**
- C26.9–C26.11 — pending per live issue #286

## C26.7 history and current blocker

Original C26.7 SHA `a05d349a37a41629ee13b47b4652df74d287273d` failed Chromium because a Screens route input marked `readonly` kept the editable background.

The cause was diagnosed as CSS specificity: the generic editable selector accumulated specificity through repeated `:not([type=...])` clauses and overrode `[readonly]`.

The smallest generic correction was committed as:

`642e33c83d17e3c53beb588841626551e6ea305f`

It replaces the specificity-producing type filters with `:where(...)`; no `!important` was introduced and the C26.7 Playwright regression was not weakened.

At exact `642e33c...`:

- Preview Licensing #402 / run `34255187708` — SUCCESS
- Interop #279 / run `34255187740` — SUCCESS
- Wave11 #380 / run `34255187706` — SUCCESS
- L3 #358 / run `34255187748` — SUCCESS
- EliteSCADA CI #1454 / run `34255187823` — **FAILURE**

EliteSCADA CI details:

- Backend build, test and smoke — SUCCESS
- Web build — SUCCESS
- Chromium end-to-end job `102159657922` — **FAILURE**, step `Run browser E2E tests`

No rerun was performed.

**Do not assume the `642e33c...` Chromium failure is identical to the earlier `a05d349...` assertion.** The next coordinator must fetch/revalidate the complete failure evidence for job `102159657922` before diagnosing or editing.

## Immediate next task

1. Read `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-08.md` first, then the remaining mandatory coordination files.
2. Revalidate live branch HEAD, issue #286, #287/#288/#212/#266/#263, canonical C11, #285, #289 and workflows for the exact candidate SHA.
3. Fetch the complete Chromium failure evidence for job `102159657922`.
4. Compare the actual failing assertion with the exact `642e33c...` CSS, affected DOM/component and `wave-14-c26-engineering-contrast.spec.ts`.
5. Implement only the smallest **generic** C26.7 correction supported by the evidence.
6. Do not weaken the regression or any security/authority/lifecycle contract.
7. Let fresh workflows run naturally and require all five normal gates green on the same exact product/test SHA.
8. Only then mark C26.7 VALIDATED and begin C26.8.

The post-C26 Work audit requirement does **not** change this immediate next task and must not be executed during C26.

## Coordination-only cleanup note

During metadata-tool discovery in the outgoing coordinator chat, a temporary placeholder file named `dummy` was accidentally created on the C26 branch and then removed immediately through a normal follow-up commit. No force push/rebase was used and no intended product file remains from that incident. Treat those commits as coordination-only, not as product validation evidence.

## Binding post-C26 Work audit gate

After C26 is complete, exact-SHA green, accepted and integrated through the authorized route into corrected canonical C11, the quality path must include the real-use ChatGPT Work audit before final Product Owner homologation.

Authority:

- issue #289;
- `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`.

Required high-level route:

`accepted C26 -> corrected canonical C11 -> package/checksum/provenance -> NEW post-C26 Preview -> technical green + fully prepared live environment -> READY FOR WORK AUDIT -> ChatGPT Work audit-only browser pass -> coordinator triage/corrections/regressions -> new exact candidate -> optional targeted Work recheck -> Product Owner final homologation`.

The coordinator must prepare the Preview, EEE Active Revision, simulation, Historian/HistoricalQuery, Alarm/Event state, audit users and explicit Runtime/Engineering URLs before consuming the approximately 40-minute Work window.

When the real candidate is ready, create the intentionally short candidate-specific `docs/WORK-UI-AUDIT-HANDOFF.md` with exact SHA, URLs, package SHA-256 and non-secret authentication instructions. Do not create a false READY handoff early.

## Permanent guardrails

- no direct `main` mutation;
- #212 not authorized to merge;
- #288 and #266 never merge;
- #287 targets canonical C11 only;
- #263 is C11 -> integration only;
- preserve #285;
- #289 does not authorize early execution or any protected merge;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose CI red before rerun;
- never weaken tests, validation, security, authentication, authorization, Identity, Engineering Lock, licensing, lifecycle, package, drivers or Runtime Active Revision authority;
- Runtime/Active cannot depend on `.escadalib`;
- no EEE-specific workaround for a generic product defect;
- Alarm / Operational Event / Audit remain separate;
- Wave13 remains paused.

When Product Owner says `siga`, continue autonomously through subsequent safe work. It does not authorize protected merges.
