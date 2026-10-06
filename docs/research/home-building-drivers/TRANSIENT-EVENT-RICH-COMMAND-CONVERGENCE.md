# TRANSIENT-EVENT-RICH-COMMAND-CONVERGENCE

Status: CHECKPOINT 3 / RESEARCH ONLY / DOCS ONLY

Issue owner: #546 — HOME-RESEARCH-05 — transient events + rich command binding architecture

Parent: #472 / #475

Contract: C-DRIVER-TRANSIENT-RICH-COMMAND-RESEARCH-01

Order: DRIVER-TRANSIENT-EVENT-RICH-COMMAND-RESEARCH-01

Scope of this checkpoint:
- converge DRIVER-TRANSIENT-EVENT-01;
- converge RICH-COMMAND-BINDING-01;
- preserve their semantic separation;
- define compatibility/versioning;
- define security and abuse-case boundaries;
- converge observability with #500;
- define L0-L4 acceptance;
- define migration impact;
- propose implementation slices and sequencing;
- provide final GO/WAIT recommendation.

Canonical terminology:
- HA = High Availability.
- HAB = Home Assistant Bridge.
- Home Assistant = external platform by name.

No product code, schema, Driver SDK, Runtime, Gateway, Script runtime, frontend, test, dependency or CI change is made by this document.

## 1. Live authority revalidation

GitHub live was revalidated before this checkpoint.

Research branch:
research/home-transient-events-rich-commands

Research HEAD before this document:
b86a4943be6e4105c261b0279be6e9ed83f24ab6

Integration:
wave15/corrections-integration

Integration/base HEAD:
77b08d60333685b4ba06aa649e3125f23b475dcf

Merge-base before this document:
77b08d60333685b4ba06aa649e3125f23b475dcf

Ahead / behind before this document:
2 / 0

Existing research delta:
- DRIVER-TRANSIENT-EVENT-ARCHITECTURE.md
- RICH-COMMAND-BINDING-ARCHITECTURE.md

No Main order newer than the #546 release changed the docs-only/no-product-code scope.

## 2. Coordination changes relevant to CHECKPOINT 3

### 2.1 #500 diagnostics foundation is complete

Issue #500 is closed as completed.

Main's accepted diagnostic ladder is:

Host
-> Driver / Data Source
-> Network Probe
-> Connection Test
-> Test Read
-> Active TAG / Development Monitor

The final #500 note explicitly says not to reopen the already accepted driver/L3 tests.

This research therefore consumes #500 as an existing foundation.

It does not invent a second health model.

### 2.2 #543 HOME COMMON S2 is independently active

#543 owns:
- managed sidecar lifecycle;
- typed/exclusive Host Resource foundation.

This research must not absorb those contracts.

Transient Event and Rich Command may later be consumed by sidecar-backed integrations, but command/event semantics remain independent of process ownership/host-resource lifecycle.

### 2.3 Existing protected-material authority remains authoritative

Communication driver protected material is host-owned and scoped by:
- project;
- Data Source;
- Driver type;
- purpose;
- reference.

Rich Command and transient events must not introduce a second secret path.

## 3. Final semantic taxonomy

The converged architecture preserves the following distinct truths.

| Concept | Meaning | Direction | Persistent current state? | Authorized mutation? | Default durability |
|---|---|---|---:|---:|---|
| TAG state | Current canonical process/device value | Driver/Source -> Runtime | YES | write only when writable | cache + optional Historian |
| TagValueChanged | Transition of canonical TAG state | Runtime internal | reflects TAG | NO | ephemeral bus event |
| Transient Device Event | One occurrence with no required persistent current value | Driver -> Runtime | NO | NO | ephemeral by default |
| Operational Event | Authored meaningful process occurrence | Runtime -> event history | NO | emission is controlled | durable when configured |
| Command | Intent to cause an action | caller -> Runtime -> Driver/Source | NO | YES | Audit; result response |
| Alarm | Alarm lifecycle condition | Runtime | lifecycle state | protected ack/shelve | alarm history |
| Audit | Security/system action record | protected boundary -> Audit | NO | records mutation | durable |
| Diagnostics | Technical health/evidence | host/driver/runtime | snapshot/counters | NO | bounded/current |
| Automation/Script | Policy/logic linking inputs to actions | canonical Runtime | owns logic only | via canonical APIs | Engineering + runtime |

No item in this table is an alias for another.

## 4. Non-negotiable identity rule

STATE != EVENT != COMMAND

This must remain true in:
- Engineering schema;
- Runtime contracts;
- Driver SDK;
- HMI;
- Scripts;
- Audit;
- diagnostics;
- tests.

Examples:

Button double click:
EVENT.

Light current state:
TAG.

Set light on:
TAG write or Command, depending on authored model.

Invoke Scene 3:
COMMAND.

Scene invoked notification from device:
EVENT.

Command execution Audit:
AUDIT.

Meaningful process record "Emergency scene invoked":
OPERATIONAL EVENT only when explicitly configured/emitted.

## 5. One canonical Runtime spine

The convergence keeps one product architecture.

Inbound state path:

Driver / Data Source
-> canonical TAG
-> CurrentTagCache
-> TagValueChanged
-> Historian / Alarm / Gateway / Scripts / Realtime

Inbound transient event path:

Driver / Data Source
-> normalize + bound + dedup by evidence
-> TransientDeviceEventOccurred
-> common bounded event dispatch
-> Scripts / Automation / Diagnostics
-> optional explicit Operational Event promotion

Outbound state path:

HMI / Script / Gateway / Command
-> Runtime.WriteAsync(TAG)
-> active owning Source/Driver
-> protocol write
-> authoritative report/readback
-> TAG

Outbound rich action path:

