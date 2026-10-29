# ADR-024 — Historian storage retention, sizing, and archive boundaries

**Status:** accepted for the owner-authorized work package in [#779](https://github.com/brunolrogerio-collab/EliteSCADA/issues/779); implementation starts from `wave15/corrections-integration@9c12d0a11c1d0747a19ffac88a27eb4d3db35fa2`.

## Context

At this baseline, `HistorianStoragePolicy` in `src/Scada.Historian/Policies/HistorianStoragePolicy.cs` is the existing policy contract. `IHistorianRetentionDownsamplingStore` is implemented by `TimescaleDbHistorianRetentionDownsamplingStore`; it persists one policy in `elitescada.historian_storage_policy_state` and reconciles time-based retention for `elitescada.tag_history` and the supported aggregate views. The API currently registers the capture provider in `HistorianConfiguration`, but does not register/expose that retention store or provide a retention administration screen. Engineering > Database is currently owned by `DatabaseTopologyApp` and administers topology, not Historian policy.

Audit retention is separate: `AuditRetentionPolicy` / `AuditRetentionCoordinator` in `Scada.Security.Audit` are configured through `AuditApi`. Database topology resolves the active Historian connection through `DatabaseRuntimeConnectionSet`. No Historian archive workflow or reliable server-filesystem free-space metric was found in the product path.

## Decision

1. **One canonical Historian policy.** Expose and extend the existing `HistorianStoragePolicy`/store path; do not add a second Engineering, file, or UI-owned policy authority. Persist the active policy with the Historian database, so Local, Remote/shared, and Remote/separate follow the topology's resolved Historian connection. Database Topology remains responsible for connection topology only.
2. **Keep data domains separate.** This policy controls only Historian raw samples and supported aggregate tiers. Alarm/event history, Audit, project/Active, Authority, credentials, HA state, and backups keep their existing owners and are never pruned by a generic database rotation rule.
3. **Truthful measurements.** Show measured bytes and sample/time coverage for Historian-owned relations where the connected provider can report them. Show database footprint separately. Show filesystem free space only when the provider supplies a trustworthy measurement; otherwise say it is unavailable. Growth is a labeled approximation based on observed intervals, never an invented forecast or hard capacity guarantee.
4. **Retention semantics.** No expiry is represented by disabled automatic expiry and is not an infinite-disk promise. Time and/or Historian byte budgets may be configured. If a size budget is implemented by deleting data, enforcement removes only complete oldest eligible chunks under a serialized database-scoped worker; chunk size and scheduling create bounded overshoot that must be displayed. Any change that can expire data requires an explicit confirmation and uses the existing safety guard. If enforcement or measurement is unavailable, fail closed and report that state; never silently delete or claim a hard cap.
5. **Explicit full-limit behavior.** The operator chooses a documented action when the configured budget is reached. No silent deletion, loss of capture, or implied durability. A warning-only policy remains safe when destructive retention has not been explicitly enabled.
6. **Archive before source removal.** Archive is a distinct, versioned, compressed, integrity-checked and restorable representation with explicit destination, period, index and recovery path. Archive creation is incremental and restartable. Source samples may be removed only after the archive has been read back/verified and the owner explicitly enabled cleanup; destination-full or interrupted operations preserve source data. Remote DB archives stream through the product/API host rather than assuming the DB host's filesystem is available.
7. **Operational safety and portability.** Reuse the existing durable-write admission and canonical database serialization discipline where applicable. Work is bounded in CPU, memory and I/O and resumable after interruption. Windows, Linux, Docker and Edge use the same policy semantics; unsupported provider capabilities are shown as unavailable rather than emulated.
8. **Documentation follows implementation.** Help and the pt-BR/en/es manual must explain capture versus aggregation versus retention versus archive/backup, units, no-expiry behavior, the effect of destructive changes, small-hardware examples, and restore verification. Mark features not yet implemented until their exact candidate is accepted.

## Consequences and implementation order

- First expose the existing time-retention policy safely through API and Engineering > Database, with measured usage and tests; keep `AuditRetentionPolicy` independent.
- Then add a measured Historian size budget and explicit at-limit behavior without promising instantaneous physical disk reclamation.
- Deliver archive/restore as a separate increment in this same issue, with verified read-back and interrupted/full-destination tests before optional source cleanup is enabled.
- #779 remains open until all accepted increments, applicable tests, and synchronized Help/manual are integrated. Preview/DEMO data and volumes are outside this work package.
