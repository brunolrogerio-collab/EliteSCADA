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

- PR #336 current Main-reviewed head: `5c77eb418b83af57ccd1812c9c21fd44919b2ca0`
- tree: `181c6296c35fbb9dd6486d3ef6a318ff0b97dc70`
- natural T1 `35907518317`: SUCCESS
- Main disposition: **REJECTED BEFORE AUD / CORRECTION REQUIRED / NOT INTEGRATED**
- reason: cross-language JS/Python comparers attempted to mimic canonical `.NET StringComparer.OrdinalIgnoreCase`, creating a second TAG-path comparison authority
- frozen separation:
  - persisted `TagBinding.Reference` -> current TAG path uses canonical backend registry semantics only;
  - Python source argument -> persisted declared binding uses exact token equality after trim;
  - after declaration membership, runtime still proves expected TagId through canonical registry/protected path
- active CODEX order: `FND04-CODEX-SOURCE-BINDING-SEPARATION-08`
- AUD: `FND04-AUD-WAIT-SOURCE-BINDING-0008`
- control commit: `8b2cbf4ebe55ede494d7ad3ed9c4809911b741e0`
- canonical handoff commit: `c7837d220e48c607a7f9fb473e33e0c0a3e43f94`
- no merge/freeze authority

