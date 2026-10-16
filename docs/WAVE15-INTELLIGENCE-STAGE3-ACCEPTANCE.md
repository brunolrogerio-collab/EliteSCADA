# Intelligence Stage 3 — finite acceptance matrix

Main scope decision, updated 2026-10-16. Parent [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588). This matrix maps the existing Stage 3 promise to code/evidence and a finite remaining scope; it does not mark the stage complete or change the Stage 4/5/6 boundary.

Current integrated product checkpoint: `ab38bfe879d6ca8074aa5c38b821c7da62634768`, PR #626. V1A #621/PR #622 is verified at prerequisite `257a4ab3`; V1B #623/PR #626 is integrated and its candidate T1 #38956508442 and exact-merge T2 #38961262161 are green. Main's post-merge audit found the following finite S3-07 test gap, so Stage 3 is still open. Follow-up scope/dispatch is recorded in #623 and #305.

| ID | Acceptance outcome | Evidence/current implementation | Remaining disposition |
| --- | --- | --- | --- |
| S3-01 | Discoverable versioned rules identify formula, units, direct/derived meaning, TAGs, windows and provenance | `AssetProcessIntelligenceKpiCatalogV1`, analytics response/provenance, `AssetProcessIntelligenceNetObservedRateV1Catalog`, and `AssetProcessIntelligencePerformanceV1BCatalog`; #605/#617/#626 | Delivered on current composition; V1B provenance is bounded to recorded qualified tuples |
| S3-02 | Bounded aggregations, descriptive trends and net observed variation | `AssetProcessIntelligenceKpiV1.Calculate`, `net-observed-rate.v1`, and V1B pointwise sample summaries | Delivered for stated observed-data semantics; arithmetic means are not time weighted and a mean of ratios is not a ratio of means |
| S3-03 | Profile signal/quality coverage and truthful unknowns | `ExecuteProfileCoverageAsync`, controlled v1 semantics/units, Historian v2/ADR-014 | Delivered within their bounds; capture settings never prove expected cadence/completeness or physical simultaneity |
| S3-04 | Compare readings under an explicit recorded operating condition | #619 / ADR-015, local 78/78, exact candidate T1 and merge CI passed | DELIVERED: current under a measured-frequency range, exact raw-unambiguous source-time pairing and canonical A/Hz; global regime equivalence remains unverified |
| S3-05 | Equivalent-regime claims have explicit sufficient evidence, not an implicit label | ADR-015 and ADR-016 plus V1B's exact synchronized recorded-time tuples and explicit provenance | Accepted only for declared recorded-data formulas: physical co-acquisition, clock accuracy, cadence/completeness and globally equivalent regimes remain unavailable/unverified; no global equivalence claim is made. Learned regimes are Stage 4 |
| S3-06 | Useful physical performance indicators when the required data actually exists | V1B catalog/calculator: specific active energy at net production rate, direct pressure-power, pressure-power/input-power ratio; executable positive/refusal tests; A #621 verified at `257a4ab3` | Implemented and T1/T2 green on PR #626; preserve limitations (no whole-window integral, total-efficiency or unsupported-only completion) |
| S3-07 | Every new KPI preserves entitlement, active scope, every TAG permission, revision/permit cancellation and bounded/no-partial reads | Current V1B tests cover 2-input revocation/context cases; code revalidates inputs around pages | **OPEN — follow-up required:** deterministic coverage for both 2-input rules and 3-input rule; revoke each selected TAG while another input page is in flight; permit cancellation and workspace/revision/entitlement/scope invalidation in both flows; no partial result or unauthorized next read |
| S3-08 | Reviewed candidates integrated with exact local/T1/post-merge evidence and clear limitations | #626 candidate T1 #38956508442; exact merge `ab38bfe` T2 #38961262161; backend/Web/Chromium gate green, full-browser E2E skipped | #626 CI evidence is green, but final Stage 3 S3-08 disposition follows S3-07 correction review, exact T1 and corrected merge T2 |

## Remaining work sequence

1. #619 is INTEGRATED / VERIFIED under [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md); do not reopen its contract or CI.
2. PERFORMANCE-V1A #621 is integrated/verified after PR #625 T1/T2 evidence; preserve the v1/v2, schema27 and loss-safe round-trip contracts. Do not reopen A or the frozen #619 semantics.
3. V1B #623 is implemented and merged by PR #626 at `ab38bfe`; its three fixed formulas and ADR-016 limitations remain frozen. Do not reopen #619/A semantics, add statistics or infer whole-window consumption/total pump efficiency.
4. Complete the authorized test-only #623 follow-up from `ab38bfe` on `work/w15-intelligence-performance-v1b-security-matrix`; allowed path is the Analytics tests file only. Main reviews exact tests/T1 before integration and exact T2 afterward.
5. Main then re-audits S3-01–S3-08 on the exact corrected composition and records acceptance or a precise remaining row. Do not release Stage 4/5/6 merely because the #626 merge CI is green.

Sensors may be absent in a plant: the module must explain unavailable metrics while still offering supported descriptive analyses. This is different from omitting a required V1 formula's implementation. Do not mark S3-06 complete solely because the engine can return unsupported for every input.

Stage 3 cannot close while a required row remains open. A genuine scope reduction/deferral must be recorded by its decision authority, not inferred from a green CI or from absent plant sensors. #588 and Wave 15 remain open after an individual slice passes.

## Explicitly later work

Learned baselines, drift/anomaly detection and inferred regimes are Stage 4. Guided screens, six gauge layouts/themes and new editor/runtime visuals are Stage 5; A's existing metadata selectors are only a prerequisite for usable inputs. Representative performance/platform qualification and broad hardening are Stage 6. The owner's HA local Preview/regression concern remains separate; Intelligence execution does not reopen HA development.
