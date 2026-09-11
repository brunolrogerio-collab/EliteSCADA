# LAST CHANGE — EliteSCADA

**Date:** 2026-09-11 BRT

**Operational state:** **WAVE 14 DIAGNOSTIC CLOSURE ACTIVE / A1+A2 ACCEPTED / A4+A5 NEXT CODEX LIVE DIAGNOSTICS / NO NEW BROAD PRODUCT CORRECTIONS IN W14 / W15 OWNS CORRECTIONS / W13 PRESERVED AND PAUSED / #212 CONDITIONAL ONLY AFTER CLOSURE + EXACT-SHA GREEN**

> GitHub live is the official and sole project memory. Revalidate refs, PR/issue state, exact files and workflows before every decision, diagnosis, code/documentation write, PR action, rerun, integration or merge. If this file differs from GitHub live, GitHub wins.

## Latest Main Coordinator acceptance — Codex A1/A2

Accepted handoff:

- issue #286 comment `5629630825` (`CODEX -> MAIN COORDINATOR`);
- Codex evidence branch `docs/w14-codex-a1-a2-20260911`;
- evidence commit `f05589f8afc4a0f658868416ee5b38d1c3681d60`;
- `docs/WAVE14-CODEX-A1-A2-DIAGNOSTIC-2026-09-11.md` plus raw evidence transcript.

### A1 — route latency / `Failed to fetch`

Closed for Wave 14 as:

- transport root: **`UNCERTAIN — BOUNDED`**;
- product transport/error UX: **`CONFIRMED / GENERIC PRODUCT`**.

In the correlated live window, forwarded navigation was repeatedly slow and one `/engineering` load remained blank for more than two minutes while local Vite and API remained healthy and fast. This excludes local API/Vite outage for the captured interval. Exact Codespaces forwarding edge versus browser/static-module delivery remains unproven because the required browser network waterfall + forwarding trace + request-level Vite telemetry were unavailable.

Independent product-owned defects are confirmed for Wave 15:

- indefinite blank SPA bootstrap lacks bounded actionable recovery;
- rejected transport fetch can be displayed as fictitious `(500) Failed to fetch` without a matching server 500.

### A2 — Working `demo` vs Active `eee-demo`

Closed as **`CONFIRMED / GENERIC PRODUCT`**.

Authenticated APIs proved Working is genuinely `demo` / `Demo Project`, with no base revision, while Active Runtime is `eee-demo` revision 2 and persistence is configured for `eee-demo`.

Exact mechanism:

- `src/Scada.Api/Runtime/EngineeringWorkspace.cs:63-75,448-456` unconditionally calls `SeedDemo()` and assigns Working `demo`;
- `src/Scada.Api/Persistence/EngineeringPersistenceApi.cs:47-78` separately restores configured `eee-demo` into Active Runtime;
- `src/Scada.Api/Persistence/EngineeringWorkspaceCheckoutService.cs:43-108` is an explicit checkout path, not automatic startup synchronization;
- `scripts/preview/launch-post-c26-preview.sh:8,145,224-230` establishes the persisted/runtime `eee-demo` context.

Active authority and cross-project activation protection remain correct. Wave 15 must implement explicit Working project bootstrap/selection/checkout semantics without auto-activating or rewriting Active.

## Canonical documentation synchronized

On `preview/wave14-post-c26-work-audit`:

- `docs/WAVE15-INITIAL-CORRECTION-BACKLOG.md` updated at `ea94d953a3feaa978616407b818c01153e82cc42`;
- `docs/WAVE14-CLOSURE-CHECKLIST.md` updated at `5a3a8d1cee824a25669542b525dd75d8a4167d5d`;
- `docs/CURRENT-COORDINATOR-HANDOFF.md` updated at `3a03fa9b91bcaf5cf4e7fbda2c091fca4b8f8fa3`.

These are documentation/coordination commits. They do not supersede the technically validated product candidate `59e815eae524b9ff043ea6bf3f797f4c01ba9143`.

## Remaining diagnostic closure priorities

The next direct-Codespace Codex mission is:

1. **A4 Runtime Trends silent return** — correlate Historian/no-data, realtime, projection and navigation/subview state;
2. **A5 Runtime Popup `—` / disappearance / auto-return** — correlate authoritative TAG value/quality, binding, realtime, Runtime projection polling and `RuntimeVisualNavigator` state;
3. if the live window remains, collect real-UI evidence for **A7 Screen/Popup Editor** and **A8 Script Engineering** using the already prepared static maps; do not spend primary time re-deriving closed A1/A2/A3 findings.

Each remaining item must close as `CONFIRMED` or `UNCERTAIN — BOUNDED` with an implementable Wave 15 correction/regression contract.

## Wave 14 strategy unchanged

Wave 14 does **not** implement newly diagnosed broad corrections. It finishes diagnosis/documentation, then integrates the accepted Wave 14 baseline into `main` through the authorized route and closes.

Canonical exit gate:

`docs/WAVE14-CLOSURE-CHECKLIST.md`

Wave 15 transfer backlog:

`docs/WAVE15-INITIAL-CORRECTION-BACKLOG.md`

The Product Owner explicitly considers Screen/Popup Editor and Script Engineering insufficiently functional for a normal developer. Wave 15 treats this as product correctness/maturity, not cosmetic polish.

## Integration boundary

The intended route remains:

`canonical C11 -> PR #263 -> wave14/corrections-integration -> PR #212 -> main`

PR #290 is Preview/audit evidence only. PR #296 and validation-only / MUST-NEVER-MERGE PRs must never be used as product integration routes.

PR #212 has conditional Product Owner authorization only after:

- Wave 14 diagnostic transfer is complete enough to avoid rediscovery;
- #263/C11 is correctly integrated;
- selected final closure docs are propagated onto the integration route;
- exact integration SHA passes universal and impact-required gates;
- any red gate is diagnosed before rerun;
- #212 head/base/expected head are revalidated immediately before merge.

## Wave 13

Wave 13 #205/#207 remains preserved and paused. Wave 15 completion alone does not resume release/signing work; a later explicit Product Owner maturity decision is required.

## Permanent governance

- GitHub live wins over chat or stale docs;
- no direct `main` mutation;
- no force push, destructive rebase or branch deletion;
- no blind workflow rerun;
- never weaken tests, validation, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics or Runtime Active authority;
- Runtime/Active remains independent of `.escadalib`;
- no EEE-specific workaround for a generic platform defect;
- Alarm / Operational Event / Audit remain distinct;
- uncertain diagnostics remain explicit;
- Wave 15 correction completion does not automatically resume Wave 13.
