# EliteSCADA MQTT Industrial Data Source / Driver Research

Status: **RESEARCH IN BRANCH / PRODUCTION NOT IMPLEMENTED**  
Date: 2026-08-27  
Assignment: `DEV 1 - EliteSCADA` / `research/mqtt-industrial-driver`

This document is a non-production architecture and Engineering research spike for a future EliteSCADA MQTT Data Source/driver. It does **not** add a production MQTT dependency, register an MQTT Data Source, change the canonical Engineering schema, alter DriverHost/DI/API/frontend composition, or authorize production MQTT implementation before the product gate is reopened.

The purpose is to turn MQTT protocol semantics into an EliteSCADA-specific implementation direction while preserving the platform boundaries already merged in `main`:

`Data Source -> owning source/driver -> TAG Engine/current cache -> EventBus/Historian/Gateway`

The canonical public/versioned Engineering model remains authoritative. Runtime protocol objects, broker sessions, library-specific client classes, observed topics and broker metadata must never become a private second project model.

---

## 1. Executive recommendation

1. Treat **MQTT 5.0 as the preferred/default protocol mode** for the first production driver because it provides materially better session control, reason codes, subscription options, flow control, Topic Aliases, Message Expiry and diagnostics.
2. Preserve **explicit MQTT 3.1.1 compatibility** in the same architectural adapter. Do not silently downgrade a configured MQTT 5 Data Source to 3.1.1 because session and subscription semantics differ and silent fallback hides compatibility/security errors.
3. Model one initial raw-MQTT EliteSCADA Data Source as **one configured broker endpoint/session identity**. Multiple brokers or intentionally independent sessions use multiple Data Sources and therefore naturally inherit EliteSCADA's existing per-Data-Source failure isolation and diagnostics.
4. Keep **raw MQTT and Eclipse Sparkplug B as distinct public protocol/profile semantics**. They may share an internal MQTT transport adapter later, but Sparkplug adds namespace rules, Protobuf payloads, Birth/Death certificates, metric aliases, sequence/state rules and Host/Edge roles that must not silently alter arbitrary raw MQTT topic semantics.
5. For raw MQTT, keep TAG Engineering **fixed and explicit**. Wildcard subscriptions may optimize transport or assist observation/import, but a runtime wildcard must not dynamically create authoritative TAGs.
6. Make first raw-MQTT payload mapping deliberately bounded: UTF-8 scalar/text, JSON with deterministic field extraction, and explicitly declared binary layouts. Unsupported payloads become visible Engineering/runtime issues rather than guessed conversions.
7. Treat **MQTT QoS as message-delivery semantics, never as SCADA TAG quality**. A connected broker and QoS 1/2 delivery do not prove that a process value is current, correctly decoded, physically valid or fresh.
8. Define point quality from successful mapped-message decoding, source/freshness policy and communication state. Retained messages require special treatment because MQTT's Retain flag does not prove when a process value was measured.
9. Provide honest Engineering assistance through **manual configuration, bounded Observe Topics, and topic-template import**. MQTT has no standard OPC-UA-style address space browser. An observed topic list is partial transient evidence, not project truth.
10. Use the existing protocol-neutral communication diagnostics contract. MQTT-specific metrics may be exposed through sanitized `ProtocolDetails`, but the driver must not fabricate polling scan/cycle/latency concepts that are not meaningful for event-driven pub/sub.
11. Use the normal TAG write path for MQTT publishes. Gateway routes remain protocol independent: a Gateway destination that happens to be owned by MQTT simply delegates through the owning MQTT Data Source.
12. Evaluate **MQTTnet** as the strongest first managed .NET laboratory candidate and **HiveMQ MQTT Client for C#** as a useful second interoperability/library candidate, especially for MQTT 5/Sparkplug exploration. This research does not decree a production dependency.
13. Use **Eclipse Mosquitto** as the first lightweight CI broker candidate and at least one independent implementation such as **HiveMQ Community Edition** for interoperability. Treat EMQX as optional lab coverage with its current BSL licensing implications reviewed explicitly.
14. When Sparkplug work is eventually authorized, begin with an EliteSCADA **Host Application/consumer role**, where NBIRTH/DBIRTH establish metric identity/state and NDATA/DDATA update those metrics. Writable NCMD/DCMD behavior should be a later explicit write slice.

---

## 2. EliteSCADA contracts that constrain MQTT

### 2.1 Driver type and Data Source remain distinct

Current EliteSCADA architecture distinguishes:

- **Driver type**: protocol/runtime implementation;
- **Data Source**: one concrete configured communication instance;
- **TAG**: one Engineering point owned by exactly one Data Source in an active revision.

MQTT must follow the same rule. A future driver type such as `mqtt.raw` is not one global broker connection for the entire SCADA server. Several MQTT Data Sources may connect simultaneously to different brokers, or intentionally to the same broker using independent session identities and mapping policies.

This has practical consequences:

- counters and reconnect state are isolated per Data Source;
- one broker failure cannot contaminate another broker's TAGs;
- the same topic string on two Data Sources represents two different communication contexts;
- Gateway routing resolves through TAG ownership, not topic strings or broker identities;
- canonical import/export must include the Data Source and TAG binding configuration without serializing resolved secrets.

### 2.2 Existing common communication diagnostics are the authority

`CommunicationDriverDiagnosticSnapshot` already provides protocol-neutral fields for:

- Data Source identity;
- driver type/runtime instance ID;
- sanitized endpoint;
- Healthy/Degraded/Reconnecting/Faulted lifecycle states;
- success/failure timestamps;
- data age;
- counters;
- response timing fields where meaningful;
- TAG-quality aggregation;
- sanitized protocol details.

MQTT should extend this surface rather than create a separate private diagnostic model.

Some common fields are not naturally meaningful to MQTT. In particular, raw pub/sub normally has no periodic polling scan. A future MQTT implementation should leave `ConfiguredScanInterval` and `LastScanDuration` null unless a separately engineered application protocol genuinely defines a scan. It must not manufacture fake polling cycles merely to populate a dashboard.

### 2.3 Gateway remains TAG-to-TAG

The merged Gateway architecture is intentionally protocol independent:

`Source TAG -> Gateway route -> Destination TAG`

A future writable MQTT TAG participates exactly like another writable source/provider:

1. Gateway resolves the destination TAG;
2. the normal owning-provider write boundary is invoked;
3. MQTT driver encodes/publishes according to that TAG's binding;
4. MQTT-specific publish failures remain driver/write diagnostics;
5. source TAG quality is not corrupted by a destination MQTT publish failure.

There must never be a `ModbusToMqttGateway` or direct concrete-driver call path.

### 2.4 Engineering import/export remains canonical

MQTT Data Source and TAG-binding configuration must eventually participate in the normal workflow:

`parse -> validate -> preview -> choose merge mode -> apply`

Observed topics, payload samples and broker browsing/monitoring state are transient Engineering aids. They do not become canonical project configuration until converted to deterministic candidates and applied through the normal Engineering model.

Secrets remain references. Passwords, private keys and resolved certificate material must never appear in canonical JSON, diagnostics or PR/test fixtures intended for production-like use.

---

## 3. MQTT 3.1.1 versus MQTT 5.0

### 3.1 Recommendation

First production direction:

- **preferred/default:** MQTT 5.0;
- **compatibility mode:** MQTT 3.1.1;
- **persisted protocol selection:** explicit per Data Source;
- **silent runtime downgrade:** rejected.

An Engineering connection-test tool may later probe broker compatibility and offer a deliberate configuration change, but Active Runtime semantics should remain deterministic from Engineering.

### 3.2 Why MQTT 5 is the preferred mode

MQTT 5 retains the core publish/subscribe model while adding features that are particularly useful for an industrial driver:

- Clean Start separated from Session Expiry;
- richer CONNACK/DISCONNECT/PUBACK/SUBACK reason codes;
- reason strings and User Properties;
- Message Expiry Interval;
- Topic Alias;
- Receive Maximum / flow control;
- maximum packet/topic-alias/server capability information;
- subscription options including No Local, Retain As Published and Retain Handling;
- more explicit server/client limits and failure diagnostics.

These make it easier to distinguish authentication rejection, unsupported QoS, subscription failure, packet-size constraints, normal disconnect and transient transport failure instead of collapsing everything into a generic socket error.

### 3.3 MQTT 3.1.1 compatibility

MQTT 3.1.1 remains common in industrial devices and gateways. It has persistent-session behavior through `CleanSession=0`, but that flag combines concerns that MQTT 5 separates.

With MQTT 3.1.1:

- a stable Client ID is essential when `CleanSession=0` is used;
- subscriptions and pending QoS 1/2 state may survive disconnection as session state;
- retained messages are broker topic state and are **not** part of the client session;
- diagnostic reason detail is much poorer than MQTT 5;
- No Local and several MQTT 5 subscription controls are unavailable.

The driver should expose version-appropriate settings rather than pretending one set maps losslessly to both versions.

### 3.4 Proposed future configuration shape

The exact canonical schema is coordinator-owned and intentionally not changed here. Research direction for one raw MQTT Data Source:

```text
protocolVersion: Mqtt5 | Mqtt311
transport: TcpTls | Tcp | WebSocketTls | WebSocket
host
port
serverName / expected TLS host where needed
clientId
keepAliveSeconds
connectTimeoutMilliseconds
reconnect:
  minimumDelayMilliseconds
  maximumDelayMilliseconds
  jitter
session:
  MQTT5:
    cleanStart
    sessionExpirySeconds
  MQTT311:
    cleanSession
limits:
  maximumInboundPayloadBytes
  optional application queue/backpressure bounds
tls:
  enabled
  validation/trust policy references
subscriptionDefaults:
  qos
  MQTT5 retain handling/options where applicable
```

Likely `secretReferences` or protected references:

- password;
- client private key / client certificate identity for mTLS;
- protected trust-store/CA reference when deployment policy requires it;
- other broker credentials/tokens introduced by future authentication schemes.

Username may be non-secret in many systems but must still be treated as configuration that can reveal topology/account identity. Password and key material are always secret.

---

## 4. Connection, TLS, mTLS and fail-closed security

### 4.1 Transport support order

Recommended first runtime slice:

1. TCP + TLS;
2. TCP without TLS only when deliberately configured for a trusted network and visibly marked insecure;
3. WebSocket/WSS only when a concrete deployment requirement justifies it.

WebSocket support is common in MQTT libraries but is not required merely because browsers use it. EliteSCADA's server-side driver has no reason to route normal plant MQTT traffic through WebSocket by default.

### 4.2 TLS rules

Production direction must be fail closed:

- validate certificate chain according to configured trust policy;
- validate hostname/server identity;
- surface certificate expiry/not-yet-valid/chain/hostname failures clearly;
- do not expose a permanent `accept any certificate` convenience mode;
- certificate trust changes are administrative/Engineering-sensitive actions;
- resolved private keys never enter diagnostics or Engineering JSON.

A test fixture may use a generated CA or deliberately trusted test certificate. Disabling validation globally is not an acceptable production design.

### 4.3 mTLS

For client-certificate authentication:

- Engineering stores only stable certificate/key references;
- deployment/security layer resolves them at the protected boundary;
- diagnostic UI may show non-secret certificate identity/thumbprint/expiry where appropriate;
- private key bytes are never returned through diagnostics;
- certificate rollover should be testable without deleting/recreating TAG Engineering.

### 4.4 Reconnect policy

Reconnect belongs to the EliteSCADA driver lifecycle rather than an opaque endless loop hidden inside a library.

Recommended behavior:

- bounded exponential or staged backoff;
- jitter to avoid restart storms across many Data Sources;
- immediate transition to `Reconnecting` after a previously healthy connection is lost;
- permanent configuration/authentication/TLS failures escalate to `Faulted` after a bounded policy rather than hammering the broker indefinitely;
- reconnect resets/re-establishes subscriptions according to negotiated session state;
- session-present state is observed rather than assumed;
- counters and reasons remain available for diagnostics.

---

## 5. MQTT delivery semantics versus SCADA quality

### 5.1 QoS 0

QoS 0 is **at most once** delivery. No MQTT-level acknowledgement guarantees retransmission.

Appropriate uses can include high-rate, disposable telemetry where the next update supersedes a lost sample. It should not be the universal default for plant values merely because it is cheaper.

### 5.2 QoS 1

QoS 1 is **at least once** delivery. Duplicates are allowed by the protocol.

This is a strong default candidate for ordinary process telemetry and writes because it balances reliability and overhead, but it does not make an application event exactly-once and it does not make a value `Good` by itself.

The driver/historian must not treat MQTT Packet Identifier as a globally stable application event ID. Packet identifiers are protocol/session-local. When true semantic deduplication matters, the publishing application should provide a stable message/event ID or source timestamp/sequence in payload/User Properties.

### 5.3 QoS 2

QoS 2 provides the strongest MQTT protocol delivery handshake and is described as exactly once at the MQTT delivery level. It has greater state/round-trip overhead and should be enabled deliberately where endpoints/brokers prove support and the application requires it.

It still does not prove that a remote PLC or business process performed an action after a command payload was delivered.

### 5.4 Ordering and duplicates

Within the protocol's ordered flows, MQTT preserves defined ordering constraints, but reconnects/retransmission and concurrent inflight QoS traffic can produce duplicate observations and unintuitive sequences at the application layer.