HMI / Script / Automation
-> canonical CommandInvocation
-> authorization
-> active CommandDefinition
-> validated parameters
-> DriverCommandBinding
-> optional rich-command executor
-> CommandResult
-> Audit
-> independent state/event evidence

No frontend/script/automation receives raw driver objects.

## 6. Event and Command may interact, but do not collapse

A transient event may legitimately cause a Command through application logic.

Example:

button double click
-> TransientDeviceEventOccurred
-> Server Script
-> invoke canonical Scene command
-> CommandResult
-> later device state reports
-> TAG updates

This does not make:
- event == command;
- command == state;
- command result == event history;
- state change == Audit.

The automation layer owns the decision.

The Driver only owns protocol translation.

## 7. Event-to-Command automation boundary

Recommended first canonical bridge:

TransientDeviceEventOccurred
-> revision-gated Server Script trigger
-> canonical invoke_command(...)
-> normal CommandExecute/security policy
-> CommandResult

Future dedicated Automation may use the same APIs.

Not recommended:
- Driver directly invoking another Driver;
- event callback directly calling ICommunicationDriver;
- TAG Gateway gaining implicit action execution;
- browser subscribing to raw event and invoking raw protocol operation;
- protocol adapter containing application automation rules.

## 8. Loop prevention and causality

Event -> automation -> Command -> device -> event/state can form valid or accidental loops.

Implementation must make loops diagnosable and bounded.

Recommended runtime execution context:
- CorrelationId: groups one user/application activity;
- CausationId: optional identity of the immediately causing event/invocation;
- Origin: HMI, ClientScript, ServerScript, Automation, Driver, System;
- Active ProjectKey + Revision;
- bounded hop/depth counter for automation re-entry diagnostics.

These fields are runtime execution context.

They do not need to become unrestricted caller-supplied fields.

Rules:
1. browser cannot forge trusted Origin/principal/revision;
2. Driver cannot manufacture a user identity;
3. automated Command receives a new InvocationId;
4. causation/correlation may link Event -> Command -> later evidence;
5. correlation does not prove that a later state/event was physically caused by the Command;
6. recursion/hop thresholds must fail safely and diagnose.

### Research delta

RESEARCH_CONTRACT_DELTA_REQUIRED — RUNTIME-INTERACTION-CAUSALITY-01

A small common runtime correlation/causation context is recommended for Event/Command/Automation diagnostics.

It is not implemented here.

## 9. Shared scalar vocabulary, separate schemas

Event payload fields and Command parameters need similar primitive types.

Do not create two incompatible primitive vocabularies.

Recommended common scalar vocabulary:
- Boolean
- Integer
- Number
- String
- Enum
- Duration
- Percentage

Event CHECKPOINT 1 only required:
- Boolean
- Integer
- Number
- String
- Enum.

Command CHECKPOINT 2 additionally justified:
- Duration
- Percentage.

Convergence recommendation:
the shared low-level scalar type/validation utilities may include all seven types, while each domain still declares its own schema.

Transient Event:
TransientEventFieldDefinition

Rich Command:
CommandParameterDefinition

Do not create a single generic "payload bag schema" that erases domain meaning.

### Percentage

Canonical semantic range:
0..100.

### Duration

Canonical wire representation must be explicit and versioned.

Do not allow callers to guess seconds versus milliseconds from a plain Number.

### Number

Reject NaN and infinity.

### String

Bound length.

### Enum

Allowlisted bounded tokens only.

## 10. Dynamic JSON policy

Arbitrary JSON is not the Driver SDK escape hatch.

Allowed:
- JSON serialization of a known canonical typed schema.

Rejected:
- unknown nested objects;
- raw protocol payload;
- caller-supplied driver method arguments;
- arbitrary service data;
- script code;
- dynamic schema controlled by browser input.

The existing visual-action Parameters field is JSON-native storage, but future ExecuteCommand use must validate it against CommandParameterDefinition before Preview/Apply and again at Runtime.

## 11. Stable identity model

### 11.1 TAG

Existing stable TagDefinition.Id.

### 11.2 Transient event

DefinitionId:
stable authored/compiled semantic event identity.

EventId:
unique occurrence identity.

### 11.3 Command

CommandId:
stable canonical action identity.

InvocationId:
unique execution request identity.

### 11.4 Equipment / Capability / Data Source

DataSourceId:
integration/runtime ownership identity.

EquipmentId:
canonical physical/logical object identity when applicable.

CapabilityId:
semantic role identity when applicable.

Protocol addresses remain behind bindings.

## 12. Stable identity must survive binding replacement

Examples:

LivingRoom.Light / SetBrightness

may move from:
- Shelly;
- Zigbee;
- Matter;
- HAB;

without changing the logical Command identity if Engineering deliberately rebinds the same capability.

Likewise:
LivingRoom.Remote / DoubleClick

may move between bridge/native protocol implementations while retaining semantic event identity where the project considers it the same authored capability.

The exact migration/reconciliation tool must still prove identity rather than guessing from labels.

## 13. Driver binding separation

State binding:
CommunicationTagBinding.

Rich Command binding:
DriverCommandBinding proposal.

Transient event binding:
driver-owned event-source binding or compiled semantic mapping.

Do not overload CommunicationTagBinding to represent every possible event and action.

Reasons:
- TAG binding represents current-value semantics;
- Command binding represents action semantics;
- transient event binding represents occurrence source semantics;
- each needs independent validation/versioning.

They may share:
- ContractVersion envelope patterns;
- SchemaId/SchemaVersion pattern;
- DataSource identity;
- safe portable targets/settings;
- common registry/validator conventions.

## 14. No raw protocol operation surface

The following must never become the common Runtime API:

invoke_driver_method(driverId, method, json)

publish_mqtt(topic, payload)

call_home_assistant_service(domain, service, json)

invoke_zigbee_cluster(cluster, command, raw)

