# Current Coordinator Handoff — Wave 15

> GitHub live is the authority. Canonical operational handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Stable state

- Wave 15 — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN.
- FND-08 — VERIFIED/FROZEN.
- FND-03 — VERIFIED/FROZEN at product checkpoint `a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`.
- INFRA-CI-01A — **VERIFIED/FROZEN** at merge `9f62ad56e3fed5574bab1fa25fc8b64f9e4ae981`.
- FND-04 — **ACTIVE / SAME CODEX EXECUTOR / NOT INTEGRATED**.
- FND-06 — NOT STARTED.
- FC0-A — BLOCKED on FND-04 + FND-06.

## INFRA-CI-01A close

PR #335:
- final candidate `6490234887152cd668943615dc9fc80990b44076`;
- merge `9f62ad56e3fed5574bab1fa25fc8b64f9e4ae981`;
- candidate T1 run `35864183668` — SUCCESS;
- integrated broad CI #1560 / `35864708583` — SUCCESS;
- Web `107193247522`, Backend `107193247822`, Chromium `107193893312` — all SUCCESS.

The infra delta is acknowledged workflow/router/policy only. It does not redefine the FND-04 product base.

## Active CODEX mission

`ORDER_ID: FND04-CODEX-REVIEW-CLOSE-04`  
`ORDER_STATE: ACTIVE`

Exact product base:
`a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

Work branch:
`work/w15-fnd-04-script-tag-reference-resolution`

Control:
`coord/w15-fnd04-dev-aud-control:docs/WAVE15-FND04-DEV-AUD-CONTROL.md`

Control commit:
`65ac0551c17f1794e5a0411897c85327880e12f8`

Plan:
`FND04-TAGREF-V1`

PR #336 head `21e2ab69a71844d56acf1b7913dc67097697f6ae` is rejected by Main preliminary review despite green T1. CODEX must close Client Visual expected-TagId enforcement, exact five-state resolver semantics, missing persistence/ambiguity/multi-TAG proofs, and reproduce RED evidence before a corrected candidate is eligible for AUD.

## Other lanes

- Normal FND-04 DEV — BLOCKED_ENV / WATCH_ONLY.
- FND-04 AUD — WAIT_CORRECTED_CANDIDATE / READ_ONLY.
- FND-03 DEV — WAIT / frozen.

Main retains candidate review, integration, post-merge verification and freeze authority.
