# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **PYTHON-WAVE-06 IMPLEMENTED IN PR / FINAL MERGE BLOCKED_BY_CI_BUDGET + E2E HARNESS RELIABILITY**  
**CI budget mode:** **CONSTRAINED until owner explicitly reports reset**

## Mandatory resume reading

Before any action read current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, plus the current wave-specific documents and source/tests.

GitHub branch/PR/head/CI is operational truth. Never call a red exact-head gate green because the remaining failure appears flaky.

## Official main state

Wave 05 is **MERGED**. Wave 06 remains **NOT MERGED TO MAIN**.

Current Wave 06 integration PR: #83 `Establish Wave 06 Client Visual Python foundation`.

- branch: `integration/python-wave-06`
- exact product head: `86b80ff72690d0b14cde9c1a315b908763ad4b49`
- PR remains Open / Draft / mergeable / NOT MERGED

## What changed after the previous handoff

The primary Wave 06 Pyodide compile-diagnostics defect is resolved. The corrected sandbox keeps Pyodide engine internals intact outside user execution and scopes denied import/native escape guards to user execution boundaries.

CI #485 on head `89f892ba29858d8a2def2ecece473609deba52ed` proved:
- Web build SUCCESS;
- backend build/full tests/Runtime smoke SUCCESS;
- Chromium: 122 passed / 1 failed;
- all Wave 06 Python editor/runtime/real-Pyodide sandbox/native-escape tests passed;
- the only failure was the legacy multilingual Wave 03 readiness test with Vite WebSocket `ECONNRESET` and browser/session navigation instability.

A targeted test-composition correction was then promoted as commit `86b80ff72690d0b14cde9c1a315b908763ad4b49` (`Stabilize locale readiness through SPA navigation`). It replaced repeated hard Engineering/Audit route reloads inside the multilingual loop with the existing SPA navigation links, preserving the route and localization assertions. No timeout was increased and no test/security/CAS/lifecycle/persistence guard was weakened.

## Latest exact-head CI — #486 FAILED ONLY ON LEGACY HARNESS RELIABILITY

Run `33191736584` on exact Wave 06 head `86b80ff72690d0b14cde9c1a315b908763ad4b49`:

- Web build: **SUCCESS**
- backend build/full tests/Runtime smoke: **SUCCESS**
- Chromium: **122 passed / 1 failed**
- all Wave 06 Python editor/runtime/sandbox tests: **PASS**

The sole failure remained `interface-wave-03-readiness.spec.ts` multilingual readiness. The first attempt lost the Chromium session while waiting for `.user-session-menu`, accompanied by Vite WS `ECONNRESET`; the retry timed out loading `/`. The remaining failure is therefore outside the Wave 06 Python functionality, but the exact-head integration matrix is still red and must not be ignored.

## Current decision

**Do not merge PR #83 yet.** The required final integrated matrix is not fully green.

**Do not trigger another unchanged or speculative full Actions run while the allowance is constrained.** The product owner reported about 50 included Actions minutes before this final attempt; CI #486 consumed part of that budget.

Correct state: **BLOCKED_BY_CI_BUDGET / FINAL E2E RELIABILITY EVIDENCE PENDING**.

When sufficient Actions allowance is available, first address or isolate the real harness/session reliability cause without weakening acceptance, then obtain one fully green exact-head Wave 06 matrix. Only then mark PR #83 Ready and merge.

## Wave 07 temporary rule

Wave 07 remains queued while Wave 06 is unmerged. After Wave 06 is merged and explicitly promoted, Wave 07 may proceed with implementation while GitHub Actions validation is deferred until the product owner explicitly reports that the Actions allowance has reset.

During that temporary period, worker deliveries must remain `IMPLEMENTED / CI_DEFERRED` or equivalent and cannot be called fully validated, complete or merge-ready.

Queued Wave 07 slices:
- DEV 1: Visual Property Registry / Engineering projection;
- DEV 2: Visual Runtime Instance;
- DEV 3: Python <-> Visual API acceptance.

## Permanent rules

Workers never modify `main`, merge their own PR, self-assign or broaden scope. Canonical Engineering remains authority. Research is not production implementation. CI economy changes frequency only, never final quality. A red exact-head matrix remains red even when 122 of 123 tests pass.
