# LAST CHANGE — EliteSCADA

**Date:** 2026-09-23 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 VERIFIED+FROZEN / INFRA-CI-01A VERIFIED+FROZEN / FND-04 ACTIVE IN SAME CODEX / FND-06 NOT STARTED / FC0-A BLOCKED**

## Latest verified product checkpoint

FND-03 product checkpoint remains:

`a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

tree:

`e48c8b9918f4d3a5ae4dee1df6211393c95b6513`

## INFRA-CI-01A closed

PR #335 merged:

`9f62ad56e3fed5574bab1fa25fc8b64f9e4ae981`

tree:

`1e19a38803e319a418f476d236dfb24fd38d377e`

Evidence:
- candidate T1 run #4 / `35864183668` — SUCCESS;
- broad post-merge CI #1560 / `35864708583` — SUCCESS;
- Web `107193247522` — SUCCESS;
- Backend `107193247822` — SUCCESS;
- Chromium `107193893312` — SUCCESS.

Therefore `INFRA-CI-01A = VERIFIED/FROZEN`.

The broad universal gate remains intact for integrated pushes/main PRs; Wave 15 leaf PRs now use profile-aware T1 routing.

## Current active work

FND-04 is now actively assigned to the same functional CODEX runtime that finished INFRA-CI-01A.

Order:

`FND04-CODEX-TAGREF-V1-03`

Exact product base:

`a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

Work branch:

`work/w15-fnd-04-script-tag-reference-resolution`

Control commit:

`65ac0551c17f1794e5a0411897c85327880e12f8`

Execution remains:
- RED-1 / RED-2 / RED-3 first on old behavior;
- then production implementation inside frozen allowlists;
- full GREEN matrix;
- one PR with `VALIDATION_PROFILE: SCRIPT_ENGINEERING, SCRIPT_RUNTIME`;
- natural Wave 15 T1 validation;
- no self-merge.

Normal FND-04 DEV remains BLOCKED_ENV / WATCH_ONLY. AUD remains WAIT_CANDIDATE.

## Remaining FC0-A blockers

- FND-04 — ACTIVE.
- FND-06 — NOT STARTED.

INFRA-CI-01A is no longer a blocker.
