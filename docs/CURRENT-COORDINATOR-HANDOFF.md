# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-05 BRT  
**Status:** **WAVE 14 ACTIVE / C24 ACCEPTED + INTEGRATED + POST-MERGE REVALIDATED / POST-DEMO COMPATIBILITY CORRECTIONS NEXT / C11 PRESERVED / WAVE13 PAUSED**

> GitHub is the sole development authority. Revalidate live refs, PR state and exact-SHA workflows before every decision or mutation. Documentation-only descendants do not redefine accepted product bytes.

## 1. Permanent governance

- repository: `brunolrogerio-collab/EliteSCADA`;
- Wave 14 integration branch: `wave14/corrections-integration`;
- integration PR #212 must remain **OPEN/DRAFT** and must **NOT** merge to `main` without later explicit Product Owner authorization;
- never alter `main` directly;
- C11 implementation PR #263 targets only integration and remains DRAFT until full C11 acceptance;
- C11 validation-only PR #266 targets `main` only for validation and must **NEVER MERGE**;
- Wave13 #205/#207 remains paused;
- diagnose every red before rerun;
- never weaken tests, validation, security, identity, lifecycle, licensing or package contracts to obtain green;
- no destructive rebase, force-push, branch deletion or unrelated cleanup;
- backend Active revision remains runtime authority;
- Alarm, Operational Event and Audit remain distinct;
- generic product defects receive generic product fixes, never EEE-specific workarounds.

## 2. Exact accepted product authority

Current accepted Wave 14 product integration SHA:

`40a491c2de2403f2934b8bae647c35072d5c2496`

This is the merge of accepted C24 candidate:

`ff5eac6ad12865b174b6ca602a13d01165e57b36`

The immediately following integration commit before this closeout was documentation-only:

`6c3e7ee1dbb9117f4b5bc55971cd2c3dd2d4ece1`

Do not confuse integration documentation HEAD with product authority. The product bytes accepted for C24 remain exactly `40a491...`.

## 3. C24 — ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED

### Product correction

C24 fixed the generic clean First Project inconsistency exposed by C11:

- built-in `dynamo.pump.standard` no longer depends on DEMO-only `pump.standard`;
- no historical DEMO entity was added to First Project;
- normal First Project Save -> Publish has regression coverage;
- validators were not relaxed;
- the legacy E2E contract now expects serialized `templateKey: null`.

### Pre-merge exact candidate matrix

On exact SHA `ff5eac6ad12865b174b6ca602a13d01165e57b36`:

- Preview Licensing CI #365 / `33996011571` — SUCCESS;
- Wave 11 Active HMI Runtime #343 / `33996011620` — SUCCESS;
- Interop Lab Smoke #242 / `33996011614` — SUCCESS;
- EliteSCADA CI #1415 / `33996011599` — SUCCESS;
- L3 Seven-Driver Lab #321 / `33996011595` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #120 / `33996011523` — SUCCESS additional evidence.

PR #278 merged by normal merge **only** into `wave14/corrections-integration`, producing exact product merge SHA `40a491c2de2403f2934b8bae647c35072d5c2496`.

PR #279 was validation-only and was closed **without merge**. Never reopen it as an integration route to `main`.

### Post-merge exact matrix

On exact product merge SHA `40a491c2de2403f2934b8bae647c35072d5c2496`:

- Preview Licensing CI #366 / `33996473101` — SUCCESS;
- Wave 11 Active HMI Runtime #344 / `33996473099` — SUCCESS;
- Interop Lab Smoke #243 / `33996473102` — SUCCESS;
- L3 Seven-Driver Lab #322 / `33996473180` — SUCCESS;
- EliteSCADA CI #1416 / `33996473170` — SUCCESS on run attempt 2.

Attempt 2 EliteSCADA jobs:

- Backend `101392980721` — SUCCESS;
- Web `101392980963` — SUCCESS;
- Chromium `101392980619` — SUCCESS.

No C03 workflow run is associated by live GitHub with `40a491...`; C03 #120 is therefore recorded only as additional pre-merge evidence on `ff5eac6...`.

### Diagnosed cancellation of attempt 1

The original EliteSCADA CI attempt was cancelled by PR concurrency, not by a red test:

