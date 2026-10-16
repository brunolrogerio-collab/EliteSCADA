# ADR-019 — Intelligence two-context deviation vector v2

**Status:** Accepted by Main for bounded Stage 4C implementation, 2026-10-16.  
**Parent:** [#588 — Asset & Process Intelligence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588)  
**Implementation package:** [#635 — Stage 4C](https://github.com/brunolrogerio-collab/EliteSCADA/issues/635)  
**Product basis:** Stage 4B verified at `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79`.

## Context

Stage 4A provides a descriptive deviation for one target under one caller-declared numeric condition. Stage 4B adds 2–8 target TAGs, but still uses one numeric context condition. The residual Stage 4 audit found that a second explicit numeric condition can refine the queried cohort without claiming regime detection or adding a multivariate anomaly model.

The reference window remains caller-selected. Historian capture metadata does not establish that it represents healthy or normal operation, expected acquisition cadence, completeness, or physically simultaneous acquisition. A count floor sufficient for the existing per-target robust score does not validate a reference as representative, nor does it justify a multivariate statistic.

## Decision

Add a separate, versioned, on-demand, read-only contract named `contextual-deviation-vector.v2` with additive routes:

- `/api/engineering/asset-process-intelligence/analytics/contextual-deviation-vector/v2/catalog`
- `/api/engineering/asset-process-intelligence/analytics/contextual-deviation-vector/v2/query`

The v2 request accepts 2–8 distinct numeric target TAGs and **exactly two** distinct numeric context TAGs, each with a caller-declared finite canonical interval `[lowerInclusive, upperExclusive)`. The fixed two-context bound is intentional for this slice. No unbounded context list, categorical mode, new shared semantic role, or user-defined formula is introduced. The new catalog uses contract/catalog version 2 and rule ID `contextual-deviation-vector.v2`.

Every selected TAG must belong to the same active scope, Equipment assignment and profile; all signal and TAG identities must be unique. Windows remain UTC, each no longer than 31 days, with a strictly later, non-overlapping evaluation window.

## Qualification and calculation

A complete reference or evaluation vector requires exactly one Good, finite numeric observation for every target and both context TAGs, with the same non-null UTC source timestamp and explicit `Synchronized` evidence. Reject duplicate/ambiguous timestamps. The source timestamp must fall in the requested window. No interpolation, nearest join, imputation, carry-forward, time weighting, or assertion that synchronized source timestamps prove physical simultaneity.

The reference cohort is the single set of complete reference vectors for which **both** context values fall in their declared canonical intervals. For each target independently, calculate median and MAD from that same joint cohort; preserve the existing signed raw difference and robust-score rule. A robust score is available only with at least 30 complete joint-reference vectors and positive MAD.

Evaluation vectors expose both context values, each interval-match predicate, and a joint-in-context predicate. Compute per-target raw differences/scores only for vectors inside both intervals. Outside that joint context, return an explicit unavailable reason and no deviation value. This is still a vector of independent per-target descriptive results, never a combined or multivariate score.

Return per-signal/per-context raw, accepted, rejected and ambiguous evidence; complete and in-context vector counts; exact windows, intervals, canonical units and provenance; stable ordering; and explicit insufficient-data reasons. Keep `regimeEquivalence=not_assessed` and Historian `completeness=unknown`.

## Safety, resource and compatibility boundaries

Reuse the existing active-scope/assignment/profile checks, Intelligence entitlement, `TrendUse`, per-TAG `TagRead`, execution permit, cancellation, workspace/revision and TAG-semantics validation, Authority policy/epoch and HA fencing. Authorize every target and context TAG before the first Historian read; revalidate every selected TAG before and after every page and before returning. Any revocation, cancellation, stale context, provider/cursor error or resource breach returns no partial payload and must not cause later unauthorized reads.

Use Historian v2 and preserve 200-row pages, one 50,000-row aggregate budget across all TAGs and both windows, and at most 500 returned evaluation vectors. Do not relax bounds for sparse data. Missing or insufficient data is reported explicitly, never synthesized.

Stage 4A and Stage 4B v1 routes, request/response shapes, catalogs and meanings remain unchanged. This slice adds no persistence or baseline learning, cache, worker, UI, schema migration, shared Engineering semantics, external model/dependency or HA behavior.

## Interpretation limits

This contract does **not** establish representative or healthy reference data, global operating-regime equivalence, expected cadence/completeness, calibration, clock accuracy, physical simultaneity, categorical operating modes, combined anomaly classification, thresholds, diagnosis, fault attribution, or remaining useful life. Those require separate evidence and decisions. Stage 4 remains partial after this bounded slice unless and until its other acceptance gaps are independently resolved.

## Required verification

- Calculator: both inclusive/exclusive context intervals and joint-context truth table; complete exact timestamps across every selected TAG; quality, numeric, synchronization, window, duplicate and missing evidence; stable order; common joint cohort; results only in joint context; empty/sparse cohort, 29 vs 30 references, MAD zero, and 500-vector cap.
- Service/API: invalid and duplicate identities, wrong profile/assignment/scope, unsupported units, denied authorization before reads; target/context `TagRead` and permit revocation between pages; cancellation and workspace/TAG/Authority/HA changes; malformed/repeated cursor, provider errors, aggregate row cap, no partial response and no extra unauthorized read; frozen v1 contract regression.
- Focused local analytics/API tests and Release API build; one exact-head `SESSION_LICENSING` T1. Main reviews and integrates; exact post-merge T2 is required before #635 closes.
