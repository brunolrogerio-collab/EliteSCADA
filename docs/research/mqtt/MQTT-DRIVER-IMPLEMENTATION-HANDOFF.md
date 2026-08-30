# MQTT Driver Implementation Handoff

Status: **DEV Driver 10 implementation branch / parked from mainline**  
Branch: `driver10/mqtt`  
Driver type: `mqtt.raw`  
Configuration schema: `elitescada.driver.mqtt.raw` v1

This document records the implementation contract of the raw MQTT industrial driver. It does not authorize merge to `main`; shared integration remains Coordinator-owned. Moving exact-head CI evidence and current mainline drift are tracked in Draft PR #128 so this implementation contract does not become stale after every validation run.

## Delivered runtime scope

- MQTT 5.0 and MQTT 3.1.1 are explicit protocol modes.
- TCP with optional TLS is supported. TLS uses platform trust and target-host validation; there is no accept-any-certificate mode.
- One configured Data Source maps to one broker/client session identity.
- Acquisition is event-driven. MQTT messages update the canonical `ICurrentTagCache` directly and do not fabricate polling cycles.
- Canonical TAG bindings use exact MQTT topic names. `+` and `#` wildcard filters are rejected for authoritative TAG identity.
- QoS 0, 1 and 2 are supported for subscriptions and publishes. QoS is never converted into TAG quality.
- Reconnect uses bounded exponential base delay plus per-runtime ±25% jitter, clipped to the configured global minimum/maximum, followed by deterministic resubscription. Jitter remains effective at the maximum backoff ceiling.
- Broker loss marks affected TAGs `BadCommunication` and moves communication diagnostics through reconnect/fault states.
- Passive receive-side transport loss is counted as a failed communication operation and disconnection even when the transport has already observed the socket/session as disconnected.
- Driver lifecycle calls are serialized. Concurrent `StartAsync`, `StopAsync` and `DisposeAsync` cannot create overlapping runtime sessions or tear down the same session concurrently.
- The cancellation token passed to `StartAsync` controls admission to start only. Cancellation after `StartAsync` returns does not own or terminate the MQTT runtime session.
- Once `StopAsync` has acquired the lifecycle gate and shutdown begins, cleanup is completed through the driver-owned session cancellation path. Later cancellation of the caller token cannot leave the runtime half-stopped.
- Explicit stop/start is supported without duplicate canonical TAG registration and is not counted as a transport reconnect.
- A completed/faulted runtime session is cleaned before explicit restart, including any still-running freshness worker.
- Runtime workers are supervised as one session: terminal receive failure cancels freshness; unexpected freshness failure faults the session, cancels receive and attempts transport disconnect.
- Protocol-local readiness is separate from TAG quality and communication health. MQTT becomes `Ready` after broker connection/authentication and acceptance of all configured subscriptions; first telemetry is not required.
- `Ready` remains latched across transient reconnects and resets only on explicit restart/stop or terminal runtime fault.
- Malformed payloads fail closed per mapped point and never silently coerce to guessed values.
- Inbound payload size is checked at the MQTTnet callback boundary before EliteSCADA copies MQTTnet payload bytes into an application-owned array.
- Retained values without a trustworthy configured source timestamp are `Stale` by default. `acceptAsCurrent` is explicit opt-in behavior.
- Optional per-TAG freshness transitions a previously `Good` sample to `Stale` after local accepted-sample silence. Source timestamp age does not drive freshness.
- Freshness uses monotonic `Stopwatch` time and freshness-managed cache transitions are serialized.
- The EliteSCADA-owned MQTT inbound channel is bounded with `BoundedChannelFullMode.Wait`; application callback admission applies backpressure rather than silent drop.
- MQTTnet 5.2.0 itself uses an internal unbounded PUBLISH dispatch queue before invoking the application callback, so `maximumBufferedMessages` is not an end-to-end process-memory bound.
- For MQTT 5, the transport advertises `Receive Maximum = min(maximumBufferedMessages, 65,535)` to bound broker-side unacknowledged QoS 1/2 inflight. MQTT 3.1.1 and QoS 0 have no equivalent protocol flow-control guarantee.
- Inbound QoS 1/2 acknowledgement is deferred until bounded EliteSCADA queue admission. Canceled, rejected or oversized messages are not acknowledged.
- MQTTnet callbacks are fenced by transport-session generation. Handlers are installed per connection and capture that connection's generation; delayed callbacks from a prior session are rejected after reconnect and QoS 1/2 stale callbacks are marked processing-failed rather than acknowledged as current-session traffic.
- Writable TAGs publish through the normal write path and do not create a `Good` cache echo merely because MQTT publish succeeded.
- Runtime diagnostics use `CommunicationDriverDiagnosticSnapshot`; scan interval, cycle count and scan duration remain absent/zero for this event-driven driver.

