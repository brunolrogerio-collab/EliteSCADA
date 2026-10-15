# ADR-014 — Asset & Process Intelligence: temporal provenance and capture semantics

- Status: **PROPOSED — Main review required; design only.**
- Authorized by Main in [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6183866671) and [#305](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305#issuecomment-6183866724).
- Design base: `wave15/corrections-integration@10ea669ff40a763b6f57d286550074a98c2668bf` (merged PR #613; post-merge CI #38675933924 passed on this SHA).
- Related decisions: [ADR-011 — Stage 3A descriptive KPIs](ADR-011-ASSET-PROCESS-INTELLIGENCE-STAGE3A.md), [ADR-012 — Stage 3B profile coverage](ADR-012-ASSET-PROCESS-INTELLIGENCE-STAGE3B-PROFILE-COVERAGE.md), and [ADR-013 — controlled signal semantics and units](ADR-013-ASSET-PROCESS-INTELLIGENCE-SEMANTIC-UNITS.md).
- Scope: propose only the minimum temporal-provenance and honest capture/completeness contract needed to assess later cross-signal Stage 3 work. No implementation is authorized.

## Context and observed gap

The current product can calculate bounded descriptive statistics for one TAG and profile coverage for multiple signals. It cannot establish when a device acquired each value or whether expected acquisitions are missing:

- `src/Scada.Core/Tags/TagValue.cs` defines `Timestamp` as EliteSCADA observation/publication time, plus nullable `SourceTimestamp` and `ServerTimestamp`.
- `src/Scada.Core/Sources/SourceProviderContracts.cs` carries nullable source/server timestamps in `QualifiedSourceSample`; `ServerMemorySourceProvider` propagates them in memory. OPC UA can provide both, DNP3 has source-time synchronization evidence, and MQTT source time depends on configured payload mapping. A declared driver capability does not make a timestamp present or synchronized on every sample.
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs` carries `HistorianCaptureProfiles`; `HistorianCaptureProfileEngineeringDto` holds capture strategy/settings. `HistorianCaptureProfileRuntimeResolver` resolves the effective profile, and `HistorianCaptureAdmission` evaluates received tag-change events. Its Periodic and MaximumInterval settings do not schedule source reads or create a value during source silence. Skipped/coalesced events are not raw Historian rows.
- `src/Scada.Historian.TimescaleDb/TimescaleHistorianSchema.cs` defines raw `tag_history` with `tag_id, ts, quality, source, value, data_type`. `TimescaleDbHistorian.InsertSql` writes those fields and `WriteBatchAsync` uses `TagValue.Timestamp` for `ts`; it does not persist `SourceTimestamp`, `ServerTimestamp`, or the effective capture-policy identity.
- `src/Scada.Core/HistoricalQueries/HistoricalQueryContracts.cs` exposes only `tag.id`, `tag.path`, `quality`, `value`, and `timestamp` in `historian.samples` v1. `TimescaleHistoricalQueryProvider` reads the SCADA `ts`. `HistoricalRetrievalProvenance.SourceTimestampsUtc` is provider/runtime provenance referring to Historian points used for retrieval, not the originating device's `TagValue.SourceTimestamp`.
- `src/Scada.Historian.TimescaleDb/TimescaleHistorianSchema.cs` rollups (1m, 5m, 15m, 1h) retain aggregate counts and value/quality summaries, not per-sample source-time evidence. `HistorianStoragePolicy` can expire raw history.
- ADR-011 therefore correctly leaves completeness `unknown` and expected count null. ADR-012's exact equal-timestamp intersection is explicitly Historian event-time overlap, not source-acquisition simultaneity. ADR-013's role/unit annotations do not remove this temporal evidence gap or authorize physical KPIs.

## Proposed temporal and capture model

Any future persisted observation contract should keep these independent facts distinct:

1. **Observed time:** the current EliteSCADA observation/publication time (`TagValue.Timestamp` / raw Historian `ts`). It remains required and must not be relabeled as device time.
2. **Source/acquisition time:** optional time actually supplied by the device/application. Absence, invalidity, or lack of clock synchronization must remain explicit; the system must never synthesize it from observed time.
3. **Intermediary/server time:** optional time supplied by a protocol server, kept separate from source time and observed time.
4. **Time evidence:** for each optional time, a bounded status and producer/protocol provenance sufficient to distinguish absent, invalid, explicitly unsynchronized, present-but-unverified, and synchronized/verified evidence. Exact enum names and which drivers can assert the strongest status require Main approval. Driver capability is descriptive, not per-sample evidence.
5. **Effective Historian capture policy:** a stable reference/version (or immutable active-project revision reference) sufficient to identify the strategy/settings that admitted a stored row. This describes storage admission only. It does not represent source polling cadence, device acquisition rate, or completeness.

`Periodic` and `MaximumInterval` remain capture-admission settings evaluated when source observations arrive. A coalesced event is not an acquisition failure; a silent source cannot be made complete by the capture policy. If a future feature needs counts for skipped/coalesced events, it must design bounded diagnostics separately rather than manufacturing Historian observations.

Expected acquisition cadence must be a separately reviewed source/signal contract with explicit provenance and evidence that the selected driver can observe it. It cannot be inferred from `PeriodMilliseconds`, `MaximumIntervalMilliseconds`, an event timestamp, or matching timestamps across tags. Unless both expected cadence and sufficient source-liveness/sequence evidence exist, coverage and completeness remain `unknown`.

## Persistence, query, compatibility, and retention direction

- Prefer an additive, nullable raw-history extension for source time, server time, their evidence statuses/provenance, and effective capture-policy identity. Keep the existing observed `ts` unchanged. Do not make the new columns required for old producers or old rows.
- Keep `historian.samples` v1 unchanged. A future consumer needing the new fields must use a separately versioned query contract/dataset; v1 readers must never silently receive changed timestamp meaning. `QualifiedSourceSample` already has optional source/server fields, but the durable write/query path and portable package/query contracts must be reviewed together before implementation.
- Upgrade must be forward-only and non-destructive: add nullable fields without rewriting old values; old rows map to `sourceTime=unknown/not recorded`; old writers that name existing columns continue to work. Rollback to an older application must leave added data intact; do not ship a destructive down-migration that drops populated provenance. Unknown/missing timestamp fields in legacy serialized samples/packages remain unknown, not backfilled.
- Exact cross-signal alignment, if later approved, must use raw observations with accepted time evidence. Existing rollups do not preserve per-sample source time and cannot prove alignment. Retention-expired or downsampled windows must not be presented as complete; the query must preserve an explicit unknown/insufficient state when raw evidence is unavailable.
- Shared-database HA and independent-database deployments are compatibility constraints only. This design does not alter replication, authority, fencing, failover, session, or database-topology behavior. Distinct databases may contain different retained observations; no cross-node synchronization claim follows from this ADR.

All timestamps need a documented UTC normalization and precision contract that does not manufacture equality after storage conversion. Main must approve per-driver synchronization evidence, admissible future/past skew handling, and whether a source timestamp outside the requested window is rejected or surfaced as invalid; no numeric skew tolerance is selected here.

## Bounds and future acceptance tests

Retain the currently proven query envelope unless separately benchmarked and approved: absolute window up to 31 days, up to 50,000 rows per window, page size 200, profile up to 16 signals, and up to 3,000 trend buckets. Measure nullable-column/index overhead, forward migration time/locks, row and wire-size growth, and bounded query cost on worst-case existing windows before enabling the feature. Do not add an unbounded per-event audit stream.

Before any implementation is proposed for approval, its focused tests must cover:

- Round-trip of observed/source/server time plus evidence status through supported source-provider serialization, runtime propagation, durable raw write, and versioned historical read; absent fields stay absent.
- Driver-specific evidence: verified and unsynchronized source time, source time absent, server/source times distinct, repeated/retained values, malformed/out-of-range timestamps, and drivers that do not supply source time. Never treat a driver capability as proof for a sample.
- Upgrade an old schema and read legacy rows as `unknown/not recorded`; run an older writer/reader against the additive schema; prove rollback leaves added columns/data intact and query v1 semantics unchanged.
- Capture-policy changes are traceable to the effective revision/profile; configured Period/MaximumInterval do not yield expected counts, a coalesced event does not become a row, and source silence creates no synthetic sample.
- Exact timestamps at database precision are not overstated as simultaneous acquisition; only Main-approved synchronized source-time evidence and skew rules could qualify for a later alignment result.
- Raw retention and downsampling cannot silently turn missing provenance into completeness; old rollups continue their existing value/quality semantics.
- Existing row/page/signal/window bounds remain enforced while measuring added storage, migration, and query costs, including maximum-density windows.
- Shared-DB and independent-DB configurations preserve existing HA and database-topology contracts; tests are compatibility checks only, not a new HA acceptance gate.

## Decisions required from Main before implementation

1. Approve the per-sample time-evidence vocabulary and the exact synchronization evidence each initially supported driver may assert.
2. Define whether any source cadence is independently knowable, which acquisition/liveness evidence is required, and which states must remain `unknown`; do not use Historian capture settings as expected cadence.
3. Select the additive SQL and portable serialization shape, versioned historical query surface, and effective capture-policy reference semantics while preserving Historian v1 readers and existing Engineering package behavior.
4. Set precision, source-vs-observed skew policy, invalid/out-of-window behavior, retention behavior, and resource budgets from measured limits.
5. Review the migration/rollback and acceptance matrix above. Only a separate explicit authorization after that review may start implementation; physical KPI formulas require their own approval after time, quality, cadence/completeness, semantic role/unit, regime, and measurement-boundary contracts are established.

## Non-goals

This ADR does not authorize or change code, DTOs, database schema, API/query behavior, UI, drivers, Historian storage/provider, capture behavior, HA, Authority/licensing, ADR-011/012/013, or Stages 4–6. It does not define a physical KPI or claim acquisition simultaneity, freshness, expected sample counts, or completeness. No PR or CI validation is part of this design-only handoff.
