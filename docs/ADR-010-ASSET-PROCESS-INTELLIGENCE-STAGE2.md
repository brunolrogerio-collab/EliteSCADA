# ADR-010 — Asset & Process Intelligence Stage 2 Configuration

- Status: accepted for implementation by Main in issue [#602](https://github.com/brunolrogerio-collab/EliteSCADA/issues/602), under parent [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588).
- Scope: authored, static Engineering metadata only. This ADR does not authorize analytics execution.

## Decision

Extend the existing `EngineeringPackage.AssetProcessIntelligence` section. Version 1 remains the scope-only Stage 1 shape; version 2 adds reusable profiles, TAG signal references, explicit Equipment/profile assignments, and ordered process-stage links. Stable IDs are identity. TAG path, name, declared type, and engineering unit are presentation/diagnostic metadata sourced from the canonical TAG; only its stable ID is stored in a signal reference. Profiles may be reused by multiple assignments. Signal roles are domain labels and are not Authority roles or capabilities. Signals declare whether they are required or optional.

No TAG value, `TagRead` result, Historian sample, inferred value, or process-write intent is persisted. This remains true even when the project already has those runtime services.

## Compatibility and validation

The Engineering package schema advances from 24 to 25 because v2 adds package data. Schema-24 scope-only payloads remain importable. A v2 Intelligence payload in a schema-24 package is invalid. Existing readers that support only schema 24 already reject any newer package schema and must continue to fail closed rather than discard the new fields.

Preview validates the complete v2 configuration against the prospective canonical tags, equipment, and locations that would exist after the selected import mode. It rejects duplicate identities/keys, absent or ambiguous references, wrong resource kinds, and invalid or duplicate stage/link ordering. Apply is permitted only after a successful full preview, so invalid input cannot partially apply other package entities. Registry snapshots and canonical export order collections deterministically for repeatable round trips.

External project import still clears Stage 1 active state according to its existing rule; local revision hydration preserves the established Stage 1 behavior. Version 1 remains accepted and normalized without inventing Stage 2 data.

## Authoring and persistence

The Engineering UI authors configuration into Working. Its configuration endpoint requires the existing `EngineeringModify` capability and records a canonical Engineering audit event. It neither publishes nor activates a runtime revision. The full canonical Engineering package and project revision remain the persistence/export boundary; the existing save/checkout lifecycle handles restart and restore.

## Explicit exclusions

No new Intelligence payload is introduced into selected-resource Fragments, reusable `.escadalib` entries, CSV/XLSX, or specialized `.escadapkg` sidecar kinds. No users, password hashes, credentials, Authority policy, runtime state, or secret material is copied into this configuration. Authority, Licensing, Runtime, HA, Historian, Stage 1 activation/permit/scope/restart contracts are unchanged by Stage 2.

## Consequences

This creates a durable, portable model that later consultative-analysis work can consume without coupling configuration authoring to runtime reads. Any future analytical read path must separately authorize `TagRead` for each TAG and the applicable dataset capability; workers, baselines, inference, dynamic quality/freshness, calibration, temporal alignment, dashboards, and process effects require separately accepted work.
