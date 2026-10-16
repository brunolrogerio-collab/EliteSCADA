# Current coordinator pointer — Wave 15

Snapshot: 2026-10-16. Replace this snapshot when state changes; keep detailed history in the owning issue/PR. Live refs, published code, and exact-SHA Actions evidence govern implementation state.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Verified product checkpoint | `ee0371ffc50113f085dc5fb36cc257931b075e1c` — PR #620 / #619 |
| Product front | Asset & Process Intelligence, issue #588; Stage 3 partially delivered |
| Completed slice | `current-at-recorded-frequency.compare.v1` — INTEGRATED / VERIFIED |
| Product executor | Chat CODEX; assigned package #621, PERFORMANCE-V1A |
| Immediate direction | Add versioned physical-input metadata with safe canonical/editor/import round-trip |
| Next owner/action | CODEX implements #621 when dispatch/capacity is confirmed; Main reviews/integrates |
| Product authorization in this snapshot | Only #621; activation, exact Main control/start commit and executor availability are recorded in its issue |

Coordination-only commits after the product checkpoint do not invalidate its product evidence. Discover the current coordination HEAD from the live integration ref.

Workflow adjustment #618 is complete. The finite [Stage 3 acceptance matrix](WAVE15-INTELLIGENCE-STAGE3-ACCEPTANCE.md) fixes the remaining V1 work. [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md) records accepted #619; [ADR-016](ADR-016-INTELLIGENCE-PERFORMANCE-V1.md) fixes the physical-input/formula scope. A concrete old-client/import capability risk requires prerequisite A (#621), then formula delivery B. Only A is active. This published Main control branch carries the current order until its docs join the implementation PR; its immutable commit is linked from #621/#305.

## Evidence

- Candidate `c2c7dd1c90db9ab7ba65bd93fd69621e7651d002`: local Analytics 78/78 and API build passed; [T1 #38832922847 attempt 2](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38832922847/attempts/2) passed (selected method 1/1, remaining Drivers 1,513 passed / 1 skipped).
- Merge `ee0371ffc50113f085dc5fb36cc257931b075e1c`: [post-merge #38834562474](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38834562474) passed backend build/test/runtime smoke, Web and Chromium integration smoke 13/13. Full-browser E2E was skipped.
- First T1 failed in unchanged HA initial-policy synchronization; one documented failed-job retry passed unchanged. Root cause remains unproven. Local HA reproduction was NOT RUN: disposable PostgreSQL/Docker unavailable; one Docker start failed in backend socket initialization. No repeated recovery or HA changes are authorized.
- #619/#620 contain source review, CF-01/02/03 corrections and exact integration evidence. #621/#305 hold activation and dispatch state; do not infer execution from publication alone.

## Operating documents

1. [Global collaboration protocol](CHAT-COLLABORATION-PROTOCOL.md), including the incremental-read and handoff cycle.
2. [Main's current order and return contract](WAVE15-MAIN-COORDINATOR-HANDOFF.md).
3. [CI policy](CI-USAGE-POLICY.md) and the active mission's required profiles.
4. [State-independent bootstrap](NEXT-COORDINATOR-CHAT-HANDOFF.md) for recovery; [document authority map](README.md) for conflicting sources.

Read #297 first, #302 next when establishing the Wave/Authority baseline; then #305 and the active mission's latest comments. For an unchanged ongoing mission, check only changed material and the live decision inputs.

## Standing limits and pending owner validation

- Windows/Linux and the product's other declared platforms remain architectural scope.
- Intelligence remains licensed, selective, read-only analytics. Existing contracts and the boundaries between Stage 3 KPIs, Stage 4 baselines, Stage 5 visuals, and Stage 6 hardening still apply.
- HA acceptance raised by the Product Owner remains for the local Preview with the owner. The prior unsupervised HA-related work is a regression risk requiring that verification; a targeted code reversion may be necessary if owner/CODEX checks find problems. CI is not a substitute for that owner acceptance. Do not switch from Intelligence to HA without a new product-direction decision.
- Wave 15 and #588 remain open; this workflow update makes no FND/FC0 or whole-stage completion claim.

Historical snapshots: [previous current pointer](CURRENT-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md), [previous Main orders](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md). They preserve evidence and are not active instructions.
