# DRIVER-TRANSIENT-EVENT-ARCHITECTURE

Status: CHECKPOINT 1 / RESEARCH ONLY / DOCS ONLY

Issue owner: #546 — HOME-RESEARCH-05 — transient events + rich command binding architecture

Contract: C-DRIVER-TRANSIENT-RICH-COMMAND-RESEARCH-01

Order: DRIVER-TRANSIENT-EVENT-RICH-COMMAND-RESEARCH-01

Scope of this checkpoint: DRIVER-TRANSIENT-EVENT-01 only.

Canonical terminology:
- HA = High Availability.
- HAB = Home Assistant Bridge.
- Home Assistant = external platform by name.

No product code, schema, Driver SDK, Runtime event implementation, Gateway, frontend, tests, dependencies or CI are changed by this document.

## 1. Authority snapshot audited

The research branch and integration were revalidated live before this checkpoint:

- integration: wave15/corrections-integration
- audited integration HEAD: 77b08d60333685b4ba06aa649e3125f23b475dcf
- research branch: research/home-transient-events-rich-commands
- audited research HEAD before this document: 77b08d60333685b4ba06aa649e3125f23b475dcf
- merge-base before this document: 77b08d60333685b4ba06aa649e3125f23b475dcf
- ahead/behind before this document: 0 / 0

GitHub issue #546 and its latest MAIN RELEASE were read before the audit. Parent program issues #472 and #475 were also revalidated.

## 2. Current product contracts audited READ-ONLY

The following live contracts were inspected on the audited base.

| Area | Live contract | Finding relevant to transient events |
|---|---|---|
| Canonical TAG | src/Scada.Core/Tags/TagDefinition.cs | TAG has stable identity and represents current value/state. DataSourceId is available. |
| Current state cache | src/Scada.Core/Tags/CurrentTagCache.cs | Update replaces current value and publishes TagValueChanged. This is current-state semantics. |
| TAG event | src/Scada.Core/Events/TagValueChanged.cs | Represents Previous -> Current for one canonical TAG. It is not a generic occurrence contract. |
| Event bus | src/Scada.Core/Abstractions/IScadaEventBus.cs and src/Scada.Core/Events/InMemoryScadaEventBus.cs | One common typed event architecture already exists, but the current in-memory publisher awaits handlers sequentially and has no common bounded producer/consumer isolation. |
| Operational Event | src/Scada.Core/Events/OperationalEventModels.cs and docs/WAVE14-C14-OPERATIONAL-EVENTS.md | First-class engineer-authored process event, Active-revision gated, historized in a dedicated dataset. Useful for meaningful configured operational history, but not a raw high-rate device-event stream as-is. |
| Alarm | src/Scada.Core/Alarms/AlarmModels.cs | Alarm definitions are TAG-oriented and own lifecycle/priority/acknowledgement semantics. A button action is not an Alarm by nature. |
| Audit | src/Scada.Security/Audit/AuditModels.cs and BufferedAuditSink.cs | Security/system action record with subject, action, outcome and target. It is not the generic device-event bus. Audit already demonstrates explicit bounded buffering/rejection diagnostics. |
| Command | src/Scada.Core/Commands/CommandModels.cs | Current canonical Command is WriteTagValue with a fixed target/value. Command execution remains separate from event occurrence. |
| Runtime writes | src/Scada.DriverHost/Runtime/EngineeringRuntimeCoordinator.cs | Runtime.WriteAsync routes through the active authoritative TAG provider. Command execution currently converges on the same TAG write path. |
| Command auth/audit | src/Scada.Api/Security/CommandEndpointExtensions.cs | Command execution is server-authorized with CommandExecute and audited. Incoming device events must not manufacture command success or process state. |
| Gateway | src/Scada.DriverHost/Runtime/GatewayRuntimeEngine.cs | Gateway consumes TagValueChanged and routes state/value. It is not a transient-event router. |
| Server Scripts | src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs and C12/C19 docs | Server runtime already has bounded script execution queues and generic ServerRuntimeEvent triggers. Operational Event emission is mediated through the Active Runtime contract. |
| Client Visual Scripts | src/Scada.Engineering/VisualScripting/PythonValidation.cs | Client Visual scripts are explicitly forbidden from subscribing to ServerRuntimeEvent today. |
| Realtime TAG delivery | src/Scada.Api/Realtime/TagRealtimeHub.cs | TAG realtime uses a bounded per-client queue and disconnects a slow client. This is a useful isolation precedent, not a transient-event implementation. |
| Authorization | src/Scada.Security/Authorization/SecurityCapability.cs | Current capabilities include View, TagRead, CommandExecute, ProcessValueWrite, etc.; there is no transient-event-specific public capability. |
| Engineering | src/Scada.Engineering/Contracts/EngineeringContracts.cs | Equipment has stable identity and Capability bindings; current schema authority is scada.engineering v16. No new schema version is reserved by this research. |
| Active authority | PublishedRuntimeActivationService / Runtime coordinator | Runtime authority is the successfully activated persisted revision. Any future event definitions must obey the same authority boundary. |

