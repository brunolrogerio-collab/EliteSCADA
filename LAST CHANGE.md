# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **PYTHON-WAVE-06 MERGED / WAVE-07 DEVELOPMENT ACTIVE + CI_DEFERRED**  
**CI budget mode:** **CONSTRAINED until owner explicitly reports reset**

## Mandatory resume reading

Before any action read current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, plus current wave-specific documents and source/tests.

GitHub branch/PR/head/CI is operational truth.

## Wave 06 — MERGED

Wave 06 is **MERGED** through PR #83 `Establish Wave 06 Client Visual Python foundation`.

- final integration head: `d665dc13b0922938a15252d9775ef6604e41bff4`
- merge commit: `cc79713434c1d7b5988158b843b137eaf488d923`
- final validation: CI #487 / run `33194041390`
- Web build: **SUCCESS**
- backend Release/full tests including PostgreSQL + Runtime smoke: **SUCCESS**
- Chromium E2E: **SUCCESS**

The final reliability correction preserved every Runtime, Engineering, Audit, session and locale assertion while splitting pt-BR, en and es into independent Playwright journeys. No timeout or acceptance requirement was weakened.

Official `main` now includes Monaco Python Engineering editing, compile-before-Preview/Apply/CAS, pinned/self-hosted Pyodide, isolated module Web Worker execution behind bridge v1, authorized TAG reads, owning-client Client Memory read/write, bounded execution/cancellation/queue/disposal/failure throttling, real-Pyodide adversarial acceptance and native escape denial.

### Post-merge CI note

Automatic `main` push CI #488 / run `33194817294` did not execute product steps: Backend and Web ended with empty step lists and `runner_id: 0`; Chromium was skipped. This is unavailable runner evidence, not a product test failure.

The merge commit `cc79713...` differs from the exact green product head `d665dc1...` only in Markdown coordination/architecture documentation, so no production source changed after CI #487.

## Wave 07 — DEVELOPMENT ACTIVE / CI DEFERRED

Wave 07 is explicitly promoted for implementation only.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- Integration branch: `integration/visual-runtime-wave-07`
- DEV 1 branch: `feature/visual-wave-07-property-registry`
- DEV 2 branch: `feature/visual-wave-07-runtime-instance`
- DEV 3 branch: `test/visual-wave-07-python-api-acceptance`

All four branches were created from the exact WaveBaseSHA.

### Temporary Actions rule

The product owner most recently reported about **19 included Actions minutes remaining**.

Until the owner explicitly reports the allowance reset:
- **do not run GitHub Actions for Wave 07**;
- because opening a PR triggers the full `pull_request` workflow, **do not open Wave 07 PRs yet**;
- workers may implement, commit, write tests and provide static/source-level evidence on their assigned branches;
- delivery status must be `IMPLEMENTED / CI_DEFERRED` or equivalent, never fully validated or merge-ready;
- do not weaken or delete tests to accommodate the deferral.

When the owner reports reset, open/reconcile the required PRs, execute worker/integration validation, and obtain the full final Wave 07 matrix before merge.

## Wave 07 objective

Stabilize the Visual Runtime Object Model and visual Asset contract before graphical Engineering:
- stable canonical visual identity;
- typed public Visual Property Registry;
- Runtime Visual Instance identity/state/lifecycle;
- deterministic precedence `Animation > Script > Binding/Expression > Engineering Base`;
- runtime overrides never silently mutate Engineering;
- stable project-owned Asset/Resource references, never arbitrary filesystem paths;
- explicit Python-to-Visual property API without direct DOM authority.

## Permanent rules

Workers never modify `main`, merge their own work, self-assign or broaden scope. Canonical Engineering remains authority. Research is not production implementation. CI economy changes timing only, never final quality.