invoke_zwave_cc(cc, command, raw)

invoke_matter(cluster, command, tlv)

shell/process execution

The driver adapter may internally perform equivalent protocol operations after resolving a registered canonical binding.

## 15. Stateful versus stateless decision table

### Case A — one truthful writable state

Example:
brightness = 70

Use:
TAG write.

### Case B — one fixed authored action implemented as one TAG write

Example:
StartPump command writes request TAG true.

Use:
legacy WriteTagValue Command.

### Case C — parameterized or atomic action

Example:
Move(40, 5 seconds)

Use:
Rich Command.

### Case D — stateless action

Example:
InvokeScene(3)

Use:
Rich Command.

### Case E — occurrence only

Example:
double click received

Use:
Transient Event.

### Case F — operational history

Example:
operator initiated emergency scene

Use:
Command Audit plus optional configured Operational Event/process logic.

### Case G — alarm condition

Example:
smoke alarm active

Use:
canonical state + Alarm semantics where applicable.

Do not encode it only as a transient click-like event if current alarm condition matters.

## 16. Event retention convergence

Default transient-event retention:

EPHEMERAL.

Allow:
- bounded in-memory dispatch;
- bounded recent diagnostics;
- counters;
- explicit Operational Event promotion.

Do not default to:
- Historian;
- Audit;
- permanent event table;
- fake TAG.

If a future regulatory/forensic requirement needs durable raw event retention, create a deliberate dataset after proving scope, volume, retention and authorization.

## 17. Command retention convergence

Default Command execution retention:
- immediate canonical CommandResult;
- durable security Audit;
- diagnostics/counters.

Do not default to:
- Operational Event;
- Historian;
- fake status TAG;
- second command-history database.

If a product-level command execution browser is later required, evaluate a dedicated dataset deliberately.

## 18. Operational Event promotion

Canonical relationship:

TransientDeviceEventOccurred
-> explicit authored promotion/rule
-> OperationalEventOccurred

Potential examples:
- safety-related manual station button pressed;
- operator scene change important for process trace;
- device tamper event significant for operations.

Do not promote every:
- click;
- hold repeat;
- rotary step;
- Central Scene notification;
- BTHome advertisement;
- device action.

Promotion must be selective and rate-bounded.

## 19. Command and Operational Event

Do not auto-emit an Operational Event for every Command.

Existing Command Audit already records protected invocation.

If business/process history needs a Command-related Operational Event, application logic can emit one with CommandId/CommandKey context.

CommandResult remains distinct.

## 20. Command and TAG confirmation

The canonical UI/runtime must distinguish:

requested
accepted
completed
state confirmed

Example:

Move(40, transition=5s)

CommandResult.Completed
does not mean:
Position TAG == 40.

Only authoritative report/readback updates Position TAG.

The UI may show:
- Command completed;
- Position not yet confirmed.

This is not an error in the architecture.

## 21. Command and transient response event

Some protocols may return a separate event after a command.

Example conceptual sequence:

InvocationId C1
-> command dispatched
-> CommandResult Accepted
-> device emits action-complete event E9
-> later state report S10

If protocol correlation evidence exists, Event may carry CorrelationId/Causation context.

Do not infer correlation merely because timing is close.

## 22. Backpressure convergence

### 22.1 Transient events

Must use bounded ingress and isolated consumers.

Driver receive loops cannot await arbitrary slow application consumers.

Recommended overflow:
- reject/drop newest at the bounded ingress when capacity is exhausted;
- increment explicit loss/gap counters;
- mark degraded when policy threshold requires;
- never silently claim delivery.

Do not coalesce semantically distinct events.

### 22.2 Commands

Must use bounded admission/concurrency.

No unbounded physical command queue.

A saturated command executor should:
- reject before dispatch when possible;
- report Rejected / NotDispatched;
- preserve ordering per target where process semantics require it.

### 22.3 Scripts

Existing bounded Script queues remain authoritative.

Event delivery into Script must not bypass:
- queue bounds;
- coalescing/rejection policy;
- timeout;
- failure isolation.

Command invocation from Script must not block unrelated driver event ingestion.

## 23. Dedup versus idempotency

These are separate.

Event dedup:
suppresses proven duplicate inbound occurrences.

Command idempotency:
determines whether repeating an outbound effect is safe.

A BTHome packet ID may help event dedup.

A Command InvocationId does not make a relay pulse safe to replay.

Never treat:
dedup key == command idempotency key.

## 24. Ordering

No global ordering guarantee exists across Data Sources.

Transient Event:
preserve accepted order per source stream where evidence supports it.

Command:
preserve issue order per target when required by driver/process semantics.

State:
current TAG value is authoritative current state, not a replay log.

Cross-source correlation is observational, not a total order.

## 25. HA authority

HA in this document means High Availability.

Only the effective Active authority may:
- execute process TAG writes;
- execute Rich Commands;
- emit authoritative server-originated runtime effects;
- forward driver transient events as active Runtime effects.

Standby materialization must remain passive.

Existing Runtime fencing principles must be reused.

A Standby must not:
- duplicate a physical command;
- duplicate a transient event into active consumers;
- create duplicate Audit claiming execution;
- advance automation from passive materialization.

HA failover creates difficult ambiguity for in-flight physical Commands.

Rule:
do not replay an in-flight command after failover unless the operation is explicitly proven safe/idempotent and the failover contract has sufficient evidence.

Otherwise outcome is Unknown.

## 26. Security boundary — event production

Trusted TransientDeviceEventOccurred may originate only from:
- active server-side Driver/Data Source runtime;
- explicitly trusted internal simulator/test provider under non-production/test contracts if supported.

Do not expose:
POST /api/events/emit-trusted-driver-event

to normal browsers/users.

