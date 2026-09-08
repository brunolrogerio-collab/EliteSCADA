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
- Pre-C26 Preview #285 — preserve untouched as historical PO homologation evidence.
- Post-C26 real browser audit gate: issue #289 — **SPECIFIED / BLOCKED UNTIL C26 AND NEW POST-C26 PREVIEW ARE READY**.
- Wave13 #205/#207 — paused.

## Exact transfer boundary

Live product HEAD immediately before this docs-only transfer checkpoint:

`a05d349a37a41629ee13b47b4652df74d287273d`

`fix(w14-c26): make Engineering field states readable`

Parent docs checkpoint:

`6adb9256968ff0b9554f41d22c642a2fa7afa693`

`docs(w14-c26): record C26.6 validation`

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
- C26.8 Screen editor functionality audit/fixes — **PENDING / DO NOT START UNTIL C26.7 IS GREEN**
- C26.9 Popup editor functionality audit/fixes — pending
- C26.10 canonical EEE residual cleanup — pending
- C26.11 package/new Preview — pending per live issue #286
- Post-C26 Work audit #289 — **MANDATORY LATER / DO NOT EXECUTE DURING C26**
- Final Product Owner homologation — only after the #289 route and resulting coordinator triage/corrections.

## C26.7 current implementation

Product/test commit:

`a05d349a37a41629ee13b47b4652df74d287273d`

Changed paths:

- `web/scada-web/src/engineering/engineering-control-states.css`
- `web/scada-web/src/main.tsx`
- `web/scada-web/tests-e2e/wave-14-c26-engineering-contrast.spec.ts`

Intent:

- explicit Engineering field-state tokens;
- readable editable/placeholder/readonly/disabled states;
- regression spanning Screens and Script Engineering;
- presentation only, without capability/security/lifecycle authority changes.

## Exact C26.7 CI state and diagnosis

At exact SHA `a05d349...` four of five normal gates are green:

- Preview Licensing CI #394 / run `34241442573` — SUCCESS
- Interop Lab Smoke #271 / run `34241442611` — SUCCESS
- Wave 11 Active HMI Runtime #372 / run `34241442718` — SUCCESS
- L3 Seven-Driver Lab #350 / run `34241442633` — SUCCESS
- EliteSCADA CI #1446 / run `34241442557` — **FAILURE**

Failed job:

- Chromium end-to-end, job `102112833905`
- failed step: `Run browser E2E tests`

Playwright report identifies the C26.7 regression itself:

`wave-14-c26-engineering-contrast.spec.ts >> C26.7 Engineering fields expose readable editable, placeholder, readonly and disabled states`

Failure is at the Screens readonly-state assertion. After adding `readonly` to the route input, its computed background remains identical to the editable state:

`rgb(16, 25, 35)`

The failing assertion is effectively:

`expect(screenReadonly.backgroundColor).not.toBe(screenEditable.backgroundColor)`

This is a deterministic C26.7 product/style failure, not evidence of infrastructure flakiness. **Do not rerun #1446 unchanged.** Diagnose why the readonly selector/state styling does not apply to that Screens field, correct the generic Engineering styling or DOM scope, preserve the regression, then let fresh exact-SHA workflows run naturally.

Do not weaken the assertion merely to obtain green CI. The acceptance explicitly requires readonly and editable states to be visually distinct.

## Immediate next action

1. Revalidate live HEAD, #286, #287, #288, #212 and exact-head workflows.
2. Refetch `engineering-control-states.css`, the Screens route-field DOM/component and the C26.7 regression from the exact live branch.
3. Diagnose selector/specificity/scope cause for Screens readonly styling.
4. Implement the smallest generic correction that makes readonly visually distinct while preserving readable contrast.
5. Do not alter authentication, authorization, capability projection, Engineering Lock, lifecycle, package or backend authority.
6. Keep the current C26.7 regression strict.
7. Wait for natural workflows on the new exact SHA and require all five normal gates green.
8. Only then mark C26.7 VALIDATED and start C26.8.

The new #289 Work audit requirement does not change this immediate C26 sequence.

## C26.6 retained authority

C26.6 final validated SHA is `7d9d97797f8e19a874a6958f2fbaec9cfb3b2b11`.

Its user-controlled Engineering collapse/restore regression found and caused correction of two real CSS Grid auto-placement defects. Do not weaken that constrained-viewport regression or regress Properties reachability/canvas dominance.

## Preserved C26.5 Preview diagnosis

`/api/historical/query` exists and is feature-gated. Preserved Preview #285 did not enable the HistoricalQuery/cursor-key configuration. The PO-observed 404 was a pre-C26 Preview harness configuration gap, not a missing backend route and not a valid no-data result. Keep #285 untouched. A new post-C26 Preview must enable safe Preview/development HistoricalQuery configuration.

Alarm / Operational Event / Audit remain distinct.

## Binding post-C26 ChatGPT Work audit gate

The new mandatory quality gate is tracked by issue #289 and fully defined in:

`docs/WAVE14-POST-C26-WORK-UI-AUDIT-DIRECTIVE.md`

It must **not** be executed during C26.

After accepted C26 is integrated into corrected canonical C11 and a new post-C26 canonical EEE package/Preview exists, the coordinator must prepare the environment before using ChatGPT Work. Work should receive a live product with backend/frontend/PostgreSQL/TimescaleDB/Historian/HistoricalQuery operational, canonical EEE loaded and Active, simulation running with dynamic TAG state, Alarm/Event/Historian useful, Runtime/Engineering/editor routes explicit and audit users prepared securely.

Only then may the coordinator declare `READY FOR WORK AUDIT` and create the intentionally short candidate-specific:

`docs/WORK-UI-AUDIT-HANDOFF.md`

That handoff must contain real exact SHA, real Preview/Runtime/Engineering URLs, package SHA-256 and non-secret authentication instructions. Do not create placeholder readiness evidence early.

The first Work pass is audit-only and should spend the approximately 40-minute budget using the actual browser product, not building/configuring it. It must not modify code, create patches, commits or merges.

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
- preserve #285;
- #289 is an additional post-C26 validation gate and does not authorize protected merges or early execution;
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

Then revalidate branch HEAD, issue #286, issue #289, PRs #287/#288/#212, canonical C11, Preview state and workflows for the exact HEAD.

When the Product Owner says `siga`, continue autonomously through safe subsequent work. `siga` never authorizes protected merges.