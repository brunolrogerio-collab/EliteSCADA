# Wave 14 Coordinator Handoff — 2026-09-05 — C24 post-merge closeout and post-demo sequence

> **Authority:** GitHub is the official project memory. Revalidate every branch, PR, SHA and CI state live before acting. Documentation-only commits after the accepted product merge do not redefine product bytes.

## 1. Non-negotiable governance

- Repository: `brunolrogerio-collab/EliteSCADA`.
- Integration branch: `wave14/corrections-integration`.
- Integration PR #212 must remain **OPEN/DRAFT** and must **NOT** merge to `main` without later explicit Product Owner authorization.
- Never alter `main` directly.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose every red before rerun. Never weaken tests, validation, security, identity, lifecycle or contracts to obtain green.
- #266 is C11 validation-only to `main`: **NEVER MERGE**.
- #263 may integrate C11 only after full final C11 acceptance.
- Wave13 #205/#207 remains paused.

## 2. C24 — formally accepted and closed

### Problem corrected

A clean First Project installed built-in `dynamo.pump.standard` with a dependency on `pump.standard`, but that equipment template existed only in historical DEMO seeding. The clean project could Save, while Publish correctly failed with `DYNAMO_TEMPLATE_NOT_FOUND`.

The accepted generic correction:

- removes the DEMO-only template dependency from the built-in Dynamo;
- adds no DEMO content to First Project;
- preserves normal Dynamo behavior and bindings;
- keeps Publish/import validators strict;
- adds a real clean First Project Save -> Publish regression;
- aligns the legacy E2E with the actual serialized contract, `templateKey: null`.

### Accepted candidate

Exact C24 candidate SHA:

`ff5eac6ad12865b174b6ca602a13d01165e57b36`

Pre-merge exact-SHA evidence:

- Preview Licensing CI #365 / `33996011571` — SUCCESS;
- Wave 11 Active HMI Runtime #343 / `33996011620` — SUCCESS;
- Interop Lab Smoke #242 / `33996011614` — SUCCESS;
- EliteSCADA CI #1415 / `33996011599` — SUCCESS, including Chromium E2E;
- L3 Seven-Driver Lab #321 / `33996011595` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #120 / `33996011523` — SUCCESS additional evidence.

PR #278 merged normally **only** into `wave14/corrections-integration`.

Exact integration product merge:

`40a491c2de2403f2934b8bae647c35072d5c2496`

Parents:

- `0380b188c5fa7aefac69db1cf7350e8ebebb8395`;
- `ff5eac6ad12865b174b6ca602a13d01165e57b36`.

PR #279 was validation-only and is CLOSED WITHOUT MERGE. It must never be reopened as a route to `main`.

### Post-merge exact-SHA evidence

On exact product merge `40a491c2de2403f2934b8bae647c35072d5c2496`:

- Preview Licensing CI #366 / `33996473101` — SUCCESS;
- Wave 11 Active HMI Runtime #344 / `33996473099` — SUCCESS;
- Interop Lab Smoke #243 / `33996473102` — SUCCESS;
- L3 Seven-Driver Lab #322 / `33996473180` — SUCCESS;
- EliteSCADA CI #1416 / `33996473170` — SUCCESS on attempt 2.

EliteSCADA CI attempt 2 job results:

- Backend build, test and smoke `101392980721` — SUCCESS;
- Web build `101392980963` — SUCCESS;
- Chromium end-to-end `101392980619` — SUCCESS.

Live GitHub returns five workflow runs associated with exact merge SHA `40a491...`; it does not associate a C03 workflow run with this merge SHA. C03 #120 is therefore retained only as additional pre-merge evidence on `ff5eac6...`.

### Cancellation diagnosis and rerun justification

At exact SHA `40a491...`, `.github/workflows/dotnet-ci.yml` uses:

- `group: elitescada-ci-${{ github.event.pull_request.number || github.ref }}`;
- `cancel-in-progress: true`.

For PR #212 the concurrency group is therefore `elitescada-ci-212`.

Timeline:

1. EliteSCADA CI #1416 attempt 1 started at `2026-09-05T22:38:05Z` on exact product SHA `40a491...`.
2. Documentation-only commit `6c3e7ee1dbb9117f4b5bc55971cd2c3dd2d4ece1` was created at `22:42:42Z` on the same integration branch/PR.
3. That commit triggered EliteSCADA CI #1417 / `33996692983` at `22:42:48Z` in the same PR concurrency group.
4. #1416 attempt 1 was cancelled at `22:43:06Z`.
5. Attempt 1 had Backend and Web SUCCESS; Chromium was cancelled while running the E2E step, with no failing step.
6. #1417 on the later documentation SHA completed SUCCESS.
7. The cancelled #1416 job was rerun without creating any new product SHA. Run `33996473170` remained pinned to exact head SHA `40a491...` and attempt 2 completed SUCCESS.

