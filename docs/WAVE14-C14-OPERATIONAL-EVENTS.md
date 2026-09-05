# Wave 14 C14 — First-Class Operational Events

## Scope

W14-C14 closes finding `C11-P2-EVT-01` by introducing a first-class, engineer-authorable and historizable model for operational process events.

This package is intentionally protocol-neutral and application-neutral. It does not model EEE-specific logic and it does not use Alarm or security Audit as substitutes for process events.

Required base:

- `2607e03d5445eefe1f434495d0ee81136c6cd220`

Development branch:

- `wave14/c14-operational-events`

Integration target remains:

- `wave14/corrections-integration`

No merge to integration or `main` is part of C14 delivery.

## Canonical flow

The C14 contract is:

`Engineering definition -> Active Runtime emission -> runtime context/timestamp -> IScadaEventBus -> durable persistence -> protected Historical Query`

The authority boundaries are deliberate:

1. Engineering owns authoring, import/export and stable definition identity.
2. Only definitions in the successfully activated Engineering revision are authoritative for emission.
3. Active Runtime creates immutable occurrences and publishes them on the existing runtime event bus.
4. Durable history persists occurrences in a dedicated append-only PostgreSQL table when Historical Query/TimescaleDB persistence is enabled.
5. Historical Query exposes a dedicated protected `operational.events` dataset.

## Domain contract

`src/Scada.Core/Events/OperationalEventModels.cs` defines:

- `OperationalEventDefinition`
- `OperationalEventEmissionContext`
- `OperationalEventOccurred : IScadaEvent`
- `OperationalEventContract`

The occurrence carries the fields needed for future Event Browser filtering without introducing any browser/UI dependency:

- `EventId`
- `DefinitionId`
- `DefinitionKey`
- `Type`
- `Category`
- `Source`
- `Area`
- `EquipmentPath`
- `TagId`
- `TagPath`
- `Operator`, when applicable
- `Operation`, when applicable
- `CommandId` / `CommandKey`, when applicable
- `Message`
- structured `Context`
- `OccurredAt`

Authored definition metadata is copied into occurrence context. Runtime emission context is then overlaid and wins on duplicate keys, so stable engineering context survives into durable history while a concrete transition may add or refine values at emission time.

Operator and command fields remain optional because legitimate process events may be produced autonomously by runtime logic.

## Engineering authoring and package contract

Operational Event is a first-class Engineering entity:

- `ImportEntityKind.OperationalEvent`
- `OperationalEventEngineeringDto`
- `EngineeringPackage.OperationalEvents`

The Engineering package schema is version `16` on this branch.

`EngineeringExchangeService` supports Operational Event definitions through the normal package flow:

- export
- parse
- preview
- validation
- create/update apply
- round-trip serialization

The event registry participates in the normal Engineering workspace lifecycle so Event definitions follow checkout/save/publish/activate authority rather than living in runtime-only state.

## Active Runtime authority

`IOperationalEventRuntime` exposes the protocol-neutral runtime contract for:

- listing active definitions
- resolving a definition by stable ID
- emitting an occurrence

`GatewayEngineeringRuntimeCoordinator` owns the active Operational Event definition snapshot beside the existing activation boundary.

A candidate definition set is built during activation. It replaces the current Event authority only after the underlying Engineering revision activates successfully. Consequently:

- emission before an Active revision is rejected;
- an unknown/inactive definition is rejected;
- after a new revision activates, removed definitions are no longer valid emission authorities;
- a failed activation cannot promote its Event definitions.

`ScadaRuntimeFacade` exposes Operational Event access only through the active Engineering runtime.

## Runtime emission

`EmitOperationalEventAsync` resolves a definition from the active snapshot and creates `OperationalEventOccurred` through `OperationalEventContract.CreateOccurrence`.

The occurrence receives:

- a unique occurrence ID;
- stable definition identity;
- UTC timestamp;
- authored scope/classification;
- optional operator/operation/command context;
- authored metadata plus dynamic structured context.

It is published as its own `IScadaEvent` type on `IScadaEventBus`.

## Alarm differentiation

Operational Event is not an Alarm lifecycle alias.

- Runtime occurrence type: `OperationalEventOccurred`
- Alarm lifecycle type: `AlarmStateChanged`
- Operational Event historical dataset: `operational.events`
- Alarm historical dataset: `alarm.events`
- Operational Event storage: `elitescada.operational_event_history`
- Alarm storage remains owned by the Alarm history implementation.

A pump start, pump stop, mode transition or duty/standby change can therefore be historized without fabricating an alarm condition.

