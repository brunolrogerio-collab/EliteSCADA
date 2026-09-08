# Wave 14 C26 Coordinator Handoff — 2026-09-08

## Authority

**GitHub live is the official memory and sole authority for EliteSCADA.**

Revalidate the repository before every decision, diagnosis, code/documentation write, PR action, workflow rerun or merge. If this snapshot diverges from GitHub live, GitHub wins.

Repository: `brunolrogerio-collab/EliteSCADA`

## Protected topology

- Integration: `wave14/corrections-integration`, PR #212 -> `main` — **OPEN/DRAFT; MUST NOT MERGE without later separate explicit Product Owner authorization**.
- Active C26 branch: `wave14/c26-po-homologation-corrections`.
- Coordinator issue: #286.
- Implementation PR #287: C26 -> canonical C11, DRAFT.
- Validation PR #288: C26 -> `main` — **VALIDATION ONLY / MUST NEVER MERGE**.
- C11 validation PR #266 — **MUST NEVER MERGE**.
- C11 -> integration PR #263 — integration route only.
- Canonical C11 branch: `wave14/c11-canonical-eee-demo`; last revalidated pre-handoff at `a724ece64a292aa1d1dedd886a72fb28ff8d90fe`, but always revalidate live before use.
- Pre-C26 Preview #285 — preserve untouched as historical PO homologation evidence.
- Post-C26 real browser audit gate: issue #289 — **SPECIFIED / BLOCKED UNTIL C26 AND NEW POST-C26 PREVIEW ARE READY**.
- Wave13 #205/#207 — paused.

## Exact product boundary at transfer

Current C26 **product/test candidate**:

`642e33c83d17e3c53beb588841626551e6ea305f`

`fix(w14-c26): let readonly Engineering styles win cascade`

The active branch contains documentation/coordination commits above this product SHA. Revalidate live branch HEAD before every action and keep the distinction between branch HEAD and exact product/test candidate.

Last exact C26 product/test SHA fully green across all five normal gates:

`7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`

At `7d9d977...`:

- EliteSCADA CI #1444 / run `34237046911` — SUCCESS
- Preview Licensing CI #392 / run `34237046935` — SUCCESS
- Interop Lab Smoke #269 / run `34237046897` — SUCCESS
- Wave 11 Active HMI Runtime #370 / run `34237046873` — SUCCESS
- L3 Seven-Driver Lab #348 / run `34237046980` — SUCCESS

## C26 progress at transfer

- C26.1 Runtime transient-failure resilience — **IMPLEMENTED / VALIDATED**
- C26.2 Runtime logical viewport/layout — **IMPLEMENTED / VALIDATED**
- C26.3 Runtime renderer technical-id leakage — **IMPLEMENTED / VALIDATED**
- C26.4 Runtime Popup composition — **IMPLEMENTED / VALIDATED**
- C26.5 Alarm/Event/Historian Runtime surface — **DIAGNOSED / REGRESSION LOCKED / VALIDATED**
- C26.6 Engineering shell/workspace usability — **IMPLEMENTED / VALIDATED**
- C26.7 Engineering theme/contrast — **IMPLEMENTED BUT NOT VALIDATED / CURRENT BLOCKER**
- C26.8 Screen editor functionality audit/fixes — **NOT STARTED / DO NOT START UNTIL C26.7 IS GREEN**
- C26.9 Popup editor functionality audit/fixes — pending
- C26.10 canonical EEE residual cleanup — pending
- C26.11 package/new Preview — pending per live issue #286
- Post-C26 Work audit #289 — **MANDATORY LATER / DO NOT EXECUTE DURING C26**
- Final Product Owner homologation — only after the #289 route and resulting coordinator triage/corrections.

## C26.7 implementation history

Initial C26.7 product/test SHA:

`a05d349a37a41629ee13b47b4652df74d287273d`

Primary paths:

- `web/scada-web/src/engineering/engineering-control-states.css`
- `web/scada-web/src/main.tsx`
- `web/scada-web/tests-e2e/wave-14-c26-engineering-contrast.spec.ts`

Intent remains:

- explicit Engineering field-state tokens;
- readable editable/placeholder/readonly/disabled states;
- regression spanning Screens and Script Engineering;
- presentation only, without capability/security/lifecycle authority changes.

At `a05d349...`, four of five gates were green and EliteSCADA CI #1446 failed in Chromium. The strict regression proved the Screens route field remained visually identical after `readonly` was applied.

The cause was diagnosed as CSS cascade specificity: the editable selector accumulated specificity through repeated `:not([type=...])` clauses and overrode `[readonly]`.

A minimal generic correction was then committed:

`642e33c83d17e3c53beb588841626551e6ea305f`

`fix(w14-c26): let readonly Engineering styles win cascade`

The correction uses `:where(...)` around the type-exclusion filters so they do not increase selector specificity. It does **not** add `!important` and does **not** weaken the existing Playwright test.

## Exact current C26.7 CI state

At exact product/test SHA `642e33c...`:

- Preview Licensing CI #402 / run `34255187708` — SUCCESS
- Interop Lab Smoke #279 / run `34255187740` — SUCCESS
- Wave 11 Active HMI Runtime #380 / run `34255187706` — SUCCESS
- L3 Seven-Driver Lab #358 / run `34255187748` — SUCCESS
- EliteSCADA CI #1454 / run `34255187823` — **FAILURE**

EliteSCADA CI #1454 jobs:

- Backend build, test and smoke — SUCCESS
- Web build — SUCCESS
- Chromium end-to-end, job `102159657922` — **FAILURE**
- failed step: `Run browser E2E tests`

No blind rerun was performed.

### Critical transfer rule

The outgoing coordinator did **not** establish a trustworthy current assertion-level diagnosis for the `642e33c...` Chromium failure before handoff. Multiple attempts to fetch the decoded job log through the connector did not surface the full text in the chat context.

Therefore the next coordinator must **not** assume the current failure is identical to the earlier `a05d349...` readonly-background assertion.

Before any C26.7 edit:

1. revalidate live branch HEAD and exact product SHA;
2. fetch the complete job `102159657922` failure evidence, Playwright report/artifact if needed;
3. read exact `642e33c...` CSS, affected DOM/component and strict C26.7 test;
4. diagnose the actual current failing assertion;
5. make only the smallest generic product correction justified by evidence;
6. keep the regression strict;
7. let fresh workflows run naturally;
8. require all five normal gates green on one exact product/test SHA;
9. only then mark C26.7 VALIDATED and start C26.8.

Do not rerun the unchanged failing SHA merely to seek green.

## Coordination-only cleanup note

During GitHub write-tool discovery in the outgoing coordinator chat, a placeholder file named `dummy` was accidentally created on the C26 branch and immediately removed through a normal follow-up commit. No force push, destructive rebase or product mutation was used. There should be no net product file from this incident.

Treat those commits and subsequent handoff/documentation commits as **coordination-only**, not product/test validation evidence.

## C26.6 retained authority

C26.6 final validated SHA remains `7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`.

Its constrained-viewport regression found and corrected two real CSS Grid auto-placement defects. Preserve user-controlled navigation/Screens/Palette/Properties collapse/restore, canvas dominance and Properties recoverability. Do not weaken that regression.

## Preserved C26.5 Preview diagnosis

`/api/historical/query` exists and is feature-gated. Preserved Preview #285 did not enable HistoricalQuery/cursor-key configuration correctly. The PO-observed 404 was a pre-C26 Preview harness configuration gap, not a missing backend route and not valid no-data.

Keep #285 untouched. A new post-C26 Preview must safely enable HistoricalQuery for Preview/development.

Alarm / Operational Event / Audit remain distinct.

## Binding post-C26 ChatGPT Work audit gate

Issue #289 and `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md` define a mandatory real browser audit after accepted C26 is integrated into corrected canonical C11 and a **new** post-C26 Preview is technically ready.

Do **not** execute this during C26.

Only after the coordinator has prepared a live SCADA with backend/frontend/PostgreSQL/TimescaleDB/Historian/HistoricalQuery operational, canonical EEE loaded and Active, simulation running, dynamic TAGs, Alarm/Event/Historian useful, explicit Runtime/Engineering URLs and secure audit users may the state be declared:

`READY FOR WORK AUDIT`

Only then create candidate-specific `docs/WORK-UI-AUDIT-HANDOFF.md` with real exact SHA, URLs, package SHA-256 and non-secret authentication instructions.

The first Work pass is audit-only and should spend the approximately 40-minute budget using the real browser product, not building/configuring it.

Required route before final Product Owner homologation:

`technical Preview green -> READY FOR WORK AUDIT -> Work exploratory real-use audit -> coordinator preserves/triages/reproduces findings -> generic corrections + deterministic regressions -> new exact candidate -> optional targeted Work recheck -> Product Owner final homologation`.

## Binding Wave14 follow-on obligations

Still mandatory after the immediate C26 sequence:

- protected whole-system backup/restore distinct from `.escadapkg`;
- Historian Administration backup/export/import/restore;
- generic TAG raw -> engineering scaling with explicit inverse-write semantics;
- human decimal-place authoring persisted through Save -> Publish -> Activate -> Runtime -> package export/import;
- real EEE Modbus/PLC variant only after generic gates, corrected package and fresh homologation.

## Hard guardrails

- never modify `main` directly;
- never merge #288 or #266;
- never merge #212 without later separate explicit Product Owner authorization;
- #287 targets canonical C11 only;
- #263 is C11 -> integration only;
- preserve #285;
- #289 does not authorize early execution or protected merges;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose every CI red before rerun; no blind rerun;
- never weaken tests, validation, security, authentication, authorization, Identity, Engineering Lock, licensing, lifecycle, package, drivers or Runtime Active Revision authority;
- Runtime/Active remains self-contained and cannot depend on `.escadalib`;
- no EEE-specific workaround for a generic platform defect;
- Wave13 remains paused.

## Mandatory reading order for next coordinator

Read live from `wave14/c26-po-homologation-corrections`:

1. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-08.md`
2. `docs/CURRENT-COORDINATOR-HANDOFF.md`
3. `LAST CHANGE.md`
4. `docs/WAVE14-C26-PO-HOMOLOGATION-EXECUTION-LOG.md`
5. `docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`
6. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
7. `docs/WAVE14-C26-COORDINATOR-HANDOFF-2026-09-07.md` for historical context
8. `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`
9. `docs/WAVE14-C25-FINAL-CANDIDATE-MATRIX-2026-09-06.md`

Then revalidate branch HEAD, issue #286, issue #289, PRs #287/#288/#212/#266/#263, canonical C11, Preview #285 and workflows for the exact product/test SHA.

When the Product Owner says `siga`, continue autonomously through safe subsequent work. `siga` never authorizes protected merges.
