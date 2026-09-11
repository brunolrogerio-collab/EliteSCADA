# Wave 14 C26 Coordinator Handoff — 2026-09-09

## Rule zero

**GitHub live is the official memory and sole authority for EliteSCADA.**

Before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge, revalidate the live branch/ref, issues, PR topology, exact files and exact-SHA workflow evidence. If this handoff conflicts with GitHub live, GitHub wins.

## Mandatory reading order

Read live from `wave14/c26-po-homologation-corrections`, in this order:

1. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-09.md`
2. `docs/CURRENT-COORDINATOR-HANDOFF.md`
3. `LAST CHANGE.md`
4. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
5. `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`
6. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
7. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-08.md`
8. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md`
9. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
10. `docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

Then revalidate live:

- active branch and exact HEAD;
- issue #286 and issue #289;
- PRs #287, #288, #212, #266 and #263;
- canonical C11 branch;
- historical Preview #285;
- workflows and complete failed-job evidence for the exact product/test SHA.

## Live topology at transfer

Repository:

`brunolrogerio-collab/EliteSCADA`

Active branch:

`wave14/c26-po-homologation-corrections`

Issue:

#286 — OPEN

PR topology revalidated immediately before this handoff:

- #287 — OPEN/DRAFT — C26 -> `wave14/c11-canonical-eee-demo`
- #288 — OPEN/DRAFT — C26 -> `main` — **VALIDATION ONLY / MUST NEVER MERGE**
- #212 — OPEN/DRAFT — `wave14/corrections-integration` -> `main` — **NOT AUTHORIZED TO MERGE**
- #266 — OPEN/DRAFT — canonical C11 -> `main` — **VALIDATION ONLY / MUST NEVER MERGE**
- #263 — OPEN/DRAFT — canonical C11 -> `wave14/corrections-integration` only
- #285 — OPEN/DRAFT historical pre-C26 Preview; preserve untouched

Heads at the last live revalidation:

- C26 product/test: `e7c8a8bf3954890a1ca841498222bf6094952baf`
- canonical C11: `a724ece64a292aa1d1dedd886a72fb28ff8d90fe`
- integration: `ff185ffd67fe4abc597af9184c21f86376ba6e17`
- Preview #285: `d92e81f821c1a9c376b39bc3684eead54b3f570e`

This handoff is published in a documentation-only commit above the product/test SHA. Revalidate the live branch HEAD after rotation and keep the distinction explicit.

## Transfer state

Current exact C26 product/test candidate:

`e7c8a8bf3954890a1ca841498222bf6094952baf`

`fix(w14-c26): serialize shared PostgreSQL schema setup`

Last exact product/test SHA proven 5/5 green:

`55f292359af86f5f28d90cc578d4ac93ccc1f19c`

`fix(w14-c26): align Popup authoring with Runtime`

Current C26 classification:

- C26.1–C26.7 — **IMPLEMENTED / VALIDATED**
- C26.8 Screen editor functionality — **IMPLEMENTED / VALIDATED**
- C26.9 Popup editor functionality — **IMPLEMENTED / VALIDATED**
- C26.10 canonical EEE cleanup — **IMPLEMENTED / NOT VALIDATED / CURRENT BLOCKER**
- C26.11 repackage and new Preview — **NOT STARTED / BLOCKED**

## Retained exact-SHA validation

### C26.7 — validated

Exact SHA:

`b1bd1f664b06684afaecbbff2d27da10760cc1ec`

Five normal gates:

- EliteSCADA CI #1466 / run `34286582875` — SUCCESS
- Preview Licensing CI #414 / run `34286582844` — SUCCESS
- Interop Lab Smoke #292 / run `34286582814` — SUCCESS
- Wave 11 Active HMI Runtime #392 / run `34286582873` — SUCCESS
- L3 Seven-Driver Lab #370 / run `34286582905` — SUCCESS

Additional natural Interop #291 / run `34286580927` also succeeded. No rerun.

### C26.8 — validated

Exact SHA:

`6de64ed4d11ac1e525f32d7071b9624e428c96ee`

`fix(w14-c26): keep canvas overlays off controls`

Five normal gates:

- EliteSCADA CI #1468 / run `34305175515` — SUCCESS
- Preview Licensing CI #416 / run `34305175507` — SUCCESS
- Interop Lab Smoke #295 / run `34305175549` — SUCCESS
- Wave 11 Active HMI Runtime #394 / run `34305175519` — SUCCESS
- L3 Seven-Driver Lab #372 / run `34305175521` — SUCCESS

Additional natural Interop #296 / run `34305175891` also succeeded. No rerun.

### C26.9 — validated

Exact SHA:

`55f292359af86f5f28d90cc578d4ac93ccc1f19c`

`fix(w14-c26): align Popup authoring with Runtime`

Five normal gates:

- EliteSCADA CI #1469 / run `34309372353` — SUCCESS
- Preview Licensing CI #417 / run `34309372378` — SUCCESS
- Interop Lab Smoke #297 / run `34309372338` — SUCCESS
- Wave 11 Active HMI Runtime #395 / run `34309372309` — SUCCESS
- L3 Seven-Driver Lab #373 / run `34309372297` — SUCCESS