- `.github/workflows/dotnet-ci.yml` at `40a491...` uses `group: elitescada-ci-${{ github.event.pull_request.number || github.ref }}` and `cancel-in-progress: true`;
- run #1416 attempt 1 began at `22:38:05Z`;
- documentation-only commit `6c3e7ee1...` was created at `22:42:42Z`;
- that commit triggered EliteSCADA CI #1417 / `33996692983` at `22:42:48Z` on the same PR #212 concurrency group;
- #1416 attempt 1 was cancelled at `22:43:06Z`;
- Backend and Web were already SUCCESS; Chromium had no failing step and was cancelled during `Run browser E2E tests`;
- #1417 on the later documentation SHA completed SUCCESS;
- rerunning the cancelled job preserved the exact original product SHA and attempt 2 completed fully SUCCESS.

Therefore C24 is formally closed as:

**C24 ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED.**

## 4. C11 must remain unsynchronized

Canonical branch:

`wave14/c11-canonical-eee-demo`

Exact preserved C11 head:

`41d24d89c3b9d2b881215255e44023fabde262f3`

Live PR state at closeout:

- #263 — OPEN/DRAFT, C11 -> integration, do not merge yet;
- #266 — OPEN/DRAFT validation-only -> `main`, **NEVER MERGE**;
- #281 — CLOSED WITHOUT MERGE.

Do not sync C24 into C11 now.

## 5. Binding Product Owner post-demo sequence

Issue #280, Product Owner comment `5554918787`, and `docs/WAVE14-COORDINATOR-HANDOFF-2026-09-05-C24-POST-DEMO-SEQUENCE.md` are the current binding sequence authority:

1. finish and accept C24 — **DONE**;
2. implement and accept post-DEMO corrections that can alter application/package/bootstrap contracts;
3. only after those are accepted, sync/adapt C11;
4. export/version/freeze the canonical EEE `.escadapkg`;
5. run final exact-SHA C11 matrix;
6. validate Preview;
7. Product Owner performs real visual homologation;
8. only then C11 may become ACCEPTED/FROZEN/DEMO-READY and #263 may integrate.

C11 must remain **NOT ACCEPTED / NOT FROZEN / NOT DEMO-READY** during the compatibility-affecting correction phase.

## 6. Issue #280 — Application Engineering Lock

The old optional Import/Export password idea is superseded.

Binding semantics:

- Engineering Lock is optional and protects application Engineering/IP;
- Import does not require the Engineering Lock password;
- Export does not require the Engineering Lock password;
- lock secret is separate from Authority credentials and Authority backup credentials;
- unlocked application exposes normal Engineering according to Authority permissions;
- locked application hides/disallows application engineering while retaining restricted Engineering administration;
- restricted Engineering must allow Authority user/login/password/profile management, licensing, Import, Export, solution replacement/restore and explicit unlock of the running application;
- configured password and `locked` flag are independent;
- no configured lock password means no Engineering-Lock authentication exists;
- package preserves lock metadata/state;
- whole `.escadapkg` is not encrypted merely for this feature;
- wrong password must remain locked without leaking protected Engineering content.

### Cryptography remains OPEN

A fixed/compiled symmetric key is not an accepted closed architecture. Define and security-review the verifier/secret/key storage mechanism before implementation can be accepted. Do not pretend client-shipped static key material provides strong secret protection.

## 7. Issue #280 — restore-first bootstrap

Fresh/clean installation must expose a first-class `Restaurar backup` path before requiring creation of disposable user/project state.

Restore must support:

- application import/restore;
- Authority import/restore using the Authority backup's own password/credential mechanism;
- optional license-file attachment/import;
- license is not required to execute the restore.

Engineering Lock password and Authority restore password are separate secrets and flows.

PR #273 is DESIGN ONLY and predates this refined restore-first decision. Revalidate and adapt it; do not merge it blindly.

## 8. PR #274 — design only

PR #274 remains DESIGN ONLY. Current direction includes:

- current Runtime user visible;
- explicit `Trocar usuário` and `Sair`;
- Runtime-only user never gains Engineering;
- switch-user invalidates prior session first and reloads backend capabilities;
- contextual Help uses stable language-neutral IDs;
- manual follows active UI language and should preferably be local/offline and versioned;
- Drivers/Sources/TAG/addressing/Scripts/Reports/HMI documentation must reflect real product contracts.

## 9. Next correction package coordination

Do not create a package branch from this document alone. Revalidate live issue/PR/branch numbering immediately before creation.

If C25/C26 remain free at that moment, the sensible split remains:

- C25 — Application Engineering Lock;
- C26 — Restore-first bootstrap / recovery.

That naming is coordination guidance, not authority over future GitHub state.

## 10. Resume rules

When the Product Owner says `siga`, proceed autonomously through the next safe tasks and stop only for a real technical/governance blocker or an action requiring explicit Product Owner authorization.

Before any mutation, revalidate the live GitHub state. #212 remains OPEN/DRAFT throughout this work and has no authorization to merge to `main`.
