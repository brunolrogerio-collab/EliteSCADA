# Current Coordinator Handoff

> GitHub live is the official memory and sole authority for EliteSCADA. Revalidate live before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge. Any divergence here is resolved in favor of GitHub.

## Current topology

- Repository: `brunolrogerio-collab/EliteSCADA`
- Active branch: `wave14/c26-po-homologation-corrections`
- Coordinator issue: #286
- PR #287: C26 -> `wave14/c11-canonical-eee-demo`, OPEN/DRAFT
- PR #288: C26 -> `main`, **VALIDATION ONLY / MUST NEVER MERGE**
- canonical C11 head at last revalidation: `a724ece64a292aa1d1dedd886a72fb28ff8d90fe`
- PR #266: C11 -> `main`, **VALIDATION ONLY / MUST NEVER MERGE**
- PR #263: C11 -> `wave14/corrections-integration` only
- PR #212: integration -> `main`, OPEN/DRAFT, **NO MERGE without later separate explicit Product Owner authorization**
- Preview #285: preserve untouched as historical pre-C26 evidence
- issue #289: mandatory post-C26 ChatGPT Work audit gate; **do not execute now**
- Wave13 #205/#207: paused

## Exact execution boundary

Current C26 product/test SHA:

`e7c8a8bf3954890a1ca841498222bf6094952baf`

`fix(w14-c26): serialize shared PostgreSQL schema setup`

The live branch HEAD after this rotation is a documentation-only commit above the product SHA. Revalidate it; never use the docs-only SHA as product validation evidence.

Last exact product/test SHA with all five normal gates green:

`55f292359af86f5f28d90cc578d4ac93ccc1f19c`

`fix(w14-c26): align Popup authoring with Runtime`

Status:

- C26.1–C26.7 — **IMPLEMENTED / VALIDATED**
- C26.8 Screen editor — **IMPLEMENTED / VALIDATED** at `6de64ed4...`
- C26.9 Popup editor — **IMPLEMENTED / VALIDATED** at `55f29235...`
- C26.10 canonical EEE cleanup — **IMPLEMENTED / NOT VALIDATED / CURRENT BLOCKER**
- C26.11 package/new Preview — **NOT STARTED / BLOCKED**

## Current exact-SHA gates

At `e7c8a8bf3954890a1ca841498222bf6094952baf`:

- EliteSCADA CI #1471 / run `34347157464` — SUCCESS
- Preview Licensing CI #419 / run `34347157585` — SUCCESS
- Interop Lab Smoke #302 / run `34347159685` — SUCCESS
- L3 Seven-Driver Lab #375 / run `34347157398` — SUCCESS
- Wave 11 Active HMI Runtime #397 / run `34347157735` — **FAILURE**
- additional natural Interop #301 / run `34347157333` — SUCCESS

No rerun was requested.

## Current blocker — C26.10 package schema

Wave11 job `102451385709`, step `Run Wave 11 Active Runtime browser lifecycle`, finished with 17 passed, 2 failed and 4 not run.

Both failures occurred during Preview/Apply setup:

- `tests-wave11/c11-eee-demo-hmi.spec.ts`
- `tests-wave11/c26-popup-composition.spec.ts`

The preview rejected Dynamo `eee.dynamo.pump` with `VISUAL_PROPERTY_INVALID` for:

- `pump-stopped-label`
- `pump-running-label`
- `pump-fault-label`

The exact error says `core.text` does not declare `backgroundColor`.

The C26.10 candidate `50363bcc...` had added `backgroundColor` and `cornerRadius` to these three text elements to prevent `PARADA` from showing through `OPERANDO`/`FALHA`. Exact backend and browser schemas define `core.text` as base + text properties and do not declare either property. The visual intent is valid, but the authored representation is package-invalid.

The uploaded Wave11 evidence is artifact `playwright-report-wave11`, ID `10102316731`, attached to run `34347157735`.

## Independent PostgreSQL correction now green

At `50363bcc...`, EliteSCADA CI exposed concurrent `CREATE SCHEMA IF NOT EXISTS elitescada` under mismatched advisory locks. Commit `e7c8a8...` changed Operational Event history to shared lock `4993446713136202561` and added it to the shared-schema concurrency regression.

EliteSCADA CI #1471 passed. Do not revert this fix while correcting the C26.10 visual schema error.

## Immediate next task

1. Read `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-09.md` first and revalidate all live refs/gates.
2. Re-read Wave11 #397/job `102451385709` plus exact C26.10 helper, regression and backend/browser visual schemas.
3. Replace the invalid text-background authoring with the smallest schema-valid EEE composition that keeps state labels opaque and legible.
4. Preserve strict package Preview and Runtime assertions.
5. Do not widen `core.text` solely for EEE; a generic contract change requires independent platform justification and synchronized backend/browser coverage.
6. Let workflows run naturally on the new exact product/test SHA; no blind rerun.
7. Require the five normal gates green on one SHA before declaring C26.10 VALIDATED.
8. Do not begin C26.11 or integrate #287 before C26 is validated, accepted and the integration is explicitly authorized.

## Validated recent milestones

- C26.7: `b1bd1f664b06684afaecbbff2d27da10760cc1ec` — 5/5 normal gates SUCCESS
- C26.8: `6de64ed4d11ac1e525f32d7071b9624e428c96ee` — 5/5 normal gates SUCCESS
- C26.9: `55f292359af86f5f28d90cc578d4ac93ccc1f19c` — 5/5 normal gates SUCCESS

## Binding post-C26 route

Issue #289 and `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md` remain mandatory only after C26 completion/acceptance, authorized C26->C11 integration, corrected canonical C11, regenerated package/checksum/provenance and a new technically ready Preview.

Prepare the live application, EEE Active Revision, simulation, TAG dynamics, Alarm/Event/Historian/HistoricalQuery, Runtime, Engineering, Screen Editor, Popup Editor and users before declaring `READY FOR WORK AUDIT`. Do not consume the approximately 40-minute Work window on setup.

## Permanent guardrails

- GitHub live wins over every handoff;
- never modify `main` directly;
- #212 has no merge authorization; `siga` never authorizes it;
- #288 and #266 must never merge;
- #287 targets canonical C11 only and is not authorized now;
- #263 is C11 -> integration only;
- preserve #285;
- #289 does not authorize early execution or protected merges;
- no force push, destructive rebase, branch deletion, blind rerun or unrelated cleanup;
- never weaken tests, validation, security, authentication, authorization, Identity, Engineering Lock, Licensing, lifecycle, package, Drivers or Runtime Active Revision authority;
- Runtime/Active cannot depend on `.escadalib`;
- no EEE-specific workaround for a generic product defect;
- Alarm, Operational Event and Audit remain separate;
- Wave13 remains paused.

Canonical detailed handoff:

`docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-09.md`
