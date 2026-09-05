# LAST CHANGE — EliteSCADA

**Date:** 2026-09-05 BRT  
**Operational state:** **WAVE 14 ACTIVE / C24 ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED / POST-DEMO COMPATIBILITY CORRECTIONS NEXT / C11 PRESERVED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only descendants do not redefine accepted product bytes.

## Current accepted product authority

Integration branch:

`wave14/corrections-integration`

Accepted C24 product merge and current product authority:

`40a491c2de2403f2934b8bae647c35072d5c2496`

Accepted C24 candidate SHA:

`ff5eac6ad12865b174b6ca602a13d01165e57b36`

The prior documentation-only integration HEAD was:

`6c3e7ee1dbb9117f4b5bc55971cd2c3dd2d4ece1`

That SHA and any later documentation-only descendants are coordination state, not product authority.

## C24 — formal closeout

**C24 ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED.**

C24 fixes the generic clean First Project inconsistency where built-in `dynamo.pump.standard` depended on historical DEMO-only template `pump.standard`:

- the built-in no longer depends on that DEMO-only template;
- no DEMO content was added to First Project;
- Save -> Publish is regression-tested through normal product paths;
- Publish/import validators were not weakened;
- the legacy Chromium E2E now asserts serialized `templateKey: null`, matching the real JSON contract.

### Exact pre-merge matrix on `ff5eac6...`

- Preview Licensing CI #365 / `33996011571` — SUCCESS;
- Wave 11 Active HMI Runtime #343 / `33996011620` — SUCCESS;
- Interop Lab Smoke #242 / `33996011614` — SUCCESS;
- EliteSCADA CI #1415 / `33996011599` — SUCCESS, including Backend, Web and Chromium E2E;
- L3 Seven-Driver Lab #321 / `33996011595` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #120 / `33996011523` — SUCCESS additional evidence.

Implementation PR #278 merged by normal merge **only** into `wave14/corrections-integration`, producing:

`40a491c2de2403f2934b8bae647c35072d5c2496`

Validation-only PR #279 was closed **without merge** and is not an integration route to `main`.

### Exact post-merge matrix on `40a491...`

- Preview Licensing CI #366 / `33996473101` — SUCCESS;
- Wave 11 Active HMI Runtime #344 / `33996473099` — SUCCESS;
- Interop Lab Smoke #243 / `33996473102` — SUCCESS;
- L3 Seven-Driver Lab #322 / `33996473180` — SUCCESS;
- EliteSCADA CI #1416 / `33996473170` — SUCCESS on rerun attempt 2, exact same product SHA.

EliteSCADA CI attempt 2 jobs:

- Backend build, test and smoke `101392980721` — SUCCESS;
- Web build `101392980963` — SUCCESS;
- Chromium end-to-end `101392980619` — SUCCESS, including `Run browser E2E tests`.

Live GitHub lists five workflow runs associated with exact merge SHA `40a491...`; no C03 workflow run is attached to this SHA. C03 #120 remains valid pre-merge additional evidence on `ff5eac6...`, but is not recorded as post-merge exact-SHA evidence.

### Why EliteSCADA CI attempt 1 was cancelled

At exact product SHA `40a491...`, `.github/workflows/dotnet-ci.yml` defines PR-scoped concurrency:

- group: `elitescada-ci-${{ github.event.pull_request.number || github.ref }}`;
- `cancel-in-progress: true`.

For PR #212 that means all in-progress EliteSCADA CI runs share `elitescada-ci-212`.

Attempt 1 of run `33996473170` started at `2026-09-05T22:38:05Z`. Documentation-only commit `6c3e7ee1...` was created at `22:42:42Z`, triggered EliteSCADA CI #1417 / `33996692983` at `22:42:48Z`, and the earlier run was cancelled at `22:43:06Z`. In attempt 1 Backend and Web were already SUCCESS and Chromium was cancelled while executing the E2E step, with no failing step. CI #1417 on the later documentation SHA completed SUCCESS.

This proves the original cancellation was supersession/concurrency, not a product red. The cancelled job was rerun without creating a new product SHA, and attempt 2 completed fully green.

## Binding Product Owner sequence after C24

Issue #280, comment `5554918787`, and `docs/WAVE14-COORDINATOR-HANDOFF-2026-09-05-C24-POST-DEMO-SEQUENCE.md` supersede the earlier immediate C24 -> C11 sync sequence:

1. C24 accepted — **DONE**;
2. implement and exact-SHA accept compatibility-affecting post-DEMO corrections;
3. only then sync/adapt C11 to accepted application/package/bootstrap contracts;
4. export/version/freeze canonical `EliteSCADA-EEE-Demo.escadapkg`;
5. run final matrix and Preview;
6. Product Owner performs real visual homologation;
7. only then C11 may become ACCEPTED/FROZEN/DEMO-READY and #263 may integrate.

C11 remains intentionally unchanged at:

`41d24d89c3b9d2b881215255e44023fabde262f3`

PR #281 was closed without merge. Do not synchronize C24 into C11 yet.

## Post-DEMO product decisions

Issue #280 remains OPEN and authoritative:

- optional Application Engineering Lock protects application engineering/IP, not Import/Export;
- Import and Export never require the Engineering Lock password;
- password presence and `locked` state are separate;
- locked Engineering must still permit Authority administration, licensing, Import/Export, solution replacement and explicit unlock;
- package preserves lock metadata/state but the whole `.escadapkg` is not encrypted;
- cryptographic architecture remains OPEN; no fixed/compiled symmetric-key design is accepted as a closed security solution;
- fresh-install bootstrap must expose `Restaurar backup` before disposable user/project creation;
- restore supports application + Authority restore credential/password + optional license, with license not required for restore.

PR #273 and PR #274 remain DESIGN ONLY and must be revalidated/adapted, never merged blindly.

## Permanent governance

- #212 remains OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization;
- never alter `main` directly;
- #263 remains C11 implementation DRAFT -> integration only;
- #266 remains C11 validation-only -> `main` and **MUST NEVER MERGE**;
- Wave13 #205/#207 remains paused;
- no force-push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose every red before rerun;
- never weaken tests, validation, security, identity, lifecycle or contracts for green CI;
- no EEE-specific workaround for generic product requirements;
- backend Active revision remains authority;
- Alarm / Operational Event / Audit remain distinct.

Read next: `docs/CURRENT-COORDINATOR-HANDOFF.md`, issue #280 and `docs/WAVE14-COORDINATOR-HANDOFF-2026-09-05-C24-POST-DEMO-SEQUENCE.md`.