## 3. Required taxonomy

The architecture must keep these meanings distinct.

### 3.1 TAG state

Meaning: a condition/value that is currently true or last authoritatively observed.

Examples:
- temperature = 21.5
- door = closed
- light = on
- cover position = 40 percent

Canonical path:
Driver/Data Source -> canonical TAG -> CurrentTagCache -> TagValueChanged -> state consumers.

TAG state may be historized through the normal historian.

### 3.2 TAG value change / process-state event

Meaning: an occurrence caused by a current-state transition.

Example:
door changed open -> closed.

The canonical state remains the TAG. TagValueChanged reports the state transition. A configured business/process occurrence may additionally be promoted to an Operational Event when explicit durable process history is useful.

A protocol event that reports a new state is not automatically a DRIVER transient event merely because the wire protocol calls it an event.

### 3.3 Device transient event

Meaning: something happened, with no requirement for a persistent current value.

Examples:
- button pressed/released
- single/double click
- hold
- rotate
- gesture
- Zigbee/Zigbee2MQTT action
- Z-Wave Central Scene
- Matter event
- BTHome button event
- remote-control action
- momentary/pulse occurrence
- device notification

This research recommends the canonical occurrence name:

TransientDeviceEventOccurred

and a stable authoring/runtime identity concept:

TransientEventDefinition

These names are preliminary contract names for Main review. They do not authorize implementation.

### 3.4 Operational Event

Meaning: an authored, classified operational process occurrence intended for Event history/browser semantics.

Operational Event remains distinct from raw device ingress. Selected device events may be explicitly mapped/promoted to an Operational Event definition, but the driver must not create OperationalEventOccurred for every technical action by default.

Disposition:

OPERATIONAL EVENT = ADAPT_VIA_COMMON_EVENT

It is not REUSE_AS_IS for transient driver events.

### 3.5 Alarm

Meaning: a condition with alarm lifecycle, priority and acknowledgement/shelving behavior.

Device transient event is not automatically Alarm.

A configured automation or process rule may derive an alarm condition from events, but the driver must not turn button clicks, Central Scene or scene keys into alarms by default.

### 3.6 Audit

Meaning: durable security/system record of a relevant user/system action, authorization or mutation.

Incoming device events do not require Audit by default.

Protected command execution continues to be audited at the command/security boundary. A device event becomes Audit only when a separate security requirement explicitly says so.

### 3.7 Command

Meaning: intent to cause an action.

Command acceptance/execution must never imply process state confirmation.

For stateful actions:
Command -> driver action -> authoritative report/readback -> TAG state.

For stateless actions:
Command outcome remains distinct from process state.

Rich Command architecture is intentionally deferred to CHECKPOINT 2.

### 3.8 Diagnostics

Meaning: technical health/transport/runtime evidence such as malformed event, dropped event, duplicate event, queue saturation, disconnect or protocol decoding failure.

Diagnostics are not process state and are not automatically operational history.

## 4. Protocol evidence walkthrough

### 4.1 BTHome

BTHome defines event-style button/dimmer objects and an optional packet ID used to identify changed advertising data and filter duplicates.

Classification:
- sensor measurement such as temperature: STATE
- button press/double/long/hold: EVENT
- dimmer rotation occurrence: EVENT
- advertisement/decryption/malformed frame status: DIAGNOSTIC

The packet ID is protocol evidence that can support bounded dedup/replay handling. It must not become a globally required sequence field.

Reference:
https://bthome.io/format/

### 4.2 Zigbee2MQTT action

Zigbee2MQTT device exposes frequently publish an action value for actions such as single, double, hold or scene selection. This is occurrence-like behavior and should not become a persistent process TAG merely to make the action observable.

