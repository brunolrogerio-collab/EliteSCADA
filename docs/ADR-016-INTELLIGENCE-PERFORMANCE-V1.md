# ADR-016 — Intelligence performance inputs and recorded physical indicators

Status: MAIN APPROVED DESIGN; PERFORMANCE-V1A #621 is INTEGRATED / VERIFIED after exact-merge T2 #38943570688 on `257a4ab3c40a023beea91a027e5c3d692c22f72c`. PERFORMANCE-V1B #623 / PR #626 is integrated at `ab38bfe879d6ca8074aa5c38b821c7da62634768`; candidate T1 #38956508442 and exact-merge T2 #38961262161 passed. Main's post-merge audit found a required S3-07 test-coverage gap. A test-only follow-up is active from exact base `ab38bfe`; full #623/Stage 3 acceptance is not VERIFIED/FROZEN pending Main review, exact T1 and corrected post-merge T2. Parent: #588.

Original design evidence base: `ee0371ffc50113f085dc5fb36cc257931b075e1c`, PR #620 / #619, verified by [post-merge 38834562474](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38834562474). A's verified base was `257a4ab3c40a023beea91a027e5c3d692c22f72c`; current product composition includes B at `ab38bfe`. The formula/temporal contracts below remain the accepted design; only the unfulfilled security-test evidence is being corrected.

## Actual dependencies and the finite split

`ControlledEngineeringSemanticsCatalog.cs` has closed v1 unit/role catalogs. `EngineeringContracts.cs` already carries versioned signal bindings and TAG unit references. `AssetProcessIntelligenceEngineeringRegistry.ValidateSemanticTagBinding` checks canonical unit/dimension consistency. There is no measured active-power, measured production, or direct pressure-rise role in v1.

`AssetProcessIntelligenceApi` negotiates `SemanticContractVersion`, but its omission guard currently protects only an absent marker. `EngineeringExchangeService` and `TagEngineeringHandler` protect older-than-26 inputs. `EngineeringTabularExchange.ParseRows/ReplaceTabularEntities` and the legacy TAG CSV path can label imported data with the current package schema without proving writer capability. Existing web profile/TAG editors write v1 selections; `/scopes` omits controlled-unit references from TAG descriptors.

These are concrete old-client/round-trip risks, so PERFORMANCE-V1 has exactly two dependent deliveries: A makes canonical metadata safe and usable; B implements the formulas below. This is not an open-ended discovery cycle or a separate lane per statistic. Stage 3 remains partial until both deliveries and the finite matrix pass.

## A — additive canonical semantics and compatibility

Keep existing v1 constants, definitions, strict validators and resolver meanings unchanged. Add separate v2 catalogs and explicit dispatch only where the new canonical metadata is supported. #619 retains its explicit v1 A/Hz checks and refusal of mA/version2. No existing binding DTO or `ControlledEngineeringUnitReference` shape change is needed.

| V2 role | Dimension | Required existing qualifier fields |
| --- | --- | --- |
| `equipment.active_input_power` | power | SignalKind=`measured_total_input`; measured total electrical active input at the assigned equipment boundary, not shaft/apparent/estimated power |
| `process.net_good_production_volume_rate` | volumetric_flow | SignalKind=`measured_output`; net accepted product output, not recirculation or a counter |
| `process.net_good_production_mass_rate` | mass_flow | SignalKind=`measured_output`; net accepted product output |
| `process.net_good_production_item_rate` | item_rate | SignalKind=`measured_output`; net accepted product output |
| `pump.direct_differential_pressure` | pressure | SignalKind=`measured_output`, PressureReference=`differential_outlet_minus_inlet`; directly measured pump pressure rise |

All unrelated qualifiers must be null. Existing v1 `pump.discharge_volumetric_flow` with FlowDirection=`pump_to_process` remains the hydraulic flow input; it is not net production. Discharge pressure is not differential pressure.

V2 adds only the ten unit codes/factors specified in #621: mA; W/kW; m3/s; kg/s and kg/h; item/s and item/h; Pa/kPa. Existing v1 bar and m3/h references remain usable through explicit canonical dispatch without changing their version. There is no unit-text inference or counter/energy-integral role.

Advance canonical package capability/schema to 27 for v2 annotations. Existing schemas1–26 preserve their accepted content/meaning; v2 references require27 and unsupported versions fail before mutation. Configuration contract2 supports the approved v1/v2 metadata. Missing/lower-capability writers must not silently remove/change/downgrade v2 annotations; intentional contract2/schema27 changes remain canonically validated. Apply the same invariant to direct TAG edits and package preview/apply.

CSV and XLSX share the existing tabular codec. Add optional TAG column `ControlledEngineeringUnitContractVersion`; new exports explicitly write2. Absent/1 means legacy capability; reject unsupported/conflicting row markers. A legacy controlled-unit header alone proves at most schema26 (without that header: existing25), never schema27. Explicit2 maps to27 and is required for v2 references. Preserve this evidence through both tabular and legacy CSV import paths; do not overwrite it with the current workspace/package version. Failed validation leaves all metadata unchanged.

Expose a static EngineeringView-protected `/api/engineering/asset-process-intelligence/semantic-contracts/v2/catalog`, and include controlled-unit references in `/scopes` TAG descriptors. Existing profile/TAG selectors use explicit supported code/version and negotiate their writes. Unknown future values must remain preserved/visible or block saving, never silently become an empty selection. This is basic metadata usability, not Stage 5 dashboards or new widgets.

The full bounded scope, allowed paths, exact unit definitions, local/legacy/CSV/XLSX/UI acceptance and T1 return contract are #621. It explicitly authorizes only the necessary additive Engineering/configuration/import/editor changes, not a rewrite of frozen v1 consumers, security or Authority policy.