Additional natural Interop #298 / run `34309372823` also succeeded. No rerun.

## C26.10 immediate history

### Project audit and initial candidate

The C26.10 structural audit of the composed canonical EEE package checked:

- 38 TAGs;
- 13 Commands;
- 6 Screens;
- 2 Popups;
- 1 Dynamo;
- 190 visual IDs.

All TAG references, Commands, navigation destinations, Popup references and Dynamo parameters resolved. All relevant geometry remained within logical bounds; the only raw zero-height item was a valid `core.line`.

One real EEE-authoring defect was found: in `eee.dynamo.pump`, the fixed `PARADA` text remained mounted beneath dynamic `OPERANDO` and `FALHA`. The dynamic text backgrounds were transparent, so labels overprinted one another.

Candidate:

`50363bcc50037cc6932a1282e58e3b6d75fda9f2`

`fix(w14-c26): make EEE pump state labels legible`

Exactly two files changed:

- `web/scada-web/tests-wave11/c11-eee-demo-hmi.ts`
- `web/scada-web/tests-wave11/c11-eee-demo-hmi.spec.ts`

The candidate added `backgroundColor` and `cornerRadius` directly to the three `core.text` state labels and asserted opaque computed Runtime colors.

### Independent failures at the first candidate

At `50363bcc...`:

- Preview Licensing #418 — SUCCESS
- Interop #299/#300 — SUCCESS
- L3 #374 — SUCCESS
- EliteSCADA CI #1470 — FAILURE
- Wave11 #396 — FAILURE

EliteSCADA CI failed on PostgreSQL `23505`, duplicate `pg_namespace_nspname_index`, during concurrent schema initialization. Exact source diagnosis showed:

- shared schema stores use advisory lock `4993446713136202561`;
- `PostgreSqlOperationalEventHistoryStore` used `4993446713136202562`;
- concurrent `CREATE SCHEMA IF NOT EXISTS elitescada` was therefore not serialized.

Wave11 #396 independently recorded one Server Script timeout at the existing 250 ms boundary, followed by recovery: 20 of 21 executions completed, zero faults, zero rejected/coalesced/dropped events, last status 0 and zero consecutive failures. No assertion or timing threshold was changed.

No blind rerun occurred.

### PostgreSQL correction

Current product/test SHA:

`e7c8a8bf3954890a1ca841498222bf6094952baf`

`fix(w14-c26): serialize shared PostgreSQL schema setup`

Exactly two files:

- `src/Scada.Persistence.PostgreSql/PostgreSqlOperationalEventHistoryStore.cs`
- `tests/Scada.Persistence.PostgreSql.Tests/PostgreSqlConcurrentInitializationTests.cs`

The Operational Event store now uses the common infrastructure lock `4993446713136202561`. The shared-schema concurrency regression now creates and queries real Operational Event store instances concurrently with the other schema stores.

EliteSCADA CI #1471 passed, including backend build/test/smoke. Preserve this generic fix.

## Current exact-SHA gate matrix

At `e7c8a8bf3954890a1ca841498222bf6094952baf`:

- EliteSCADA CI #1471 / run `34347157464` — **SUCCESS**
- Preview Licensing CI #419 / run `34347157585` — **SUCCESS**
- Interop Lab Smoke #302 / run `34347159685` — **SUCCESS**
- L3 Seven-Driver Lab #375 / run `34347157398` — **SUCCESS**
- Wave 11 Active HMI Runtime #397 / run `34347157735` — **FAILURE**
- additional natural Interop #301 / run `34347157333` — **SUCCESS**

No rerun was requested.

## Current C26.10 blocker — complete Wave11 evidence

Run:

`34347157735` — Wave 11 Active HMI Runtime #397

Job:

`102451385709` — Active revision browser lifecycle

Failed step:

`Run Wave 11 Active Runtime browser lifecycle`

Playwright summary:

- 17 passed;
- 2 failed;
- 4 did not run.

Failed tests:

1. `tests-wave11/c11-eee-demo-hmi.spec.ts:8`
2. `tests-wave11/c26-popup-composition.spec.ts:8`

Both tests failed before their Runtime assertions in package Preview:

- HTTP response itself was successful;
- `preview.canApply` was false;
- `preview.errorCount` could not be asserted because the preceding strict assertion failed;
- the same single entity carried errors: Dynamo `eee.dynamo.pump`, operation 3.

The server reported three `VISUAL_PROPERTY_INVALID` errors:

- `pump-stopped-label`: `core.text` does not declare `backgroundColor`;
- `pump-running-label`: `core.text` does not declare `backgroundColor`;
- `pump-fault-label`: `core.text` does not declare `backgroundColor`.

Exact schema comparison on `e7c8a8...`:

- backend `BuiltinVisualObjectSchemas.Text` = `Base.Concat(TextProperties)`;
- browser `BUILTIN_VISUAL_OBJECT_TYPES.text` = `[...BASE, ...TEXT]`;
- neither declares `backgroundColor` or `cornerRadius`;
- C26.10 currently authors both on all three state labels.

