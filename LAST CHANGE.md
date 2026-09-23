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

`FND04-CODEX-WAIT-AUD-05`

Exact product base:

`a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

Work branch:

`work/w15-fnd-04-script-tag-reference-resolution`

Control commit:

`4981794478447de020499e473b6401173e294a7e`

PR #336 candidate `21e2ab69a71844d56acf1b7913dc67097697f6ae` reached green T1 but is not integration-ready. Main review found Client Visual expected-TagId enforcement missing, a sixth resolver state outside the frozen contract, and incomplete direct acceptance/RED evidence. CODEX is correcting the same PR inside the existing FND-04 allowlists; no merge is authorized.

Normal FND-04 DEV remains BLOCKED_ENV / WATCH_ONLY. AUD remains ACTIVE / READ_ONLY_REVIEW.

## Remaining FC0-A blockers

- FND-04 — ACTIVE.
- FND-06 — NOT STARTED.

INFRA-CI-01A is no longer a blocker.


## FND-04 corrected candidate under audit

- candidate: `8dba4f1161d4ca5190ddfa37b48d9736478d73ec`
- tree: `393938536ee524d2bd7c713ed9f791679fd6c2cb`
- PR #336 natural T1 `35895957135` — SUCCESS
- Main preliminary review: prior known blockers closed
- CODEX: `WAIT_AUD / NO_MUTATION`
- AUD: `FND04-AUD-CANDIDATE-0005 / ACTIVE / READ_ONLY_REVIEW`
- no integration/freeze yet

## FND-04 current Main gate

- PR #336 immutable candidate: `c89ad92ed38dcacc6d00c4a9b907720f9dbfcf9e`
- reported tree: `7fa948de25e4f120566113dc8d14a0a696342a32`
- natural Wave 15 T1 `35910214760`: SUCCESS
- Main disposition: **ACCEPTED FOR INDEPENDENT REAUDIT / NOT INTEGRATED**
- source declaration matching: exact after trim in Client Visual + Server Script
- persisted binding -> current TAG path: canonical backend registry proof with expected TagId
- no JS/Python Unicode readable-path comparer clone
- CODEX: `FND04-CODEX-WAIT-REAUDIT-09 / WAIT_AUD / NO_MUTATION`
- AUD: `FND04-AUD-REAUDIT-SOURCE-BINDING-0009 / ACTIVE / READ_ONLY_REVIEW`
- control commit: `9d1baec56ae723d68bc3a58dd00b1903417caa5b`
- canonical handoff commit: `aaded72a8df22b9f1d9cde30a7ff5fa300d8b312`
- no merge/freeze authority