A client-provided visual/script event remains an application/UI event, not a trusted Driver event.

## 27. Security boundary — command invocation

All ordinary process Rich Command invocation uses:
SecurityCapability.CommandExecute

and normal server-side AuthorizationResource scope.

Caller may provide only:
- CommandId;
- allowlisted canonical parameter values;
- optional public idempotency/correlation token only if a future API explicitly defines it.

Caller may not provide:
- DriverType;
- DataSourceId;
- PortableTarget;
- OperationKey;
- raw binding settings;
- secret reference;
- principal;
- roles;
- ProjectKey;
- Revision.

Those are resolved by the server/Active Runtime.

## 28. Privileged operations remain separate

Rich Command is not authority for:
- permit join;
- Zigbee network create/reset;
- Z-Wave inclusion/exclusion;
- Matter fabric administration;
- credential/key rotation;
- controller backup/restore;
- firmware upgrade;
- certificate trust mutation;
- factory reset when treated as administration;
- HA role transfer;
- host-resource allocation;
- sidecar lifecycle administration.

These require dedicated protected contracts/capabilities.

## 29. Protected material

No Event payload or Command parameter may carry ordinary secrets.

Driver binding Settings are non-secret.

Protected material remains referenced and resolved through the host-owned protected-material authority.

Do not include resolved secret values in:
- event payload;
- command parameters;
- CommandResult.Detail;
- diagnostics;
- Audit;
- Operational Event context.

## 30. Audit abuse cases

Audit must remain useful under failure.

Record:
- denied invocation;
- accepted/completed invocation;
- rejected/failed/timeout/unknown outcome;
- InvocationId;
- canonical Command identity;
- sanitized result;
- project/revision;
- principal/source context.

Do not:
- log secrets;
- log raw binary protocol payloads;
- claim state confirmation;
- overwrite Unknown as Failed merely to simplify dashboards.

## 31. Event abuse cases

Implementation tests must cover:
- malformed payload;
- oversized payload;
- unknown field;
- invalid enum;
- sequence wrap;
- duplicate protocol delivery;
- identical legitimate events;
- event storm;
- slow consumer;
- consumer exception;
- stale revision;
- passive HA node;
- spoofed client origin;
- unsupported event definition;
- source removal/rebind during activation;
- protocol reconnect replay.

## 32. Command abuse cases

Implementation tests must cover:
- unknown Command;
- disabled Command;
- unauthorized principal;
- stale Active revision;
- missing parameter;
- unknown parameter;
- type confusion;
- out-of-range number;
- NaN/infinity;
- invalid enum;
- oversized string/body;
- caller attempts to change target;
- caller supplies protocol-specific fields;
- command flood;
- per-target concurrency exhaustion;
- cancellation before dispatch;
- disconnect after possible dispatch;
- duplicate HTTP retry;
- HA failover while in-flight;
- malformed driver result;
- driver plugin/module unavailable;
- protected-material resolution failure where relevant;
- Script recursion/event-command feedback loop.

## 33. Observability convergence with #500

#500's communication diagnostic authority remains:
CommunicationDriverDiagnosticSnapshot

and the six-step diagnostic ladder remains authoritative.

Transient Event and Rich Command must integrate into that framework without creating a second health page.

### 33.1 Existing common health remains unchanged

Continue to use:
- Data Source identity;
- driver type/runtime instance;
- endpoint;
- operational state;
- state timestamps;
- success/failure timestamps;
- last sanitized error;
- data age;
- operation/scan duration;
- failure rate;
- associated TAG count;
- TAG quality;
- common counters;
- sanitized protocol details.

### 33.2 Interaction diagnostics are additional evidence

Recommended optional typed interaction diagnostics:

Transient Events:
- received;
- accepted;
- duplicatesSuppressed;
- invalidRejected;
- droppedIngress;
- consumerDrops;
- lastAcceptedAt;
- lastDroppedAt.

Rich Commands:
- requested;
- admissionRejected;
- dispatched;
- accepted;
- completed;
- failed;
- timedOut;
- unknown;
- lastCommandAt;
- lastUnknownAt.

Do not force Internal Memory/non-network providers to fabricate network metrics.

### 33.3 Common health decision

A transient event drop or repeated Unknown command result may degrade a Data Source.

The exact threshold/policy belongs to implementation.

One isolated application consumer drop should not automatically mark the physical Driver Faulted if communication remains healthy.

Diagnostics must distinguish:
- transport/driver failure;
- event delivery pressure;
- command executor saturation;
- application consumer failure.

### Research delta

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-INTERACTION-DIAGNOSTICS-01

Add optional typed event/command interaction evidence under the existing common diagnostics authority.

Do not create a second Driver health model.

## 34. #500 diagnostic ladder extension

The accepted six steps remain unchanged for current-state communication.

For event/action-capable integrations, optional detail may appear inside the relevant Driver/Data Source diagnostic card:

Host
-> Driver/Data Source
   -> Event ingress health
   -> Command executor health
-> Network Probe
-> Connection Test
-> Test Read
-> Active TAG

Do not add fake universal:
"Event Test"
or
"Command Test"

unless a driver can implement a safe bounded Engineering test.

A Command test that causes physical action must be explicitly protected and clearly labeled.

## 35. Connection Test and event/command semantics

Connection Test proves communication/session capability.

It does not prove:
- all transient events will arrive;
- all Command parameters are supported;
- physical actuation will complete.

Driver discovery/browse may advertise supported capabilities.

Active Runtime still owns actual event/command execution.

## 36. Engineering Preview and activation

Any future persisted TransientEventDefinition, Command parameter schema or DriverCommandBinding must participate in:

Working
-> Preview/validation
-> Apply
-> Save
-> Publish
-> Activate
-> Active Runtime