The renderer can consume fill/background values generically, but renderer tolerance is not package-contract authority. Preview correctly rejected properties absent from the declared object schema.

Artifact:

- name: `playwright-report-wave11`;
- artifact ID: `10102316731`;
- uploaded by run `34347157735`.

### Classification

This is a deterministic C26.10 authoring/schema mismatch, not a rerun candidate. The visual requirement remains legitimate: dynamic state labels must fully obscure `PARADA`. The chosen `core.text` property representation is invalid.

## Mandatory next action

Before any edit:

1. Revalidate GitHub live and distinguish the docs-only branch HEAD from product/test SHA `e7c8a8...`.
2. Re-fetch #286, #287/#288/#212/#266/#263, #289, canonical C11, #285 and exact product-SHA workflows.
3. Re-read job `102451385709`, artifact if needed, and exact files:
   - `web/scada-web/tests-wave11/c11-eee-demo-hmi.ts`
   - `web/scada-web/tests-wave11/c11-eee-demo-hmi.spec.ts`
   - `src/Scada.Engineering/VisualScripting/BuiltinVisualObjectSchemas.cs`
   - `web/scada-web/src/visual-runtime/builtinVisualObjectSchemas.ts`
   - `web/scada-web/src/engineering/visual-editor/CanonicalVisualRenderer.tsx`

Then:

1. Replace the invalid text-background representation with the smallest schema-valid composition using public visual objects/properties.
2. Keep the state plates opaque, bounds-covering and ordered Falha > Operando > Parada.
3. Keep package Preview validation strict.
4. Keep deterministic authored-property/bounds and Runtime computed-style coverage.
5. Do not add an EEE exception to validation.
6. Do not widen `core.text` solely for this EEE fixture. A true generic schema enhancement would require an independently justified product requirement, synchronized backend/browser schemas, authoring support and regressions.
7. Do not alter security, authentication, authorization, Identity, Engineering Lock, Licensing, lifecycle, package authority, Drivers or Runtime Active Revision authority.
8. Run available local checks and publish a new exact product/test commit only after the correction is coherent.
9. Let normal workflows fire naturally; diagnose every red before considering rerun.
10. Require all five normal gates green on the same exact SHA.

Only then may C26.10 be marked **VALIDATED**.

C26.11 must not start before that.

## C26.11 and authorized route

After C26.10 is exact-SHA 5/5 green:

1. complete and record C26 validation;
2. obtain Product Owner acceptance and explicit authorization for C26 -> C11;
3. merge only #287 into canonical C11;
4. validate corrected canonical C11;
5. regenerate canonical `.escadapkg`;
6. regenerate SHA-256 checksum and provenance;
7. prove portability/self-containment and Runtime independence from `.escadalib`;
8. create a **new** post-C26 Preview; do not reuse #285;
9. bring the real environment to technical readiness;
10. only then declare `READY FOR WORK AUDIT`.

Neither `siga` nor CI green alone authorizes an integration or protected merge.

## Mandatory post-C26 ChatGPT Work gate

Authority:

- issue #289;
- `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`.

Do not execute now.

Before spending the approximately 40-minute Work window, prepare:

- live SCADA URLs;
- canonical EEE Active Revision;
- running simulation and dynamic TAGs;
- Alarm, Operational Event and Historian/HistoricalQuery evidence;
- Runtime and Engineering;
- Screen Editor and Popup Editor;
- users/authentication instructions without versioned secrets;
- new package SHA-256 and provenance.

Only then create `docs/WORK-UI-AUDIT-HANDOFF.md` and declare `READY FOR WORK AUDIT`.

The first Work pass is audit-only. Work must not develop, patch, commit or merge. Coordinator triage, deterministic reproduction and generic corrections follow the audit; a targeted Work recheck is used only if justified.

## Permanent guardrails

- GitHub live always wins;
- never alter `main` directly;
- #288 and #266 are validation-only and **MUST NEVER MERGE**;
- #212 remains OPEN/DRAFT and has no merge authorization;
- `siga`, CI green, C26 completion, Preview, Work audit or homologation never authorizes #212;
- #287 is only C26 -> canonical C11 and is not authorized to merge now;
- #263 is only canonical C11 -> integration;
- preserve #285 untouched;
- issue #289 does not authorize early execution or any protected merge;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- no blind rerun;
- never weaken tests/validation, security, authentication, authorization, Identity, Engineering Lock, Licensing, lifecycle, package, Drivers or Runtime authority;
- Runtime/Active must be self-contained and cannot depend on `.escadalib`;
- never mask a generic EliteSCADA defect with an EEE-specific workaround;
- Alarm, Operational Event and Audit remain separate concepts and authorities;
- Wave13 #205/#207 remains paused.

## Coordination closeout

At the end of every interaction, record the latest actions in the repository, normally as a precise checkpoint comment on issue #286. Documentation-only commits do not validate product behavior.
