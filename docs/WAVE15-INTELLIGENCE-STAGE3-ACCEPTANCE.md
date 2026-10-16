# Intelligence Stage 3 — finite acceptance matrix

Main scope decision, updated 2026-10-16. Parent [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588). This matrix maps the existing Stage 3 promise to code/evidence and a finite remaining scope; it does not mark the stage complete or change the Stage 4/5/6 boundary.

Verified product checkpoint and integration base before this Main control commit: `ee0371ffc50113f085dc5fb36cc257931b075e1c`, PR #620 / #619. CODEX has one assigned implementation package, #621 (PERFORMANCE-V1A); its activation/availability is recorded in the issue.

| ID | Acceptance outcome | Evidence/current implementation | Remaining disposition |
| --- | --- | --- | --- |
| S3-01 | Discoverable versioned rules identify formula, units, direct/derived meaning, TAGs, windows and provenance | `AssetProcessIntelligenceKpiCatalogV1`, analytics response/provenance and `AssetProcessIntelligenceNetObservedRateV1Catalog`; #605/#617 | Existing bounded rules delivered; every new rule must satisfy the same traceability |
| S3-02 | Bounded aggregations, descriptive trends and net observed variation | `AssetProcessIntelligenceKpiV1.Calculate`: Good finite sample count/min/max/mean/first/last/delta/buckets; `net-observed-rate.v1` | Delivered for the defined observed-data semantics; arithmetic means are not time weighted |
| S3-03 | Profile signal/quality coverage and truthful unknowns | `ExecuteProfileCoverageAsync`, controlled v1 semantics/units, Historian v2/ADR-014 | Delivered within their bounds; capture settings never prove expected cadence/completeness or physical simultaneity |
| S3-04 | Compare readings under an explicit recorded operating condition | #619 / ADR-015, local 78/78, exact candidate T1 and merge CI passed | DELIVERED: current under a measured-frequency range, exact raw-unambiguous source-time pairing and canonical A/Hz; global regime equivalence remains unverified |
| S3-05 | Equivalent-regime claims have explicit sufficient evidence, not an implicit label | No global equivalent-regime proof exists; ADR-016 now fixes finite V1 input/context/time requirements | DESIGN FIXED / B ACCEPTANCE OPEN: qualified recorded tuples support only the declared formulas; missing physical/global proof remains unavailable/unverified. B must preserve/test this meaning; learned regimes are Stage 4 |
| S3-06 | Useful physical performance indicators when the required data actually exists | ADR-016 fixes per-observation specific active energy at net production rate, direct pressure-power and input-power ratio; current v1 inputs remain insufficient | ACTIVE prerequisite A #621 (separate v2 catalogs, schema27, loss-safe round-trip/selectors); formula B follows verified A. Positive physical fixtures and refusal paths remain required; no silent v1 rewrite or unsupported-only completion |
| S3-07 | Every new KPI preserves entitlement, active scope, every TAG permission, revision/permit cancellation and bounded/no-partial reads | #619 new-flow in-flight frequency revocation, cancellation, revision and aggregate-budget cases accepted | Mandatory again within PERFORMANCE-V1 B, not a separate unbounded hardening lane |
| S3-08 | Reviewed candidates integrated with exact local/T1/post-merge evidence and clear limitations | #619: local78/78; T1 #38832922847 attempt2; post-merge #38834562474 on ee0371f | Required for A and B; reuse accepted slices. T1 first failure/root-cause limit and unrun local HA reproduction remain in #619 |

## Remaining work sequence

1. #619 is INTEGRATED / VERIFIED under [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md); do not reopen its contract or CI.
2. Implement/review/integrate PERFORMANCE-V1A #621 under [ADR-016](ADR-016-INTELLIGENCE-PERFORMANCE-V1.md). The bounded audit found a concrete old-client/import capability risk requiring safe metadata before new physical consumers. Preserve v1; add only the approved v2 role/unit/catalog, schema27 negotiation, CSV/XLSX loss guards and basic editor compatibility. Milliampere belongs here, not a changed #619 acceptance path.
3. After verified A, Main activates PERFORMANCE-V1B at its exact new base with ADR-016's fixed formula families, dimensions, context/time requirements, positive examples and refusals. This is the single identified dependency/review split, not a new research cycle per statistic. The rate-based specific-energy rule does not claim whole-window consumption or silently accept cumulative counters; pressure-only power is not total pump efficiency.
4. Main audits S3-01 through S3-08 on the exact composition and records acceptance or the precise remaining row. No new KPI is added to this V1 closing scope without an identified unmet requirement or a new Product Owner direction.

Sensors may be absent in a plant: the module must explain unavailable metrics while still offering supported descriptive analyses. This is different from omitting a required V1 formula's implementation. Do not mark S3-06 complete solely because the engine can return unsupported for every input.

Stage 3 cannot close while a required row remains open. A genuine scope reduction/deferral must be recorded by its decision authority, not inferred from a green CI or from absent plant sensors. #588 and Wave 15 remain open after an individual slice passes.

## Explicitly later work

Learned baselines, drift/anomaly detection and inferred regimes are Stage 4. Guided screens, six gauge layouts/themes and new editor/runtime visuals are Stage 5; A's existing metadata selectors are only a prerequisite for usable inputs. Representative performance/platform qualification and broad hardening are Stage 6. The owner's HA local Preview/regression concern remains separate; Intelligence execution does not reopen HA development.
