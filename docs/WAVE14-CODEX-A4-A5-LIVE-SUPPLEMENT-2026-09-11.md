# Wave 14 — Codex A4/A5 live supplement — 2026-09-11

## Authority and scope

Live instruction revalidated in issue #286 comment 5629941076, which recognizes the partial A4/SIM package at b549cae83e2aa2099f685b5259439bb5425b80cd and makes A5 the remaining priority. Product candidate remains 59e815eae524b9ff043ea6bf3f797f4c01ba9143; package SHA-256 remains e995051b4744f904663350102683c886af9674aaae8fff1dbc76f294ff2d774d. This supplement is diagnosis/evidence only.

## A4 — W14-CODEX-TRENDS-001

- Severity: P2
- Classification: UNCERTAIN — BOUNDED
- Area: Runtime / Basic Trends / Historian / navigation
- Reproduction: from Runtime, open Basic Trends and observe mount, live-connection state and whether it returns to the prior screen. Two attempts were made.
- Expected: Trends remains mounted and resolves either data, explicit no-data or explicit request/realtime failure without silently changing navigation.
- Observed: silent return did not reproduce (0/2). One attempt remained mounted at “Conectando dados ao vivo…” with five configured series while local History returned HTTP 200 with 104 points. A later attempt was masked by the already-bounded forwarded-browser Failed to fetch condition.
- Reproducibility: 0/2 for silent return; 1/2 for persistent connecting state.
- Responsible path: BasicTrendViewer state plus parent RuntimeVisualNavigator/projection lifecycle. BasicTrendViewer has local loading/no-data/error handling and no intentional navigation-away path.
- Known exclusions: local API/Vite outage during the captured History request; empty Historian result; intentional navigation by BasicTrendViewer.
- Missing observation: stable forwarded session with browser network waterfall, realtime/SignalR state, navigator active view and projection projectKey/revision/activatedAt under shared timestamps/request IDs.
- Wave 15 contract: distinguish data, valid no-data, history request failure and realtime failure in visible UI; keep Trends mounted through unrelated projection polls when authority is unchanged; preserve explicit user navigation.
- Deterministic regression: mount Trends with each history/realtime state, inject projection polls and transport failure/recovery, assert explicit status and stable active view; change Active authority and assert deliberate reinitialization.

## A5a — W14-CODEX-POPUP-VALUE-001

- Severity: P2
- Classification: UNCERTAIN — BOUNDED
- Area: Runtime Popup / TAG live-value projection
- Steps: open Runtime; select DETALHES P01; compare Current, Frequency, Pressure and Flow in the popup with /api/tags/current and Active projection bindings.
- Expected: Good numeric zero renders as a formatted numeric value; unavailable data has an explicit reason.
- Observed: popup mounted with all four values as “—”. Authoritative current TAG samples were Good zero. Exact TAG paths were EEE.P01.CurrentA, EEE.P01.FrequencyHz, EEE.P01.PressureBar and EEE.P01.FlowM3h, with timestamps 2026-09-11T04:04:28.9785852Z through 04:04:29.0521109Z, quality 0/Good, source eee.sim.server-memory. Active projection bindings referenced those exact TAG IDs.
- Reproducibility: 1/1 in the captured popup opening.
- Responsible path: browser live TAG feed into visualEditorLiveValues formatting.
- Known exclusions: wrong popup binding; missing authoritative sample; non-Good quality; zero-as-falsy formatting. The formatter renders zero when a sample is present and only emits “—” for missing/unavailable/null/undefined.
- Missing observation: browser /api/tags request and hook sample state at the exact popup frame, unavailable/freshness reason, and realtime transport trace. Forwarding instability prevented this correlation.
- Wave 15 contract: expose last request/last success and unavailable reason; render Good zero as numeric unless a documented freshness policy marks it unavailable.
- Deterministic regression: cover zero versus null, Good versus non-Good, stopped versus running source, reconnect, and open/close/reopen.

## A5b — W14-CODEX-POPUP-PERSISTENCE-001

- Severity: P1
- Classification: CONFIRMED / GENERIC PRODUCT
- Area: Runtime Popup / projection polling / navigation persistence
- Steps: open DETALHES P01, leave it open, wait during Runtime projection polling and transport degradation.
- Expected: transient projection fetch failure preserves the last successful projection and popup stack while projectKey, revision and activatedAt remain unchanged.
- Observed: the popup disappeared after about 8 seconds without user interaction. Soon afterward Runtime rendered HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE (500) Failed to fetch. The main screen remained/recovered. Local API stayed healthy: /api/runtime/application HTTP 200 in 0.103803 s and /api/tags HTTP 200 in 0.029961 s at 2026-09-11T05:21:47Z; /api/tags/current HTTP 200 in 0.136252 s at 05:22:35Z. Active identity remained eee-demo revision 2, activatedAtUtc 2026-09-11T04:02:50.5928172Z.
- Reproducibility: 1/1 in the captured popup opening; upstream forwarded transport trigger remains bounded under A1.
- Mechanism: RuntimeVisualNavigator stores popup stack in local state. RuntimeApplicationMount clears lastSuccessfulProjection and sets projection null for failures outside its narrow retryable set, unmounting the navigator. Recovery remounts initial navigation with an empty popup stack even when Active authority is unchanged.
- Wave 15 contract: retain last successful projection for transport failures and preserve active screen/popup stack while projectKey/revision/activatedAt are unchanged. Reinitialize only on a real authority change.
- Deterministic regression: open popup; inject rejected fetch and retryable HTTP failure; recover the same authority and assert popup persists. Then change revision/authority and assert deliberate reinitialization.

## Related natural finding

W14-CODEX-SIM-001 remains CONFIRMED / GENERIC PRODUCT / P1 at b549cae83e2aa2099f685b5259439bb5425b80cd. Server Script dispatch timeouts reach five consecutive failures, enter permanent throttle, and leave one queued/coalescing event without automatic recovery. Preserve process isolation, allowlist, timeout and Active authority; Wave 15 must add bounded recovery and observability rather than an EEE special case or arbitrary timeout removal.

## Actions and boundaries

Performed read-only UI reproduction, authenticated GET inspection, local API/Vite timing, Active projection/binding comparison and static code-path tracing. Added only documentation/evidence on docs/w14-codex-a1-a2-20260911. No product code, restart, activation, checkout, save/publish, reset/delete, workflow rerun, package change, test change, protected merge/base mutation, force push or lifecycle/Active/security/licensing weakening was performed.

