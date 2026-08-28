# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **PYTHON-WAVE-06 MERGED / WAVE-07 READY FOR COORDINATOR PROMOTION**  
**CI budget mode:** **CONSTRAINED until owner explicitly reports reset**

## Mandatory resume reading

Before any action read current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, plus current wave-specific documents and source/tests.

GitHub branch/PR/head/CI is operational truth.

## Official main state

Wave 05 is **MERGED**.

Wave 06 is **MERGED** through PR #83 `Establish Wave 06 Client Visual Python foundation`.

- final integration head: `d665dc13b0922938a15252d9775ef6604e41bff4`
- merge commit: `cc79713434c1d7b5988158b843b137eaf488d923`
- final validation: CI #487 / run `33194041390`
- Web build: **SUCCESS**
- backend Release/full tests including PostgreSQL + Runtime smoke: **SUCCESS**
- Chromium E2E: **SUCCESS**

## Final Wave 06 reliability correction

The remaining legacy multilingual Wave 03 readiness journey was not a Python product defect. Repeated CI showed browser/session closure while one test serialized pt-BR, en and es journeys under one 30-second Playwright test budget.

The final correction preserved every Runtime, Engineering, Audit, session and locale assertion but generated one isolated Playwright test per locale. This removed cross-locale lifetime accumulation and gave each independent user journey its normal test budget without increasing timeouts or weakening acceptance.

CI #487 passed the exact final integration head. Wave 06 therefore satisfied its final gate and PR #83 was merged.

## Wave 06 delivered baseline

Official `main` now includes:

- canonical Client Visual Script Engineering from Wave 05 consumed by the runtime/editor;
- Monaco Python editing and diagnostics;
- pinned/self-hosted Pyodide runtime;
- isolated module Web Worker execution behind bridge v1;
- compile-before-Preview/Apply/CAS behavior;
- authorized TAG reads and owning-client Client Memory read/write;
- bounded execution, cancellation, queue/coalescing, disposal and failure throttling;
- real-Pyodide adversarial acceptance and native escape denial;
- controlled Engineering handler preview without direct process authority.

Server Python, Visual Runtime object/property implementation and graphical visual Engineering remain later work.

## Next gate

Wave 07 is next and may be promoted by the coordinator only after rereading its architecture/Definition of Ready and synchronizing the assignment board.

Queued Wave 07 slices remain:
- DEV 1: Visual Property Registry / Engineering projection;
- DEV 2: Visual Runtime Instance;
- DEV 3: Python <-> Visual API acceptance.

The owner's temporary Actions rule remains in force: Wave 07 implementation may proceed after coordinator promotion, but GitHub Actions validation is deferred until the owner explicitly reports the allowance reset. Worker deliveries during that interval must be labeled `IMPLEMENTED / CI_DEFERRED` or equivalent, never fully validated/merge-ready.

## Permanent rules

Workers never modify `main`, merge their own PR, self-assign or broaden scope. Canonical Engineering remains authority. Research is not production implementation. CI economy changes frequency only, never final quality.
