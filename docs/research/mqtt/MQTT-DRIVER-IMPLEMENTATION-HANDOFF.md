# MQTT Driver Implementation Handoff

Status: **DEV Driver 10 implementation branch / parked from mainline**  
Branch: `driver10/mqtt`  
Driver type: `mqtt.raw`  
Configuration schema: `elitescada.driver.mqtt.raw` v1

This document records the implementation state of the raw MQTT industrial driver. It does not authorize merge to `main`; integration remains owned by the Coordinator.

## Delivered runtime scope

- MQTT 5.0 and MQTT 3.1.1 are explicit protocol modes.
- TCP with optional TLS is supported. TLS uses the platform validation defaults and target-host validation; there is no accept-any-certificate mode.
- One configured Data Source maps to one broker/client session identity.
- Acquisition is event-driven. MQTT messages update the canonical `ICurrentTagCache` directly and do not fabricate polling cycles.
- Canonical TAG bindings use exact MQTT topic names. `+` and `#` wildcard filters are rejected for authoritative TAG identity.
- QoS 0, 1 and 2 are supported for subscriptions and publishes. QoS is never converted into TAG quality.
- Reconnect uses bounded exponential delay and deterministic resubscription.
- Broker loss marks affected TAGs `BadCommunication` and moves communication diagnostics through reconnect/fault states.
- Passive receive-side transport loss is counted as a failed communication operation and a disconnection even when the transport has already observed the socket/session as disconnected.
- Driver lifecycle calls are serialized. Concurrent `StartAsync`, `StopAsync` and `DisposeAsync` cannot create overlapping runtime sessions or tear down the same session concurrently.
- The cancellation token passed to `StartAsync` controls admission to the start operation only; after `StartAsync` returns, later cancellation of that caller token does not own or terminate the MQTT runtime session.
- Once `StopAsync` has acquired the lifecycle gate and shutdown begins, cleanup is completed using the driver-owned session cancellation path. Later cancellation of the caller's stop token cannot leave the runtime half-stopped.
- Explicit stop/start is supported without duplicate canonical TAG registration; completed cancellation state and freshness references are released/reset between starts.
- Explicit stop/start is not counted as a transport `Reconnect`. The reconnect counter is reserved for automatic recovery inside one runtime session.
- A completed/faulted runtime session is cleaned before an explicit restart, including cancellation of any still-running freshness loop.
- Malformed payloads fail closed per mapped point and do not silently coerce values to `0`, `false` or another guessed type.
- Inbound payload size is checked at the MQTTnet callback boundary before EliteSCADA copies the MQTTnet payload into an application-owned byte array. An oversized payload stops new inbound admission and faults the Data Source rather than allocating/copying the oversized application payload.
- Retained values without a trustworthy configured source timestamp are `Stale` by default. `acceptAsCurrent` is an explicit opt-in policy.
- Optional per-TAG freshness timeout transitions a previously valid `Good` sample to `Stale` when no newer accepted MQTT sample arrives in time. The freshness clock starts from local sample acceptance and is deliberately independent from mapped source timestamps.
- Freshness uses the monotonic `Stopwatch` clock, so wall-clock adjustment cannot prematurely expire or extend a point freshness interval.
- Freshness-managed cache updates are serialized with freshness expiry transitions so an older expiry decision cannot overwrite a newer accepted sample or communication failure.
- The MQTTnet inbound adapter uses a bounded channel with `FullMode.Wait`. Callback completion therefore applies backpressure instead of silently dropping newest/oldest telemetry or allowing an unbounded memory queue.
- Inbound QoS 1/2 acknowledgement is deferred until the application message has been admitted to the bounded EliteSCADA transport queue. A canceled, rejected or oversized message is not acknowledged.
- Writable TAGs publish through the normal driver write path and do not pretend that a successful MQTT publish means the remote process accepted the command.
- Runtime diagnostics use `CommunicationDriverDiagnosticSnapshot`; scan interval, cycle count and scan duration remain absent/zero for the event-driven driver.

## Payload mapping

Implemented payload formats:

1. `utf8Scalar`
   - Boolean: only `true` / `false`.
   - Int16 / Int32 / Int64: invariant integer parsing with range checks.
   - Float / Double: invariant finite numeric parsing.
   - String / Enum: UTF-8 text.
   - DateTime: date/time strings require an explicit `Z` UTC designator or numeric offset and are normalized to UTC. Offset-less timestamps are rejected rather than interpreted in the host time zone.

