# Stage 4F — experimental contract (PR #687 integrated; simulation-validated only)

| Field | Value |
| --- | --- |
| Status | **Additive v1 experimental implementation PR #687 is integrated at merge `48cfe002ac437f5248d0127aaecce28c82dc9ba9`; exact T1 #39532394830 and exact-merge T2 #39532910787 passed. Simulation-validated only; not field-validated or frozen for operational release.** |
| Date | 2026-10-21 |
| Parent | [#588 — Asset & Process Intelligence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588) |
| Governing scope | [ADR-021](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md) and [the 4F acceptance plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md) |
| Product baseline inspected | Exact PR base `63182e021730ad4fb35532481e28d7a16fc04be3` |

This note records the integrated implementation boundary without reopening 4A–4E or claiming a field-validated detector. It is not an ADR or general release approval: the route/schema/method are an additive v1 experimental contract whose integration and mechanics are verified, while operational/field acceptance remains separate. The Product Owner authorized simulation-validated experimental implementation on 2026-10-20 and closed Stage 4 engineering within this scope on 2026-10-21. Output remains explicitly experimental; synthetic checks do not meet operational acceptance targets or establish field validity. Data-dependent field acceptance is deferred to future-wave #701; parent #588 remains open for later product scope.

## Existing contracts inspected

- `AssetProcessIntelligenceContextualDeviationVectorV2QueryRequest`, `...Response` and `...Catalog` in `src/Scada.Api/Licensing/AssetProcessIntelligenceContextualDeviationVectorV2.cs` implement 4C's frozen independent per-TAG scores, exact synchronized vector construction, two caller-provided numeric context intervals, and explicit non-claims. This is not the joint-score contract and must not change.
- `AssetProcessIntelligenceReferenceEvidenceV1QueryRequest` / `...Response` / `...Catalog` in `src/Scada.Api/Licensing/AssetProcessIntelligenceReferenceEvidenceV1.cs` provide evidence counts and keep health, representativeness, cadence and completeness unclaimed unless evidenced.
- `AssetProcessIntelligenceContextEligibilityV1QueryRequest` / `...Response` / `...Catalog` in `src/Scada.Api/Licensing/AssetProcessIntelligenceContextEligibilityV1.cs` use the versioned context-profile identity and fail-closed exact-time predicate matching.
- `AssetProcessIntelligenceOperatingContextProfileEngineeringDto` and `AssetProcessIntelligenceAssignmentEngineeringDto` in `src/Scada.Engineering/Contracts/EngineeringContracts.cs` are the current Engineering-owned context/assignment configuration. Stage 4F should reference these stable identities and the active workspace revision, not duplicate policy or profile data in a second authoritative store.
- `MapPost` registrations in `src/Scada.Api/Licensing/AssetProcessIntelligenceAnalyticsApi.cs` and the corresponding `ExecuteContextualDeviationVectorV2Async`, `ExecuteReferenceEvidenceV1Async` and `ExecuteContextEligibilityV1Async` paths in `AssetProcessIntelligenceAnalyticsService.cs` are the current extension/security pattern. They check `TrendUse`, the Intelligence entitlement, canonical profile/scope/TAG-read fences, execution permit cancellation, bounded Historian reads and final workspace/configuration revalidation.

## Implemented logical boundary

The integrated implementation uses the following semantic boundary; none of it claims field validity.

### Request semantics

1. Resolve exactly one authorized scope and equipment assignment; identify 2–8 numeric target signals by their stable profile signal IDs. Resolve every target to its current TAG identity/type/unit and reject duplicate, missing, ambiguous or incompatible selections before Historian reads.
2. Select one existing operator-authored 4E operating-context profile by stable ID and expected version. Return non-comparable/insufficient when it is missing, stale, ambiguous, or any required predicate cannot be evaluated. Report declared-context matching only; do not infer physical regime equivalence.
3. Carry an explicit caller-selected reference window and evaluation window. Provenance must preserve the exact windows, active workspace revision, assignment/profile/context IDs and versions. Do not claim that the reference is healthy or representative.
4. Do not accept caller-supplied policy, arbitrary SQL, credentials, imported model weights, or an alternate data source. Keep input selection bounded by the existing query service and catalog policy.

### Cohort and score semantics

- A cohort may contain a decision only when every selected target has exactly one `Good`, finite numeric observation at the same unique UTC source timestamp with explicit `Synchronized` evidence and a matching declared context. Duplicate timestamps, missing values, quality/synchronization failures and context mismatches remain exclusions/evidence; never align by nearest time, hold, interpolation or imputation.
- Treat cadence/completeness as unknown unless an existing explicit contract proves it. Do not turn observation counts into hours or exposure. Reuse the existing 4D/4E evidence semantics; do not weaken them to get a score.
- The logical result has a distinct version/rule identity and explicit outcome. A score is present only when the experimental method's fit and conditioning requirements are satisfied. If no threshold has been validated for the declared operating context, return the score as **experimental without classification**; do not fabricate a low/normal/alert state. Every unavailable-score outcome returns **no score** and a stable insufficiency reason; an API error is not converted to a score.
- The current score response has no operational or synthetic decision threshold (`decisionThreshold: null`), sets `validationStatus=experimental_not_field_validated` and `classificationStatus=none`, and returns per-target level/marginal-variance contributions plus the cross-signal interaction. It makes no alarm, fault, probability, diagnosis, efficiency or RUL claim.
- The response enumerates selected target and context TAGs and includes reference/evaluation evidence per signal: raw rows, Good-quality rows, type-compatible values, UTC/in-window/synchronized source timestamps, duplicate timestamp groups/rows, accepted synchronized unique rows, and context-predicate matches. Duplicate groups/rows are limited to UTC source timestamps inside that requested window; out-of-window rows cannot contaminate a window's diagnostics. These diagnostic counters overlap and are not additive; only the accepted unique count is the final per-signal source-evidence gate. Complete-cohort counts are reported separately.
- The current fit requires at least `max(100, 20 × target count)` reference innovations after contiguous-run breaks. A 40-cohort evaluation window needs warmup; reference/evaluation windows are exact, disjoint, UTC and at most 31 days. A shared 50,000-raw-row budget and 500 returned score cap bound the read-only query.
- Include auditable complete-cohort counts and source-time, context, reference-window and software/rule provenance. Keep result interpretation to deviation from the caller-selected reference under the declared context. No fault diagnosis, cause, probability, efficiency claim without required measurements, RUL, healthy-state certification or safe-state claim.

### Execution/security semantics

- Add a separate versioned route; do not reuse `/contextual-deviation-vector/v2/query` or change its behavior. The route must remain read-only and can return a score without an operational decision threshold while field evidence is pending.
- Follow existing Analytics service guards: runtime `TrendUse`, Intelligence entitlement, canonical Authority and per-TAG `TagRead`, execution/permit cancellation, the established Historian provider, explicit row/window limits, and final workspace/runtime/configuration revalidation before returning a result.
- Remain on-demand, read-only and bounded. No model training service, background learner, process write, alarm mutation, local-only authorization cache, parallel policy store, or new persistence layer is implied by this design.
- Preserve fail-closed behavior on permission/permit revocation, scope/profile/configuration changes, provider errors, cursor repetition, exceeded budgets, incomplete cohorts and unavailable calibration. Never return a partial or stale score after invalidation.

## Integrated implementation and evidence

The implementation is in [PR #687](https://github.com/brunolrogerio-collab/EliteSCADA/pull/687), merged at `48cfe002ac437f5248d0127aaecce28c82dc9ba9` from exact base `63182e021730ad4fb35532481e28d7a16fc04be3`; PR head `ee0e087ac8a1d6891a554b08f8de9fbf5ddf1a24`. Exact-head T1 #39532394830 passed (1,684 passed / 1 skipped / 0 failed, plus classifier/Common sanity/T1 gate); exact-merge T2 #39532910787 passed backend build/test/runtime smoke, Web build and Chromium integration smoke. Full-browser E2E was skipped, not passed. Code is in `src/Scada.Core/Intelligence/Experimental/ExperimentalJointDeviation.cs`, `src/Scada.Api/Licensing/AssetProcessIntelligenceExperimentalJointDeviationV1.cs` and `AssetProcessIntelligenceAnalyticsService.cs`, with separate API routes `/experimental-joint-deviation/v1/catalog` and `/experimental-joint-deviation/v1/query`. It reuses the existing Historian v2 reader, 4E typed predicates, `TrendUse`, entitlement, per-TAG `TagRead`, execution permit, query budget and final workspace/config revalidation. Local Windows/Linux evidence is recorded in `CURRENT-COORDINATOR-HANDOFF.md`. The user's local Preview was not accessed or changed.

Bounded implementation-verification surface exercised locally and on exact PR/merge CI:

1. additive versioning: all frozen 4A–4E request/response/catalog behavior and serialization remain unchanged;
2. stable identity resolution: missing/duplicate/stale assignment, profile, context version, signal, TAG or unit fails before data access;
3. cohort construction: exact UTC uniqueness, `Good` finite numeric values, explicit `Synchronized` evidence, all-target completeness, declared-context matching and no imputation;
4. mathematical fail-closed cases: undersized cohort, singular/ill-conditioned fit and invalid parameters return insufficiency with no score; absent field calibration suppresses classification rather than blocking the experimental score;
5. deterministic score and per-signal explanations from frozen model/configuration inputs; separate fitting/calibration/holdout roles whenever a threshold is exercised in simulation, with no access to holdout during fitting/calibration;
6. security/lifecycle: disabled Intelligence, denied `TagRead`, permit revocation/cancellation and configuration changes discard work; bounded paging/row budgets reject partial-result leakage;
7. storage/restart only if the selected method actually requires persisted artifacts, using an explicitly approved canonical ownership and migration design;
8. isolated Windows and Linux tests plus exact-head CI and exact-merge evidence. Synthetic tests prove mechanics only; field validity still needs representative held-out evidence and is deferred to #701.

## Field-validation gates (not blockers to the experimental implementation)

- A separately authorized owner/product-test cohort must pass the provenance, coverage, label, exposure and context criteria in the 4F plan before field-performance claims. The prepared request workbook/local preflight establish structure and declared metadata only; they do not establish analytical eligibility.
- Future representative evaluation must assess or improve sample sufficiency, regularization and threshold against the binding operational targets. The completed #675 and #680 experiments retain their results; #680's one-use holdout must not be replayed. PR #683 remains experimental evidence only and does not approve field performance.
- Simulation closes software/math/integration gaps only. Stage 4 engineering is closed within the experimental scope; field validation remains a separate future-wave gate in #701, and later product stages remain separate.

## Source anchors

- [ADR-021 — Stage 4 residual scope](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md)
- [Stage 4F acceptance plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md)
- [#675 measured study closeout](https://github.com/brunolrogerio-collab/EliteSCADA/issues/675#issuecomment-6298853697)
- [#659 accepted source qualification](https://github.com/brunolrogerio-collab/EliteSCADA/issues/659#issuecomment-6282568180)
- [#683 experimental PR disposition](https://github.com/brunolrogerio-collab/EliteSCADA/pull/683#issuecomment-6303024402)