Classification:
- action: EVENT
- reported attribute such as on/off, level, temperature: STATE
- bridge/device availability: STATE or DIAGNOSTIC depending the canonical semantic
- malformed/unsupported converter output: DIAGNOSTIC

Reference:
https://www.zigbee2mqtt.io/

### 4.3 Z-Wave Central Scene

Central Scene notifications carry a scene number and key attribute, including press/release/held semantics. Held behavior may produce repeated notifications.

Classification:
- Central Scene notification: EVENT
- scene number/key attribute: typed event payload
- supported-state values exposed by other Z-Wave Value IDs: STATE where they have current-value semantics

Held/repeat occurrences must not be silently coalesced merely to save queue space.

Reference:
https://docs.silabs.com/z-wave/8.1.0/z-wave-api/central-scene

### 4.4 Matter event

Matter has typed event paths and event numbers. Event number is useful protocol evidence for ordering/dedup within the relevant source domain, but the common model must not require every protocol to provide one.

Classification:
- Matter cluster event: EVENT
- Matter attribute subscription/report: STATE
- interaction/session failures: DIAGNOSTIC unless they drive a separate canonical availability state

References:
https://github.com/project-chip/connectedhomeip
https://github.com/project-chip/connectedhomeip/blob/master/src/app/EventLogging.h

### 4.5 Industrial digital pulse/event

This requires semantic classification rather than protocol-name classification.

If the signal has a meaningful current state:
protocol event/report -> TAG STATE update -> TagValueChanged.

If the source represents only a momentary edge/occurrence with no truthful persistent state:
EVENT.

If both meanings independently matter, an implementation may produce:
- state update for the persistent condition; and
- a separate transient event for the occurrence.

Do not manufacture a latched Boolean TAG only to preserve an occurrence.

### 4.6 Generic device notification

Classify by meaning:
- current condition/value -> STATE
- one-time semantic occurrence -> EVENT
- technical transport/health detail -> DIAGNOSTIC
- alarm lifecycle condition -> ALARM only through explicit alarm semantics
- requested action -> COMMAND

## 5. Canonical transient-event contract

### 5.1 Stable definition identity

A TransientEventDefinition should be protocol-neutral and stable across binding changes.

Minimum proposed definition semantics:
- DefinitionId: required stable Guid
- Key: required stable key
- EventType: required bounded canonical semantic token
- DataSourceId: required stable Data Source identity
- EquipmentId: optional stable Equipment identity
- CapabilityId: optional canonical Capability identity
- PayloadSchema: bounded typed field definitions
- Metadata: bounded authoring metadata only when needed

Protocol-specific topic, cluster object, bridge method, raw driver object or library class stays behind the driver binding.

A driver binding may resolve the protocol source to the stable definition, but consumers should not receive raw driver binding internals as their primary identity.

### 5.2 Occurrence identity

Each accepted canonical occurrence should have:
- EventId: required unique occurrence identity
- DefinitionId: stable definition identity
- EventType: copied/resolved canonical event type
- DataSourceId
- EquipmentId when applicable
- CapabilityId when applicable
- OccurredAtUtc
- ObservedAtUtc
- TimestampOrigin
- Sequence: optional
- SequenceDomain: optional bounded discriminator when needed
- Payload: bounded typed payload
- CorrelationId: optional

EventId is occurrence identity. DefinitionId is not occurrence identity.

An occurrence GUID is useful for correlation, diagnostics, retention/promotion and consumer idempotency even when the protocol itself lacks an event number.

### 5.3 Deliberate exclusions from the common occurrence v1

Do not require these fields in the common occurrence:
- DriverType
- raw protocol topic/path
- raw cluster/command-class object
- arbitrary driver binding object
- generic TAG Quality
- generic Confidence
- arbitrary JSON object

Driver/protocol diagnostics may retain raw evidence behind diagnostic boundaries.

## 6. Source identity

Recommended consumer-facing chain:

DataSourceId -> EquipmentId? -> CapabilityId? -> TransientEventDefinition

This is sufficient to relate events to canonical project identity without exposing a raw protocol object.

Rules:
1. DataSourceId is the authoritative integration/source identity.
2. EquipmentId identifies the canonical physical/logical device when applicable.
3. CapabilityId scopes the semantic behavior when the Capability model owns the event.
4. The driver binding is implementation detail and may change without changing the canonical event definition.
5. Consumers must not invoke or subscribe to a concrete driver method.

## 7. Timestamps

A common event must not pretend that every device has a trustworthy clock.

