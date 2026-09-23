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

`ORDER_ID: FND04-CODEX-WAIT-AUD-05`  
`ORDER_STATE: ACTIVE`

Exact product base:
`a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

Work branch:
`work/w15-fnd-04-script-tag-reference-resolution`

Control:
`coord/w15-fnd04-dev-aud-control:docs/WAVE15-FND04-DEV-AUD-CONTROL.md`

Control commit:
`4981794478447de020499e473b6401173e294a7e`

Plan:
`FND04-TAGREF-V1`

PR #336 head `21e2ab69a71844d56acf1b7913dc67097697f6ae` is rejected by Main preliminary review despite green T1. CODEX must close Client Visual expected-TagId enforcement, exact five-state resolver semantics, missing persistence/ambiguity/multi-TAG proofs, and reproduce RED evidence before a corrected candidate is eligible for AUD.

## Other lanes

- Normal FND-04 DEV — BLOCKED_ENV / WATCH_ONLY.
- FND-04 AUD — ACTIVE / READ_ONLY_REVIEW.
- FND-03 DEV — WAIT / frozen.

Main retains candidate review, integration, post-merge verification and freeze authority.


## FND-04 corrected candidate under audit

- candidate: `8dba4f1161d4ca5190ddfa37b48d9736478d73ec`
- tree: `393938536ee524d2bd7c713ed9f791679fd6c2cb`
- PR #336 natural T1 `35895957135` — SUCCESS
- Main preliminary review: prior known blockers closed
- CODEX: `WAIT_AUD / NO_MUTATION`
- AUD: `FND04-AUD-CANDIDATE-0005 / ACTIVE / READ_ONLY_REVIEW`
- no integration/freeze yet

## FND-04 current Main gate

- PR #336: MERGED
- exact merge SHA: `6c810647c9773a19b212d9c33694780141786ac7`
- merge tree: `1221ff55963052be4e924dd644efbaa65763f546`
- AUD: ACCEPTABLE on exact candidate `c89ad92ed38dcacc6d00c4a9b907720f9dbfcf9e`
- post-merge EliteSCADA CI `35913456486`: IN PROGRESS
- Web build: SUCCESS
- Backend build/test/smoke: IN PROGRESS
- state: **INTEGRATED / POST-MERGE CI PENDING / NOT YET VERIFIED-FROZEN**
- control plane commit: `bb876c51bb1bb116e0f33b2a61291bf78a213b2c`