The cancellation is therefore confirmed as branch supersession/concurrency, not a product red.

Formal C24 status:

**C24 ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED.**

## 3. Product versus documentation authority

Accepted product authority remains:

`40a491c2de2403f2934b8bae647c35072d5c2496`

The following known descendant before this closeout is documentation-only:

`6c3e7ee1dbb9117f4b5bc55971cd2c3dd2d4ece1`

Later closeout/documentation descendants remain documentation state only. Always distinguish current integration HEAD from the exact accepted product SHA above.

## 4. C11 remains intentionally unsynchronized

Branch:

`wave14/c11-canonical-eee-demo`

Preserved exact head:

`41d24d89c3b9d2b881215255e44023fabde262f3`

Live state at C24 closeout:

- #263 — OPEN/DRAFT, C11 -> integration;
- #266 — OPEN/DRAFT validation-only -> `main`, **NEVER MERGE**;
- #281 — CLOSED WITHOUT MERGE.

Do not sync C24 into C11 now.

## 5. Binding Product Owner sequence

Issue #280 and Product Owner comment `5554918787` supersede the older sequence that would have synchronized/frozen C11 immediately after C24.

Required order:

1. finish and accept C24 — **DONE**;
2. implement and accept compatibility-affecting post-DEMO product corrections;
3. only then sync/adapt C11 to the final accepted application/package/bootstrap contracts;
4. export/version/freeze the canonical `EliteSCADA-EEE-Demo.escadapkg`;
5. run final C11 exact-SHA matrix;
6. validate Preview;
7. Product Owner performs real visual homologation;
8. only after all acceptance conditions may C11 become ACCEPTED/FROZEN/DEMO-READY and #263 integrate.

C11 remains deliberately **NOT ACCEPTED / NOT FROZEN / NOT DEMO-READY** during the post-DEMO compatibility phase.

## 6. Application Engineering Lock — binding semantics

The earlier idea of requiring a password for `.escadapkg` Import/Export is cancelled.

The intended feature is an optional **Application Engineering Lock** for application engineering/IP protection:

- password is optional;
- Import does **not** request the Engineering Lock password;
- Export does **not** request the Engineering Lock password;
- it is not an Authority/login credential;
- unlocked application exposes normal Engineering according to normal Authority authorization;
- locked application must not expose/permit application engineering editing;
- restricted locked Engineering must retain Authority user/login/password/profile administration, licensing, Import, Export, solution replacement/restore and explicit unlock of the currently running application;
- configured secret and `locked` flag are independent state;
- a project may have a configured secret with `locked=false`;
- no configured secret means there is no Engineering-Lock password authentication;
- package preserves lock metadata/state;
- the entire `.escadapkg` is not encrypted merely to implement the Lock;
- correct password unlocks the running application; wrong password remains locked without leaking protected Engineering content.

### Cryptographic architecture remains OPEN

No fixed/compiled symmetric-key architecture has been accepted as the security design. Define and security-review the verifier/secret/key mechanism before acceptance. Engineering Lock secret remains distinct from Authority login credentials and Authority-backup encryption/passwords.

## 7. Restore-first bootstrap — binding semantics

Fresh/clean installation must offer `Restaurar backup` at the initial bootstrap surface before requiring creation of a disposable user/project.

Restore supports:

- application import/restore;
- Authority import/restore with the Authority backup's own password/credential mechanism;
- optional license-file attachment/import;
- license is not required to execute restore.

Engineering Lock password is not the Authority restore password.

PR #273 remains DESIGN ONLY and must be revalidated/adapted to this newer restore-first decision. Do not merge it blindly.

## 8. PR #274 remains DESIGN ONLY

Current product direction to preserve:

- current Runtime user remains visible;
- `Trocar usuário` and `Sair` are explicit actions;
- Runtime-only users never gain Engineering;
- switch-user invalidates the previous session first and reloads backend capabilities;
- Help uses stable language-neutral IDs;
- manual follows active UI locale and should preferably be versioned/local/offline;
- detailed Drivers/Sources/TAG/addressing/Scripts/Reports/HMI documentation must derive from real product contracts.

## 9. Next correction package coordination

Revalidate numbering live immediately before branch creation. If C25/C26 remain unused then, the current sensible split is:

- C25 — Application Engineering Lock;
- C26 — Restore-first bootstrap / recovery.

This is coordination guidance only. GitHub live state remains authoritative.

## 10. Operating mode

When the Product Owner says `siga`, continue autonomously across the next safe tasks. Pause only for a real technical/governance blocker or an action requiring explicit Product Owner authorization.

#212 remains OPEN/DRAFT throughout and is not authorized for merge to `main`.