Required:
- ObservedAtUtc: server-side canonical observation/ingress timestamp.
- OccurredAtUtc: best supported occurrence timestamp.
- TimestampOrigin: Device or Observed.

Rule:
- if a trustworthy protocol/device event timestamp is available, OccurredAtUtc may use it;
- otherwise OccurredAtUtc = ObservedAtUtc and TimestampOrigin = Observed.

Do not infer device time from local receive time without marking the origin.

If a future protocol exposes more detailed timing metadata, it may remain protocol diagnostic metadata unless Main approves a common field.

## 8. Ordering, sequence, dedup and replay

There is no truthful global event ordering across all Data Sources.

Guarantee target:
- preserve accepted occurrence order per canonical Data Source/source stream where transport/runtime evidence supports it;
- make no global ordering promise.

Sequence is optional.

Examples:
- BTHome packet ID: small wrapping counter useful for duplicate-advertisement evidence.
- Matter EventNumber: stronger monotonic protocol event evidence.
- Z-Wave Central Scene: use only sequence/transaction evidence actually surfaced by the chosen runtime stack; do not invent one.
- Zigbee/Z2M action: dedup only when concrete transport/message identity proves a duplicate.

Dedup rule:
Never deduplicate only because EventType + Payload + near-equal timestamp match. Two real double clicks may legitimately be identical.

Counter wrap must be handled inside a bounded per-source window.

Replay/out-of-order diagnostics should be emitted only when the protocol provides enough evidence to make that judgment.

## 9. Payload architecture

Recommendation:

schema-referenced bounded typed payload fields.

Minimum scalar vocabulary for CHECKPOINT 1:
- Boolean
- Integer
- Number
- String
- Enum token

Potential later types may be added only when real protocol mappings justify them.

Do not use arbitrary JSON as a generic Driver SDK escape hatch.

Preliminary implementation safety bounds for later validation:
- maximum 32 fields per occurrence;
- bounded key length;
- bounded string/enum value length;
- maximum serialized canonical payload size around 16 KiB.

These are research guardrails for test design, not product constants authorized by this document. Main/implementation must set final limits from measured protocol/runtime behavior.

Nested arbitrary objects, code validators and raw protocol blobs are out of scope for the common event payload.

## 10. Operational Event disposition

Final CHECKPOINT 1 disposition:

ADAPT_VIA_COMMON_EVENT

Reasons OperationalEventOccurred is not appropriate as the raw driver transient-event contract:
1. its definition is explicitly engineer-authored and Active-revision gated;
2. every occurrence is designed for the operational event history path;
3. its current Context values are string-based rather than schema-referenced typed event payload;
4. it does not directly model DataSourceId/CapabilityId/source-stream sequence semantics;
5. the current common event bus awaits subscribers sequentially, while Operational Event history persistence is a subscriber;
6. high-rate technical device actions must not automatically create permanent Event Browser history.

Reuse:
- same common IScadaEvent architecture;
- same Active Runtime authority principles;
- same protected Historical Query/Operational Event contract when an explicit configured promotion exists.

Future explicit mapping:

TransientDeviceEventOccurred
-> optional configured promotion rule
-> OperationalEventOccurred
-> operational.events durable history

Promotion must be intentional and bounded, never automatic for every driver event.

## 11. Alarm relationship

Disposition:

NO AUTOMATIC DRIVER EVENT -> ALARM CONVERSION

A future automation/rule may use an event as input and decide to create/update an alarm-relevant canonical state or invoke approved alarm logic.

The driver itself must not decide that double-click, Central Scene, remote button or scene invocation is an alarm.

## 12. Audit relationship

Disposition:

NO DEFAULT DEVICE EVENT -> AUDIT

Audit remains for:
- user/system protected mutations;
- command execution;
- authorization denial/failure;
- administrative/security actions.

If a human command causes an external device action:
- the command/security boundary creates the Audit record;
- any later device event remains its own event;
- any later authoritative state report updates the TAG;
- correlation may link them without making them the same semantic object.

## 13. TAG and Historian relationship

Disposition:

NO FAKE TAG FOR EVENT

Do not create a persistent action TAG containing values such as double_click, scene_3 or held only to make a transient event observable.

Historian remains state/sample history by default.

A real state reported by a protocol event may update a TAG and therefore participate in historian capture normally.

Transient events do not enter Historian automatically.

## 14. Gateway relationship

CHECKPOINT 1 v1 decision:

TAG Gateway remains state-routing only.

