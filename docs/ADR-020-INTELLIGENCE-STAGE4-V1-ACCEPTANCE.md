# ADR-020 — Intelligence Stage 4 V1 acceptance boundary

| Field | Value |
| --- | --- |
| Status | Accepted by Product Owner and Main, 2026-10-16 |
| Parent | [#588 — Asset & Process Intelligence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588) |
| Audit | Read-only residual audit recorded in [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6226876195) |
| Product code checkpoint | Stage 4C PR #637 merge at 81ac4193194e1471e812e8164f9b9e15e63512ea |
| Coordination checkpoint | PR #638 merge at 51b4cb5f372aaaff6507d4980d85bff6e73b3c1f |

## Context

The Stage 4 residual audit found that the current analytics do not automatically qualify a reference period as healthy or representative, infer global regime equivalence, or calculate a joint multivariate anomaly score. The existing Stage 4A–4C contracts instead accept a caller-selected reference window and explicit numeric context, and return descriptive per-TAG results over observations that satisfy quality, synchronization, and cohort requirements.

The Product Owner replied “siga” to Main's explicit recommendation to keep those limits. This ADR resolves the Stage 4 V1 acceptance interpretation. It does not change the frozen Stage 4A, 4B, or ADR-019/4C API contracts, nor claim that the system verifies the operator's process judgment.

## Decisions

### Reference period

- The caller explicitly selects the reference window and is responsible for choosing a period believed to represent known-good operation for the intended comparison.
- The product reports the selected window and the evidence actually available from Historian observations.
- The product does not autonomously certify health, normality, representativeness, expected acquisition cadence, or temporal completeness. Unknown evidence remains unknown; a sample-count threshold is not proof of a healthy baseline.

### Context and regime

- Context is defined only by explicit numeric conditions selected by the caller and encoded in the versioned query contract.
- Results are conditional on those selected conditions. They do not establish global equivalence between operating regimes or account for unselected process factors.
- A regime-equivalence state of not_assessed is intentional and must not be replaced by an inferred equivalence claim without a new accepted contract and evidence.

### Multi-signal vector

- Stage 4 V1's multi-signal result is a vector of independent descriptive/robust statistics per target TAG, computed over the exact complete synchronized co-observation cohort.
- “Vector” does not mean a combined multivariate score, covariance model, anomaly classifier, diagnosis, or prognosis.
- Existing per-target sample/MAD requirements remain the applicable limits; they are not sample sufficiency criteria for a future joint model.

## Stage 4 V1 disposition

Main accepts Stage 4 V1 as **COMPLETE / VERIFIED / FROZEN within this boundary**, based on the already-integrated and verified 4A, 4B, and 4C slices:

- Stage 4A: PR #631, exact T1 #38970785957 and post-merge T2 #38971337239.
- Stage 4B: PR #633, exact T1 #38974826487 and post-merge T2 #38975551802.
- Stage 4C: PR #637, candidate T1 #38978898568 attempt 3 and post-merge T2 #38981435925 attempt 2 at 81ac4193194e1471e812e8164f9b9e15e63512ea.

The post-merge full-browser E2E was skipped and is not claimed. The previous transient timing failure remains without an established root cause. This acceptance does not close parent issue #588, complete the Intelligence product, authorize Stage 5B or Stage 6, or close Wave 15.

## Future changes

Any later request for automatic baseline qualification, regime detection/equivalence, or a joint multivariate model requires a new bounded package, explicit acceptance criteria, and the representative evidence/dataset for that claim. Do not modify frozen Stage 4 contracts to accommodate such work.

Stage 5 starts with the bounded generic measurement-widget package #639 (Stage 5A). Guided dashboard generation from asset/process profiles is separate Stage 5B; Stage 6 hardening follows later.
