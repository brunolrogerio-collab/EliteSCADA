# Current coordinator pointer — Wave 15

Snapshot: 2026-10-15. Replace this snapshot when state changes; keep detailed history in the owning issue/PR. Live refs, published code, and exact-SHA Actions evidence govern implementation state.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Verified product checkpoint | `54c7c9942b57328cabcef740c99f4ab3207cc040` — PR #617 |
| Product front | Asset & Process Intelligence, issue #588; Stage 3 partially delivered |
| Completed slice | `net-observed-rate.v1` — INTEGRATED / VERIFIED |
| Product executor | Chat CODEX; assigned package #619 |
| Immediate direction | Implement recorded-current comparison under an explicit measured-frequency range |
| Next owner/action | CODEX implements/tests/publishes the assigned draft PR; Main reviews and integrates |
| Product authorization in this snapshot | Only #619; exact Main control/start commit is recorded in its activation comment |

Coordination-only commits after the product checkpoint do not invalidate its product evidence. Discover the current coordination HEAD from the live integration ref.

Workflow adjustment #618 is integrated at coordination HEAD `120d93a477924b34994e2a1c2167b873c93c512d`; CODEX acknowledged it. The finite [Stage 3 acceptance matrix](WAVE15-INTELLIGENCE-STAGE3-ACCEPTANCE.md) fixes the remaining V1 work. [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md) defines #619. This published Main control branch carries the current assignment until it joins the implementation PR/integration; its immutable commit is linked from #619/#305.

## Evidence

- Candidate `453430328040daf111031a76ff11279599d880ea`: focused local Intelligence tests 73/73; [T1 #38811841547](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38811841547), attempt 2, passed (1,490 .NET passed / 1 skipped).
- Merge `54c7c9942b57328cabcef740c99f4ab3207cc040`: [post-merge CI #38813353099](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38813353099) passed backend build/test/runtime smoke, Web build, and Chromium integration smoke 13/13. Full-browser E2E was skipped.
- [Accepted slice disposition in #588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6203170115); [execution ledger #305](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305#issuecomment-6203171386).
- The earlier checkout-access blocker is resolved. Do not resume its retries or reopen the completed slice.

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