The existing Gateway consumes TagValueChanged and writes destination TAGs. It should not be expanded automatically into an event router.

If future Product requirements need event-to-command automation, use a dedicated canonical automation/Server Script consumer rather than making Gateway understand raw driver event payloads.

## 15. Retention

Default retention policy for canonical transient events:

EPHEMERAL BY DEFAULT

Destinations:
- common bounded runtime event delivery: YES
- bounded diagnostics/recent-event observation: YES
- Operational Event history: only through explicit configured promotion
- Historian: NO by default
- Audit: NO by default
- dedicated durable transient-event history: NOT REQUIRED for v1

A dedicated durable event store should be reconsidered only if a concrete forensic/regulatory/high-volume replay requirement appears. Do not add one merely because events exist.

## 16. Consumers

### v1 consumers recommended

Server Scripts / canonical Automation:
YES, after a bounded revision-gated canonical transient-event trigger is approved. Script receives the canonical event contract, never a driver object.

Diagnostics:
YES. At minimum counters and last error/gap information should integrate with the common diagnostics direction, including #500 where applicable.

Runtime diagnostics UI:
YES for bounded diagnostic visibility, not as an unbounded event feed.

Operational Event promotion:
YES when explicitly configured.

### v1 consumers not recommended

TAG Gateway:
NO.

Client Visual Scripts:
NO for the first contract. Current validation deliberately prevents Client Visual scripts from subscribing to ServerRuntimeEvent. A future protected client-event contract requires a separate Main decision.

External realtime API:
DEFER. If later required, it needs protected filtered subscriptions and per-client bounded queues.

Historian:
NO raw transient stream.

Alarm engine:
NO direct generic subscription as an automatic conversion rule.

## 17. Backpressure and slow-consumer isolation

This is a mandatory gate.

The current InMemoryScadaEventBus awaits handlers sequentially. A raw high-rate driver receive callback must not be allowed to block behind arbitrary slow consumers.

Required future shape:

Driver adapter
-> validate/normalize
-> bounded transient-event ingress per Data Source
-> evidence-based ordering/dedup
-> canonical TransientDeviceEventOccurred
-> bounded isolated consumer delivery
-> consumers

The common architecture remains one event architecture; this is not a second event bus. The bounded dispatcher/consumer isolation is an extension of common runtime event delivery semantics.

### Overflow

Recommended default semantics:
- preserve already accepted occurrence order;
- reject/drop the newest occurrence that cannot be accepted into the bounded ingress;
- increment an explicit dropped/gap diagnostic;
- mark the affected source/consumer degraded when the loss threshold requires it;
- never silently pretend the event was delivered.

Do not silently DropOldest accepted canonical events, because doing so rewrites the observable sequence behind consumers.

Protocol adapters may suppress proven duplicates before canonical acceptance.

### Coalescing

Do not coalesce semantically distinct transient events.

Examples that must remain distinct:
- two identical double clicks;
- press then release;
- repeated held notifications where the protocol defines each notification as an occurrence;
- rotate-left steps.

Only state streams may safely retain-latest when the current state contract makes that semantically correct.

### Slow consumers

Each slow consumer must be isolated from the driver ingress path.

Possible implementation pattern:
- bounded consumer queue;
- explicit overflow diagnostic;
- disconnect/disable subscriber when it cannot keep up;
- preserve other consumers.

TagRealtimeHub's current slow-client bounded-queue/disconnect behavior is a useful precedent, but is not reused as an implementation by this research.

### Critical behavior

Safety-critical or process-authoritative behavior must not depend solely on an ephemeral best-effort event stream without explicit product requirements for durability/acknowledgement.

Where current condition matters, model STATE.
Where operator history matters, explicitly promote to Operational Event.
Where a protected requested action matters, use Command/Audit.

## 18. Authorization and trust boundary

Driver transient-event production is server-side Runtime authority.

A browser/client cannot manufacture a trusted driver event.

Consumer authorization must be checked server-side at the public/automation boundary.

No new SecurityCapability is reserved in CHECKPOINT 1.

Server Script consumption must obey:
- Active revision authority;
- declared dependencies/filters;
- bounded execution queue;
- normal script sandbox;
- no driver object exposure.

A future realtime event API must define its own read/filter authorization rather than reuse TagRead by assumption.

## 19. Lifecycle and Engineering authority

Any future TransientEventDefinition persisted in Engineering must follow the normal lifecycle:

Working
-> Save
-> Publish
-> Activate
-> Runtime authority