2. `json`
   - deterministic JSON scalar extraction;
   - RFC 6901 JSON Pointer including `~0` and `~1` escapes;
   - array index tokens are canonical decimal indices (`0` or a non-zero digit followed by digits); leading-zero forms such as `01` are rejected;
   - optional JSON Pointer source timestamp extraction;
   - optional required-source-timestamp policy;
   - mapped DateTime/source-time strings require an explicit `Z` or numeric offset.

`DateTime` writes accept `DateTimeOffset`, UTC/local `DateTime`, and reject `DateTimeKind.Unspecified` because it does not identify an unambiguous instant.

JSON field extraction is read-only for non-root writes in this slice. Publishing a JSON sub-field would require an explicit envelope/template contract and is rejected rather than guessed.

## Canonical Data Source settings

The driver uses the existing public `DataSourceEngineeringDto.Settings` and `SecretReferences`; no MQTT-private persistence store exists.

| Key | Meaning | Default / notes |
| --- | --- | --- |
| `host` | broker host | required |
| `port` | TCP port | 8883 with TLS, 1883 without TLS |
| `tls` | TLS enabled | `true` |
| `clientId` | MQTT Client ID | required |
| `protocolVersion` | `mqtt5` or `mqtt311` | `mqtt5` |
| `username` | user name | optional, non-secret field |
| `keepAliveSeconds` | keep-alive | 30 |
| `connectTimeoutMilliseconds` | connection timeout | 10000 |
| `reconnectMinimumMilliseconds` | initial reconnect delay | 1000 |
| `reconnectMaximumMilliseconds` | maximum reconnect delay | 30000 |
| `mqtt311.cleanSession` | MQTT 3.1.1 session policy | `false` |
| `mqtt5.cleanStart` | MQTT 5 Clean Start | `false` |
| `mqtt5.sessionExpirySeconds` | MQTT 5 session expiry | 3600 |
| `maximumInboundPayloadBytes` | inbound payload bound | 1048576 |
| `maximumConsecutiveConnectFailures` | fault threshold | 5 |
| `maximumBufferedMessages` | maximum queued inbound MQTT events | 4096; bounded 1..1000000 |

Protocol-specific MQTT 3.1.1 and MQTT 5 settings are mutually validated. Configuration for the wrong protocol version fails before activation.

### Secret references

`DataSourceEngineeringDto.SecretReferences["password"]` is the canonical password reference. Plaintext `Settings["password"]` is rejected both at the generic Engineering import boundary and by the MQTT compiler.

`MqttRuntimeFactory` exposes a narrow adapter delegate so a host-owned resolver can translate the canonical reference into short-lived credential material. It is **not** a secret store. The common host security abstraction still needs Coordinator reconciliation.

Resolved password material is owned by `MqttResolvedCredentials`, passed only through the connection boundary and explicitly zeroed when that connection attempt completes. The MQTTnet adapter also zeroes its own temporary password copy.

## Canonical TAG binding

`TagEngineeringDto.Address` stores the exact subscribe topic.

Supported MQTT metadata keys:

| Key | Meaning |
| --- | --- |
| `mqtt.payloadFormat` | `utf8Scalar` or `json` |
| `mqtt.jsonPointer` | JSON value extraction pointer |
| `mqtt.sourceTimestampJsonPointer` | JSON source timestamp pointer |
| `mqtt.sourceTimestampRequired` | fail if source timestamp is absent/invalid |
| `mqtt.freshnessTimeoutMilliseconds` | optional silence limit after the last accepted sample before a `Good` value becomes `Stale` |
| `mqtt.retainedValuePolicy` | `staleWithoutSourceTimestamp` or `acceptAsCurrent` |
| `mqtt.qos` | subscription QoS 0/1/2 |
| `mqtt.publishTopic` | exact publish topic for writable TAG |
| `mqtt.publishQos` | publish QoS 0/1/2 |
| `mqtt.publishRetain` | publish retain flag |

Wildcard traffic never creates canonical TAGs automatically.

### Freshness semantics

Freshness is independent from broker connectivity, MQTT QoS and process/source clock age.