MQTT 5 `Receive Maximum=1` can deliberately limit concurrent QoS 1/2 flow when stronger ordering simplicity is worth throughput, but duplicates are still not converted into an application-level exactly-once process guarantee.

### 5.5 Critical rule: QoS != TAG quality

EliteSCADA must never map:

- QoS 0 -> Bad;
- QoS 1 -> Good;
- QoS 2 -> Excellent;

or any similar fiction.

A TAG should become `Good` only after the configured topic/binding accepted a structurally valid message, type conversion succeeded, freshness policy passed, and the value is usable according to EliteSCADA semantics.

A TAG may become `Stale` because it has not received an update within an engineered period even while the broker TCP connection is perfectly healthy.

A malformed JSON field can make one TAG bad/configuration-invalid while other TAGs from the same MQTT Data Source remain healthy.

A broker disconnect can eventually drive affected points to `BadCommunication` according to the configured grace/freshness policy, while TAGs on a different MQTT Data Source remain untouched.

---

## 6. Retained messages, freshness and source time

### 6.1 Retained is not historical freshness

A retained MQTT message is the broker's currently retained value for a topic. The Retain flag does not tell EliteSCADA when the physical process value was measured.

A subscriber can therefore receive a syntactically valid retained process value immediately after connection even when that value was produced hours, days or months earlier.

### 6.2 Recommended safe retained policy

For industrial process TAGs, recommended default:

- record that the incoming message was retained;
- if a configured source timestamp can be extracted and passes freshness rules, the value may become `Good`;
- if no trustworthy source timestamp exists, treat retained initial data conservatively, for example `Stale` or `Uncertain`, until a live message arrives;
- permit an explicit engineer-configured policy to trust retained values as current only when that is truly the publisher contract.

The exact quality chosen between `Stale` and `Uncertain` should align with the final common quality policy, but silent `Good` should not be the universal default.

### 6.3 MQTT 5 Message Expiry

Message Expiry can reduce delivery of old queued/retained application messages when publishers and brokers use it correctly. It is useful but is not an industrial source timestamp.

It cannot replace an explicit application timestamp when the SCADA must know when a process sample was measured.

### 6.4 Proposed timestamp mapping

Raw MQTT itself provides no standard process source timestamp. Binding profiles may therefore support:

1. `receivedAtUtc` as the server receive timestamp;
2. optional source timestamp extracted from JSON using a configured JSON Pointer;
3. optional MQTT 5 User Property mapping when a known publisher contract defines one;
4. later binary-layout timestamp extraction only through an explicit schema.

Configuration should include accepted format/unit/timezone rules where ambiguity exists.

If a source timestamp is marked required and cannot be parsed, the affected TAG should show an explicit mapping/configuration failure rather than falling back silently to receive time.

---

## 7. Topic names, filters and wildcard policy

MQTT topic names are case-sensitive strings. Topic filters may use wildcard levels:

- `+` matches one topic level and occupies an entire level;
- `#` matches zero or more levels and must be the final filter level;
- topic names themselves do not contain wildcards;
- topics beginning with `$` have special wildcard matching behavior and should not be assumed to match a root wildcard in the same way as ordinary application topics.

### 7.1 Authoritative TAG binding recommendation

First production raw MQTT TAG bindings should resolve to deterministic concrete input identity.

Preferred model:

- TAG stores a concrete topic name plus payload extractor, **or**
- a defined shared filter is part of a binding profile, but the specific TAG also carries deterministic discriminator/extraction rules that identify exactly which received messages can update it.

Do **not** let a wildcard subscription dynamically create TAGs in Active Runtime. That would make broker traffic a private mutable Engineering model and would bypass Preview/Apply/revision/history semantics.

### 7.2 Transport subscription optimization

The runtime compiler may group many fixed TAG bindings into efficient broker subscriptions. For example, several concrete topics under a known hierarchy may be covered by a safe wildcard subscription internally.

That is a transport optimization only. It must not change canonical TAG identity or cause unexpected topics to become engineered points.

### 7.3 Shared subscriptions

MQTT shared subscriptions are designed to distribute messages among subscribers in a shared group. They are valuable for horizontally scaled consumers but violate the normal assumption that each configured SCADA Data Source receives every update for its TAGs.

Recommendation: exclude shared subscriptions from the first raw SCADA driver slice unless an explicit clustered-runtime architecture defines ownership/failover semantics. They must not be accidentally accepted as ordinary topic filters.

---

## 8. Payload mapping to EliteSCADA TAGs

Raw MQTT deliberately says almost nothing about payload schema. EliteSCADA therefore needs explicit mapping contracts rather than protocol guesses.

### 8.1 Profile A: UTF-8 scalar/text

Initial support should cover deterministic parsing into current scalar TAG types:

- Boolean;
- signed integer types;
- Float/Double;
- String;
- DateTime only when an explicit accepted format is configured;
- Enum where an explicit numeric/text mapping exists.

Use invariant/culture-independent parsing for wire data. UI locale must not affect protocol decoding.

Whitespace, decimal separators, Boolean spellings and invalid ranges should have documented rules. A value outside the target type range is a conversion error, not silent truncation.

### 8.2 Profile B: JSON

A single JSON message commonly carries multiple process values. One topic may therefore update several fixed TAGs.

Recommended extraction syntax: **RFC 6901 JSON Pointer** or an equivalently small deterministic path model.

Example:

```json
{
  "motor": {
    "speed": 1450.2,
    "running": true,
    "capturedAt": "2026-08-27T12:34:56.789Z"
  }
}
```

Bindings may map:

```text
Topic: plant/line1/motor7/state
/speed       -> LINE1/MOTOR7/SPEED
/running     -> LINE1/MOTOR7/RUNNING
/capturedAt  -> source timestamp mapping
```

One malformed/missing field should affect only the TAGs that depend on it. Successfully extracted sibling values should remain usable.

Do not embed arbitrary JavaScript/Python expressions into the first MQTT payload mapper. Complex transformation belongs to the future expression/scripting subsystem or explicit reusable mapping contracts.

### 8.3 Profile C: binary

Binary payload support must be explicitly declared. Never decode unknown bytes by calling `ToString()` or assuming a machine-native layout.

First bounded binary profile could declare:

```text
valueKind
offset
length
endianness
encoding / numeric representation
optional scale
```

Examples include fixed-width signed/unsigned numbers, IEEE-754 floating values, Boolean bit/byte layouts and bounded text encodings.

Variable proprietary binary protocols, Protobuf schemas other than an explicit future Sparkplug subsystem, compressed payloads or cryptographic application envelopes should be rejected until a dedicated schema/plugin exists.

### 8.4 Content type and MQTT 5 properties

