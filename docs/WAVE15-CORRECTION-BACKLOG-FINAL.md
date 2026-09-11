# Wave 15 — Correction Backlog Finalized from Wave 14 Diagnostics

**Prepared:** 2026-09-11 BRT  
**Execution status:** NOT STARTED — executable only after Wave 14 is integrated into and validated on `main`.

> GitHub live is the sole authority. Revalidate each item against the exact new validated `main` before implementation.

## Priority order

| ID | Priority | Area | Wave 14 state | Wave 15 correction objective |
|---|---|---|---|---|
| W15-P0-01 | P0/P1 | Engineering Working identity | CONFIRMED | Replace unconditional `demo` Working bootstrap with explicit persisted Working selection/checkout semantics while preserving Published/Active authority and fail-closed cross-project activation. |
| W15-P1-01 | P1 | Server Script runtime recovery/observability | CONFIRMED | Preserve sandbox isolation/timeout/bounded queue while removing permanent silent throttle latch; add bounded recovery and observable health/freshness. |
| W15-P1-02 | P1 | Screen/Popup legacy schema compatibility | CONFIRMED | Add compatibility/migration/recovery for known persisted legacy visual types without accepting arbitrary unknown types. |
| W15-P1-03 | P1 | Screen/Popup selection stability | CONFIRMED | Selecting persisted objects must never blank/poison the Engineering SPA; contain malformed-object failures and preserve editor session. |
| W15-P1-04 | P1 | Runtime projection/navigation persistence | CONFIRMED | Retain last successful projection and Screen/Popup navigation state through transport failures when Active authority is unchanged; reinitialize only on real authority change. |
| W15-P1-05 | P1/P2 | Script Engineering functional maturity | CONFIRMED defects + incomplete end-to-end maturity | Add event-specific authoring, composable snippet insertion, API signatures/parameters/object-property targeting, useful diagnostics and a UI-only representative compound flow. |
| W15-P1-06 | P1/P2 | Engineering/SPA error and fallback UX | CONFIRMED | Replace indefinite blank/fictitious status/fake Working identity with bounded truthful loading, unavailable, transport-error and retry states. |
| W15-P2-01 | P2 | Runtime Trends | UNCERTAIN — BOUNDED | Keep Trends mounted and distinguish data/no-data/History error/realtime error. Retest with stable forwarding after upstream corrections. |
| W15-P2-02 | P2 | Popup live values | UNCERTAIN — BOUNDED | Expose live-feed freshness/request state and preserve Good numeric zero. Retest after Server Script recovery and projection/navigation correction. |
| W15-P2-03 | P2 | Shared header responsiveness | CONFIRMED UI | Remove common notebook-width overlap without breaking Runtime/Engineering navigation/account controls. |
| W15-P2-04 | P2 | Account accessibility | CONFIRMED UI | Add accessible names and keyboard-operable semantics to account/menu interactive controls. |
| W15-P2-05 | P2 | Engineering navigation/scroll | CONFIRMED UI | Eliminate harmful dependence on global page scroll and preserve editor workspace/canvas priority. |
| W15-P2-06 | P2 | Engineering Lock footprint | CONFIRMED UI | Reduce excessive footprint without weakening lock authority or diagnostics. |
| W15-P2-07 | P2 | Templates/Equipment/Dynamos/Libraries | CONFIRMED usability | Add useful preview/inspection/metadata before reuse/insertion. |
| W15-U-01 | — | Codespaces/forwarding latency root | UNCERTAIN — BOUNDED | No product transport patch until browser waterfall + forwarding/edge + Vite request telemetry identifies a product-owned divergence. |
| W15-U-02 | — | Alarm timestamp semantics | UNCERTAIN — BOUNDED | Reopen only with same-occurrence authority/timestamp comparison; keep Alarm, Operational Event and Audit distinct. |

## W15-P0-01 — Working identity/bootstrap

**Wave 14 authority:** #286 `5629630825`, commit `f05589f8afc4a0f658868416ee5b38d1c3681d60`.

Confirmed mechanism:

- `EngineeringWorkspace` unconditionally seeds `demo` into in-memory Working;
- persistence startup separately recovers configured `eee-demo` into Active Runtime;
- no startup checkout replaces Working;
- lifecycle correctly blocks cross-project activation.

Required regression:

- persisted `eee-demo` Active with no persisted `demo` starts with coherent explicit Working selection;
- deliberate alternate Working may coexist but is explicit;
- Active never changes as a side effect of Working bootstrap;
- cross-project activation remains rejected;
- restart/reopen preserves semantics.

## W15-P1-01 — Server Script throttle recovery

**Wave 14 authority:** #286 `5634220264`, commit `b549cae83e2aa2099f685b5259439bb5425b80cd`.

Confirmed mechanism:

- isolated Python timer reached six timeouts and five consecutive failures;
- generic failure throttle latched `isThrottled=true`;
- one event remained queued while subsequent events coalesced heavily;
- TAG and Historian progression stopped while API/Vite and Active authority remained healthy;
- current latch has no automatic bounded recovery.

Correction contract:

- preserve process isolation, allowlist, timeout, cancellation and Active revision gate;
- separate/measure startup+IPC, handler and replay cost;
- use bounded recovery such as cooldown/half-open/probe with auditability;
- expose script health, last useful execution and TAG freshness;
- retain bounded queue/coalescing and prevent stale writes/busy loops;
- do not merely raise/remove timeout and do not special-case EEE.

Regression:

- long-running timer load;
- injected transient timeouts recover automatically in bounded fashion;
- persistent failure visibly degrades while queue remains bounded;
- no stale write after recovery;
- TAG/History freshness and throttle/recovery counters are asserted.

## W15-P1-02 / P1-03 — Visual compatibility and selection stability

Authorities:

- `docs/WAVE14-DIAGNOSTIC-LEGACY-VISUAL-TYPE-COMPATIBILITY.md`;
- #286 `5634503355`.

Requirements:

- known persisted legacy visual identifiers migrate/normalize before strict schema consumers;
- arbitrary unknown identifiers remain rejected with contained actionable diagnostics;
- selection through canvas and tree must preserve SPA/editor session;
- malformed object/property/destination must not blank all Engineering;
- deterministic coverage for Screen + Popup + current `core.*` + known legacy + truly unknown negative;
- only after selection stability is restored should the full dependent authoring matrix be corrected/validated: move/resize, clipboard, history, grouping, lock, alignment/distribution, z-order, Properties, bindings, text/content, zoom/pan, grid/snap, preview and persistence.

## W15-P1-04 — Projection/navigation persistence

**Wave 14 authority:** #286 `5634220264` A5b.

Confirmed mechanism:

- forwarded projection failure can clear last successful projection;
- navigator unmount destroys local active Screen/Popup stack;
- recovery remounts initial navigation even when `projectKey/revision/activatedAtUtc` is unchanged.

Correction contract:

- retain last successful projection across transport/retryable failure;
- preserve navigation/popup stack while Active authority identity is unchanged;
- show stale/unavailable state explicitly;
- deliberate reinitialization only when real authority changes.

Regression:

- open Popup, inject rejected fetch/retryable failure, recover same authority, Popup remains;
- change revision/project/activation identity, navigator deliberately reinitializes.

## W15-P1-05 — Script Engineering maturity

Authorities:

- `docs/WAVE14-DIAGNOSTIC-SCRIPT-EVENT-AUTHORING-GAP.md`;
- `docs/WAVE14-STATIC-SCRIPT-ENGINEERING-CAPABILITY-MAP.md`;
- #286 `5634503355` A8.

Confirmed defects:

- surfaced `timer` and `tagChanged` entry points lack required event-specific authoring controls;
- switching event kinds can leave hidden stale fields that validation rejects;
- consecutive visible `Inserir · Ler` actions emit complete snippets without syntactic separators, creating invalid Python;
- API/object/property discovery is insufficient for the Product Owner representative flow without guessing.

Minimum Wave 15 bar:

- cursor-aware syntactically composable insertion;
- callable API signatures and parameters;
- explicit object/property targeting and supported visual properties;
- discover TAGs, screens, popups, objects and properties from UI;
- understandable line/column diagnostics;
- author every surfaced event kind or do not surface unsupported combinations;
- validate/preview true and false branches;
- save/revision/publish/activate where applicable;
- observe runtime execution/failure;
- complete through visible UI: read/compare two TAGs and conditionally alter an allowed object visual state/property.

## W15-P1-06 — Error/fallback UX

Product-owned correction is required even when initiating transport failure is external:

- no indefinite blank SPA bootstrap;
- no rejected fetch rendered as fictitious HTTP 500;
- no missing snapshot represented as authoritative `Demo Project`;
- distinguish actual HTTP response status from transport rejection;
- expose endpoint/context/timestamp/retry where useful;
- retry must recover without mutating project/lifecycle authority.

## W15-P2-01 — Trends bounded follow-up

Wave 14 did **not** reproduce the originally reported silent return (0/2). It did reproduce Trends remaining at `Conectando dados ao vivo…` while History had 104 points, then transport instability obscured further correlation.

After W15-P1-01 and W15-P1-04, retest with stable forwarding and shared timestamps/request IDs for History requests, SignalR state, active navigator view and projection identity.

Required behavior/regression:

- data;
- valid no-data;
- History error;
- realtime disconnected/reconnecting;
- projection polling failure/recovery under unchanged authority;
- genuine Active authority change.

## W15-P2-02 — Popup live-value bounded follow-up

Wave 14 proved authoritative values were numeric zero/Good and bindings were correct while Popup rendered `—`, but did not capture the exact browser live-value hook/freshness state.

After upstream runtime corrections:

- expose last request/last success/unavailable/freshness reason;
- Good numeric zero must render as zero unless a documented freshness policy invalidates it;
- test zero/null, Good/non-Good, stopped/running, reconnect and open/close/reopen.

## Preserved future requirements — not Wave 14 defect conclusions

- protected whole-system backup/restore distinct from `.escadapkg`;
- Historian Administration backup/export/import/restore;
- generic raw→engineering scaling with explicit inverse-write semantics;
- decimal-place authoring persisted through Save/Revision/Publish/Activate/Runtime/package;
- real EEE Modbus/PLC variant only after generic maturity gates;
- Wave 13 signed Windows release/signing work only after later explicit Product Owner maturity decision.

## Wave 15 opening gate

Do not execute this backlog until:

1. Wave 14 diagnostic transfer is preserved in GitHub;
2. canonical C11 is integrated through #263 into `wave14/corrections-integration`;
3. selected Wave 14 closure/Wave 15 documents are propagated onto that integration route without merging #290/#296/validation-only PRs;
4. exact integration SHA is green on required gates;
5. #212 is revalidated and merged to `main` through its protected route;
6. exact new `main` is validated;
7. Wave 14 coordination/audit surfaces are closed/preserved appropriately.

Wave 15 then starts from the exact validated new `main`, never from the Wave 14 Preview branch.