- A valid incoming sample starts or refreshes the point freshness clock only after the sample is accepted into the canonical current-value cache.
- The freshness interval is measured with the local monotonic clock from that acceptance event.
- A mapped `SourceTimestamp` is preserved as process provenance but never used to shorten or extend the MQTT freshness interval.
- A newly received value with an old but valid source timestamp can therefore still be `Good`; source age and communication silence are intentionally different dimensions.
- A `Good` sample that later exceeds its receive/acceptance timeout is republished as `Stale` while preserving value and source timestamp.
- A later valid accepted message recovers the point to the quality determined by payload/retained policy and restarts the freshness clock when applicable.
- Accepted samples, freshness expiry and communication/decode failure quality updates are serialized for freshness-managed TAGs, preventing an older expiry decision from overwriting a newer state.
- Freshness expiration does not fabricate a communication failure or force the broker connection out of `Healthy` by itself.

## Inbound buffering, backpressure and acknowledgement

`maximumBufferedMessages` configures the bounded queue between MQTTnet callbacks and canonical TAG processing.

The channel uses:

- one canonical reader;
- multiple possible MQTTnet callback writers;
- `BoundedChannelFullMode.Wait`;
- cancellation of blocked writers during intentional disconnect/dispose.

This intentionally avoids `DropOldest`, `DropNewest` and unbounded buffering. When the process cannot consume MQTT messages fast enough, callback completion waits for capacity, allowing pressure to propagate toward the protocol library instead of silently corrupting application-level telemetry history.

For inbound QoS 1 and QoS 2, MQTTnet automatic acknowledgement is disabled for the callback. The transport calls the library's deferred acknowledgement API only after the message has successfully entered the bounded EliteSCADA queue. If queue admission is canceled during disconnect/dispose, the message remains unacknowledged so broker/session semantics may redeliver it where applicable.

Before `ReadOnlySequence<byte>.ToArray()` creates an EliteSCADA-owned payload copy, the callback compares MQTTnet's payload length with `maximumInboundPayloadBytes`. If the limit is exceeded:

- no EliteSCADA payload byte-array copy is created;
- new inbound admissions are stopped for that transport session;
- QoS 1/2 is marked processing-failed and is not acknowledged by EliteSCADA;
- a permanent transport error is queued to the runtime;
- the runtime marks communication failure, disconnects once and transitions the Data Source to `Faulted` rather than reconnecting forever into the same retained/oversized message.

This protects the EliteSCADA application allocation boundary. MQTTnet necessarily still owns/observes the protocol packet that reached its callback, so this is **not** a claim that the protocol library allocated zero memory for an oversized network packet. MQTT 5 also exposes a Maximum Packet Size property, but packet size and configured payload size are different contracts and are not conflated here; MQTT 3.1.1 has no equivalent property.

The acknowledgement boundary for normal-sized messages is deliberately **queue admission**, not canonical TAG transaction completion. This avoids acknowledging messages that were discarded by local backpressure while still keeping MQTT protocol handling outside the TAG cache. A process failure after queue admission but before TAG processing can therefore still cause application-level replay/loss scenarios according to broker/session/QoS behavior. Packet identifiers are not treated as durable event identity and no deduplication is invented from them.

The queue is bounded by message count and each individual payload is separately limited by `maximumInboundPayloadBytes`. This is deterministic per-message protection, but it is not a separate aggregate-byte memory quota; real burst validation is still required before choosing production capacities.

## Engineering Import/Export

MQTT uses the existing `IEngineeringExchangeService` path and therefore remains GUI-independent.

Covered paths:

- JSON parse/export;
- Preview/Apply;
- Data Source CSV export/import including Settings and SecretReferences;
- TAG CSV export/import including Address and MQTT metadata;
- public package/revision-compatible DTO representation.

The freshness and inbound-buffer settings live in those same public maps and therefore round-trip through the existing package/CSV contracts without MQTT-private persistence.

No plaintext password/private key is introduced by the MQTT-specific code.

## Runtime composition

`MqttEngineeringCompiler` compiles canonical Engineering into `MqttRuntimePlan` objects.

`MqttRuntimeFactory` composes those plans into `MqttDriver` instances using:

- canonical `ICurrentTagCache`;
- canonical `ITagRegistry`;
- an injected MQTT transport factory;
- an optional host-owned credential resolver adapter.

This keeps MQTTnet classes behind `IMqttClientTransport` and prevents the protocol library from becoming canonical project truth.

## Automated test coverage authored

- `MqttPayloadCodecTests`
- `MqttDriverTests`
- `MqttEngineeringCompilerTests`
- `MqttEngineeringExchangeTests`
- `MqttEventDrivenReadinessTests`
- `MqttRuntimeFactoryTests`
- `MqttCredentialLifetimeTests`
- `MqttFreshnessAndBufferTests`
- `MqttTransportSafetyTests`
- `MqttLifecycleConcurrencyTests`
- `MqttBrokerIntegrationTests`