MQTT 5 Content Type and Payload Format Indicator are useful hints and should be exposed in Engineering observation/diagnostics where present. They are not sufficient authority to invent a payload schema automatically.

A publisher saying `application/json` can justify proposing a JSON preview, but TAG creation still requires deterministic selected mappings and Preview/Apply.

### 8.5 Payload limits and backpressure

Every Data Source should have a bounded accepted payload size and bounded internal delivery queue/backpressure policy.

A broker or publisher must not be able to exhaust the SCADA process with unbounded oversized messages or an event burst. Repeated oversized/undecodable payloads should increment protocol diagnostics and affect only relevant mappings/Data Source health according to policy.

---

## 9. Writable TAG -> MQTT publish semantics

### 9.1 Explicit publish binding

A writable raw-MQTT TAG needs a deterministic publish definition:

- exact topic name, never a wildcard;
- payload encoder/profile;
- publish QoS;
- retain flag;
- optional MQTT 5 properties only when explicitly configured;
- optional response/confirmation mapping when the application protocol defines one.

### 9.2 Retained commands are dangerous by default

Default command/write publish policy should be `retain=false`.

A retained command can be replayed automatically to a later subscriber, which is hazardous for process actions. If retained writes are ever permitted, Engineering should require explicit opt-in and strong warning/validation rather than inheriting the retained policy of telemetry topics.

### 9.3 Publish acknowledgement is not process acknowledgement

Critical distinction:

- PUBACK/PUBCOMP indicates MQTT protocol delivery progress;
- broker acceptance indicates broker handling;
- neither proves that a downstream PLC, actuator or edge application executed the requested process change.

If an application needs semantic command acknowledgement, the protocol contract must define a response topic/message ID/state echo and EliteSCADA should model that separately.

### 9.4 Self-echo and No Local

MQTT 5 subscription `No Local` can suppress messages published by the same client from being delivered back on that subscription. It is useful but is not available as the same standard control in MQTT 3.1.1 and is not a process acknowledgement mechanism.

The write/read mapping must remain correct even when a publisher echoes commands or publishes resulting state asynchronously.

### 9.5 Gateway interaction

A Gateway route targeting an MQTT TAG invokes the normal TAG write. The MQTT driver then encodes and publishes. No Gateway-specific MQTT option is needed.

Gateway validation should still reject read-only MQTT TAGs or invalid publish bindings before activation when possible.

---

## 10. Honest MQTT discovery and Engineering UX

### 10.1 MQTT has no OPC-UA-style browse

Raw MQTT defines topic-based publish/subscribe but no standard address-space browsing service that enumerates all application topics and schemas.

EliteSCADA must not present an observed topic list as a complete standard broker browse tree.

### 10.2 Manual topic configuration

Manual configuration is always available:

1. create/select MQTT Data Source;
2. enter concrete topic or mapping profile;
3. choose payload type/extractor;
4. choose freshness/QoS policy;
5. preview/test with a bounded Engineering connection;
6. create candidate TAGs;
7. validate/Preview/Apply.

### 10.3 Observe Topics tool

Recommended first discovery aid: **Observe Topics**.

Engineering opens a temporary protected client/subscription using the selected broker security context and a user-chosen bounded filter.

Guardrails should include:

- explicit manual start;
- explicit topic filter, not automatic `#` on a large plant broker without warning;
- bounded duration, for example a default 30-60 seconds;
- cancellation;
- cap on unique topics;
- cap on messages and total captured payload bytes;
- payload preview truncation;
- no resolved secrets in captured results;
- clear partial-result status.

For each observed concrete topic, show where available:

- topic;
- first/last seen;
- message count;
- QoS;
- retained flag;
- payload size;
- MQTT 5 Payload Format Indicator/Content Type/User Property names where safe;
- bounded sample payload preview;
- likely UTF-8/JSON classification only as a suggestion.

An inactive publisher will not appear. A topic never published during the observation window is invisible. Therefore the UI must label the result as **observed traffic**, never as a full broker namespace.

### 10.4 Topic template import

For well-defined publisher conventions, Engineering can let the user specify a template/filter such as:

```text
plant/+/motors/+/state
```

Then map captured topic segments into candidate path/name metadata while still producing fixed TAG candidates.

Example:

```text
plant/line1/motors/M07/state
  -> LINE1/M07/RUNNING
  -> LINE1/M07/SPEED
```

The template never creates arbitrary Active Runtime points outside Preview/Apply.

### 10.5 Broker-specific inventory APIs

Some brokers expose management/admin APIs that can report subscriptions, retained messages or recent topic activity. These are non-standard and may require stronger credentials.

They may later be implemented as broker-specific Engineering adapters, but they must never be described as standard MQTT discovery and must not be required by the runtime driver.

### 10.6 Candidate workflow

Recommended pattern, consistent with OPC UA research:

`manual/observe/template -> build transient candidates -> validate -> preview -> choose merge semantics -> apply`

Observed traffic does not mutate canonical Engineering.

---

## 11. Data age, stale policy and point isolation

### 11.1 Per-binding freshness

Pub/sub data has no natural scan period. Each TAG or reusable mapping profile should therefore support a `staleAfter`/maximum-data-age policy when continuous updates are expected.

Examples:

- fast process state: stale after 5 s;
- machine status: stale after 30 s;
- production counter only published on change: no short stale timer, or use a domain-specific longer threshold;
- event topics: not represented as a current-value TAG unless a clear current-state mapping exists.

### 11.2 Data Source data age

For MQTT, common `DataAge` should represent time since the latest successful **mapped TAG update** (or a documented aggregate equivalent), not merely time since a PINGRESP or socket packet.

A broker connection can remain healthy while publishers stop sending process data.

### 11.3 Parse failures remain isolated

One malformed JSON topic should not make every topic on that broker `BadCommunication`.

Recommended separation:

- transport/session failure -> Data Source communication degradation and affected TAG communication quality;
- subscription rejection -> affected subscription/TAG binding configuration failure;
- payload decode failure -> affected TAG(s) `BadConfiguration`/appropriate quality while transport may remain Healthy/Degraded;
- stale publisher -> affected TAGs Stale while broker connection may remain Healthy.

The aggregate Data Source state may become Degraded when a meaningful proportion of configured mappings are failing, but TAG quality remains authoritative per point.

---

## 12. Mapping MQTT to common driver diagnostics

### 12.1 Operational state

Suggested mapping:

**Starting**
- resolving endpoint/credentials;
- establishing transport/TLS;
- CONNECT/CONNACK and initial subscriptions pending.

**Healthy**
- broker session established;
- required subscriptions accepted;
- no material current transport/configuration failure;
- mapped message flow/freshness consistent with configured expectations.

**Degraded**
- connected but one or more subscriptions rejected;
- persistent payload/extraction failures;
- configured expected data has become stale;
- repeated publish failures while session still exists;
- negotiated limits force partial capability.