## Protocol-local readiness evidence

`MqttDriver` implements `IMqttReadinessEvidenceSource` and returns `MqttReadinessSnapshot` with states:

- `NotStarted`;
- `Starting`;
- `Ready`;
- `Faulted`;
- `Stopped`.

The snapshot includes expected subscription count, accepted subscription count, whether mandatory initial broker/subscription initialization completed, and sanitized terminal fault detail.

Semantics:

- `Ready` means connection/authentication completed and configured SUBACKs were accepted.
- A TAG may still have `NoCurrentSample`, `Stale`, `Bad`, or another point-local quality while the Data Source remains ready.
- Transient broker loss changes communication diagnostics to reconnecting but does not erase successful mandatory initialization for that runtime session.
- A permanent runtime fault changes readiness to `Faulted`.
- Explicit stop changes readiness to `Stopped`.
- Explicit restart starts again at `Starting` and must complete mandatory initialization before becoming `Ready`.

The future common DriverHost readiness source/snapshot remains Coordinator-owned. `IMqttReadinessEvidenceSource` is MQTT-local evidence for later adaptation, not a competing host contract.

## Payload mapping

Implemented payload formats:

1. `utf8Scalar`
   - Boolean: only `true` / `false`.
   - Int16 / Int32 / Int64: invariant integer parsing with range checks.
   - Float / Double: invariant finite numeric parsing.
   - String / Enum: strict UTF-8 text.
   - DateTime: requires explicit `Z` or numeric UTC offset and normalizes to UTC.

2. `json`
   - deterministic JSON scalar extraction;
   - RFC 6901 JSON Pointer including `~0` and `~1` escapes;
   - array index tokens are canonical decimal indices; leading-zero forms such as `01` are rejected;
   - optional JSON Pointer source timestamp extraction;
   - optional required-source-timestamp policy;
   - mapped DateTime/source-time strings require explicit `Z` or numeric offset.

`DateTime` writes accept `DateTimeOffset`, UTC/local `DateTime`, and reject `DateTimeKind.Unspecified` because it does not identify an unambiguous instant.

JSON non-root field publishing remains unsupported without an explicit envelope/template contract and fails closed instead of inventing one.

## Canonical Data Source settings

MQTT uses existing public `DataSourceEngineeringDto.Settings` and `SecretReferences`; no MQTT-private persistence store exists.

| Key | Meaning | Default / notes |
| --- | --- | --- |
| `host` | broker host | required |
| `port` | TCP port | 8883 with TLS, 1883 without TLS |
| `tls` | TLS enabled | `true` |
| `clientId` | MQTT Client ID | required |
| `protocolVersion` | `mqtt5` or `mqtt311` | `mqtt5` |
| `username` | user name | optional, non-secret field |
| `keepAliveSeconds` | keep-alive | 30, bounded to MQTT wire range 1..65,535 seconds |
| `connectTimeoutMilliseconds` | connection timeout | 10000 |
| `reconnectMinimumMilliseconds` | reconnect minimum/base start | 1000 |
| `reconnectMaximumMilliseconds` | reconnect maximum/base ceiling | 30000 |
| `mqtt311.cleanSession` | MQTT 3.1.1 session policy | `false` |
| `mqtt5.cleanStart` | MQTT 5 Clean Start | `false` |
| `mqtt5.sessionExpirySeconds` | MQTT 5 session expiry | 3600 |
| `maximumInboundPayloadBytes` | inbound application payload bound | 1048576; bounded 1..67,108,864 |
| `maximumConsecutiveConnectFailures` | terminal-fault threshold | 5 |
| `maximumBufferedMessages` | EliteSCADA inbound application queue capacity | 4096; bounded 1..1,000,000; also caps advertised MQTT 5 Receive Maximum at min(value, 65,535) |

The ±25% reconnect jitter is a fixed runtime anti-herding policy, not separately persisted Engineering. Protocol-specific MQTT 3.1.1 and MQTT 5 settings are mutually validated. Undefined protocol enum values fail before transport connection.

