# Current Coordinator Handoff

> **GitHub live is the official memory and sole authority for EliteSCADA.** Revalidate live before every decision, diagnosis, documentation/code write, PR action, workflow rerun, integration or merge. Any divergence here is resolved in favor of GitHub.

## Coordination model

Wave 14 uses the **Main Coordinator + diagnostic co-coordinator** model.

- **Main Coordinator:** ChatGPT coordination session designated by the Product Owner; owns classification, sequencing, closure criteria, integration and guardrails.
- **GPT Codex:** diagnostic co-coordinator with direct Codespace/application/browser/local-port access; owns correlated live diagnosis/evidence capture where that access is necessary.
- **Product Owner:** final product direction and maturity authority.

Primary coordination ledger: issue #286.

Canonical collaboration protocol:

`docs/WAVE14-MAIN-COORDINATOR-CODEX-COLLABORATION-PROTOCOL.md`

Canonical strategic closure plan:

`docs/WAVE14-DIAGNOSTIC-CLOSURE-AND-WAVE15-TRANSFER.md`

Canonical exit gate:

`docs/WAVE14-CLOSURE-CHECKLIST.md`

Wave 15 transfer backlog:

`docs/WAVE15-INITIAL-CORRECTION-BACKLOG.md`

## Current phase — Wave 14 diagnostic closure

Product Owner direction remains:

**finish diagnosis -> identify what/where/why/how to correct -> preserve evidence, mechanism, correction contract and deterministic regression -> integrate accepted Wave 14 baseline to `main` -> close Wave 14 -> open Wave 15 for corrections.**

Wave 13 remains preserved and paused until a later explicit Product Owner maturity decision.

## Live product/topology checkpoint

Always revalidate these values before acting:

- `main`: `edbdf446ea657713bdc487be91bf10bfcd03c684` at the last closure checkpoint;
- PR #212: Wave 14 integration -> `main`, conditional Product Owner authorization only after closure package + exact-SHA green;
- PR #263: canonical C11 -> `wave14/corrections-integration`;
- canonical C11: `19d5257d970f53ae798c5fa53946fce07c586452` at the last revalidation;
- accepted C26 product: `08e2530671de10d48933c4b712a1a1abc9e41dce`;
- PR #290: OPEN/DRAFT / Preview-only; current coordination head moves through docs/evidence commits and is not automatically a product candidate;
- technically validated Preview candidate: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`;
- frozen package SHA-256: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`;
- PR #296: DIAGNOSTIC ONLY / MUST NEVER MERGE / no blind rerun;
- #285: preserved historical pre-C26 Preview evidence;
- Wave13 #205/#207: paused.

## Latest accepted Codex handoff — A1 + A2

Accepted authority:

- issue #286 comment `5629630825`;
- branch `docs/w14-codex-a1-a2-20260911`;
- commit `f05589f8afc4a0f658868416ee5b38d1c3681d60`;
- `docs/WAVE14-CODEX-A1-A2-DIAGNOSTIC-2026-09-11.md`;
- `evidencias/W14-CODEX-A1-A2-2026-09-11.txt`.

### A1 — forwarding / route latency / `Failed to fetch`

**Closure:** `UNCERTAIN — BOUNDED` at transport root; independent product error UX is `CONFIRMED / GENERIC PRODUCT`.

Correlated live evidence showed forwarded `/engineering` and `/` taking roughly 25–28 seconds and one forwarded `/engineering` remaining blank for more than two minutes, while local Vite `/engineering` returned 200 in about 151 ms, API `/health` in about 2.7 ms, listeners/processes remained healthy, and Runtime projection API traffic remained 200 in roughly 63–115 ms.

This excludes local API/Vite outage in the captured window. Exact Codespaces edge versus browser/static-module delivery cause remains bounded because browser network waterfall + forwarding trace + request-level Vite telemetry were unavailable.

Separately confirmed product defects:

- indefinite/very long blank SPA bootstrap has no bounded actionable recovery state;
- rejected fetch/transport failure can be presented as `(500) Failed to fetch` even without a server HTTP 500.

Wave 15 owns the product correction. Transport root must not be patched speculatively.

### A2 — Working `demo` vs Active `eee-demo`

**Closure:** `CONFIRMED / GENERIC PRODUCT`.

Authenticated APIs proved Working is genuinely `demo` / `Demo Project`, not merely a frontend fallback, while Active Runtime is `eee-demo` revision 2 and persistence is configured for `eee-demo`.

Exact mechanism:

- `src/Scada.Api/Runtime/EngineeringWorkspace.cs:63-75,448-456` unconditionally calls `SeedDemo()` and assigns the in-memory Working identity `demo`;
- `src/Scada.Api/Persistence/EngineeringPersistenceApi.cs:47-78` separately recovers the configured persisted project into Runtime Active;
- `src/Scada.Api/Persistence/EngineeringWorkspaceCheckoutService.cs:43-108` is the explicit checkout path and is not called automatically at startup;
- `scripts/preview/launch-post-c26-preview.sh:8,145,224-230` configures/creates `eee-demo` persistence/Runtime context.