**Reconnecting**
- previously active session lost and reconnect/backoff is running.

**Faulted**
- invalid configuration;
- persistent authentication/authorization rejection;
- TLS trust/hostname/certificate failure;
- broker/protocol incompatibility that cannot recover without Engineering change.

### 12.2 Common counters

Reasonable use of existing `CommunicationDriverCounters`:

| Common counter | MQTT interpretation |
| --- | --- |
| `Connections` | successful MQTT session establishments |
| `Disconnections` | observed session/transport disconnects |
| `Reconnects` | successful or attempted reconnect cycles, exact convention must be locked and consistent |
| `Requests` | tracked protocol control/write operations such as connect/subscribe/unsubscribe/publish requiring completion; document exact counting |
| `SuccessfulOperations` | successful tracked operations |
| `FailedOperations` | failed/rejected tracked operations |
| `Timeouts` | connect/control acknowledgement/keepalive/write operation timeouts |
| `ReadOperations` | inbound Application Messages considered by mapping pipeline, if adopted consistently |
| `WriteOperations` | TAG-write-triggered publish attempts |
| `UpdatesPublished` | successful TAG updates emitted to the common runtime |
| `Cycles` | **not naturally meaningful** for event-driven MQTT; leave zero unless a future common definition is adopted |

Do not redefine `Cycles` as an arbitrary receive-loop iteration just to make the number nonzero.

### 12.3 Timing fields

Potentially meaningful:

- connect duration;
- subscribe/publish acknowledgement duration;
- rolling operation latency for tracked acknowledged operations.

Not meaningful as generic raw-MQTT polling concepts:

- configured scan interval;
- scan duration.

These should remain null rather than fabricated.

### 12.4 Sanitized protocol details

Useful `ProtocolDetails` candidates:

```text
negotiatedProtocolVersion
clientId
sessionPresent
transport/TLS mode
activeSubscriptionCount
inboundApplicationMessageCount
retainedMessageCount
duplicateFlagCount
payloadDecodeFailureCount
unmappedMessageCount
oversizedMessageCount
lastConnAckReason
lastDisconnectReason
negotiatedReceiveMaximum / server limits when MQTT5
outboundInflightCount
currentReconnectDelay
```

Never include passwords, bearer tokens, private keys, resolved secret values or full sensitive certificate private material.

### 12.5 Health is not liveness

`/health` remains service liveness/readiness. Detailed broker/topic/session state belongs in protected communication diagnostics.

---

## 13. Multi-broker and multi-Data-Source behavior

### 13.1 One Data Source, one initial broker/session identity

The safest first model is one Data Source -> one broker endpoint/session identity.

Advantages:

- clear diagnostics;
- clear retained/session semantics;
- deterministic subscriptions;
- simple ownership of TAGs and writes;
- independent reconnect/failure isolation.

### 13.2 Several brokers

Create several Data Sources.

Example:

| Data Source | Broker | Role |
| --- | --- | --- |
| `MQTT_LINE1` | `mqtts://broker-line1:8883` | line telemetry |
| `MQTT_UTILITIES` | `mqtts://utilities-broker:8883` | energy/water |
| `MQTT_CLOUD_BRIDGE` | `mqtts://cloud.example:8883` | selected external integration |

Failure of `MQTT_CLOUD_BRIDGE` must not change quality/counters of `MQTT_LINE1`.

### 13.3 Same topic on several brokers

`plant/motor7/state` on broker A and broker B are distinct communication identities because the owning Data Sources differ.

The topic string alone is not a globally stable TAG binding identity.

### 13.4 Broker HA/failover

A broker cluster already exposed through one stable DNS/VIP/load-balanced endpoint can appear as one broker endpoint to the client if the broker product guarantees compatible session/retained behavior.

Do not silently implement an arbitrary list of unrelated fallback brokers in the first raw driver. Retained data, persistent sessions and authorization may differ between them.

A future explicit endpoint-set/failover policy would need:

- broker identity expectations;
- session migration assumptions;
- retained-state reconciliation;
- deterministic diagnostics identifying the active endpoint;
- failover acceptance tests.

---

## 14. Raw MQTT versus Sparkplug B

### 14.1 They are not the same Engineering model

Raw MQTT defines transport/topic/delivery semantics but not a standard industrial metric namespace or payload model.

Sparkplug B adds an industrial application contract including:

- defined topic namespace;
- Protobuf payload schema;
- Edge Node, Device and Host Application roles;
- NBIRTH/DBIRTH certificates describing metrics;
- NDATA/DDATA updates;
- NDEATH/DDEATH/offline semantics;
- metric aliases;
- sequence and `bdSeq` state;
- Rebirth behavior;
- NCMD/DCMD command messages;
- Host/Application STATE awareness.

Therefore Sparkplug must not be a hidden toggle that silently changes how existing raw MQTT TAG bindings are interpreted.

### 14.2 Recommended architecture separation

Illustrative future public driver/profile types:

```text
mqtt.raw
mqtt.sparkplug.b
```

Final names belong to canonical schema/module design.

The two may share an internal tested MQTT transport layer for connection, TLS, MQTT versions, reconnect and base diagnostics. They should have separate binding compilers and Engineering UX because their identity/state models differ materially.

### 14.3 First Sparkplug role for EliteSCADA

Recommended initial target when Sparkplug implementation is explicitly authorized: **Host Application / SCADA consumer**.

The Host Application would:

1. connect to configured MQTT infrastructure;
2. observe Sparkplug Birth certificates;
3. build/validate transient metric candidates for Engineering import;
4. use stable metric names + Edge/Device identity as canonical binding identity;
5. use aliases only as current Sparkplug session optimization;
6. apply NDATA/DDATA updates to fixed engineered TAGs;
7. react to Death/session-state events by changing relevant metric quality/stale state;
8. request Rebirth when aliases/state are inconsistent according to Sparkplug rules.

### 14.4 Birth certificates and aliases

NBIRTH/DBIRTH establish the metrics available for an Edge Node/device session. Birth payloads carry metric names, types/current values and aliases.

Aliases are not safe as sole Engineering identity because they are compact session/message identifiers whose meaning is established by Birth state.

Stable future binding identity should include at least:

```text
group_id
edge_node_id
optional device_id
metric name/path
```

Current alias can be cached in runtime session state.

### 14.5 Death, bdSeq and stale state

NDEATH is configured through MQTT Will semantics and carries `bdSeq` correlation with NBIRTH. Sparkplug Host logic uses Birth/Death/session state to distinguish current online metric state from stale/offline state.

This is stronger industrial state semantics than raw MQTT, where a broker connection alone says nothing about an individual publisher's process freshness.

### 14.6 Commands

