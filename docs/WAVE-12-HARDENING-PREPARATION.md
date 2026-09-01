# Wave 12 — Hardening Preparation

**Status:** PREPARED / NOT STARTED  
**Prepared:** 2026-09-01 BRT  
**Entry baseline:** accepted Wave 11 product-code `main` `4ccc29cb4bb334dc473d8265f48a9c8601993413`

This document prepares Wave 12 for the next Coordinator. It is not implementation authority and its creation does not start Wave 12.

## Objective

Harden the already-implemented EliteSCADA platform before Windows release packaging/signing and owner validation. Wave 12 is a reliability, security, failure-behavior and regression pass over accepted product contracts, not a feature-expansion wave.

## Entry conditions

- Wave 11 issue #194 accepted/closed.
- Live `main` re-read before any implementation branch is created.
- `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, this handoff preparation and live CI must agree.
- No Wave 12 implementation branch exists at preparation time.

## Hardening scope

1. Fail-closed and recovery behavior across Working -> Revision -> Published -> Active -> Runtime.
2. Authentication/authorization boundary review for Runtime, Engineering, writes, commands, alarms, administration and persistence reads/mutations.
3. Audit durability/sanitization and verification that protected actions cannot trust caller-supplied identity.
4. Persistence/restart/recovery consistency, including Active revision recovery and previous-active preservation on failed activation.
5. `.escadapkg` integrity, malformed/corrupt package rejection, asset integrity, preview/apply atomicity and no secret leakage.
6. Runtime resource/fault isolation: scripts, subscriptions, timers, WebSocket/realtime clients, visual instances, driver/runtime diagnostics and cancellation/timeout boundaries.
7. Concurrency/race review around Engineering mutations, TAG writes, Gateway paths, lifecycle transitions and retained state.
8. Diagnostic/error sanitization and actionable failure messages without exposing protected material.
9. Regression and CI hardening: diagnose reproducible flakes/root causes; do not weaken assertions or safety boundaries.
10. Documentation/operational readiness required to hand a stable baseline to Wave 13.

## Explicit non-goals

- no new external protocol/Driver family;
- no new product feature merely because it is desirable;
- no redesign of accepted canonical Engineering contracts without a demonstrated defect;
- no physical Driver L4 claims;
- no production Authenticode implementation or release signing. That remains Wave 13;
- no owner validation/feedback execution. Those remain Waves 14/15;
- no private signing keys in repository, CI or product artifacts.

## Coordinator start protocol

When the next Coordinator actually starts Wave 12:

1. re-read live `main`, current open issues/PRs and exact Actions state;
2. read `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `docs/ROADMAP.md` and this file;
3. audit current tests/failure surfaces before deciding slices;
4. create a dedicated Wave 12 branch from the then-live `main` only at that point;
5. persist material findings and exact next actions in repository coordination docs;
6. use EliteSCADA CI as the universal acceptance gate and run affected specialized workflows based on actual subsystem impact.

## Acceptance direction

Wave 12 is complete only when identified hardening defects are either fixed with regression evidence or explicitly dispositioned with documented residual risk, exact implementation/post-main CI is green, continuity docs are synchronized, and Wave 13 receives a stable known baseline.
