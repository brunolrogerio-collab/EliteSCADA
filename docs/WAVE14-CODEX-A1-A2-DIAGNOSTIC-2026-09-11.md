# Wave 14 — Codex A1/A2 correlated diagnostic — 2026-09-11

Status: **diagnostic closure only; no product correction performed**  
Authority: issue #286 comment `5629189357`, revalidated live at 2026-09-11 04:40 UTC.  
Source branch/head at capture: `preview/wave14-post-c26-work-audit` / `e1e0602e7ed60104650594624ab467e6a0cec61b`.  
Validated product candidate retained: `59e815eae524b9ff043ea6bf3f797f4c01ba9143`.  
Frozen package SHA-256 retained: `e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d`.

## Executive result

- **A1 is `UNCERTAIN — BOUNDED` at root-cause level.** The failure/latency is real and repeatable only on the forwarded browser path in this window. Local Vite and API stayed healthy and fast while the forwarded page remained blank. Evidence excludes an API outage and strongly bounds the fault to the browser/forwarding/static-module delivery path. The exact component inside Codespaces forwarding versus browser client could not be observed with the available telemetry.
- **A1 product error UX is `CONFIRMED / GENERIC PRODUCT`.** Earlier in the same live window Runtime rendered `HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE` with `(500) Failed to fetch`; no corresponding API 500 was present. The UI converts a transport-level fetch failure into a misleading server-status presentation and does not expose a useful retry/diagnostic boundary.
- **A2 is `CONFIRMED / GENERIC PRODUCT`.** The Working workspace is truly `demo`; it is not merely the `Demo Project` fallback. Runtime Active is truly `eee-demo` revision 2. The deterministic mechanism is two independent startup paths: `EngineeringWorkspace` always calls `SeedDemo()` and assigns `demo`, while persisted Runtime recovery uses `EngineeringRuntime:ProjectKey=eee-demo` and restores only Active Runtime. No automatic checkout synchronizes Working to the configured persisted project.

## A1 — forwarded route latency and `Failed to fetch`

### Finding W14-CODEX-A1-001

- **Severity:** P1
- **Area:** Codespaces forwarding / SPA bootstrap / Runtime ↔ Engineering navigation
- **Title:** Forwarded SPA can remain blank for more than two minutes while local Vite and API are healthy
- **Status/classification:** `UNCERTAIN — BOUNDED`; environment/transport attribution, not proven product/API outage
- **Reproduction:** authenticate in the forwarded Preview; navigate `/engineering`, then `/`, then `/engineering`; observe the route until the full shell is usable; in the same interval query local `127.0.0.1:5173` and `127.0.0.1:5080`, process table and logs without restart.
- **Expected:** route becomes interactive promptly or shows a bounded, actionable error.
- **Observed:** first `/engineering` navigation exceeded the 10 s navigation wait and became usable only after about 25 s. Return to `/` also exceeded 10 s and became usable after about 28 s. A second `/engineering` navigation reported navigation completion in 6.942 s but remained a completely blank white page for more than two minutes.
- **Reproducibility:** 3/3 route transitions were materially slow; 1/3 remained blank beyond the observation window.
- **Evidence:** browser console showed Vite connect/connected at `04:37:43.158Z`/`04:37:43.770Z` for the blank load, but no subsequent React DevTools/module-load line through the correlated local check at `04:39:52Z`. Prior successful loads showed React module completion 13–16 s after Vite connected. At `04:39:52Z`, while the forwarded page was still blank, local `GET http://127.0.0.1:5173/engineering` returned 200 in 0.150986 s and local `GET http://127.0.0.1:5080/health` returned 200 in 0.002692 s.
- **Process/log evidence:** API PID 2776 listened on `127.0.0.1:5080`; Vite PID 3014 listened on `0.0.0.0:5173`. `.preview/api.log` remained active and recorded 200 responses for Runtime projection in 63–115 ms. `.preview/web.log` contained only Vite startup and had not changed since `04:03:02Z`, so it provides no per-request forwarding telemetry.
- **Facts excluded:** no API crash, listener loss, local Vite outage, local API health failure, or matching API 500 occurred in the correlated interval.
- **Missing observation:** Codespaces edge/forwarder request trace and browser network waterfall for the stalled JS/module request. Browser console alone showed successful Vite handshake.
- **Wave 15 correction/diagnostic contract:** first reproduce with a request-id/timestamp across browser network waterfall, Codespaces port-forward trace, Vite access logging and API request logging. Only then decide whether product code is involved. Independently add a bounded SPA bootstrap timeout with an actionable retry and transport diagnostics instead of an indefinite white page.
- **Deterministic regression:** inject a delayed/failed main module or forwarded API request; assert that the shell reports a bounded transport error with retry and that retry recovers without reload. Infrastructure acceptance must correlate forwarded and local request IDs while local health remains green.

### Finding W14-CODEX-A1-002

