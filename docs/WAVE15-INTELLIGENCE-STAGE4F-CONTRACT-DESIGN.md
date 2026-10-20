# Stage 4F — candidate contract boundary (design only)

| Field | Value |
| --- | --- |
| Status | **Design aid only — not a frozen API/DTO, method decision, or implementation authorization** |
| Date | 2026-10-20 |
| Parent | [#588 — Asset & Process Intelligence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588) |
| Governing scope | [ADR-021](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md) and [the 4F acceptance plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md) |
| Product baseline inspected | `6b324035ef54bfb57901a4d6a2a6f5f621b09156` |

This note records the narrow API boundary that can be designed without reopening 4A–4E or claiming a validated joint detector. It is intentionally not an ADR and does not reserve a route, JSON schema, C# DTO, algorithm, threshold, sample minimum, calibration procedure, or persistence strategy. Any product implementation still requires a separately authorized, eligible representative cohort and a fresh Main release under ADR-021. Until then, joint detection remains **NOT DELIVERED / NOT VALIDATED**, #588 remains **PARTIAL**, and 4G remains gated.

## Existing contracts inspected

- `AssetProcessIntelligenceContextualDeviationVectorV2QueryRequest`, `...Response` and `...Catalog` in `src/Scada.Api/Licensing/AssetProcessIntelligenceContextualDeviationVectorV2.cs` implement 4C's frozen independent per-TAG scores, exact synchronized vector construction, two caller-provided numeric context intervals, and explicit non-claims. This is not the joint-score contract and must not change.
- `AssetProcessIntelligenceReferenceEvidenceV1QueryRequest` / `...Response` / `...Catalog` in `src/Scada.Api/Licensing/AssetProcessIntelligenceReferenceEvidenceV1.cs` provide evidence counts and keep health, representativeness, cadence and completeness unclaimed unless evidenced.
- `AssetProcessIntelligenceContextEligibilityV1QueryRequest` / `...Response` / `...Catalog` in `src/Scada.Api/Licensing/AssetProcessIntelligenceContextEligibilityV1.cs` use the versioned context-profile identity and fail-closed exact-time predicate matching.
- `AssetProcessIntelligenceOperatingContextProfileEngineeringDto` and `AssetProcessIntelligenceAssignmentEngineeringDto` in `src/Scada.Engineering/Contracts/EngineeringContracts.cs` are the current Engineering-owned context/assignment configuration. Stage 4F should reference these stable identities and the active workspace revision, not duplicate policy or profile data in a second authoritative store.
- `MapPost` registrations in `src/Scada.Api/Licensing/AssetProcessIntelligenceAnalyticsApi.cs` and the corresponding `ExecuteContextualDeviationVectorV2Async`, `ExecuteReferenceEvidenceV1Async` and `ExecuteContextEligibilityV1Async` paths in `AssetProcessIntelligenceAnalyticsService.cs` are the current extension/security pattern. They check `TrendUse`, the Intelligence entitlement, canonical profile/scope/TAG-read fences, execution permit cancellation, bounded Historian reads and final workspace/configuration revalidation.

## Candidate logical boundary

The following semantic inputs/outputs are useful design constraints, **not field names to freeze**.

### Request semantics

1. Resolve exactly one authorized scope and equipment assignment; identify 2–8 numeric target signals by their stable profile signal IDs. Resolve every target to its current TAG identity/type/unit and reject duplicate, missing, ambiguous or incompatible selections before Historian reads.
2. Select one existing operator-authored 4E operating-context profile by stable ID and expected version. Return non-comparable/insufficient when it is missing, stale, ambiguous, or any required predicate cannot be evaluated. Report declared-context matching only; do not infer physical regime equivalence.
3. Carry an explicit caller-selected reference window and evaluation window. Provenance must preserve the exact windows, active workspace revision, assignment/profile/context IDs and versions. Do not claim that the reference is healthy or representative.
4. Do not accept caller-supplied policy, arbitrary SQL, credentials, imported model weights, or an alternate data source. Keep input selection bounded by the existing query service and catalog policy.

### Cohort and score semantics

- A cohort may contain a decision only when every selected target has exactly one `Good`, finite numeric observation at the same unique UTC source timestamp with explicit `Synchronized` evidence and a matching declared context. Duplicate timestamps, missing values, quality/synchronization failures and context mismatches remain exclusions/evidence; never align by nearest time, hold, interpolation or imputation.
- Treat cadence/completeness as unknown unless an existing explicit contract proves it. Do not turn observation counts into hours or exposure. Reuse the existing 4D/4E evidence semantics; do not weaken them to get a score.
- The logical result has a distinct version/rule identity and explicit outcome. A score is present only when the selected method's own fitting, conditioning and calibration requirements are satisfied. Every unavailable-score outcome returns **no score** and a stable insufficiency reason; an API error is not converted to a low/normal score.
- When a future method is approved, expose its score, decision threshold and exact method/calibration identity together. Also expose per-target residual/contribution information sufficient to explain how each selected signal relates to the aggregate result. The numeric definition, normalization and contribution calculation are method-dependent and remain undecided here.
- Include auditable per-signal and complete-cohort evidence plus source-time, context, reference-window and software/rule provenance. Keep result interpretation to deviation from the caller-selected reference under the declared context. No fault diagnosis, cause, probability, efficiency claim without required measurements, RUL, healthy-state certification or safe-state claim.

### Execution/security semantics

- Add a separate versioned route only after the data/method gate and a fresh implementation release. Do not reuse `/contextual-deviation-vector/v2/query` or change its behavior.
- Follow existing Analytics service guards: runtime `TrendUse`, Intelligence entitlement, canonical Authority and per-TAG `TagRead`, execution/permit cancellation, the established Historian provider, explicit row/window limits, and final workspace/runtime/configuration revalidation before returning a result.
- Remain on-demand, read-only and bounded. No model training service, background learner, process write, alarm mutation, local-only authorization cache, parallel policy store, or new persistence layer is implied by this design.
- Preserve fail-closed behavior on permission/permit revocation, scope/profile/configuration changes, provider errors, cursor repetition, exceeded budgets, incomplete cohorts and unavailable calibration. Never return a partial or stale score after invalidation.

## Future implementation evidence checklist

Once eligible data and a fresh package are authorized, add exact-contract tests for:

1. additive versioning: all frozen 4A–4E request/response/catalog behavior and serialization remain unchanged;
2. stable identity resolution: missing/duplicate/stale assignment, profile, context version, signal, TAG or unit fails before data access;
3. cohort construction: exact UTC uniqueness, `Good` finite numeric values, explicit `Synchronized` evidence, all-target completeness, declared-context matching and no imputation;
4. mathematical fail-closed cases: undersized cohort, singular/ill-conditioned fit, unavailable calibration and invalid parameters return insufficiency with no score/threshold;
5. deterministic score, threshold and per-signal explanations from frozen model/configuration inputs, with no access to holdout during fitting/calibration;
6. security/lifecycle: disabled Intelligence, denied `TagRead`, permit revocation/cancellation and configuration changes discard work; bounded paging/row budgets reject partial-result leakage;
7. storage/restart only if the selected method actually requires persisted artifacts, using an explicitly approved canonical ownership and migration design;
8. isolated Windows and Linux tests plus exact-head CI and exact-merge evidence. Synthetic tests prove mechanics only; 4G/field validity still needs representative held-out evidence.

## Unresolved release gates (intentionally not decided)

- A separately authorized owner-Historian cohort must pass the existing provenance, coverage, label, exposure and context criteria in the 4F plan. The prepared request workbook/local preflight establish structure and declared metadata only; they do not establish analytical eligibility.
- A preregistered evaluation must choose or reject method, sample sufficiency, regularization, calibration partition and threshold against the binding acceptance plan. The completed #675 result selected no winner; #680's one-use holdout must not be replayed; #683 remains an experimental draft and does not approve a method.
- Only after those gates may Main freeze the exact API/catalog schema and release product implementation. 4G validation, broader Stage 4 closure and later stages remain separate gates.

## Source anchors

- [ADR-021 — Stage 4 residual scope](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md)
- [Stage 4F acceptance plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md)
- [#675 measured study closeout](https://github.com/brunolrogerio-collab/EliteSCADA/issues/675#issuecomment-6298853697)
- [#659 accepted source qualification](https://github.com/brunolrogerio-collab/EliteSCADA/issues/659#issuecomment-6282568180)
- [#683 experimental PR disposition](https://github.com/brunolrogerio-collab/EliteSCADA/pull/683#issuecomment-6303024402)