NCMD/DCMD should be treated as an explicit writable Sparkplug slice, not automatically enabled when read/import is added.

The same EliteSCADA safety rules apply:

- TAG must be explicitly writable;
- authorization stays backend enforced;
- command metric type/identity must match Engineering;
- transport acknowledgement does not equal device-process acknowledgement unless Sparkplug/application semantics provide an explicit state confirmation.

### 14.7 Sparkplug conformance testing

Sparkplug production work should use the current Eclipse specification and available Technology Compatibility Kit/conformance material as an acceptance target rather than testing only against one hand-written publisher.

---

## 15. Candidate .NET MQTT client libraries

Package/repository status below was checked on **2026-08-27**. Exact versions, licenses, advisories and transitive dependencies must be reviewed again immediately before any production dependency is added.

### 15.1 MQTTnet

Repository: `https://github.com/dotnet/MQTTnet`  
NuGet: `https://www.nuget.org/packages/MQTTnet/`  
License: MIT (`https://github.com/dotnet/MQTTnet/blob/master/LICENSE`)

Observed strengths:

- mature managed .NET implementation;
- MQTT 3.1.1 and MQTT 5 support;
- TCP, TLS and WebSocket support;
- current .NET targets include modern .NET / .NET 10 compatibility;
- active releases and broad ecosystem adoption;
- no need for a native C library in the normal client path;
- client APIs expose enough protocol behavior for EliteSCADA to own reconnect/subscription recovery and diagnostics.

Current package listing observed during this research showed MQTTnet **5.2.0.1603**, updated 2026-07-01. This is evidence of current maintenance, **not** a production pin recommendation.

Important architecture note: MQTTnet 5 no longer centers the old ManagedClient abstraction. That is acceptable, and arguably preferable, for EliteSCADA because reconnect/session/subscription restoration should be an explicit diagnosable driver lifecycle rather than hidden behind a black-box convenience loop.

**Research recommendation:** strongest first laboratory candidate.

### 15.2 HiveMQ MQTT Client for C#

Repository: `https://github.com/hivemq/hivemq-mqtt-client-dotnet`  
Documentation: `https://hivemq.github.io/hivemq-mqtt-client-dotnet/`  
License: Apache-2.0.

Observed strengths:

- modern managed C# client;
- explicit focus on MQTT 5;
- modern .NET including .NET 10;
- TLS/mTLS and TCP/WebSocket transports;
- backpressure/manual acknowledgement features useful for burst-control experiments;
- active maintenance;
- associated Sparkplug work can provide a useful comparison implementation.

Research caution:

- current documentation emphasizes MQTT 5 strongly; do not assume full MQTT 3.1.1 compatibility parity without a dedicated test;
- any Sparkplug extension must be assessed against Sparkplug specification/TCK rather than accepted merely because it comes from the same vendor ecosystem.

**Research recommendation:** second lab candidate, especially valuable for MQTT 5/Sparkplug comparison.

### 15.3 Eclipse Paho C

Repository: `https://github.com/eclipse-paho/paho.mqtt.c`

Observed strengths:

- mature Eclipse project;
- MQTT 3.x and MQTT 5 support;
- synchronous/asynchronous APIs;
- TLS variants;
- current maintenance, including a 1.3.16 release observed in 2026.

Trade-off for EliteSCADA:

- native C packaging/deployment increases complexity for an otherwise managed .NET 10 server;
- native binary architecture/security/update concerns would need a compelling interoperability reason.

**Research recommendation:** interoperability/reference candidate, not first managed production choice.

### 15.4 M2Mqtt and older clients

Repository examples such as `https://github.com/eclipse-paho/paho.mqtt.m2mqtt` remain historically important but did not show the same current MQTT 5/.NET 10 direction as the two managed candidates above during this research.

**Research recommendation:** do not select as first production candidate without new evidence.

### 15.5 Library decision criteria for implementation time

The actual production dependency review must score:

- MQTT 5 and 3.1.1 behavior required by product scope;
- TLS/mTLS and certificate validation hooks;
- reconnect/session/subscription control;
- backpressure and payload-size controls;
- reason-code/property exposure;
- cancellation and async behavior;
- thread-safety/lifecycle behavior;
- .NET 10 support;
- license/transitive dependencies;
- release cadence/security advisories;
- broker interoperability test results;
- packaging footprint;
- ability to expose diagnostics without library-private state leaking into public contracts.

---

## 16. Broker candidates for CI and laboratory testing

### 16.1 Eclipse Mosquitto

Project: `https://mosquitto.org/`  
Repository: `https://github.com/eclipse-mosquitto/mosquitto`  
License file: `https://github.com/eclipse-mosquitto/mosquitto/blob/master/LICENSE.txt`

Strengths:

- lightweight and straightforward to containerize;
- supports MQTT 5, 3.1.1 and older 3.1 compatibility;
- TLS and client-certificate authentication support;
- useful deterministic local CI fixture;
- permissive Eclipse/EDL licensing model suitable for test infrastructure review.

**Research recommendation:** default first CI broker candidate.

### 16.2 HiveMQ Community Edition

Repository: `https://github.com/hivemq/hivemq-community-edition`

Strengths:

- independent broker implementation for interoperability testing;
- MQTT 3.x/5 support;
- TCP/TLS/WebSocket options;
- Apache-2.0 project;
- container-friendly Java runtime.

**Research recommendation:** second CI/nightly interoperability broker candidate.

### 16.3 EMQX

Repository: `https://github.com/emqx/emqx`

Technical strengths include broad MQTT protocol support and a substantial broker feature set.

Licensing caution: current EMQX releases use **Business Source License 1.1** terms for relevant modern versions. Do not treat current EMQX as an Apache-licensed fixture by habit from older ecosystem history.

**Research recommendation:** optional interoperability/lab target after licensing/use restrictions are reviewed; not the default embedded/redistributed test fixture.

### 16.4 Real broker/device lab

CI containers cannot prove all industrial deployment behavior. Later acceptance should include at least one real production-class or cloud/edge broker setup covering:

- certificate rollover;
- network interruption/NAT/firewall behavior;
- unstable WAN latency/loss;
- burst loads and backpressure;
- broker restart/HA failover behavior;
- publisher devices/gateways with imperfect MQTT implementations;
- long-lived persistent sessions.

---

## 17. Recommended software and broker test matrix

### 17.1 Protocol/version

- MQTT 5 connection/subscription/publish;
- MQTT 3.1.1 compatibility mode;
- explicit configured-version mismatch;
- no silent downgrade.

### 17.2 Authentication/security

- valid TLS server certificate;
- unknown CA;
- hostname mismatch;
- expired/not-yet-valid certificate;
- valid mTLS client certificate;
- missing/untrusted client certificate;
- username/password success;
- bad password/authorization rejection;
- verify diagnostics contain no secret values.