- **Severity:** P2
- **Area:** Runtime projection error UX
- **Title:** Transport `Failed to fetch` is presented as HTTP 500 without matching server 500 evidence
- **Status/classification:** `CONFIRMED / GENERIC PRODUCT` for the presentation; underlying transport remains bounded under A1-001
- **Reproduction:** keep the Codespace running and observe the embedded/forwarded Runtime during a transport interruption.
- **Expected:** distinguish network/forwarding failure from an HTTP response and offer retry/context.
- **Observed:** Runtime alternated between the complete `EliteSCADA — EEE Demo` projection and `HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE — Runtime não disponível para esta sessão. (500) Failed to fetch`. Local API logs and direct requests showed 200/healthy service in the same window.
- **Reproducibility:** observed twice in the embedded Runtime during this diagnostic window, with later spontaneous recovery.
- **Wave 15 correction contract:** preserve server HTTP status when a response exists; classify `TypeError/Failed to fetch` as transport-unavailable with endpoint, timestamp and retry; do not synthesize 500.
- **Deterministic regression:** mock rejected fetch versus real 500 and assert distinct codes/messages and successful retry recovery.

## A2 — Working `demo` versus Runtime Active `eee-demo`

### Finding W14-CODEX-A2-001

- **Severity:** P1
- **Area:** Engineering bootstrap / persistence / lifecycle identity
- **Title:** Working starts from hard-coded in-memory `demo` while configured persisted Runtime recovers `eee-demo`
- **Status/classification:** `CONFIRMED / GENERIC PRODUCT`
- **Reproduction:** start the documented Preview with existing PostgreSQL persistence; authenticate; query workspace, persistence status, both lifecycle records and Runtime application without checkout, activation or mutation.
- **Expected:** Engineering Working identity is explicitly selected/restored and coherently relates to the configured project, while Active remains authoritative and independent.
- **Observed:** `/api/engineering/workspace` returned `projectKey=demo`, `projectName=Demo Project`, `baseRevision=null`, `changeVersion=0`, `isDirty=false`. `/api/runtime/application` returned `projectKey=eee-demo`, `projectName=EliteSCADA — EEE Demo`, `revision=2`, `activatedAtUtc=2026-09-11T04:02:50.5928172+00:00`. `/api/engineering/persistence/status` returned PostgreSQL enabled, `configuredProjectKey=eee-demo`, `hasProjects=true`.
- **Lifecycle evidence:** `demo` lifecycle is empty (`workingRevision`, `publishedRevision`, `activeRevision` all null). `eee-demo` has working/published/active revision 2; persisted activation is `2026-09-09T21:17:27.905488+00:00`. UI correctly warns that configured Runtime project is `eee-demo` and blocks cross-project activation.
- **Mechanism:** `scripts/preview/launch-post-c26-preview.sh` sets `PROJECT_KEY="eee-demo"`, exports `EngineeringRuntime__ProjectKey`, and creates First Project only when persistence has no projects. `EngineeringWorkspace` constructor (`src/Scada.Api/Runtime/EngineeringWorkspace.cs:63-75`) always calls `SeedDemo()`, whose state assignment (`:448-456`) sets `_projectKey="demo"`, `_projectName="Demo Project"`, no base revision and clean change version 0. Separately, `src/Scada.Api/Persistence/EngineeringPersistenceApi.cs:47-78` initializes persistence and calls `RecoverConfiguredEngineeringRuntimeAsync`, which loads the configured project into Runtime Active. That recovery does not call the checkout service or replace the in-memory Working workspace.
- **Facts excluded:** the visible `Demo Project` is not a missing-snapshot UI fallback; the authenticated workspace API independently proves `demo`. It is not a prior persisted `demo` checkout: the lifecycle for `demo` is empty and `baseRevision`/`checkedOutAtUtc` are null. Active authority is not corrupted; `eee-demo` persistence and Runtime projection agree on revision 2.
- **Wave 15 correction contract:** introduce an explicit Working bootstrap selection contract after persistence initialization. Resolve the configured/current project deterministically, checkout its intended revision into `EngineeringWorkspace`, and expose selection/recovery state. Keep Working and Active separate; never auto-activate, never rewrite Active, and retain cross-project activation rejection. Reserve the demo seed for an explicit no-persistence/new-project mode rather than unconditional construction.
- **Deterministic regression:** with PostgreSQL containing active `eee-demo` r2 and no persisted `demo`, startup must yield Active `eee-demo` r2 and Working explicitly selected according to the bootstrap contract (expected `eee-demo` checkout), with coherent base revision and clean state. Add a second test where a deliberately selected different Working project coexists with Active and the UI shows that choice rather than a fallback. Assert activation remains rejected across project keys.
- **Dependencies/order:** define project-selection semantics first; then implement server bootstrap/checkout; then update UI recovery/selection messaging; finally run persistence restart, lifecycle, package and Active-authority regressions.

## Correlated measurements

See `evidencias/W14-CODEX-A1-A2-2026-09-11.txt` for the compact raw transcript. No cookie, password, token or response body containing authentication material is included.

## Actions and protected boundaries

Performed: read-only browser reproduction; authenticated GETs; local curl timing; process/port inspection; log tails; static source inspection; GitHub live revalidation; documentation-only evidence creation.

Not performed: no product edit, patch, package regeneration, activation, checkout, save, publish, reset, project deletion, process restart, workflow rerun, PR merge, base mutation, force push, destructive rebase, security/lifecycle/Active-authority change or test weakening.
