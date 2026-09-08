# LAST CHANGE — EliteSCADA

**Date:** 2026-09-08 BRT  
**Operational state:** **WAVE14 C26 ACTIVE / C26.1–C26.6 VALIDATED / C26.7 IMPLEMENTED BUT CI-BLOCKED / C26.8 NOT STARTED / POST-C26 WORK AUDIT #289 SPECIFIED + NOT YET EXECUTABLE / #212 NOT AUTHORIZED / #266+#288 NEVER MERGE / WAVE13 PAUSED**

> GitHub live is the official and sole project memory. Revalidate refs, PR state and exact-SHA workflows before every decision or mutation.

## Current product boundary

Active branch:

`wave14/c26-po-homologation-corrections`

Current C26 **product/test** candidate:

`642e33c83d17e3c53beb588841626551e6ea305f`

`fix(w14-c26): let readonly Engineering styles win cascade`

The branch contains coordination-only commits above that product/test SHA. Revalidate the live branch HEAD before every action. Those coordination commits do **not** validate C26.7 and do not authorize C26.8.

A temporary placeholder file was accidentally created during coordinator metadata tooling discovery and then removed by a normal follow-up commit. There is no intended product change from that incident and no force push/rebase was used.

Last exact product/test SHA fully green across all five normal gates:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

## Current C26 status

- C26.1–C26.6 — IMPLEMENTED / VALIDATED;
- C26.7 Engineering theme/contrast — IMPLEMENTED BUT NOT VALIDATED / CURRENT BLOCKER;
- C26.8 Screen editor functionality audit/fixes — NOT STARTED / BLOCKED BY C26.7;
- C26.9–C26.11 — pending per live issue #286.

## C26.7 implementation and CI evidence

Original C26.7 implementation SHA:

`a05d349a37a41629ee13b47b4652df74d287273d`

At `a05d349...`, four normal gates were green and EliteSCADA CI #1446 failed in the Chromium C26.7 contrast regression because the Screens route field remained visually identical after `readonly` was applied.

The deterministic cause was diagnosed in `web/scada-web/src/engineering/engineering-control-states.css`: the editable input selector accumulated more CSS specificity through repeated `:not([type=...])` clauses than the `[readonly]` selector, so editable styling won the cascade.

Minimal generic correction:

`642e33c83d17e3c53beb588841626551e6ea305f`

The fix uses `:where(...)` around the type-exclusion filters so those filters no longer increase specificity. No `!important` was introduced and the strict Playwright regression was not weakened.

At exact product/test SHA `642e33c...`:

- Preview Licensing CI #402 / run `34255187708` — SUCCESS;
- Interop Lab Smoke #279 / run `34255187740` — SUCCESS;
- Wave 11 Active HMI Runtime #380 / run `34255187706` — SUCCESS;
- L3 Seven-Driver Lab #358 / run `34255187748` — SUCCESS;
- EliteSCADA CI #1454 / run `34255187823` — **FAILURE**.

EliteSCADA CI subjobs:

- Backend build, test and smoke — SUCCESS;
- Web build — SUCCESS;
- Chromium end-to-end, job `102159657922` — **FAILURE**, step `Run browser E2E tests`.

No blind rerun was performed.

**Important transfer note:** the exact current failing Playwright assertion from job `102159657922` must be fetched/revalidated from GitHub before the next diagnosis. Do not assume it is identical to the earlier `a05d349...` failure and do not modify code from memory.

Immediate next product action:

1. revalidate live branch HEAD, #286, #287/#288/#212 and exact product-SHA workflows;
2. fetch the full Chromium evidence for job `102159657922`;
3. compare that failure against the exact `642e33c...` CSS, DOM/component and strict C26.7 regression;
4. implement only the smallest generic product correction justified by that evidence;
5. preserve the test and all authority/security/lifecycle contracts;
6. let fresh workflows run naturally and require all five normal gates green on one exact product/test SHA;
7. only then mark C26.7 VALIDATED and start C26.8.

## Post-C26 quality gate — NOT YET EXECUTABLE

Product Owner direction adds a mandatory real-use ChatGPT Work audit after C26/canonical C11/new Preview technical readiness and before final Product Owner homologation.

Authority:

- issue #289 — `W14 Post-C26 — ChatGPT Work real UI/functional audit gate`;
- `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`.

Classification:

**SPECIFIED / NOT IMPLEMENTED OR EXECUTED / BLOCKED UNTIL POST-C26 CANDIDATE EXISTS.**

Required route:

`C26 complete + exact-SHA green + accepted -> authorized C26->C11 integration -> corrected canonical EEE package/checksum/provenance -> NEW post-C26 Preview -> technical green + live environment fully prepared -> READY FOR WORK AUDIT -> ChatGPT Work browser audit-only pass -> coordinator triage/reproduction -> generic corrections + deterministic regressions -> new exact candidate -> optional targeted Work recheck -> Product Owner final homologation`.

The approximately 40-minute Work window must be spent primarily using the real product, not building/configuring it. Only at that future ready candidate should `docs/WORK-UI-AUDIT-HANDOFF.md` be created with real exact SHA, URLs, package SHA-256 and non-secret authentication instructions.

This gate does not alter the current C26 blocker and does not authorize any merge.

## Protected governance

- canonical C11 branch: `wave14/c11-canonical-eee-demo`; revalidate its exact HEAD live before use;
- PR #287: C26 -> C11 only;
- PR #288: validation-only -> `main`, **NEVER MERGE**;
- PR #266: validation-only -> `main`, **NEVER MERGE**;
- PR #212: integration -> `main`, OPEN/DRAFT, **MUST NOT MERGE without later separate explicit Product Owner authorization**;
- PR #263: C11 -> integration only;
- Preview #285 remains preserved as pre-C26 historical evidence;
- #289 must not be executed prematurely during C26;
- no direct `main` mutation, force push, destructive rebase, branch deletion, blind CI rerun or contract weakening;
- Runtime/Active cannot depend on `.escadalib`;
- no EEE-specific workaround for a generic product defect;
- Alarm / Operational Event / Audit remain distinct;
- Wave13 #205/#207 remains paused.

Current coordinator handoff:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-08.md`

Post-C26 Work audit directive:

`docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`