### 17.3 Reconnect/session

- broker restart;
- short network interruption;
- persistent MQTT 5 session resume with Session Expiry;
- MQTT 5 Clean Start path;
- MQTT 3.1.1 `CleanSession=0` resume;
- MQTT 3.1.1 clean session;
- subscription restoration when session is not present;
- bounded reconnect backoff and jitter;
- permanent auth/TLS failure transitions to Faulted without rapid hammering.

### 17.4 Retained/freshness

- retained value received immediately after subscription;
- retained value with valid fresh source timestamp;
- retained value with old source timestamp -> stale policy;
- retained value without timestamp -> conservative initial quality;
- live update upgrades current point state;
- MQTT 5 Message Expiry where broker/client support is exercised.

### 17.5 QoS/duplicates/order

- QoS 0 normal stream and tolerated loss test;
- QoS 1 normal acknowledgement;
- QoS 1 duplicate delivery/reconnect path;
- QoS 2 handshake/interoperability;
- multiple inflight messages/order behavior;
- MQTT 5 Receive Maximum constrained case;
- prove duplicate protocol delivery does not create unsafe process-write assumptions.

### 17.6 Topic/filter mapping

- exact topic;
- `+` wildcard observation/template behavior;
- `#` wildcard behavior;
- `$` topic wildcard rule;
- case-sensitive topic distinction;
- two Data Sources using the same topic string remain independent;
- shared subscription rejected/not enabled in first slice.

### 17.7 Payload mapping

- UTF-8 Boolean/integer/float/string;
- range overflow rejected;
- invariant decimal parsing;
- JSON one topic -> several TAGs;
- missing JSON field isolated to dependent TAG;
- malformed JSON does not corrupt unrelated mappings;
- source timestamp extraction success/failure;
- binary little/big endian values;
- binary payload too short/offset error;
- unsupported binary schema rejected;
- oversized payload/backpressure.

### 17.8 Writes

- writable TAG -> exact topic publish;
- configured QoS 0/1/2;
- default `retain=false`;
- explicit retained write warning/policy;
- publish rejection/timeout;
- broker acknowledgement does not masquerade as remote device process ACK;
- self-echo behavior with/without MQTT 5 No Local;
- Gateway-owned write delegates through the same normal TAG write path once production MQTT is permitted.

### 17.9 Diagnostics and isolation

- two active MQTT Data Sources on different brokers;
- one broker failure leaves the other healthy;
- per-Data-Source counters isolated;
- data age reflects mapped TAG updates, not just PING traffic;
- stale TAG while connection remains Healthy/Degraded as policy dictates;
- malformed topic/payload affects only relevant TAGs;
- `ConfiguredScanInterval`/`LastScanDuration` remain null for raw pub/sub;
- no secrets in endpoint/protocol details.

### 17.10 Interoperability

At minimum:

- MQTTnet client + Mosquitto;
- MQTTnet client + HiveMQ CE;
- second C# client against both brokers for behavior comparison where useful;
- later industrial/cloud broker lab;
- mixed 3.1.1 and 5 publishers/subscribers where broker supports both.

---

## 18. Sparkplug-specific future test matrix

Only after a separate Sparkplug implementation slice is authorized:

- valid NBIRTH establishes all expected Edge metrics;
- DBIRTH establishes Device metrics;
- metric alias established by Birth and used by subsequent DATA;
- DATA alias received before valid Birth -> reject/request Rebirth, never guess identity;
- NDEATH/NBIRTH `bdSeq` transition;
- disconnect/reconnect and Rebirth recovery;
- sequence discontinuity handling;
- Host STATE behavior where required by current Sparkplug specification;
- NCMD/DCMD only for explicitly writable engineered metrics;
- Protobuf malformed payload isolation;
- metric datatype changes produce candidate/reconciliation issues, not silent coercion;
- Sparkplug TCK/conformance suite where available/applicable;
- independent Edge Nodes/devices remain isolated within the same broker/Data Source model chosen for the Sparkplug driver.

---

## 19. Proposed future production implementation slices

These are **future sequencing recommendations only**. This branch does not implement them.

### Slice 1: canonical MQTT configuration and binding contract

Coordinator-owned/shared Engineering work:

- versioned driver-owned MQTT Data Source schema;
- secret-reference fields;
- explicit protocol version/session/TLS/reconnect policy;
- raw MQTT TAG read/write binding DTO/schema;
- deterministic payload extractor/encoder representation;
- migration/JSON round-trip/Preview/Apply/package tests.

No transport runtime should precede a stable public contract.

### Slice 2: raw MQTT transport/runtime adapter

- selected reviewed .NET library dependency;
- MQTT 5 and 3.1.1 explicit modes;
- TLS/mTLS;
- connect/disconnect/reconnect/session management;
- subscription lifecycle;
- bounded queues/backpressure;
- common diagnostics.

Initially no Engineering observation UX is required for runtime correctness.

### Slice 3: read-only fixed TAG mappings

- exact topic bindings;
- scalar UTF-8;
- JSON extraction;
- explicit bounded binary profile;
- source/receive timestamps;
- retained/freshness/stale quality;
- independent per-TAG parse failure;
- multi-Data-Source acceptance.

### Slice 4: writable TAG/publish + Gateway acceptance

- explicit publish topic/encoder/QoS/retain=false default;
- normal authorization/write boundary;
- publish operation diagnostics;
- Gateway route to MQTT destination;
- destination failure isolation.

### Slice 5: Engineering Observe Topics/import

- temporary protected broker connection;
- bounded observation;
- candidate generation;
- topic-template/path mapping;
- Preview/Apply only;
- no runtime dynamic TAG creation.

### Slice 6: advanced MQTT 5 capabilities

As justified by real product needs:

- richer subscription options;
- Receive Maximum/flow control tuning;
- Topic Alias optimization;
- Message Expiry policy;
- reason-code diagnostics;
- server capability limits;
- optional WebSocket transport.

### Slice 7: Sparkplug B Host Application

Separate public profile/driver semantics sharing only safe lower transport infrastructure:

- Sparkplug namespace and Protobuf payload;
- Birth/Death/aliases/sequence/Rebirth;
- metric candidate import;
- fixed engineered metric mappings;
- Sparkplug state -> TAG quality;
- later NCMD/DCMD write slice.

### Slice 8: hardening/module packaging

Only after product validation gate and architecture permit:

- installable Driver Module packaging if selected;
- compatibility/version manifest;
- support diagnostics;
- broker/client interoperability matrix;
- long-duration soak/security testing.

---

## 20. INTEGRATION REQUIRED before production MQTT

The research is intentionally isolated. Production implementation requires coordinator-owned decisions/integration in these areas:

1. **Canonical Engineering schema**: MQTT Data Source settings and TAG bindings must become public versioned entities/configuration rather than library-private objects.
2. **Secret/certificate resolution**: define the exact protected deployment/service interface for username/password, private key, client certificate and trust references.
3. **Driver registration/runtime composition**: DriverHost/DI/compiler integration only when the external-protocol production gate is reopened.
4. **TAG binding abstraction**: current common TAG model must carry/validate driver-owned MQTT mapping without turning generic core contracts into MQTT-specific fields.
5. **Quality/freshness rules**: settle exact `Stale`/`Uncertain` behavior for retained initial values and stale publisher data consistently with the common quality model.
6. **Write authorization/Audit**: MQTT publish writes must use the same backend-enforced TAG/process-write security boundary as other writable Data Sources.
7. **Gateway acceptance**: validate MQTT destination writes through the owning provider, never direct Gateway-to-MQTT calls.
8. **Diagnostics API/UI**: expose MQTT through existing protected common diagnostics and sanitized protocol details.
9. **Engineering UX/localization**: Data Source editor, mapping editor and Observe Topics candidate flow require coordinator-owned frontend/localization integration.
10. **Import/export/package**: all MQTT configuration, except resolved secrets/runtime state, must round-trip through canonical JSON/revision/project package flows.
11. **Dependency/license/security review**: pin a reviewed library version only during production implementation and re-check advisories/license/transitive graph then.
12. **Sparkplug product identity**: decide final public driver/profile/module naming and lifecycle separately from raw MQTT rather than retrofitting a hidden mode later.

---

## 21. Risks and explicit non-goals

### Risk: topic traffic becomes accidental Engineering

Mitigation: observed/wildcard traffic only creates transient candidates. Active TAGs remain fixed canonical Engineering entities.

### Risk: retained value shown as fresh process truth

Mitigation: explicit retained/freshness policy; source timestamp mapping; conservative initial quality.

### Risk: QoS interpreted as data quality

Mitigation: keep delivery semantics and TAG quality separate in contracts, diagnostics and UI wording.

### Risk: command replay through retained publish

Mitigation: `retain=false` default for writes and explicit validation/warning for any retained command policy.

### Risk: broker is connected but publishers are dead

Mitigation: per-binding stale/data-age policy and aggregate diagnostics based on mapped updates, not socket-only liveness.

### Risk: wildcard subscription overload

Mitigation: bounded Observe Topics; fixed runtime Engineering; payload/queue limits; transport subscription optimization remains internal.

### Risk: reconnect storm across many Data Sources

Mitigation: per-instance bounded exponential/staged backoff with jitter and permanent-failure classification.

### Risk: Sparkplug aliases become canonical identity

Mitigation: persist group/edge/device/metric identity; alias remains session runtime state established by Birth.

### Risk: library choice leaks into public schema

Mitigation: public MQTT configuration/binding contracts remain library independent; adapter translates them to the selected client.

### Explicit non-goals for first raw MQTT driver

- MQTT broker/server implementation inside EliteSCADA;
- arbitrary dynamic TAG creation from wildcard traffic;
- generic event-stream exactly-once semantics;
- broker administration API standardization;
- shared-subscription clustered SCADA ownership;
- arbitrary scripting inside payload mappings;
- opaque proprietary binary decoding;
- automatic broker failover across unrelated endpoints;
- treating MQTT publish acknowledgement as PLC command acknowledgement;
- Sparkplug behavior hidden inside raw topic mode.

---

## 22. Research conclusion

MQTT fits EliteSCADA cleanly **only if it remains a source/provider behind the existing TAG boundary**.

The protocol's strengths are asynchronous pub/sub, broad ecosystem support, efficient one-to-many distribution and flexible broker topologies. Its weakness from a SCADA Engineering perspective is the deliberate absence of a standard process address space and payload model. EliteSCADA must therefore supply deterministic Engineering contracts for topic identity, payload extraction, freshness, writes and safe observation/import without pretending those features are part of raw MQTT itself.

The recommended first production architecture is:

```text
Canonical Engineering
    -> MQTT Data Source (explicit broker/session/security/version)
    -> fixed TAG bindings (topic + deterministic payload mapping)
    -> MQTT transport adapter
    -> TAG Engine / Current Cache / EventBus
    -> Historian / Alarms / Realtime / Gateway
```

with writes flowing back through:

```text
Authorized TAG write / Gateway destination write
    -> owning MQTT Data Source
    -> deterministic encoder
    -> exact-topic publish
```

MQTT 5 should be preferred, MQTT 3.1.1 supported explicitly, and QoS must remain a delivery contract rather than a counterfeit quality model.

Sparkplug B deserves its own future driver/profile semantics because it adds the stateful industrial metric model that raw MQTT intentionally lacks. Sharing a lower MQTT transport layer is sensible; sharing the public Engineering meaning is not.

No production MQTT implementation is authorized by this research document.

---

## 23. External references

External package/version facts below were checked on **2026-08-27**. Re-check exact versions, licenses and security status immediately before production implementation.

### MQTT specifications

- OASIS MQTT Version 3.1.1:  
  `https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/mqtt-v3.1.1.html`
- OASIS MQTT Version 5.0:  
  `https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html`

### Sparkplug

- Eclipse Sparkplug specification site:  
  `https://sparkplug.eclipse.org/specification/`
- Sparkplug Specification Version 3.0.0 PDF:  
  `https://sparkplug.eclipse.org/specification/version/3.0/documents/sparkplug-specification-3.0.0.pdf`

### Client libraries

- MQTTnet repository:  
  `https://github.com/dotnet/MQTTnet`
- MQTTnet NuGet:  
  `https://www.nuget.org/packages/MQTTnet/`
- MQTTnet license:  
  `https://github.com/dotnet/MQTTnet/blob/master/LICENSE`
- HiveMQ MQTT Client for C#:  
  `https://github.com/hivemq/hivemq-mqtt-client-dotnet`
- HiveMQ C# client documentation:  
  `https://hivemq.github.io/hivemq-mqtt-client-dotnet/`
- Eclipse Paho C:  
  `https://github.com/eclipse-paho/paho.mqtt.c`
- Eclipse Paho M2Mqtt:  
  `https://github.com/eclipse-paho/paho.mqtt.m2mqtt`

### Brokers

- Eclipse Mosquitto:  
  `https://mosquitto.org/`
- Eclipse Mosquitto repository:  
  `https://github.com/eclipse-mosquitto/mosquitto`
- HiveMQ Community Edition:  
  `https://github.com/hivemq/hivemq-community-edition`
- EMQX:  
  `https://github.com/emqx/emqx`

### Payload mapping reference

- RFC 6901 JSON Pointer:  
  `https://www.rfc-editor.org/rfc/rfc6901`
