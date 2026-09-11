# Wave 14 — Diagnostic Closure Acceptance — 2026-09-11

**Authority:** Main Coordinator acceptance after GitHub-live revalidation  
**Scope:** diagnostic closure only; no new Wave 14 product correction is authorized by this document.

> GitHub live remains the sole authority. Revalidate current issues, PRs, branches, SHAs and CI before any integration or merge action.

## Closure decision

The remaining Wave 14 diagnostic lanes are accepted at the level required to transfer correction work into Wave 15. A finding is considered closed for Wave 14 when it is either `CONFIRMED` with an implementable correction/regression contract or `UNCERTAIN — BOUNDED` with explicit facts, exclusions, missing observation and future capture method.

This acceptance does **not** mean the product defects are fixed. It means Wave 15 can implement without repeating the exploratory audit from zero.

## Accepted direct-Codespace handoffs

### A1 / A2

Authority:

- #286 comment `5629630825`;
- evidence commit `f05589f8afc4a0f658868416ee5b38d1c3681d60`.

Accepted state:

- **A1 transport root:** `UNCERTAIN — BOUNDED`; local API/Vite outage excluded in the captured window; unresolved root is in forwarded-browser/static-module delivery and requires browser waterfall + Codespaces forwarding/edge trace + Vite request-level telemetry under shared timestamps/request identity.
- **A1 product error UX:** `CONFIRMED / GENERIC PRODUCT`; indefinite blank bootstrap and rejected fetch shown as fictitious HTTP 500 are product-owned failures.
- **A2 Working `demo` vs Active `eee-demo`:** `CONFIRMED / GENERIC PRODUCT`; unconditional `EngineeringWorkspace.SeedDemo()` creates the in-memory Working while persistence startup recovers configured `eee-demo` separately into Active Runtime. Cross-project activation protection is correct and must remain fail-closed.

### SIM / A4 / A5

Authority:

- #286 handoff `5634220264`;
- evidence commit `b549cae83e2aa2099f685b5259439bb5425b80cd`;
- `docs/WAVE14-CODEX-RECHECK-SIM-TRENDS-DIAGNOSTIC-2026-09-11.md` and associated evidence.

Accepted state:

- **W14-CODEX-SIM-001:** `P1 / CONFIRMED / GENERIC PRODUCT`. A timer-driven isolated Python Server Script reached six timeouts and five consecutive failures, set `isThrottled=true`, retained a queued event and heavy coalescing, and then stopped useful process progression while API/Vite remained healthy. TAG and Historian timestamps froze together. The generic throttle is a durable latch with no automatic bounded recovery. Wave 15 must preserve isolation, allowlist, timeout, cancellation, Active revision authority and bounded queues while adding observable, bounded recovery; simply raising/removing the timeout is not acceptable.
- **A4 Trends:** `P2 / UNCERTAIN — BOUNDED`. The previously reported silent return did not reproduce in two attempts. One Trends instance remained mounted at `Conectando dados ao vivo…` while local History returned HTTP 200 with 104 points. A later attempt was obscured by the already-bounded forwarding failure. Excluded: empty Historian result, local API/Vite outage in the captured request, and intentional navigation inside `BasicTrendViewer`. Future capture requires stable forwarding plus browser waterfall, SignalR state, navigator active view and projection identity under shared timestamps/request IDs.
- **A5a Popup values:** `P2 / UNCERTAIN — BOUNDED`. Popup fields rendered `—` while authoritative current TAG samples were numeric zero with Good quality and bindings referenced the exact TAG IDs. Static inspection excludes wrong binding, absent authoritative sample, non-Good quality and zero-as-falsy formatting. Missing evidence is the exact browser request/hook sample/freshness state during the occurrence. Wave 15 must expose live-feed request/success/unavailable state and preserve Good zero unless an explicit freshness rule invalidates it.
- **A5b Popup persistence:** `P1 / CONFIRMED / GENERIC PRODUCT`. A Popup disappeared after a forwarded projection failure while local `/api/runtime/application` and `/api/tags` remained HTTP 200 and Active authority stayed `eee-demo` revision 2. `RuntimeApplicationMount` can clear the last successful projection and unmount `RuntimeVisualNavigator`; recovery remounts initial navigation with an empty popup stack despite unchanged Active authority. Wave 15 must retain last successful projection/navigation state through transport failures when project/revision/activation identity is unchanged and reinitialize only on a real authority change.

### A7 / A8

Authority:

- #286 handoff `5634503355`;
- diagnostic branch docs-only HEAD `71456b04a41df5a935d4c69a3d1a86df40cec132` at handoff time.

Accepted state:

- **A7 Screen/Popup Editor selection crash:** `P1 / CONFIRMED / GENERIC PRODUCT`. Selecting an existing persisted visual object caused the complete Engineering SPA to become a blank dark viewport while remaining on `/engineering`; reproduced 5/5 total across Screen and Popup. The selected-state render path reaches `PropertyInspector` and `DynamicPropertyEditor`; exact thrown exception below that boundary was not captured, but Wave 14 already independently confirmed the legacy visual-schema compatibility defect. Selection-dependent editor functions therefore remain `NOT VALIDATED` because the deterministic crash blocks them; this is an acceptable bounded closure for Wave 14, not evidence that those functions work.
- **A8 Script Engineering composition/discovery:** `P2 / CONFIRMED / GENERIC PRODUCT` in addition to the previously confirmed event-authoring defect. Consecutive visible `Inserir · Ler` actions generate complete snippets without syntactic separation, creating invalid Python. The assistance exposes TAG names/IDs/types, but object properties/API signatures/parameters/targeting/examples are insufficient for a developer to discover and compose the Product Owner representative flow without guessing. Combined with `docs/WAVE14-DIAGNOSTIC-SCRIPT-EVENT-AUTHORING-GAP.md`, Wave 15 must make snippet insertion cursor-aware/composable, expose callable signatures and visual-object/property addressing, and prove an end-to-end UI-only compare-two-TAGs/change-state workflow.

## Previously accepted static diagnoses retained

- **A3 persisted legacy visual types:** `P1 / CONFIRMED / GENERIC PRODUCT`; canonical document `docs/WAVE14-DIAGNOSTIC-LEGACY-VISUAL-TYPE-COMPATIBILITY.md`. Known legacy identifiers need compatibility/migration before strict schema consumers; arbitrary unknown types must remain rejected safely.
- **A6 Engineering fallback/recovery UX:** `CONFIRMED / GENERIC PRODUCT`; missing/unloaded public model must not masquerade as an authoritative `Demo Project` Working identity.
- **Script event authoring:** `CONFIRMED / GENERIC PRODUCT`; surfaced `timer` and `tagChanged` entry points cannot be fully authored because required event-specific fields are not exposed and stale event-specific fields can become hidden validation blockers.

## Wave 14 diagnostic exit status

Accepted for transfer:

- A1 — closed as bounded transport root + confirmed product UX;
- A2 — confirmed;
- A3 — confirmed;
- A4 — bounded;
- A5a — bounded;
- A5b — confirmed;
- A6 — confirmed;
- A7 — confirmed selection blocker; remaining selection-dependent feature inventory is explicitly blocked/not validated rather than silently passed;
- A8 — confirmed developer-usability defects; remaining end-to-end lifecycle proof transfers as Wave 15 acceptance work;
- SIM — confirmed generic P1 and supersedes the earlier `not confirmed` classification.

No remaining material Wave 14 finding is left as an unbounded `UNCERTAIN` placeholder.

## Required Wave 15 implementation order

1. **Working authority/bootstrap** — correct A2 without touching Active authority as a side effect.
2. **Server Script recovery/observability** — correct permanent throttle/freshness behavior generically.
3. **Editor selection/schema compatibility** — remove the selection crash and legacy compatibility failure; only then complete dependent editor functional corrections.
4. **Runtime projection/navigation resilience** — preserve screen/popup state across transport failure when Active authority is unchanged.
5. **Script Engineering authoring/discovery** — event-specific fields, composable insertion, API/property discovery, UI-only representative compound flow.
6. **Trends and Popup live-value follow-up** — retest their bounded live-feed findings after generic Server Script and projection/navigation corrections, using stable transport telemetry.
7. deterministic P2 shell/accessibility/navigation/resource-preview backlog.

## Permanent boundaries

- Wave 14 diagnostic closure does not authorize new broad product correction on the Preview branch;
- #290 remains Preview-only and is never the route to `main`;
- #296 remains diagnostic-only / MUST NEVER MERGE;
- validation-only / MUST-NEVER-MERGE PRs remain non-merge routes;
- no direct `main` mutation, force push, destructive rebase or evidence deletion;
- no weakening of security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics, Drivers or deterministic regressions;
- Runtime/Active remains independent of `.escadalib`;
- Alarm, Operational Event and Audit remain distinct;
- no EEE-specific workaround for generic defects;
- Wave 13 remains paused until a later explicit Product Owner maturity decision.

## Next coordinator action

With this diagnostic package accepted, the Main Coordinator may now prepare the Wave 14 integration route:

`canonical C11 -> PR #263 -> wave14/corrections-integration -> exact-SHA validation -> PR #212 -> main`

Before every integration/merge action, revalidate GitHub live and ensure only intended baseline + selected closure documentation are carried. #290/#296/validation-only branches must not be merged as a shortcut.