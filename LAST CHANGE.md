# LAST CHANGE — EliteSCADA

**Date:** 2026-09-09 BRT
**Operational state:** **WAVE14 C26 ACTIVE / C26.1–C26.9 VALIDATED / C26.10 IMPLEMENTED BUT NOT VALIDATED — CURRENT WAVE11 PACKAGE-SCHEMA BLOCKER / C26.11 NOT STARTED / #212 NOT AUTHORIZED / #266+#288 NEVER MERGE / POST-C26 WORK #289 NOT YET EXECUTABLE / WAVE13 PAUSED**

> GitHub live is the official and sole project memory. Revalidate refs, PR/issue state, exact files and exact-SHA workflows before every decision, diagnosis, code/documentation write, PR action, rerun or merge. If this file differs from GitHub live, GitHub wins.

## Current execution boundary

- Repository: `brunolrogerio-collab/EliteSCADA`
- Active branch: `wave14/c26-po-homologation-corrections`
- Coordinator issue: #286
- Implementation PR #287: C26 -> `wave14/c11-canonical-eee-demo`, OPEN/DRAFT
- Validation PR #288: C26 -> `main`, OPEN/DRAFT, **VALIDATION ONLY / MUST NEVER MERGE**

Current exact C26 **product/test** candidate:

`e7c8a8bf3954890a1ca841498222bf6094952baf`

`fix(w14-c26): serialize shared PostgreSQL schema setup`

The coordinator-rotation commit containing this document is documentation-only and sits above that product/test SHA. Revalidate the live branch HEAD and do not treat a docs-only HEAD as a validated product candidate.

Last exact C26 product/test SHA proven green across all five normal gates:

`55f292359af86f5f28d90cc578d4ac93ccc1f19c`

`fix(w14-c26): align Popup authoring with Runtime`

## Current C26 status

- C26.1–C26.7 — **IMPLEMENTED / VALIDATED**
- C26.8 Screen editor basic authoring — **IMPLEMENTED / VALIDATED** at `6de64ed4d11ac1e525f32d7071b9624e428c96ee`
- C26.9 Popup editor basic authoring — **IMPLEMENTED / VALIDATED** at `55f292359af86f5f28d90cc578d4ac93ccc1f19c`
- C26.10 Canonical EEE cleanup — **IMPLEMENTED / NOT VALIDATED / CURRENT BLOCKER**
- C26.11 Repackage + new Preview — **NOT STARTED / BLOCKED**

## C26.10 candidate chain

Initial C26.10 visual candidate:

`50363bcc50037cc6932a1282e58e3b6d75fda9f2`

`fix(w14-c26): make EEE pump state labels legible`

The structural audit found one residual EEE-authoring defect: `PARADA` remained mounted beneath the dynamic `OPERANDO` and `FALHA` texts in the canonical pump Dynamo. The candidate added opaque backgrounds and corner radii directly to those three `core.text` elements and added strict Runtime assertions.

At `50363bcc...`, EliteSCADA CI exposed an independent generic PostgreSQL startup race: `PostgreSqlOperationalEventHistoryStore` used advisory lock `4993446713136202562`, while the other stores creating shared schema `elitescada` use `4993446713136202561`. Concurrent `CREATE SCHEMA IF NOT EXISTS` caused PostgreSQL `23505` on `pg_namespace_nspname_index`.

Minimal generic database correction:

`e7c8a8bf3954890a1ca841498222bf6094952baf`

- Operational Event history now uses shared infrastructure lock `4993446713136202561`;
- `PostgreSqlConcurrentInitializationTests` now includes real concurrent Operational Event initialization/query;
- no security, authority, lifecycle, package, Driver or Runtime contract was weakened.

EliteSCADA CI #1471 passed on `e7c8a8...`, confirming the backend correction in the normal gate.

## Exact gate state at current product/test SHA

At `e7c8a8bf3954890a1ca841498222bf6094952baf`:

- EliteSCADA CI #1471 / run `34347157464` — **SUCCESS**
- Preview Licensing CI #419 / run `34347157585` — **SUCCESS**
- Interop Lab Smoke #302 / run `34347159685` — **SUCCESS**
- L3 Seven-Driver Lab #375 / run `34347157398` — **SUCCESS**
- Wave 11 Active HMI Runtime #397 / run `34347157735` — **FAILURE**
- additional natural Interop #301 / run `34347157333` — **SUCCESS**

No rerun was requested.

### Current Wave11 blocker

Failed job:

`102451385709` — `Active revision browser lifecycle`

Failed step:

`Run Wave 11 Active Runtime browser lifecycle`

Result:

- 17 passed;
- 2 failed;
- 4 did not run.

The two failing tests were:

- `tests-wave11/c11-eee-demo-hmi.spec.ts`
- `tests-wave11/c26-popup-composition.spec.ts`

Both failed during package Preview because `preview.canApply` was false. The single invalid entity was Dynamo `eee.dynamo.pump`, operation 3. The backend returned `VISUAL_PROPERTY_INVALID` for:

- `pump-stopped-label`
- `pump-running-label`
- `pump-fault-label`

Exact reported reason:

`core.text` does not declare property `backgroundColor`.

The exact backend and browser built-in schemas define `core.text` as base + text properties; neither `backgroundColor` nor `cornerRadius` belongs to that object contract. The canonical EEE helper currently authors both properties on these text elements. Therefore C26.10 is not package-valid and is not validated.

Playwright artifact:

- `playwright-report-wave11`
- artifact ID `10102316731`
- run `34347157735`

## Immediate next safe action

1. Revalidate GitHub live, including the docs-only branch HEAD and exact product/test SHA `e7c8a8...`.
2. Re-read Wave11 #397 / job `102451385709` and the exact C26.10 package/schema files before editing.
3. Correct the EEE state-plate composition using only schema-valid public visual objects/properties, with deterministic package and Runtime regressions.
4. Do not weaken Preview validation or the strict tests.
5. Do not widen `core.text` solely to accommodate EEE. Any generic schema expansion would require a genuine platform requirement, synchronized backend/browser contracts and full regressions.
6. Publish only the smallest evidence-supported correction and let all workflows start naturally.
7. Require all five normal gates green on the same new exact product/test SHA.
8. Only then mark C26.10 VALIDATED and begin C26.11.

Do not rerun the unchanged `e7c8a8...` candidate: the failure is deterministic and diagnosed.

## C26.7–C26.9 retained validation authority

- C26.7 validated at `b1bd1f664b06684afaecbbff2d27da10760cc1ec`: Elite #1466, Preview #414, Interop #292, Wave11 #392 and L3 #370 — all SUCCESS.
- C26.8 validated at `6de64ed4d11ac1e525f32d7071b9624e428c96ee`: Elite #1468, Preview #416, Interop #295, Wave11 #394 and L3 #372 — all SUCCESS.
- C26.9 validated at `55f292359af86f5f28d90cc578d4ac93ccc1f19c`: Elite #1469, Preview #417, Interop #297, Wave11 #395 and L3 #373 — all SUCCESS.

## Post-C26 quality route — not yet executable

Issue #289 and `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md` remain binding, but must not be executed now.

Required order:

`C26 exact-SHA green + accepted -> separately authorized C26->C11 integration -> corrected canonical C11 -> new package/checksum/provenance -> NEW post-C26 Preview -> technical green + fully prepared live environment -> READY FOR WORK AUDIT -> ChatGPT Work real browser audit -> coordinator triage/reproduction/corrections -> new exact candidate -> targeted Work recheck if justified -> final Product Owner homologation`

Do not spend the approximately 40-minute Work window preparing database, package, ports, application or users. Create `docs/WORK-UI-AUDIT-HANDOFF.md` only when a real candidate is ready, with exact SHA, real URLs, package SHA-256 and non-secret authentication instructions.

## Protected governance

- never modify `main` directly;
- PR #287 is only C26 -> canonical C11 and must not merge before C26 is validated, accepted and its integration is explicitly authorized;
- PR #288 and PR #266 are validation-only and **MUST NEVER MERGE**;
- PR #263 is only canonical C11 -> `wave14/corrections-integration`;
- PR #212 remains OPEN/DRAFT and **MUST NOT MERGE** without later, separate and explicit Product Owner authorization;
- neither `siga`, green CI, C26 completion, Preview, Work audit nor homologation authorizes #212;
- preserve Preview #285 untouched as pre-C26 evidence;
- do not execute issue #289 early;
- no force push, destructive rebase, branch deletion, blind rerun or unrelated cleanup;
- never weaken tests, validation, security, authentication, authorization, Identity, Engineering Lock, Licensing, lifecycle, package, Drivers or Runtime Active Revision authority;
- Runtime/Active must remain self-contained and cannot depend on `.escadalib`;
- no EEE-specific workaround for a generic product defect;
- Alarm, Operational Event and Audit remain distinct;
- Wave13 #205/#207 remains paused.

Canonical rotation handoff:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-09.md`

Copy-ready next-chat handoff:

`docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