Activation must fail closed for:
- unknown binding schema;
- incompatible schema version;
- unresolved Data Source;
- unsupported driver command executor;
- invalid parameter definition;
- event source binding that cannot be resolved;
- duplicate stable IDs/keys;
- forbidden raw operation shape.

Failed activation cannot promote new Event/Command authority.

## 37. Schema/versioning strategy

Audited base authority:
scada.engineering schema v16.

This research does not reserve a future version.

Implementation rule:
1. rebase against then-current integration;
2. inspect then-current CurrentSchemaVersion;
3. select one deliberate next migration only when product code is released;
4. keep new fields optional/additive where possible;
5. keep legacy package compatibility explicit;
6. update JSON/package/CSV/backup/revision roundtrip together.

Do not reserve "v17" from this research because integration may move before implementation.

## 38. Event Engineering migration

Projects without transient event definitions:
no behavior change.

Potential future models:
A. first-class Engineering TransientEvents collection;
B. definitions compiled from Equipment Capability + driver binding metadata.

Main must choose one.

Migration must not infer event definitions from arbitrary TAGs named:
Action
Button
Scene
Event

unless an explicit migration rule has evidence.

No fake event creation from legacy data by naming heuristic.

## 39. Command Engineering migration

Legacy CommandEngineeringDto must remain readable.

Legacy:
- Kind = WriteTagValue;
- fixed Value;
- TargetTagId/Path;
- no parameters;
- no DriverCommandBinding.

Future Rich Command fields must be optional/discriminated.

No-body:
POST /api/commands/{id}/execute

must continue to work for legacy/fixed parameterless Commands.

Do not reinterpret legacy Value as a rich parameter bag.

## 40. Visual action migration

Current C16:
- ExecuteCommand requires CommandId;
- Parameters on ExecuteCommand are rejected.

Future migration must preserve old actions exactly.

If Rich Command static preset Parameters are allowed:
- action contract version increments deliberately;
- old v1 action remains no-parameter;
- candidate Preview validates preset keys/types against Command;
- invalid legacy JSON is never silently interpreted.

Alternative:
leave visual action parameterless and use dedicated runtime Command form/dialog.

Main must choose.

## 41. Script migration

Existing scripts continue unchanged.

No existing write_tag call is reinterpreted as Command.

Future:
invoke_command(...)
is additive.

Server/Client scope rules remain distinct.

Dependencies must include stable Command reference before invocation is allowed.

## 42. Gateway migration

No migration.

Gateway remains TAG-to-TAG.

Existing routes remain unchanged.

Do not add action interpretation to existing gateway route fields.

## 43. Reusable libraries and fragments

Current reusable Screen/Popup v1 deliberately rejects navigation/command actions rather than silently dropping project-bound dependencies.

Rich Command implementation must preserve this fail-closed behavior until reusable dependency closure supports Commands.

Future reusable fragment/library support must:
- include or parameterize Command dependencies;
- include parameter schema;
- remap CommandId deterministically on incorporation when needed;
- keep DriverCommandBinding project-specific unless the package explicitly supports portable Data Source rebinding;
- reject dangling references.

Transient event bindings have the same dependency-closure requirement.

Do not make copy/paste appear successful while silently stripping Event/Command behavior.

## 44. Equipment/Capability migration

Current Capability role binding already supports:
CommandId

This is reusable.

A future event role binding may require a stable EventDefinitionId or equivalent.

Do not encode transient events as TagId merely because the current role-binding shape has only TagId/CommandId.

Potential research/product delta:

RESEARCH_CONTRACT_DELTA_REQUIRED — CAPABILITY-EVENT-BINDING-01

Add an explicit event identity role binding when first-class transient-event definitions are selected.

## 45. External API policy

### 45.1 Commands

Evolve the existing canonical Command endpoint rather than create protocol endpoints.

Possible future shape:

POST /api/commands/{id}/execute
body = canonical parameter values

Legacy no-body remains valid.

Backend returns truthful result.

### 45.2 Transient events

Do not expose a public unfiltered raw Driver event stream by default.

If future HMI/client use requires it:
- protected subscription;
- explicit EventDefinition filters;
- per-client bounded queues;
- slow-client isolation;
- active revision awareness;
- no secret/protocol raw payload.

Client Visual Script event consumption remains deferred until a separate product decision.

## 46. HMI UX convergence

### State control

When action represents current state:
show state from TAG.

Examples:
- toggle light;
- slider brightness;
- setpoint.

### Stateless action

Show a button/form:
- Invoke scene;
- Identify;
- Pulse.

Do not display a fake persistent selected/active state unless a real TAG reports one.

### Rich stateful action

Form may gather:
- position;
- transition time.

After submit:
- pending;
- accepted/completed result;
- state confirmation independently from TAG.

### Unknown outcome

UI must explicitly say outcome unknown/uncertain.

Do not show green "completed successfully" when dispatch may have happened but response was lost.

## 47. Script API convergence

Recommended eventual Server Script APIs:

event handler:
on_transient_event(event)

command:
invoke_command(command_id, parameters)

The exact names are implementation decisions.

Event object:
canonical typed fields only.

Command:
canonical parameter values only.

No:
driver.send(...)
mqtt.publish(...)
ha.call_service(...)
zigbee.command(...)
matter.invoke(...)

Client Visual Script may later call the same protected backend Command API under user authority.

## 48. Automation result handling

Automation must be able to branch on CommandResult.

Example:

result = invoke_command(...)

if result.outcome == "Unknown":
    emit diagnostic / stop sequence / request operator intervention

Do not automatically retry Unknown.

For deterministic sequences, automation author must explicitly choose retry/reconcile policy.

## 49. L0 strategy — pure contract validation

L0 must run without network, driver process or browser.

### Event L0

