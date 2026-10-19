# Intelligence Stage 4D — reference evidence and data sufficiency

Main order `W15-INTELLIGENCE-4D-REFERENCE-EVIDENCE-01`, 2026-10-19. Parent #588; implementation #649; executor existing chat CODEX. Architecture/scope derives from ADR-021; frozen 4A–4C/ADR-020 remain unchanged. Exact dispatch base is activated in #649 after publication of this order; revalidated prior coordination base is `15697b30e2633510198fca0285df633156f7f338`, product `0054d587fe855bbea4b1af81726af016208643da`.

## Start and isolation

Stage5A technical gates passed and stable Docker Preview was delivered at http://localhost:18080 on product `0054d587`. Owner explicitly ordered continued development during manual evaluation; #639 visual acceptance remains open and does not block 4D. Preserve that delivered `elitescada-stable` revision/data/volumes/ports. Implement/test in a separate clean checkout and DB/Compose resources, preserving other Docker projects and unrelated changes. The initial test-stack reset was directly owner-authorized; do not infer permission for later automatic resets/redeploys.

Planned implementation branch: `work/w15-intelligence-stage4d-reference-evidence`. No duplicate product executor, worker self-merge or self-selected next stage.

## Real seams inspected before release

- `src/Scada.Api/Licensing/AssetProcessIntelligenceAnalyticsApi.cs`: canonical analytics route group/catalog/query pattern.
- `AssetProcessIntelligenceAnalyticsService.ExecuteProfileCoverageAsync` / `PrepareProfileCoverageAsync`: profile/scope/assignment resolution, Required flags, configured signals, entitlement/permit and bounds. Existing exact Historian-event timestamp overlap is not synchronized source acquisition.
- `ExecuteContextualDeviationVectorV2Async`, `ReadContextualDeviationVectorV2ObservationsAsync`, `RevalidateContextualReferencePlansAsync`: Historian v2 source-timestamp evidence, aggregate bounds, cancellation and per-page/all-plan revalidation.
- `AssetProcessIntelligenceAnalyticsContracts.cs`, `AssetProcessIntelligenceContextualDeviationVectorV2.cs`: windows, identities, quality/rejection evidence and provenance; old public shapes/calculations stay frozen.
- `ApiAuthorizationCheck.Principal.SubjectId`: canonical authenticated subject, not a client actor field. Consume `AssetProcessIntelligenceExecutionGate` without changing HA/Authority/permit implementations.
- Existing tests: `tests/Scada.Drivers.Tests/AssetProcessIntelligenceAnalyticsTests.cs`, `AssetProcessIntelligenceEndpointContractTests.cs`, `AssetProcessIntelligenceExecutionGateTests.cs`.

## Bounded additive decision

1. Add a separately versioned **on-demand, read-only evidence report**, proposed `reference-evidence.v1` catalog/query under the existing analytics group. No new score or change to frozen routes/catalog/request/response/meanings.
2. Resolve one unambiguous active scope/Equipment assignment/profile; explicit reference window and optional later non-overlapping comparison. Report configured signals with canonical Required/Optional semantics. Optional unavailable observations must not suppress unrelated required-signal evidence. Missing required historical observations yield insufficiency; invalid identities/configuration or denied access fail closed, not a fabricated data result.
3. Return exact project/revision/scope/assignment/profile/signal/TAG IDs, dataset/query/contract versions and windows. Record reference submission from authenticated server-derived subject and server UTC time. This records request submission, not an unobserved past UI click; no spoofed actor or assertion of healthy/representative reference.
4. Per signal/window expose raw, Good-finite, synchronized-accepted, rejected and ambiguous counts; quality/rejection distributions; missing/unsupported states. Complete source-cohort counts require exact non-null UTC source timestamps with the existing named Synchronized evidence. Reject duplicate/ambiguous/unsynchronized observations. Define required versus selected cohorts truthfully; optional absence does not poison the required cohort. No-required-numeric-cohort is explicit, not an invented required set.
5. Expected cadence/count/completeness and health/regime equivalence remain unknown/not_assessed without canonical supporting evidence. Capture interval does not imply acquisition cadence/completeness. Show counts/stable insufficiency reasons; do not invent a universal count floor or call these counts validation of a future joint model. The frozen per-target n>=30 rule is not a joint-model threshold.
6. The serializable response is the evidence record. No persisted baseline/store, SQL, cache, learner, worker or schema migration. Existing configurations retain canonical persistence; a future saved-reference object requires an explicit later package. No hidden authoritative workspace store.
7. Bounds: <=16 configured profile signals; <=31 days per window; 200-row pages; a single 50,000-raw-row budget across all selected signals/windows. Stable order, no silent truncation/repeated/malformed cursors. No partial response after provider/cancellation/budget/authorization/state failure.
8. Reuse entitlement, active scope/explicit permit, TrendUse, all queried TAG TagRead, clean Working/Active coherence and canonical fences. Authorize all queried signals before the first read; revalidate all plans around each page and before return. Revocation must prevent later unauthorized reads and any partial result. Never modify HA/Authority code to make this package pass.

Before code, CODEX confirms these seams; a real contract conflict returns to Main, not a parallel authority. Scope excludes 4E persisted operating profiles/predicates, 4F joint model, 4G whole-stage closure, 5B dashboards, Stage6, HA, drivers, widgets, license changes and Preview deployment changes. No imputation, interpolation, nearest join, carry-forward, process writes, diagnosis/RUL or platform-specific/new external dependency.

## Required acceptance

- Calculator/contract tests: provenance and actor spoofing, windows/IDs/versions, reconciled counts/reasons, empty/sparse/poor quality, duplicate/missing/unsynchronized source timestamps, required/optional absence, no required numeric cohort, unknown cadence, stable order and all bounds.
- Service/API tests: entitlement/TrendUse/TAG denial with zero reads; revocation during/between pages, permit cancellation, dirty/revision/project/TAG changes, provider/cursor/budget failures with no partial result or later unauthorized read; no background work/process writes for disabled scopes; frozen 4A–4C regression.
- Focused local tests and Release API build in isolated resources. Exercise existing configuration persistence/restart compatibility where affected; never use/reset the owner Preview database. Windows/Linux-compatible code; no security/test/timeout weakening or unrelated Modbus/HA correction.
- One draft PR with `VALIDATION_PROFILE: SESSION_LICENSING`, exact base/head/tree, actual changed symbols, local commands/counts, exact T1 and explicit caveats. CODEX owns local/T1; Main reviews, conditionally merges and verifies exact-merge T2. Do not close 4D until every accepted 4D criterion and required gate passes.
- Then Main releases 4E separately, followed by 4F method/data gate and 4G. Stage4 remains PARTIAL until those criteria are resolved; no Stage5B/6 or full Intelligence/Wave closure follows from this report.