## B — fixed formula families (implemented by PR #626; semantics frozen)

The implemented versioned catalog/query reuses the existing analytics route group, under `/performance/v1/catalog` and `/performance/v1/query`. It accepts one active scope, one stable assignment/profile, explicit distinct signal IDs, one bounded UTC window and one of these rules; irrelevant inputs are refused, not read.

| Rule ID | Qualified inputs and pointwise formula | Meaning |
| --- | --- | --- |
| `specific-active-energy-at-recorded-production-rate.v1` | P>=0 in W; measured net production R>0 in product-units/s; P/R in J/product-unit, divided by 3,600,000 for kWh/product-unit | Per-observation active-energy intensity for volume, mass or items, never silently interchanged |
| `recorded-differential-pressure-power.v1` | Forward Q>0 in m3/s; directly measured pump pressure rise DeltaP>=0 in Pa; Q*DeltaP in W | Pressure-only power at matched recorded times |
| `recorded-pressure-power-input-ratio.v1` | The same Q/DeltaP plus measured active P>0; 100*Q*DeltaP/P in percent | Pressure-power relative to measured electrical active input; not total pump efficiency |

Require all inputs from the same canonical assignment/equipment boundary and their exact declared semantics/units. Do not derive active power from current, assume power factor or zero suction pressure, replace pressure rise by discharge pressure, substitute commanded values, or treat a cumulative meter as a rate. Counter/reset/rollover integration is a different model and is not silently accepted. The defined specific-energy rule is a positive rate-based calculation, not an invented whole-window consumption integral.

Dimensional positive fixtures: P=2 kW and net volume production=20 m3/h produce 0.1 kWh/m3. Q=20 m3/h and DeltaP=3 bar produce 1,666.666... W; relative to 2 kW input, the pressure-power ratio is 83.333... percent. These examples must be executable tests, not unsupported-only outcomes.

The conversion 1 kWh=3.6e6 J follows [NIST SP 811](https://www.nist.gov/pml/special-publication-811/nist-guide-si-appendix-b-conversion-factors/nist-guide-si-appendix-b9). The [DOE/HI pumping sourcebook](https://www.energy.gov/sites/default/files/2014/05/f16/pump.pdf) distinguishes pressure, elevation and velocity head, and fluid/shaft/electrical power. Main's limited pressure-only indicator is a design inference from those distinctions, not a certified total-efficiency method. Do not clip a ratio to 100% or turn it into diagnosis; flag an out-of-expected-range result without hiding its inputs.

## B — temporal evidence, operating context and acceptance

Use raw Historian v2, paged/filtered by observed time. Require Good finite numeric values and explicit synchronized source timestamps inside the requested window. Join exact source times only; every participating TAG must have one raw row at that source time, including otherwise rejected rows when deciding ambiguity. No nearest-time matching, held value, interpolation or tolerance. Preserve observed/source times and rejection reasons. A 31-day window, page<=200 and combined 50,000 raw rows across all selected inputs bound the operation; overflow, malformed metadata and cursor loops return no partial metric.

Calculate per-qualified-tuple values, then deterministic sample summaries over at least two qualified tuples. A mean of those pointwise ratios must not be replaced by a ratio of means. Nonpositive denominators, invalid process directions, unsupported dimensions and nonfinite arithmetic have explicit refusals/counts, not fabricated zero. Return sufficient bounded tuple evidence to reproduce outputs, input code/version/qualifiers, rule/formula version, window, assignment, workspace/revision and counts.

For S3-05, the finite V1 context is the stated input meanings, common assignment, requested window and exact qualified recorded-time tuple. This supports only the declared observed-data formulas. Synchronized timestamps do not prove physical co-acquisition, clock accuracy, expected acquisition cadence, calibration, completeness or globally equivalent operating regimes; report these unknown/unverified, and do not expose a verified-equivalent-regime or total-efficiency result without actual evidence. A frequency predicate in #619 is not that proof. Learned regime models remain Stage 4.

Every selected TAG and TrendUse must be authorized before any read, around every page and at final publication, with existing entitlement, active scope, durable Authority/permit/revision gates and linked cancellation. Loss of authorization, scope, permit, entitlement or revision discards the entire operation. B tests cover positive physical fixtures, row bounds, malformed evidence, refusals and no-partial failure behavior. Main's post-merge audit found that the specific revocation/context matrix is incomplete: the current between-page tests use only the two-input differential-pressure rule and revoke only its first selected TAG; they do not prove each input across both two-input rules and the three-input rule. The bounded test-only follow-up is authorized in #623 from `ab38bfe`, with only `AssetProcessIntelligenceAnalyticsTests.cs` allowed to change. Integration requires Main review, exact T1 and exact post-merge T2.

## Release sequence and permanent limits

Main verified A after exact post-merge evidence at `257a4ab3c40a023beea91a027e5c3d692c22f72c`, activated B in #623, and integrated PR #626 at `ab38bfe` with green exact T1/T2. Main then performed the required S3-01–S3-08 audit and opened the test-only correction for the remaining S3-07 matrix. CODEX provides one coherent draft PR/local evidence/exact T1; Main reviews/integrates and verifies exact-merge T2. No new statistic or changed frozen contract is authorized.

No HA, Authority policy, licensing behavior, driver/time-evidence, Historian storage/provider, workflow, background worker/store, baseline or dashboard change is authorized. Windows/Linux and all declared product platforms remain scope. Owner HA local Preview/regression acceptance remains pending and is not replaced by CI. #588 and Wave15 remain open after A or B individually passes.