Prove:
- stable DefinitionId;
- unique EventId;
- scalar payload serialization;
- required/optional fields;
- field count/string/body limits;
- timestamp origin;
- optional sequence domain;
- wrap-aware dedup helper;
- identical legitimate events are not value-deduped;
- binding schema version validation;
- unsupported binding fails closed.

### Command L0

Prove:
- stable CommandId;
- unique InvocationId;
- parameter schema serialization;
- defaults;
- min/max/step;
- enum allowlist;
- Duration normalization;
- Percentage bounds;
- NaN/infinity rejection;
- unknown parameter rejection;
- DriverCommandBinding version validation;
- no raw RPC/JSON operation type;
- result outcome/dispatch mapping;
- Unknown semantics;
- legacy WriteTagValue compatibility.

### Shared L0

Prove:
- correlation/causation context bounds;
- protected fields cannot be supplied from untrusted request DTO;
- canonical scalar vocabulary remains deterministic;
- no secret fields exist in event/command public payloads.

## 50. L1 strategy — in-process Runtime fakes

L1 uses fake drivers/executors and the real common Runtime components.

### Event L1

Prove:
- burst ingress;
- bounded queue;
- reject/drop-newest behavior;
- gap diagnostics;
- duplicate suppression from explicit sequence evidence;
- counter wrap;
- out-of-order evidence;
- slow consumer isolation;
- consumer exception isolation;
- Server Script queue isolation;
- Active revision replacement;
- Standby no-forwarding;
- event -> script -> command causation context.

### Command L1

Prove:
- parameterless legacy Command;
- parameterized Rich Command;
- rejected invalid parameter before driver;
- authorization before driver;
- successful Accepted;
- successful Completed;
- Failed;
- Rejected;
- TimedOut before dispatch;
- Unknown after possible dispatch;
- executor saturation;
- per-target concurrency;
- cancellation semantics;
- no synthetic TAG state;
- later authoritative TAG update;
- stateless completion with no TAG;
- Audit mapping;
- no blind retry.

### Shared L1

Prove:
- event storm cannot starve Command admission globally;
- slow Command does not block unrelated Data Source event ingestion;
- automation loop threshold/diagnostic;
- Active revision change during request fails safely;
- HA fencing prevents duplicate physical effect.

## 51. L2 strategy — protocol-speaking simulated peers

L2 proves adapter semantics against real protocol framing/behavior without physical hardware.

At least one event-heavy protocol and one rich-command-capable protocol must be selected.

Recommended representatives when their implementation lanes are ready:

Event:
- Zigbee2MQTT action through a real MQTT broker/bridge simulator; or
- BTHome frame fixtures/receiver transport for advertisement event semantics.

Rich Command:
- Shelly RPC simulated device/server; or
- protocol peer that exposes parameterized/stateless operation.

Industrial regression:
- reuse existing IEC-104/DNP3 command semantic evidence to prove common result changes do not destroy ambiguity/SBO/pulse behavior.

L2 must cover:
- reconnect;
- duplicate delivery;
- malformed payload;
- delayed response;
- dropped response after dispatch;
- saturation;
- capability mismatch;
- schema/binding mismatch;
- sanitized diagnostics.

## 52. L3 strategy — integrated DriverHost/Engineering/Runtime

L3 uses the real host/application lifecycle with simulated protocol peers.

Required end-to-end flow:

Engineering candidate
-> Preview
-> Apply
-> Save
-> Publish
-> Activate
-> Driver runtime
-> Event/Command
-> Script/HMI consumer
-> Audit/Diagnostics
-> restart/recovery where applicable

### Event L3

Prove:
- Equipment/Capability/event definition materialization;
- active-only forwarding;
- Script event trigger;
- explicit Operational Event promotion;
- no raw Historian write;
- no fake TAG;
- diagnostics surface under #500 authority.

### Command L3

Prove:
- HMI parameter rendering;
- protected endpoint;
- CommandExecute scope;
- valid invocation;
- Audit;
- truthful CommandResult;
- independent state confirmation;
- Server Script invocation;
- legacy no-body Command remains green.

### Common L3 regression

Prove existing:
- TAG write;
- Alarm;
- Operational Event;
- Gateway;
- Historian;
- realtime TAG;
- reusable library failure behavior;
- package roundtrip;
- restart Active recovery.

## 53. L4 strategy — physical hardware

L4 must use selected real hardware, not claim universal compatibility.

### Event hardware evidence

Examples:
- real button single/double/hold;
- burst rotation/steps;
- coordinator reconnect;
- repeated/duplicate protocol behavior where observable;
- timestamp/sequence evidence;
- event loss diagnostics under induced pressure where safe.

### Command hardware evidence

Examples:
- real stateless action;
- real parameterized action;
- positive acknowledgement;
- negative response;
- disconnect before dispatch;
- disconnect immediately after dispatch where safe;
- state readback independent from result.

### HA hardware/runtime evidence

Where HA product mode is supported:
- only Active executes;
- Standby observes no duplicate physical action;
- failover with in-flight command yields truthful Unknown unless proven safe;
- no replay after failover by default.

L4 matrix must state:
- exact device;
- exact firmware;
- exact coordinator/controller;
- exact integration mode;
- what was actually proven.

Do not generalize from one model to an entire protocol ecosystem.

## 54. Performance gates

Implementation should define measured budgets.

Minimum required measurements:
- event ingress sustained rate;
- event burst capacity;
- event end-to-end dispatch latency;
- slow-consumer isolation;
- dropped-event diagnostic latency;
- Command admission latency;
- per-target and per-DataSource concurrency;
- Command execution timeout behavior;
- memory growth under burst;
- Script queue interaction;
- realtime UI overhead if exposed.

No product constants are fixed by research.

## 55. Failure-injection gates