A failed activation cannot publish a new transient-event definition set.

Removed definitions stop being valid authorities when the new revision becomes Active.

The exact schema shape/version is intentionally not selected here.

## 20. Compatibility

Required compatibility preference:

ADDITIVE EVOLUTION

Projects without transient-event definitions must continue to run with identical TAG/Alarm/Command/Gateway semantics.

No existing TAG, Command, Operational Event, Alarm, Audit or Gateway contract should be reinterpreted.

No schema version is reserved by research.

## 21. Research contract deltas required

These are documentation-only deltas. NONE are implemented here.

### RESEARCH_CONTRACT_DELTA_REQUIRED — TRANSIENT-DEVICE-EVENT-CONTRACT-01

Add a common protocol-neutral stable definition + immutable occurrence contract with canonical source identity, typed bounded payload and optional sequence evidence.

### RESEARCH_CONTRACT_DELTA_REQUIRED — COMMON-EVENT-BOUNDED-DISPATCH-01

Add bounded producer/consumer isolation for high-rate transient event delivery so slow consumers cannot stall driver receive loops.

This must extend the common Runtime event architecture rather than create a protocol-specific second Event Bus.

### RESEARCH_CONTRACT_DELTA_REQUIRED — SERVER-SCRIPT-TRANSIENT-EVENT-TRIGGER-01

Add a revision-gated canonical Server Script trigger/subscription model for selected transient-event definitions/types, using existing sandbox/queue limits.

### RESEARCH_CONTRACT_DELTA_REQUIRED — TRANSIENT-EVENT-OPERATIONAL-PROMOTION-01

Define an explicit authored mapping from selected canonical transient events to existing Operational Event definitions when durable process history is required.

No automatic promotion.

## 22. L0/L1 implementation gates for this decision

Full L0-L4 convergence belongs to CHECKPOINT 3, but CHECKPOINT 1 cannot recommend implementation without these minimum gates.

### L0

Prove:
- definition/occurrence serialization;
- typed payload validation/bounds;
- EventId versus DefinitionId;
- DataSource/Equipment/Capability identity;
- device-vs-observed timestamp semantics;
- optional sequence/counter behavior;
- wrap-aware bounded dedup;
- no arbitrary JSON escape hatch.

### L1

Fake producer/consumers must prove:
- burst traffic;
- proven duplicate suppression;
- identical legitimate events are both preserved;
- out-of-order evidence handling;
- full ingress queue;
- slow consumer isolation;
- explicit drop/gap diagnostics;
- one consumer failure does not block another;
- script queue integration does not block driver ingress.

## 23. Open uncertainties for Main

1. Whether stable transient-event definitions belong directly in Engineering schema or can initially be compiled from future Equipment/Capability + driver binding metadata without a new first-class package collection.
2. Final product-wide bounded-dispatch primitive: evolve IScadaEventBus delivery internals versus introduce a common runtime event-dispatch service behind the same IScadaEvent semantics.
3. Exact first public consumer surface after Server Scripts/Diagnostics: protected Runtime UI subscription versus defer until a concrete UX requires it.

These are Main decisions. No schema or runtime implementation is selected here.

## 24. CHECKPOINT 1 decision

DRIVER-TRANSIENT-EVENT-01 = GO_WITH_GATES

Rationale:
- the semantic gap is real and already accepted by #472/#475;
- existing TAG state cannot truthfully represent stateless occurrences;
- Operational Event is valuable but is not the correct raw ingestion contract as-is;
- the existing typed IScadaEvent architecture can be extended instead of creating a second event system;
- the current synchronous subscriber delivery is not sufficient for high-rate driver events without bounded isolation.

Mandatory gates before product implementation:
1. Main accepts the canonical definition/occurrence identity and typed bounded payload contract.
2. Main accepts COMMON-EVENT-BOUNDED-DISPATCH-01 before high-rate drivers emit transient events.
3. L0/L1 proves bounds, timestamp semantics, evidence-based dedup and slow-consumer isolation.
4. Initial consumers remain bounded: Server Scripts/Automation + Diagnostics + explicit Operational Event promotion; Gateway and Client Visual scripts are not broadened implicitly.

## 25. Checkpoint declarations

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO SCHEMA CHANGED

NO EVENT RUNTIME CHANGED

NO DRIVER SDK CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

HA = HIGH AVAILABILITY

HAB = HOME ASSISTANT BRIDGE

NO MERGE PERFORMED