### Secret references

`DataSourceEngineeringDto.SecretReferences["password"]` is the canonical password reference. Plaintext `Settings["password"]` is rejected at the Engineering boundary and by the MQTT compiler.

`MqttRuntimeFactory` exposes a narrow resolver adapter so a host-owned service can translate the canonical reference into short-lived credentials. It is not a secret store.

Resolved password material is owned by `MqttResolvedCredentials`, limited to the MQTT protocol field length, and zeroized on normal disposal and validation/construction failure. The MQTTnet adapter also zeroizes its temporary password copy.

## Canonical TAG binding

`TagEngineeringDto.Address` stores the exact subscribe topic.

Supported MQTT metadata keys:

| Key | Meaning |
| --- | --- |
| `mqtt.payloadFormat` | `utf8Scalar` or `json` |
| `mqtt.jsonPointer` | JSON value extraction pointer |
| `mqtt.sourceTimestampJsonPointer` | JSON source timestamp pointer |
| `mqtt.sourceTimestampRequired` | fail if configured source timestamp is absent/invalid |
| `mqtt.freshnessTimeoutMilliseconds` | optional accepted-sample silence limit before `Good` becomes `Stale` |
| `mqtt.retainedValuePolicy` | `staleWithoutSourceTimestamp` or `acceptAsCurrent` |
| `mqtt.qos` | subscription QoS 0/1/2 |
| `mqtt.publishTopic` | exact publish topic for writable TAG |
| `mqtt.publishQos` | publish QoS 0/1/2 |
| `mqtt.publishRetain` | publish retain flag |

Wildcard traffic never creates canonical TAGs automatically. Undefined TAG data type, payload format, retained policy and QoS enum values fail closed before runtime/codec fall-through.

### Freshness semantics

Freshness is independent from broker connectivity, MQTT QoS and process/source clock age.

- A valid incoming sample starts or refreshes the freshness clock only after acceptance into the canonical current-value cache.
- Freshness uses local monotonic time from that acceptance event.
- `SourceTimestamp` is preserved as process provenance but never shortens or extends the MQTT freshness interval.
- A newly received value with an old but valid source timestamp can still be `Good`.
- A `Good` sample exceeding its receive/acceptance timeout becomes `Stale` while preserving value and source timestamp.
- Later valid telemetry recovers according to payload/retained policy and restarts freshness where configured.
- Accepted samples, expiry and communication/decode failure quality updates are serialized for freshness-managed TAGs.
- Freshness expiry alone does not fabricate broker communication failure.

## Inbound buffering, backpressure, acknowledgement and session fencing

`maximumBufferedMessages` configures the bounded queue between MQTTnet callbacks and canonical TAG processing.

The EliteSCADA queue uses one canonical reader, multiple possible callback writers, `BoundedChannelFullMode.Wait`, and cancellation of blocked writers during disconnect/dispose. It intentionally avoids `DropOldest` and `DropNewest`.

The MQTTnet regular client has a separate internal PUBLISH dispatch queue implemented with an unbounded concurrent queue before application callbacks. A blocked EliteSCADA callback therefore does not, by itself, prove that all process-level inbound buffering is bounded. For MQTT 5 connections, EliteSCADA advertises `Receive Maximum = min(maximumBufferedMessages, 65,535)`. Because automatic QoS 1/2 acknowledgement is disabled until EliteSCADA queue admission, this constrains the broker-side unacknowledged QoS 1/2 inflight window and limits how much QoS 1/2 work can accumulate ahead of the callback. It does not constrain QoS 0 traffic, and MQTT 3.1.1 has no Receive Maximum property. Sustained-rate memory/backpressure behavior therefore remains a mandatory live validation item for those paths.

For QoS 1/2, automatic MQTTnet acknowledgement is disabled in the callback. EliteSCADA acknowledges only after successful bounded-queue admission. If admission is interrupted during disconnect/dispose, processing is marked failed and no ACK is issued.

Before `ReadOnlySequence<byte>.ToArray()` creates an EliteSCADA-owned payload copy, the callback checks MQTTnet payload length against `maximumInboundPayloadBytes`. Oversized traffic:

- creates no EliteSCADA-owned payload byte-array copy;
- stops new inbound admission for that transport session;
- marks QoS 1/2 processing failed and does not acknowledge;
- queues a permanent transport error;
- causes the runtime to fault instead of reconnecting forever into the same retained/oversized message.

