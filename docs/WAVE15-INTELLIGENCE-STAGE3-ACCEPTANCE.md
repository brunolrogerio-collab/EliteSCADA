# Intelligence Stage 3 — finite acceptance matrix

Main scope decision, 2026-10-15. Parent [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588). This matrix maps the existing Stage 3 promise to code/evidence and a finite remaining scope; it does not mark the stage complete or change the Stage 4/5/6 boundary.

Verified product checkpoint: `54c7c9942b57328cabcef740c99f4ab3207cc040`; integration before this Main control commit: `120d93a477924b34994e2a1c2167b873c93c512d` (documentation-only delta). CODEX has one active implementation package, #619.

| ID | Acceptance outcome | Evidence/current implementation | Remaining disposition |
| --- | --- | --- | --- |
| S3-01 | Discoverable versioned rules identify formula, units, direct/derived meaning, TAGs, windows and provenance | `AssetProcessIntelligenceKpiCatalogV1`, analytics response/provenance and `AssetProcessIntelligenceNetObservedRateV1Catalog`; #605/#617 | Existing bounded rules delivered; every new rule must satisfy the same traceability |
| S3-02 | Bounded aggregations, descriptive trends and net observed variation | `AssetProcessIntelligenceKpiV1.Calculate`: Good finite sample count/min/max/mean/first/last/delta/buckets; `net-observed-rate.v1` | Delivered for the defined observed-data semantics; arithmetic means are not time weighted |
| S3-03 | Profile signal/quality coverage and truthful unknowns | `ExecuteProfileCoverageAsync`, controlled v1 semantics/units, Historian v2/ADR-014 | Delivered within their bounds; capture settings never prove expected cadence/completeness or physical simultaneity |
| S3-04 | Compare readings under an explicit recorded operating condition | Existing equal-duration same-TAG comparison has `RegimeEquivalence` unverified | ACTIVE #619: current conditioned on a measured-frequency range, exact eligible source-time pairing, canonical A/Hz, reproducible differences and refusals |
| S3-05 | Equivalent-regime claims have explicit sufficient evidence, not an implicit label | No global equivalent-regime proof exists in the current APIs | OPEN: Main must specify the finite V1 operating-context/temporal-evidence requirements with the performance package; missing proof keeps comparison unavailable/unverified. Learning baselines is Stage 4 |
| S3-06 | Useful physical performance indicators when the required data actually exists | Controlled v1 catalog lacks active-power, production and differential-pressure semantics; existing discharge pressure is not differential pressure | OPEN PERFORMANCE-V1 package: explicit, dimensionally valid specific energy per production and hydraulic pressure-power/input-power indicators, including semantic/unit/time prerequisites and refusal paths. Main must approve additive/versioned prerequisites before implementation; no silent catalog-v1 rewrite |
| S3-07 | Every new KPI preserves entitlement, active scope, both TAG permissions, revision/permit cancellation and bounded/no-partial reads | Existing analytics service/gate and #617 evidence | Mandatory acceptance within #619 and PERFORMANCE-V1, not a separate unbounded hardening lane |
| S3-08 | Reviewed candidates integrated with exact local/T1/post-merge evidence and clear limitations | #617: local 73/73, T1 #38811841547 attempt 2, post-merge #38813353099 | Required for each remaining bounded package; do not rerun completed slices for reassurance |

## Remaining work sequence

1. Implement/review/integrate #619 under [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md).
2. Main specifies PERFORMANCE-V1 once, including S3-05 and S3-06's missing canonical input/time contracts. Reuse the existing v1 contracts unchanged and approve only the additive/versioned delta that real formulas require. The deliverable must define each formula, dimensions, input qualifications, contextual limits, positive test case, and explicit insufficient/unsupported cases.
3. Implement/review/integrate the approved PERFORMANCE-V1 package. Split implementation only for an identified dependency/review risk; do not create a new research cycle for every individual statistic.
4. Main audits S3-01 through S3-08 on the exact composition and records acceptance or the precise remaining row. No new KPI is added to this V1 closing scope without an identified unmet requirement or a new Product Owner direction.

Sensors may be absent in a plant: the module must explain unavailable metrics while still offering supported descriptive analyses. This is different from omitting a required V1 formula's implementation. Do not mark S3-06 complete solely because the engine can return unsupported for every input.

Stage 3 cannot close while a required row remains open. A genuine scope reduction/deferral must be recorded by its decision authority, not inferred from a green CI or from absent plant sensors. #588 and Wave 15 remain open after an individual slice passes.

## Explicitly later work

Learned baselines, drift/anomaly detection and inferred regimes are Stage 4. Guided screens, six gauge layouts/themes and editor/runtime visuals are Stage 5. Representative performance/platform qualification and broad hardening are Stage 6. They are not added to #619. The owner's HA local Preview/regression concern remains a separate acceptance item; Intelligence execution does not reopen HA development.