Test deliberate:
- network partition;
- broker/bridge restart;
- sidecar crash where applicable;
- protocol malformed event;
- duplicate packet;
- sequence reset/wrap;
- driver exception in event normalization;
- consumer exception;
- queue full;
- command send exception before transmission;
- ambiguous send failure;
- response timeout;
- Active revision swap;
- HA authority loss;
- shutdown with pending event/command work.

Shutdown must not manufacture success.

## 56. Implementation slicing

Do not implement Event + Rich Command + every protocol + HMI + Scripts in one branch.

Recommended slices:

### S0 — shared contract lock

Scope:
- scalar type vocabulary;
- Event definition/occurrence contract;
- Command parameter/invocation/result contract;
- common binding envelope conventions;
- causality context if Main accepts it.

No protocol integration.

Gate:
L0.

### S1 — bounded common event delivery

Scope:
- bounded transient-event ingress;
- isolated consumers;
- drop/gap diagnostics;
- Active/HA fencing;
- no Script/HMI yet beyond test consumer.

Gate:
L0 + L1 event.

### S2 — Rich Command Runtime core

Scope:
- parameter validation;
- DriverCommandBinding registry/validation;
- optional executor seam;
- result/dispatch semantics;
- legacy WriteTagValue preserved;
- authorization/Audit endpoint evolution.

Gate:
L0 + L1 command.

### S3 — Engineering lifecycle/schema

Scope:
- persisted definitions/bindings;
- Preview/Apply;
- Save/Publish/Activate;
- import/export;
- revision persistence;
- package/backup roundtrip;
- schema migration chosen from current live base.

Gate:
L0/L1 + Engineering lifecycle.

### S4 — Script/Automation integration

Scope:
- Server Script transient event trigger;
- Server Script canonical Command invocation;
- declared dependencies;
- correlation/loop bounds;
- no driver access.

Client Visual Script command exposure may be a separate sub-slice because user authorization semantics differ.

Gate:
L1/L3.

### S5 — HMI integration

Scope:
- schema-driven parameter controls;
- parameterless compatibility;
- accepted/completed/unknown feedback;
- independent TAG state confirmation;
- optional static preset action parameters only if Main accepts that design.

Gate:
L3 browser/runtime.

### S6 — representative protocol adoption

Adopt on bounded protocol lanes after common foundations.

Recommended initial proof set:
- one transient-event-heavy integration;
- one parameterized/stateless rich-command integration.

Each protocol owns:
- binding schema;
- normalization;
- protocol result mapping;
- L2/L4 evidence.

### S7 — reusable library/fragment expansion

Only if Product requires portable views/assets with event/command actions.

Extend dependency closure deliberately.

Do not block initial Runtime foundation on this slice if fail-closed current behavior remains acceptable.

## 57. Implementation hotspot ownership

Shared hotspot files likely include:
- Scada.Core event/command contracts;
- Scada.Drivers.Abstractions;
- EngineeringContracts / schema;
- EngineeringExchangeService;
- EngineeringRuntimeCoordinator / Runtime facade;
- command endpoint/security;
- Script contracts/runtime;
- diagnostics projection;
- visual action/runtime API.

Main should serialize/coordinate these slices rather than run multiple writers across the same shared files.

Protocol workers should consume frozen shared contracts and avoid expanding central enums/interfaces independently.

## 58. Relationship to #543

#543 may provide:
- sidecar lifecycle;
- host-resource leases.

A sidecar-backed event/command integration uses:

Host Resource / sidecar
-> protocol adapter
-> common Event/Command contracts.

This research does not require #543 for direct-network protocols.

Do not put Event/Command semantics into Host Resource contracts.

## 59. Relationship to #497 / protected material

Rich command execution may need the same Data Source credentials already used by the driver session.

Reuse the active driver/session/protected material scope.

Do not resolve arbitrary new secrets from invocation parameters.

No separate Event/Command secret store.

## 60. Relationship to #472 Capability model

Capability remains semantic projection.

Examples:

Button:
- event role -> EventDefinitionId.

Scene:
- invoke role -> CommandId.

Cover:
- position state -> TagId;
- move rich action -> CommandId only when needed.

Light:
- on/off -> writable TagId when truthful;
- transition/color atomic action -> CommandId when needed.

Capability does not become Runtime execution authority.

## 61. Compatibility matrix

| Existing feature | Required result |
|---|---|
| Existing TAGs | unchanged |
| Existing Driver writes | unchanged |
| Existing WriteTagValue Commands | unchanged |
| Existing HMI ExecuteCommand no-body | unchanged |
| Existing CommandExecute authorization | unchanged |
| Existing Command Audit | unchanged |
| Existing Operational Event | unchanged |
| Existing Alarm | unchanged |
| Existing TAG Gateway | unchanged |
| Existing Historian | unchanged |
| Existing Server Script APIs | unchanged until additive APIs are enabled |
| Existing Client Visual Scripts | unchanged |
| Existing drivers without rich executor | unchanged |
| Existing projects with no transient events | unchanged |
| Existing reusable Screen/Popup action restriction | remains fail-closed |

## 62. Migration impact summary

Low-risk/additive:
- new optional event definitions;
- new optional command parameter definitions;
- new optional driver command bindings;
- new optional executor capability;
- new optional diagnostics evidence;
- new optional Script event/command APIs.

High-risk/shared:
- Engineering schema version;
- Runtime coordinator activation;
- shared Driver abstractions;
- command endpoint request/response shape;
- visual action parameter semantics;
- Script authorization identity;
- reusable dependency closure.

These must be Main-owned coordinated changes.

## 63. Explicit contract delta register

From CHECKPOINT 1:

RESEARCH_CONTRACT_DELTA_REQUIRED — TRANSIENT-DEVICE-EVENT-CONTRACT-01

RESEARCH_CONTRACT_DELTA_REQUIRED — COMMON-EVENT-BOUNDED-DISPATCH-01