The deterministic tests cover exact-topic validation, typed payloads, strict UTC/offset timestamp parsing, canonical JSON array indices, JSON Pointer, retained semantics, malformed payload isolation, ambiguous DateTime write rejection, event-driven cache updates, writes, reconnect/resubscribe, permanent transport-fault behavior, Engineering compilation, public Import/Export fidelity, secret reference enforcement, runtime composition, credential zeroization, freshness expiration/recovery, separation of source-time age from receive freshness, stop/restart lifecycle, passive-disconnect diagnostics, bounded-buffer configuration limits, startup-token ownership, concurrent starts, caller-cancellation-safe shutdown and explicit-restart reconnect accounting.

`MqttBrokerIntegrationTests` is an opt-in live-broker contract. When `ELITESCADA_MQTT_INTEGRATION_HOST` is configured it exercises the production MQTTnet adapter against MQTT 5.0 and/or 3.1.1, QoS 0/1/2 and retained delivery. Without that environment variable the test intentionally returns without opening the network and therefore does **not** constitute broker interoperability evidence. The reproducible procedure and environment-variable contract are documented in `docs/research/mqtt/MQTT-BROKER-VALIDATION.md`.

The current execution environment does not contain the .NET 10 SDK or a local MQTT broker, so neither the deterministic suite nor the live broker contract has been executed here. GitHub Actions should not be spent merely as reassurance CI; run the focused suite when Coordinator integration or a justified driver validation run is scheduled.

## Shared decisions required before central runtime integration

### 1. Host-owned secret resolver

The public Engineering model already stores secret references correctly, but the common DriverHost security service that resolves them has not yet been defined. The MQTT branch provides only the adapter seam required to consume such a service.

The Coordinator should establish one common resolver contract for all communication drivers instead of allowing MQTT, OPC UA and future proprietary modules to create incompatible secret stores.

### 2. Event-driven readiness

`EngineeringRuntimeCoordinator` currently requires all runtime TAGs to reach `TagQuality.Good` before activation succeeds.

That assumption is valid for polling sources but invalid for event-driven MQTT. A broker can be connected, authenticated and subscribed while no publisher has emitted a first sample yet.

The MQTT implementation establishes the intended semantics:

- connected + subscriptions accepted => communication state `Healthy`;
- a TAG with no received sample remains `NoCurrentSample`;
- absence of first telemetry is not itself a broker communication failure;
- a configured freshness expiration may make an existing sample `Stale` without implying broker failure.

The common activation policy needs a protocol-neutral readiness contract that can represent this distinction.

## Validation still required

Before production integration, validate with at least two independent broker implementations where practical. The opt-in broker contract now automates the basic protocol/QoS/retained matrix, but no live result has been recorded yet.

1. Eclipse Mosquitto
   - run `MqttBrokerIntegrationTests` for MQTT 5 and 3.1.1;
   - TCP and trusted TLS;
   - username/password authentication;
   - QoS 0/1/2;
   - retained messages;
   - persistent session reconnect;
   - broker restart and network interruption;
   - malformed and oversized payload behavior, including confirmation that oversized payloads fault without an EliteSCADA application payload copy;
   - oversized retained QoS 1/2 behavior and broker redelivery after operator remediation/restart;
   - burst traffic above configured inbound capacity to verify bounded backpressure and shutdown behavior;
   - QoS 1/2 redelivery when bounded queue admission is interrupted before acknowledgement;
   - freshness timeout and recovery under real broker traffic.

2. Independent implementation such as HiveMQ Community Edition
   - run the same basic live broker contract;
   - connection/session interoperability;
   - subscription and publish acknowledgements;
   - QoS and retained interoperability;
   - TLS hostname/chain failures;
   - sustained burst/backpressure interoperability;
   - oversized-payload policy interoperability;
   - deferred QoS acknowledgement during broker disconnect/reconnect.

Vendor/cloud broker validation should be added when a concrete deployment target is selected.

## Explicitly outside this slice

- Sparkplug B semantics;
- WebSocket transport;
- dynamic wildcard-to-TAG creation;
- fake hierarchical MQTT browsing;
- binary-layout payload schemas;
- MQTT 5 User Property source-time mapping;
- topic observation UI;
- manufacturer-specific topic heuristics.

Those features require explicit scope and must not silently change raw MQTT semantics.