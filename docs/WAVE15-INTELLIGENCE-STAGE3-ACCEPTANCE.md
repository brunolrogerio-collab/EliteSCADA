# Intelligence Stage 3 — finite acceptance matrix

Main scope decision, updated 2026-10-16. Parent [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588). This matrix maps the existing Stage 3 promise to code/evidence and a finite remaining scope; it does not mark the stage complete or change the Stage 4/5/6 boundary.

Verified product checkpoint / current integration base before this Main control commit: `257a4ab3c40a023beea91a027e5c3d692c22f72c`, PR #625, exact post-merge T2 #38943570688 green. PERFORMANCE-V1A #621 / PR #622 is INTEGRATED / VERIFIED; PERFORMANCE-V1B #623 is the one active implementation package. Its exact control commit and executor dispatch/availability are recorded in #623.

| ID | Acceptance outcome | Evidence/current implementation | Remaining disposition |
| --- | --- | --- | --- |
| S3-01 | Discoverable versioned rules identify formula, units, direct/derived meaning, TAGs, windows and provenance | `AssetProcessIntelligenceKpiCatalogV1`, analytics response/provenance and `AssetProcessIntelligenceNetObservedRateV1Catalog`; #605/#617 | Existing bounded rules delivered; every new rule must satisfy the same traceability |
| S3-02 | Bounded aggregations, descriptive trends and net observed variation | `AssetProcessIntelligenceKpiV1.Calculate`: Good finite sample count/min/max/mean/first/last/delta/buckets; `net-observed-rate.v1` | Delivered for the defined observed-data semantics; arithmetic means are not time weighted |
| S3-03 | Profile signal/quality coverage and truthful unknowns | `ExecuteProfileCoverageAsync`, controlled v1 semantics/units, Historian v2/ADR-014 | Delivered within their bounds; capture settings never prove expected cadence/completeness or physical simultaneity |
| S3-04 | Compare readings under an explicit recorded operating condition | #619 / ADR-015, local 78/78, exact candidate T1 and merge CI passed | DELIVERED: current under a measured-frequency range, exact raw-unambiguous source-time pairing and canonical A/Hz; global regime equivalence remains unverified |
| S3-05 | Equivalent-regime claims have explicit sufficient evidence, not an implicit label | No global equivalent-regime proof exists; ADR-016 now fixes finite V1 input/context/time requirements | DESIGN FIXED / B ACCEPTANCE OPEN: qualified recorded tuples support only the declared formulas; missing physical/global proof remains unavailable/unverified. B must preserve/test this meaning; learned regimes are Stage 4 |
| S3-06 | Useful physical performance indicators when the required data actually exists | ADR-016 fixes per-observation specific active energy at net production rate, direct pressure-power and input-power ratio; A #621 canonical v2 metadata/compatibility is now verified at base `257a4ab3` | ACTIVE: B #623 implements three rules with positive physical fixtures, truthful provenance and refusal paths; no silent v1 rewrite, total-efficiency claim or unsupported-only completion |
| S3-07 | Every new KPI preserves entitlement, active scope, every TAG permission, revision/permit cancellation and bounded/no-partial reads | #619 new-flow in-flight frequency revocation, cancellation, revision and aggregate-budget cases accepted | Mandatory again within PERFORMANCE-V1 B, not a separate unbounded hardening lane |
| S3-08 | Reviewed candidates integrated with exact local/T1/post-merge evidence and clear limitations | #619: local78/78; T1 #38832922847 attempt2; post-merge #38834562474 on ee0371f | Required for A and B; reuse accepted slices. T1 first failure/root-cause limit and unrun local HA reproduction remain in #619 |

## Remaining work sequence

1. #619 is INTEGRATED / VERIFIED under [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md); do not reopen its contract or CI.
2. PERFORMANCE-V1A #621 is integrated/verified after PR #625 T1/T2 evidence; preserve the v1/v2, schema27 and loss-safe round-trip contracts. Do not reopen A or the frozen #619 semantics.
3. Implement/review/integrate PERFORMANCE-V1B #623 from its exact Main control start on verified product base `257a4ab3`. It contains only ADR-016's three fixed formula families, dimensions, context/time requirements, positive examples and refusal rules. The rate-based specific-energy rule does not claim whole-window consumption or silently accept cumulative counters; pressure-only power is not total pump efficiency.
4. Main audits S3-01 through S3-08 on the exact composition and records acceptance or the precise remaining row. No new KPI is added to this V1 closing scope without an identified unmet requirement or a new Product Owner direction.

Sensors may be absent in a plant: the module must explain unavailable metrics while still offering supported descriptive analyses. This is different from omitting a required V1 formula's implementation. Do not mark S3-06 complete solely because the engine can return unsupported for every input.

Stage 3 cannot close while a required row remains open. A genuine scope reduction/deferral must be recorded by its decision authority, not inferred from a green CI or from absent plant sensors. #588 and Wave 15 remain open after an individual slice passes.

## Explicitly later work

Learned baselines, drift/anomaly detection and inferred regimes are Stage 4. Guided screens, six gauge layouts/themes and new editor/runtime visuals are Stage 5; A's existing metadata selectors are only a prerequisite for usable inputs. Representative performance/platform qualification and broad hardening are Stage 6. The owner's HA local Preview/regression concern remains separate; Intelligence execution does not reopen HA development.
