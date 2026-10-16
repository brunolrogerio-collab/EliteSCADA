# Current coordinator pointer — Wave 15

Snapshot: 2026-10-16. Replace this snapshot when state changes; keep detailed history in the owning issue/PR. Live refs, published code, and exact-SHA Actions evidence govern implementation state.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Verified product checkpoint | `ee0371ffc50113f085dc5fb36cc257931b075e1c` — PR #620 / #619 |
| Product front | Asset & Process Intelligence, issue #588; Stage 3 partially delivered |
| Completed slice | `current-at-recorded-frequency.compare.v1` — INTEGRATED / VERIFIED |
| Integrated product HEAD | `a96b49b5531835b2b07ca6f2dc4a2f24be63973e` — PR #622 / #621, NOT VERIFIED |
| Product executor | Chat CODEX; #621 PERFORMANCE-V1A implementation reviewed and integrated |
| Immediate gate | Broad post-merge Backend failed twice; runtime smoke NOT RUN |
| Next owner/action | Main preserves A as unverified and waits for new causal evidence or a product-owner direction |
| Product authorization in this snapshot | No additional same-head test/CI, HA product edit or #623 activation |

Coordination-only commits after the product checkpoint do not invalidate its product evidence. Discover the current coordination HEAD from the live integration ref.

Workflow adjustment #618 is complete. The finite [Stage 3 acceptance matrix](WAVE15-INTELLIGENCE-STAGE3-ACCEPTANCE.md) fixes the remaining V1 work. [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md) records accepted #619; [ADR-016](ADR-016-INTELLIGENCE-PERFORMANCE-V1.md) fixes the physical-input/formula scope. Prerequisite A (#621) is integrated but its verification gate is red. Formula delivery B [#623](https://github.com/brunolrogerio-collab/EliteSCADA/issues/623) is DESIGN FIXED / PLANNED / NOT ACTIVE. This documentation-only control branch updates the resume point; its name does not activate B. Immutable control commit and direct CODEX dispatch are recorded in #621/#305.

## Evidence

- Last verified merge `ee0371ffc50113f085dc5fb36cc257931b075e1c`: #619/#620 and [post-merge #38834562474](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38834562474) passed backend build/test/runtime smoke, Web and Chromium integration smoke13/13. Full-browser E2E was skipped.
- A candidate `5260ccfe7a1ba6b5ef8cfc3dc0b1d6a5be070f6d`: corrected local Core135/135 and endpoint/analytics97/97; [T1 #38893549917](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38893549917) passed. Merge `a96b49b5531835b2b07ca6f2dc4a2f24be63973e` has exactly the candidate tree; tree identity is not a commit SHA.
- Broad [#38894694846 attempt1](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38894694846/attempts/1) and the single evidenced [attempt2](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38894694846/attempts/2) failed in `RealApiProcesses_HaD2SeparateDatabasesPromotesAndFailsBack`, initial-policy synchronization at `WaitForInitialPolicyCommitAsync:844`. Backend jobs116952251038 and116957374868; Drivers1518 passed/1 failed/1 skipped in each. Runtime smoke NOT RUN; Web and Chromium integration passed; full-browser E2E skipped. No further same-head CI retry is authorized.
- CODEX ran the existing method on exact merge a96 in two isolated local environments: Windows1/1 pass (36s test; two DB names on one server) and Linux x64 SDK10.0.400 1/1 pass (72s test; two DBs on one server). Linux result confirmed promotion B, explicit failback A, epochs1→2→3 and same NodeId Ready standby. It disproves a deterministic isolated Linux failure, not runner interaction. The method emits child logs only on failure, so no API logs were captured from passing runs.
- The local Linux run used the T2 restore/build/test commands and same SHA but was not an exact full-environment match: `ELITESCADA_TEST_POSTGRES` pointed to database `postgres`, while CI points to `elitescada_test`. HA-D2 itself overwrites the administrative connection database to `postgres`; other tests may use the seed directly, so this discrepancy may affect suite context. Local totals2,402 passed/1 failed/1 skipped; HA-D2 separate and shared cases passed; ESPHome reconnect diagnostics failed its2s BadCommunication wait. Restore/build passed with0 warnings/errors; this is not an all-green or fully equivalent CI run. All disposable resources were removed; stable resources untouched; worktree clean at a96.
- T2 `-m:1` limits MSBuild worker processes, not xUnit's internal test-method parallelism. `dotnet test` on the solution starts a test host per project; observed order was Core→Persistence→Historian→Drivers→Security. Drivers has no assembly-wide disable-parallelization config. Only the HA collection is marked `DisableParallelization=true`; other xUnit collections may run in parallel. T1 filters/runs shared HA first, then remaining Drivers separately. This execution-order difference and two T2 logs with peer connection refusals are facts; neither proves a cause.
- Exact T2 CI attempts1/2 remain red on the separate-DB HA test; T1 passed. The isolated Windows/Linux method passes and local T2 command sequence passed the HA cases but differed in DB seed and failed one ESPHome timing test. Root cause remains unknown. No additional same-head test/CI, HA product development or B activation; B stays inactive until required verification passes.

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
