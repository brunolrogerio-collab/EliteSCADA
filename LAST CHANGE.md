# LAST CHANGE — EliteSCADA

**Date:** 2026-09-08 BRT  
**Operational state:** **WAVE14 C26 ACTIVE / C26.1–C26.6 VALIDATED / C26.7 IMPLEMENTED BUT CI-BLOCKED / C26.8 NOT STARTED / #212 NOT AUTHORIZED / #266+#288 NEVER MERGE / WAVE13 PAUSED**

> GitHub live is the official and sole project memory. Revalidate refs, PR state and exact-SHA workflows before every decision or mutation.

## Current product boundary

Branch:

`wave14/c26-po-homologation-corrections`

Current C26 product HEAD before latest coordinator handoff documentation:

`a05d349a37a41629ee13b47b4652df74d287273d`

`fix(w14-c26): make Engineering field states readable`

Last exact product/test SHA fully green across all five normal gates:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

## Current C26 status

- C26.1–C26.6 — IMPLEMENTED / VALIDATED;
- C26.7 Engineering theme/contrast — IMPLEMENTED BUT NOT VALIDATED;
- C26.8 Screen editor functionality audit/fixes — NOT STARTED / BLOCKED BY C26.7;
- C26.9–C26.11 pending per live issue #286.

## C26.7 exact CI evidence

At `a05d349...`:

- Preview Licensing CI #394 / `34241442573` — SUCCESS;
- Interop Lab Smoke #271 / `34241442611` — SUCCESS;
- Wave 11 Active HMI Runtime #372 / `34241442718` — SUCCESS;
- L3 Seven-Driver Lab #350 / `34241442633` — SUCCESS;
- EliteSCADA CI #1446 / `34241442557` — FAILURE.

Failed job `102112833905`, Chromium end-to-end.

The failing regression is `web/scada-web/tests-e2e/wave-14-c26-engineering-contrast.spec.ts`. In Screens, after the route input is marked `readonly`, its computed background remains equal to editable (`rgb(16, 25, 35)`). This is a deterministic C26.7 acceptance failure, not a rerun candidate.

Next product action: diagnose the readonly selector/scope/specificity on Screens, correct it generically without weakening the regression, and require fresh exact-SHA 5/5 green. Only then start C26.8.

## Protected governance

- PR #287: C26 -> C11 only;
- PR #288: validation-only -> `main`, **NEVER MERGE**;
- PR #266: validation-only -> `main`, **NEVER MERGE**;
- PR #212: integration -> `main`, OPEN/DRAFT, **MUST NOT MERGE without later separate explicit Product Owner authorization**;
- Preview #285 remains preserved;
- no direct `main` mutation, force push, destructive rebase, blind CI rerun or contract weakening;
- Runtime/Active cannot depend on `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- Wave13 #205/#207 remains paused.

Current coordinator handoff:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-08.md`