C14 does not alter Alarm acknowledgement, shelving, unshelving, active-state semantics or Alarm Browser contracts.

## Security Audit differentiation

Operational Event is also not a security Audit substitute.

Security Audit continues to use `Scada.Security.Audit.AuditEvent` and its audit sink/durability contracts for security-relevant actions such as TAG writes and alarm acknowledgements.

Operational process occurrences use `OperationalEventOccurred`. They are published to the runtime event bus and persisted by the Operational Event history subscriber. C14 does not write process events into the security audit sink.

A user/operator identifier may legitimately be carried by an Operational Event when a human command caused the process transition. That contextual field does not convert the process occurrence into a security Audit record.

## Durable persistence

`PostgreSqlOperationalEventHistoryStore` owns the durable history table:

`elitescada.operational_event_history`

The table stores:

- occurrence and definition identity;
- type/category/source;
- Area/Equipment/TAG scope;
- operator/operation/command context;
- message;
- structured context as `jsonb`;
- UTC timestamp.

The table is append-only. PostgreSQL triggers reject:

- `UPDATE`
- `DELETE`
- `TRUNCATE`

Indexes cover timestamp and common filtering dimensions including definition, type, category, source, area and TAG.

`OperationalEventHistoryPersistenceHostedService` subscribes only to `OperationalEventOccurred`; the Alarm persistence subscriber remains separate.

## Restart behavior

When durable Historical Query persistence is enabled, PostgreSQL is the authority for Operational Event history. Historical query does not depend on an in-process list of prior occurrences.

The persistence test writes an occurrence, disposes the first store instance, constructs a new store instance using the same database and then queries the prior occurrence. This proves the intended restart boundary: process memory may disappear while durable Event history remains queryable.

## Protected Historical Query

C14 adds the dedicated dataset:

`operational.events`

Supported public fields are:

- `event.id`
- `definition.id`
- `definition.key`
- `type`
- `category`
- `source`
- `area`
- `equipment.path`
- `tag.id`
- `tag.path`
- `operator`
- `operation`
- `command.id`
- `command.key`
- `message`
- `context`
- `timestamp`

The provider supports timestamp range/query paging plus field filters and text search appropriate to the dataset.

`HistoricalQueryApi.RequiredCapability("operational.events")` requires `SecurityCapability.View`. The dataset therefore passes through the existing authenticated Historical Query authorization boundary and is not exposed through a parallel unprotected endpoint.

## Tests

C14 adds or extends tests covering:

### Domain / Engineering

`tests/Scada.Core.Tests/OperationalEventContractTests.cs`

- stable occurrence identity;
- timestamp and command/operator context;
- authored + dynamic structured context merge;
- explicit distinction from `AlarmStateChanged`;
- explicit distinction from security `AuditEvent`;
- dedicated Historical Query dataset;
- Engineering preview/apply/export round trip;
- package schema version 16.

### Active Runtime / authorization

`tests/Scada.Drivers.Tests/OperationalEventRuntimeTests.cs`

- emission before activation rejected;
- successful Active revision publishes `OperationalEventOccurred` on `IScadaEventBus`;
- unknown definitions rejected;
- a new Active revision replaces previous Event definition authority;
- `operational.events` maps to the protected runtime `View` capability.

### PostgreSQL persistence/query

`tests/Scada.Persistence.PostgreSql.Tests/PostgreSqlOperationalEventHistoryStoreTests.cs`

- append and durable query;
- reconstruction of the store to exercise restart durability;
- filtering by definition/type/TAG/operator;
- search;
- append-only mutation rejection.

Existing Alarm and Audit suites remain the regression authority for those independent subsystems.

## CI validation

Per `docs/CI-VALIDATION-POLICY.md`, C14 requires:

- `EliteSCADA CI` universal gate;
- `L3 Seven-Driver Lab` because C14 changes Runtime/event-routing-sensitive paths.

Final workflow run IDs, SHAs and conclusions are recorded here only after the branch has been executed by GitHub Actions. A run that has not executed is not counted as evidence.

## Contract exported to C18

C18 is a consumer of C14, not a competing schema authority.

C18 should consume:

- the `OperationalEvent` domain vocabulary defined by C14;
- the `operational.events` Historical Query dataset;
- the published field names above;
- the existing protected Historical Query API and authorization behavior.

C18 must not create a frontend-only Event occurrence schema, repurpose `alarm.events`, or use security Audit as Event Browser history.

C14 intentionally does not implement Event Browser UI.