RESEARCH_CONTRACT_DELTA_REQUIRED — SERVER-SCRIPT-TRANSIENT-EVENT-TRIGGER-01

RESEARCH_CONTRACT_DELTA_REQUIRED — TRANSIENT-EVENT-OPERATIONAL-PROMOTION-01

From CHECKPOINT 2:

RESEARCH_CONTRACT_DELTA_REQUIRED — RICH-COMMAND-PARAMETER-SCHEMA-01

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-COMMAND-BINDING-01

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-COMMAND-EXECUTOR-01

RESEARCH_CONTRACT_DELTA_REQUIRED — COMMAND-INVOCATION-RESULT-01

RESEARCH_CONTRACT_DELTA_REQUIRED — SCRIPT-COMMAND-INVOKE-01

RESEARCH_CONTRACT_DELTA_REQUIRED — HMI-RICH-COMMAND-01

Convergence additions:

RESEARCH_CONTRACT_DELTA_REQUIRED — RUNTIME-INTERACTION-CAUSALITY-01

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-INTERACTION-DIAGNOSTICS-01

RESEARCH_CONTRACT_DELTA_REQUIRED — CAPABILITY-EVENT-BINDING-01

NONE are implemented by this research.

## 64. What should NOT be a new contract

Do not create:
- second TAG model;
- Residential Event Bus;
- Home Command Bus;
- protocol-specific HMI command API;
- protocol-specific Script API;
- generic raw RPC Command;
- generic arbitrary JSON event;
- second Audit system;
- second Historian;
- second Driver health model;
- command-specific secret store;
- event-specific secret store.

## 65. Main decisions required before S0

Main must freeze:

1. first-class TransientEventDefinition versus compiled Capability/binding definition;
2. exact common Event type names/namespaces;
3. exact scalar type enum/serialization;
4. exact bounded event dispatcher ownership relative to IScadaEventBus;
5. exact Command parameter/invocation/result type names;
6. exact DriverCommandBinding envelope;
7. rich executor location: Driver SDK optional interface vs DriverHost adapter registry;
8. Server Script trusted authorization identity/policy;
9. HMI static preset Parameters versus dedicated runtime form;
10. diagnostics typed extension shape;
11. causality context fields and hop-limit semantics;
12. implementation schema version after rebase.

## 66. GO/WAIT assessment by component

### Transient Event domain contract

GO_WITH_GATES

Reason:
semantic need is proven and no existing contract truthfully covers occurrence-only device input.

### Common bounded event dispatch

GO_WITH_GATES / REQUIRED BEFORE HIGH-RATE ADOPTION

Reason:
current sequential in-memory publish is not adequate for untrusted-rate driver event delivery.

### Operational Event promotion

GO_WITH_GATES

Reason:
existing Operational Event is correct durable process event destination when explicit promotion is desired.

### Rich Command parameter schema

GO_WITH_GATES

Reason:
current fixed WriteTagValue model cannot represent parameterized/stateless action without raw driver exposure.

### Driver rich-command executor

GO_WITH_GATES

Reason:
optional additive capability preserves current Driver interface compatibility.

### HMI Rich Command

WAIT_FOR_CORE_CONTRACT

Do not broaden ExecuteCommand Parameters before Runtime/Engineering schemas are frozen.

### Server Script event/command APIs

WAIT_FOR_CORE_CONTRACT

Scripts should consume the frozen canonical contracts, not define them.

### Client Visual transient event subscriptions

WAIT_PRODUCT_REQUIREMENT

No v1 need proven.

### Gateway Event/Command expansion

REJECT_FOR_V1

Gateway remains TAG state-routing.

### Raw durable transient-event store

WAIT_PRODUCT_REQUIREMENT

No v1 requirement.

### Dedicated Command history store

WAIT_PRODUCT_REQUIREMENT

Existing Audit is sufficient for first implementation.

## 67. Final overall recommendation

TRANSIENT-EVENT-RICH-COMMAND-CONVERGENCE = GO_WITH_GATES

The research has enough evidence for Main to prepare coordinated foundation implementation.

It is not authorization for this research branch to implement or merge product code.

Required implementation order:

1. Main contract lock/rebase.
2. S0 shared contracts.
3. S1 bounded transient event Runtime.
4. S2 Rich Command Runtime.
5. S3 Engineering lifecycle/schema.
6. S4 Scripts/Automation.
7. S5 HMI.
8. S6 representative protocols.
9. S7 reusable portability only when required.

S1 and S2 may be parallel only if Main isolates shared hotspots or freezes shared S0 types first.

## 68. Exit criteria before first protocol claims support

A protocol integration may claim transient-event support only when:
- common Event contract is active;
- bounded dispatch is proven;
- source identity is canonical;
- dedup/ordering semantics are evidence-based;
- diagnostics expose drops/errors;
- L2 protocol behavior is proven;
- L4 device behavior is scoped to tested hardware if claimed.

A protocol integration may claim Rich Command support only when:
- canonical parameter schema is active;
- binding schema is registered/versioned;
- CommandExecute authorization is used;
- Audit is recorded;
- result ambiguity is truthful;
- no state is synthesized;
- no blind retry after possible dispatch;
- L2 protocol behavior is proven;
- L4 physical behavior is scoped to tested hardware if claimed.

## 69. CHECKPOINT 3 declarations

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO SCHEMA CHANGED

NO EVENT RUNTIME CHANGED

NO COMMAND RUNTIME CHANGED

NO DRIVER SDK CHANGED

NO GATEWAY CHANGED

NO SCRIPT RUNTIME CHANGED

NO FRONTEND CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

HA = HIGH AVAILABILITY

HAB = HOME ASSISTANT BRIDGE

NO MERGE PERFORMED