This protects the EliteSCADA application allocation boundary. MQTTnet still necessarily receives/observes the network packet. MQTT 5 Maximum Packet Size is not conflated with the application payload policy, and MQTT 3.1.1 has no equivalent advertised property.

The ACK boundary is deliberately queue admission, not canonical TAG transaction completion. Process failure after admission but before TAG processing can still produce application-level replay/loss according to broker/session/QoS behavior. Packet identifiers are not treated as durable event identity and no application deduplication is invented from them.

Transport connection generations fence asynchronous callbacks across reconnect. Every installed application-message/disconnect handler captures the generation of the session that installed it. Disconnect removes current handlers and invalidates the generation; reconnect installs fresh handlers with a new generation. If an invocation list from the old session was already captured by MQTTnet and executes later, its old generation cannot be relabeled as current-session traffic. For QoS 1/2 the stale callback fails processing without acknowledgement.

The EliteSCADA queue is bounded by message count and each individual payload has a separate maximum. There is no separate aggregate-byte memory quota yet, and the MQTTnet internal queue caveat above means `maximumBufferedMessages` must not be represented as a universal process-memory cap.

## Engineering Import/Export

MQTT uses the existing `IEngineeringExchangeService` path and remains GUI-independent.

Covered paths:

- JSON parse/export;
- Preview/Apply;
- Data Source CSV export/import including Settings and SecretReferences;
- TAG CSV export/import including Address and MQTT metadata;
- public package/revision-compatible DTO representation.

Freshness and inbound-buffer settings round-trip through the same canonical maps. No plaintext password/private key is introduced by MQTT-specific persistence.

## Runtime composition

`MqttEngineeringCompiler` compiles canonical Engineering into `MqttRuntimePlan` objects.

`MqttRuntimeFactory` composes plans into `MqttDriver` instances using canonical `ICurrentTagCache`, canonical `ITagRegistry`, an injected MQTT transport factory and an optional host-owned credential resolver adapter.

MQTTnet types remain behind `IMqttClientTransport`; they do not enter canonical Engineering, TAG identity or runtime plans.

The protocol-owned compiler/factory pair is intentionally ready for later adaptation to Coordinator-reserved common runtime planner/factory contracts. This branch does not add MQTT switches to the shared coordinator.

## Convergence v1 alignment

- runtime composition remains protocol-owned and library-independent at the plan/factory boundary;
- no private shared Driver registry/module loader is introduced;
- canonical identity remains Data Source `Source` + exact topic in TAG `Address`;
- protected authentication remains `SecretReferences` plus injected resolver/lease semantics;
- readiness is explicit evidence, not inferred from TAG quality;
- ordinary scalar writes remain `WriteAsync(tagId, value)`;
- real source timestamps are preserved only when explicitly mapped;
- freshness is local monotonic receive/acceptance evidence rather than source-clock ordering;
- shared readiness, credential resolution, rich binding DTOs, runtime registry and current-vs-history ingress policy remain Coordinator-owned.

## Module / dependency / license manifest

| Item | MQTT Driver 10 status |
| --- | --- |
| Proposed stable module/package ID | `EliteSCADA.Driver.Mqtt` |
| Driver type(s) provided | `mqtt.raw` |
| Driver contract version | `1` |
| Public configuration schema | `elitescada.driver.mqtt.raw` v1 |
| Acquisition mode | Event-driven |
| Runtime capabilities | Read, Write, Subscribe, Diagnostics, SourceTimestamp |
| Engineering capabilities | None in descriptor; compilation/import/export use canonical host services plus MQTT-owned adapters |
| External runtime dependency | `MQTTnet` NuGet `5.2.0.1603` |
| Runtime platform | .NET 10 plus platform TCP/TLS stack |
| Third-party license classification | MQTTnet MIT |
| Commercial-license requirement | None identified for MQTTnet/raw MQTT |
| Production distribution status | **BLOCKED ON LIVE EVIDENCE / COORDINATOR INTEGRATION**, not on MQTTnet commercial licensing |
| Hardware/vendor simulator requirement | No protocol hardware required; broker implementations are interoperability targets |

Production integration still requires live broker evidence and Coordinator-owned common seams. Exact-head CI evidence and mainline drift are intentionally maintained in Draft PR #128 rather than duplicated as a volatile statement here.

## Automated test coverage authored

Deterministic suites include:

- `MqttPayloadCodecTests`
- `MqttDriverTests`
- `MqttEngineeringCompilerTests`
- `MqttEngineeringExchangeTests`
- `MqttEventDrivenReadinessTests`
- `MqttReadinessEvidenceTests`
- `MqttRuntimeFactoryTests`
- `MqttCredentialLifetimeTests`
- `MqttFreshnessAndBufferTests`
- `MqttTransportSafetyTests`
- `MqttLifecycleConcurrencyTests`
- `MqttReconnectBackoffTests`
- `MqttPointValidationTests`
- `MqttProtocolTextValidationTests`
- `MqttWorkerSupervisionTests`
- `MqttTransportGenerationTests`
- `MqttTransportDisconnectCancellationTests`

Opt-in live broker suites:

- `MqttBrokerIntegrationTests`
- `MqttBrokerShutdownRedeliveryTests`

Deterministic coverage includes exact topics, protocol UTF-8 text validation, typed payloads, strict timestamps, canonical JSON Pointer indices, retained semantics, malformed payload isolation, write typing, Engineering compile/exchange, secret handling and zeroization, freshness, bounded-buffer validation, MQTT 5 Receive Maximum wiring, lifecycle concurrency, disconnect cancellation/failure recovery, reconnect jitter, readiness, sibling-worker supervision, and stale callback generation fencing across reconnect.

The live broker harness covers MQTT 5/3.1.1, QoS 0/1/2, retained delivery, bounded bursts and persistent-session QoS 1/2 redelivery when shutdown interrupts full-queue admission. It is deliberately opt-in. Without `ELITESCADA_MQTT_INTEGRATION_HOST`, those tests return without opening a broker connection and are not interoperability evidence.

The local/container environment used by this chat does not provide .NET 10 or a local MQTT broker. GitHub Actions has provided deterministic build/test/smoke/E2E evidence for prior exact branch checkpoints, including CI #831 for parent head `e0d859e1c60c3725a7787916bcc9654607b054ab`. Moving exact-head evidence after later commits is recorded in Draft PR #128. No named live broker result has been recorded yet.

## Shared decisions required before central runtime integration

### 1. Host-owned secret resolver

The public Engineering model stores secret references correctly, but the common DriverHost resolver contract remains Coordinator-owned. MQTT supplies only the narrow adapter needed to consume such a service.

### 2. Common readiness adapter

`MqttDriver` exposes MQTT-specific `IMqttReadinessEvidenceSource`. The common DriverHost activation/readiness contract remains Coordinator-owned and should adapt this evidence rather than make the MQTT-local interface host-wide.

## Validation still required

Before production integration, execute the live contract against at least two independent broker implementations where practical.

1. Eclipse Mosquitto
   - MQTT 5 and MQTT 3.1.1;
   - TCP and trusted TLS;
   - username/password authentication through final host secret resolution;
   - QoS 0/1/2 and retained messages;
   - bounded-queue burst recovery;
   - MQTT 5 QoS 1/2 sustained-rate memory behavior with advertised Receive Maximum;
   - MQTT 5 QoS 0 sustained-rate memory behavior, which is not constrained by Receive Maximum;
   - MQTT 3.1.1 sustained-rate memory behavior for QoS 0/1/2, which has no Receive Maximum property;
   - persistent-session full-queue QoS 1/2 shutdown/redelivery;
   - broker restart and unclean network interruption;
   - malformed and oversized normal/retained payload behavior;
   - process termination after queue admission but before canonical TAG processing;
   - freshness timeout and recovery under real traffic.

2. Independent implementation such as HiveMQ Community Edition
   - same protocol/QoS/retained contract;
   - connection/session interoperability;
   - TLS hostname/chain failures;
   - bounded-burst/backpressure interoperability;
   - sustained-rate memory observation across MQTT 5 QoS 1/2, MQTT 5 QoS 0 and MQTT 3.1.1;
   - persistent-session redelivery;
   - oversized-payload and unclean reconnect behavior.

Deployment-specific cloud/vendor broker validation should be added when a concrete target is selected.

## Explicitly outside this slice

- Sparkplug B semantics;
- WebSocket transport;
- dynamic wildcard-to-TAG creation;
- fake hierarchical MQTT browsing;
- binary-layout payload schemas;
- MQTT 5 User Property source-time mapping;
- topic observation UI;
- manufacturer-specific topic heuristics;
- mTLS/client certificates and custom trust-store references.

Those features require explicit scope and must not silently change raw MQTT semantics.