Lifecycle remains correct: Active authority is not corrupted and cross-project activation is rejected.

Wave 15 correction contract: explicit Working bootstrap/selection/checkout semantics after persistence initialization, while keeping Working separate from Published/Active and never auto-activating or rewriting Active.

## Other closed diagnostic work

### A3 — persisted legacy visual schema crash

`CONFIRMED_GENERIC_PRODUCT`.

Canonical diagnosis:

`docs/WAVE14-DIAGNOSTIC-LEGACY-VISUAL-TYPE-COMPATIBILITY.md`

Known persisted legacy identifiers can reach strict current `core.*` schema lookup without a compatibility boundary. Wave 15 must normalize/migrate known legacy types before strict consumers while retaining fail-closed behavior for truly unknown types.

### A6 — recovery/fallback UX

`CONFIRMED_GENERIC_PRODUCT` at the product UX/state boundary. Missing/unloaded Engineering public model must never masquerade as authoritative Working identity; loading/unavailable/retry state must be explicit.

### Script Engineering confirmed subfinding

Canonical diagnosis:

`docs/WAVE14-DIAGNOSTIC-SCRIPT-EVENT-AUTHORING-GAP.md`

The UI surfaces event kinds including `timer` and `tagChanged` but does not expose required event-specific fields (`timerIntervalMs`, `tagReference`). Changing event kind can also leave hidden stale event-specific values that later fail validation. This is a confirmed generic Wave 15 defect; the broader A8 real developer flow remains open.

## Remaining high-value Wave 14 diagnostics

### A4 — Runtime Trends silent return

Next direct-Codespace priority. Reproduce and correlate Historian/no-data, realtime, projection, route/subview state and error handling. Determine whether silent return is navigation-state reset, projection refresh, Historian/realtime failure handling or another mechanism. Finish as `CONFIRMED` or `UNCERTAIN — BOUNDED` with Wave 15 regression contract.

### A5 — Runtime Popup `—` / disappearance / auto-return

Next direct-Codespace priority alongside A4. Correlate authoritative TAG value/quality, binding resolution, realtime, Runtime projection refresh and `RuntimeVisualNavigator` navigation state. Distinguish value projection failure from popup/navigation-state loss. Finish as `CONFIRMED` or `UNCERTAIN — BOUNDED`.

### A7 — Screen/Popup Editor real functional inventory

Static capability map exists; real UI must classify exposed actions as `WORKS / DEFECT / ABSENT-UNSUPPORTED / NOT VALIDATED`. Do not infer functional usability merely from code presence.

### A8 — Script Engineering real functional inventory

Static map and the confirmed event-authoring defect exist; real UI must still prove or refute the developer-complete workflow from discovery -> authoring -> diagnostics -> trigger/binding -> persistence -> runtime observation/debugging.

## Immediate next sequence

1. Codex performs A4 + A5 correlated live diagnosis first; if window remains, continue A7/A8 evidence only.
2. Main Coordinator accepts/rejects each handoff against live GitHub and updates checklist/backlog.
3. When A4/A5/A7/A8 are sufficiently closed, finalize Wave 14 diagnostic transfer documentation.
4. Revalidate #263 and integrate canonical C11 into `wave14/corrections-integration` only when the closure package is ready.
5. Propagate selected closure/roadmap/handoff docs onto the integration route without merging Preview #290.
6. Exact-SHA validate integration; diagnose any red gate before rerun.
7. Revalidate #212 and merge to `main` only under its conditional authorization and expected-head protection.
8. Validate exact new `main`.
9. Close Wave 14 surfaces without merging forbidden PRs or deleting evidence.
10. Open Wave 15 from exact validated new `main`; execute corrections.
11. Fresh Wave 15 Codespace Preview -> technical readiness -> real browser audit -> diagnostic/log review -> targeted residual corrections -> Product Owner maturity decision.
12. Wave 13 remains paused unless separately resumed.

## Permanent guardrails

- GitHub live wins over stale docs/chat for current state;
- no direct `main` mutation;
- no force push, destructive rebase or branch deletion;
- no blind workflow rerun;
- never weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics or drivers;
- Runtime/Active remains independent of `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- no EEE-specific workaround for a generic platform defect;
- uncertain diagnostics stay explicit;
- no new broad product corrections in remaining Wave 14 diagnostic work;
- Wave 15 completion does not automatically resume Wave 13.

Current decision:

`WAVE14 = FINISH A4/A5/A7/A8 DIAGNOSIS + DOCUMENT + TRANSFER -> INTEGRATE ACCEPTED BASELINE TO MAIN -> CLOSE -> WAVE15 = CORRECTIONS + DEVELOPER-FUNCTIONAL MATURITY -> FRESH PREVIEW + AUDIT -> PRODUCT OWNER MATURITY DECISION -> WAVE13 REMAINS PAUSED UNTIL SEPARATELY RESUMED`
