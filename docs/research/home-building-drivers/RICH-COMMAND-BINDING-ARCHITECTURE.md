# RICH-COMMAND-BINDING-ARCHITECTURE

Status: CHECKPOINT 2 / RESEARCH ONLY / DOCS ONLY

Issue owner: #546 — HOME-RESEARCH-05 — transient events + rich command binding architecture

Contract: C-DRIVER-TRANSIENT-RICH-COMMAND-RESEARCH-01

Order: DRIVER-TRANSIENT-EVENT-RICH-COMMAND-RESEARCH-01

Scope of this checkpoint: RICH-COMMAND-BINDING-01 only.

Canonical terminology:
- HA = High Availability.
- HAB = Home Assistant Bridge.
- Home Assistant = external platform by name.

No product code, schema, Driver SDK, Runtime command implementation, Gateway, frontend, tests, dependencies or CI are changed by this document.

## 1. Authority snapshot audited

GitHub live was revalidated before this checkpoint.

- integration: wave15/corrections-integration
- integration/base HEAD: 77b08d60333685b4ba06aa649e3125f23b475dcf
- research branch: research/home-transient-events-rich-commands
- research HEAD before this document: f4759a44c32a7f0505722eb67dbfb60bcb72d75d
- merge-base: 77b08d60333685b4ba06aa649e3125f23b475dcf
- ahead/behind before this document: 1 / 0
- existing research delta before this document: only DRIVER-TRANSIENT-EVENT-ARCHITECTURE.md

Issue #546 and its current MAIN RELEASE were reread. No newer Main order changed the research-only/docs-only scope.

## 2. Current Command architecture audited READ-ONLY

The live product already has a useful canonical Command boundary, but it is intentionally narrow.

| Area | Live contract | Finding |
|---|---|---|
| Core Command | src/Scada.Core/Commands/CommandModels.cs | CommandKind currently contains only WriteTagValue. CommandDefinition stores one fixed TargetTagId/Path + one configured Value. |
| Engineering | CommandEngineeringDto | Stable Id/Key/Name, fixed Value, target TAG, scope metadata, Enabled. No invocation-time parameter schema. |
| Validation | CommandEngineeringValidator | Requires one target TAG and one configured value; only WriteTagValue is valid. |
| Runtime | EngineeringRuntimeCoordinator.ExecuteCommandAsync | Resolves active Command, then calls the owning driver or Server Memory WriteAsync on the Command target TAG. |
| Driver SDK | ICommunicationDriver | Runtime surface exposes ReadAsync/WriteAsync only; there is no common rich-operation executor. |
| HMI API | /api/commands/{id}/execute | Backend resolves/authorizes/executes one canonical Command. Current request has no body. |
| HMI visual action | VisualNavigationActionEngineeringDto | ExecuteCommand carries stable CommandId. The general action contract already has JSON-native Parameters, but C16 explicitly rejects Parameters for ExecuteCommand. |
| HMI runtime | runtimeCommandApi.ts | POSTs only Command identity. No invocation parameters are sent. |
| Authorization | SecurityCapability.CommandExecute | Existing capability and Command-scoped AuthorizationResource already protect execution. |
| Audit | AuditActions.CommandExecute | Existing Command endpoint records denied/succeeded/failed Audit. |
| Equipment Capability | CapabilityRoleBindingEngineeringDto | Capability roles can already reference CommandId. This is compatible with richer Commands without changing the Capability reference shape. |
| Server Scripts | ServerScriptRunner.py | Supports TAG reads/writes and Operational Event emission; no canonical invoke-command API exists. |
| Client Visual Scripts | ScriptApiSurface.ClientVisual | Conceptually permits RequestAuthorizedBackendOperation, but the public client Script API does not currently expose a general Command invocation provider. |
| Gateway | GatewayRuntimeEngine | Routes TAG state/value only. No Command routing. |
| Active lifecycle | Engineering Runtime | Command authority follows the successfully activated Engineering revision. |

## 3. Evidence from existing protocol implementations

The product already demonstrates why canonical Command result semantics cannot be reduced to "write succeeded".

### 3.1 IEC-104

The existing IEC-104 command coordinator distinguishes:

- Accepted;
- Completed;
- Rejected;
- TimedOut;
- Ambiguous;
- Cancelled.

It also tracks whether execute may have been transmitted and whether positive acceptance was observed.

Important conclusion:

A transport/protocol command may have a physically ambiguous outcome after dispatch.

The common Rich Command contract therefore needs a truthful "unknown/ambiguous" result and must not automatically retry externally effectful operations.

### 3.2 DNP3

The existing DNP3 implementation demonstrates that one canonical TAG write may already hide richer protocol control behavior behind the driver binding:

- Select-Before-Operate vs Direct Operate;
- Latch On/Off;
- Pulse On/Off;
- count;
- on-time;
- off-time;
- Trip/Close code;
- analog command variation.

Today these are primarily configured in the point/binding profile rather than supplied dynamically per invocation.

Important conclusion:

Rich Command should not replace every protocol-aware TAG write.

It is required only when invocation-time action semantics or parameters cannot be truthfully represented by the existing TAG write contract.

## 4. Central decision: when to use TAG write versus Rich Command

### 4.1 Keep Runtime.WriteAsync(TAG) / WriteTagValue

Use the existing TAG path when the user/system intent is simply:

"set this canonical current value"

Examples:
- light on/off where a writable canonical OnOff TAG exists;
- setpoint = 22.5;
- cover position = 40 percent when one writable position TAG truthfully represents the operation;
- fan mode = Auto when one canonical writable state TAG truthfully represents the operation.

The normal rule remains:

write request
-> driver operation
-> authoritative report/readback
-> canonical TAG state.

Do not publish requested state as confirmed process state merely because the write returned.

### 4.2 Use Rich Command

Use Rich Command when the intent is an action that cannot be represented honestly as one ordinary TAG value.

Examples:
- InvokeScene(sceneId);
- Pulse(duration);
- OpenFor(seconds);
- Move(position, transitionTime);
- SetColor(red, green, blue) when atomic multi-argument semantics matter;
- Identify(duration);
- StartCalibration(mode);
- a bounded device-specific action that is legitimate ordinary process operation.

### 4.3 Do not use Rich Command as a universal escape hatch

Rich Command must not become:
- raw driver method invocation;
- raw MQTT topic/payload execution;
- raw Zigbee cluster invocation from UI;
- raw Z-Wave Command Class call;
- raw Home Assistant service call;
- raw shell/network/API call;
- arbitrary JSON RPC passthrough;
- substitute for admin/commissioning operations.

## 5. Privileged operations excluded

The ordinary process Command contract must remain separate from privileged administration/commissioning.

Explicitly excluded:
- permit join;
- network create/reset;
- factory reset;
- controller/NVM restore;
- key rotation;
- device inclusion/exclusion where it changes network membership;
- firmware upgrade;
- certificate/trust changes;
- HA promotion/failover;
- installation/admin maintenance.

These belong to dedicated protected contracts with their own authorization, audit and lifecycle.

## 6. Canonical model recommendation

The existing Command identity should be preserved and evolved additively.

Recommended common concepts:

- CommandDefinition
- CommandParameterDefinition
- CommandInvocation
- CommandResult
- DriverCommandBinding
- optional driver command executor capability

The names are preliminary contract names for Main review.

## 7. CommandDefinition

A canonical CommandDefinition represents stable product/project intent independent of protocol implementation.

Minimum semantics:

- Id: stable Guid
- Key: stable human/developer key
- Name
- Description optional
- Area optional
- EquipmentId or EquipmentPath when applicable
- CapabilityId optional
- ParameterDefinitions
- execution shape/binding mode
- Enabled

Command identity must remain stable when the underlying protocol binding changes.

Example:

Equipment: LivingRoom.Light

Command:
SetBrightness

must remain the same canonical Command even if implementation changes from:
- Shelly RPC;
- Zigbee;
- Matter;
- HAB;
- another future driver.

Canonical Command != protocol method.

## 8. Preserve legacy WriteTagValue Commands

Existing CommandKind.WriteTagValue remains valid and supported.

Existing projects with:
- TargetTagId/TargetTagPath;
- configured fixed Value;
- no parameters;

must continue to execute with current semantics.

Rich Command evolution must be additive rather than reinterpret existing Commands.

Recommended compatibility model:

1. legacy/fixed WriteTagValue Command:
   canonical Command -> existing TAG write path;

2. rich bound Command:
   canonical Command -> validated invocation -> DriverCommandBinding -> bounded driver command executor.

Do not silently convert existing TAG Commands into rich Driver commands.

## 9. Parameter schema

Recommended v1 parameter types:

- Boolean
- Integer
- Number
- String
- Enum
- Duration
- Percentage

### 9.1 Why these types

Boolean:
ordinary flags/options.

Integer:
counts, scene IDs, bounded discrete positions when semantically integer.

Number:
continuous values.

String:
bounded identifiers/text only where genuinely needed.

Enum:
canonical allowlisted modes.

Duration:
explicit time semantics without callers guessing milliseconds versus seconds.

Percentage:
common normalized 0..100 semantic for level/position/brightness.

### 9.2 Types not required in v1

Color:
Do not add a dedicated Color scalar yet. A first implementation can model canonical red/green/blue or hue/saturation/brightness parameters explicitly. Add a common Color type only after multiple real capability contracts prove one stable representation.

Equipment reference:
Not in ordinary v1 parameters. Allowing callers to select arbitrary Equipment at invocation time can widen the command target beyond the authorized binding.

TAG reference:
Not in ordinary v1 parameters for the same reason.

Arbitrary object/JSON:
Rejected.

Binary/blob:
Rejected.

Code/expression:
Rejected.

Secret/sensitive parameter:
Rejected for ordinary process Commands. Secrets belong to protected material, not per-invocation Command payloads.

## 10. Parameter metadata

Recommended declarative metadata:

- Key
- Type
- Required
- DefaultValue optional
- Minimum optional
- Maximum optional
- Step optional
- Unit optional
- EnumValues optional
- DisplayLabel optional
- Description optional
- MaximumLength optional for String

Rules:
- unknown invocation parameters are rejected;
- duplicate parameter keys are invalid;
- defaults must validate against the same schema;
- Minimum <= Maximum;
- Step must be positive when present;
- Enum values must be bounded, unique and non-empty;
- Percentage is bounded to 0..100;
- Duration must have explicit canonical serialization/normalization;
- NaN and infinity are invalid Number values;
- String values are bounded;
- no custom code/script validator.

## 11. Parameter bounds

Main/implementation must set measured product constants, but the architecture requires explicit limits.

Recommended initial research guardrails:
- at most 16 parameters per Command;
- bounded parameter key length;
- bounded enum count;
- bounded string length;
- bounded serialized invocation body;
- bounded numeric/duration ranges through definitions.

These are test-design guardrails, not authorized product constants.

## 12. CommandInvocation

A CommandInvocation is runtime intent, not Engineering configuration.

Recommended fields:

- InvocationId
- CommandId
- Parameters
- RequestedAtUtc
- CorrelationId optional

Server-owned execution context should additionally know:
- active ProjectKey;
- active Revision;
- authenticated principal or trusted service/script identity;
- source surface such as HMI, Client Script, Server Script or Automation.

The caller must not be allowed to forge:
- principal;
- roles;
- project/revision authority;
- DataSourceId;
- driver operation;
- protocol target.

## 13. Invocation identity and retries

InvocationId is correlation identity.

It does not automatically make a physical operation idempotent.

Rules:
- every accepted request gets one stable invocation identity;
- duplicate HTTP/network retries must not be blindly replayed when physical outcome is unknown;
- automatic retry is allowed only when the resolved driver operation explicitly proves retry safety/idempotence;
- otherwise an Unknown result requires an explicit higher-level decision before retry.

A future bounded idempotency cache may be useful, but this checkpoint does not prescribe persistence or duration.

## 14. CommandResult

A canonical Command result must describe execution truth without claiming process state.

Recommended outcome vocabulary:

- Rejected
- Failed
- Accepted
- Completed
- TimedOut
- Unknown

Recommended dispatch evidence:

- NotDispatched
- Dispatched
- PossiblyDispatched

Recommended result fields:
- InvocationId
- CommandId
- Outcome
- DispatchState
- StartedAtUtc
- FinishedAtUtc
- DetailCode optional
- sanitized bounded Detail optional
- CorrelationId optional

### 14.1 Meaning

Rejected:
validation/admission/remote rejection with no successful acceptance.

Failed:
definitive execution failure.

Accepted:
executor/protocol positively accepted the action, but completion is not known or not part of the protocol.

Completed:
protocol/executor operation completion semantics were satisfied.

TimedOut:
timeout occurred in a condition where physical dispatch/result is still known sufficiently to classify as timeout rather than ambiguity.

Unknown:
operation may have been dispatched or accepted, but physical outcome cannot be proven.

### 14.2 Critical rule

Completed != process state confirmed.

Example:

SetPosition(40) returns Completed

does not authorize EliteSCADA to publish:
position = 40

unless authoritative state/readback reports 40.

## 15. Requested / Dispatched lifecycle

Do not create a large workflow engine merely to represent Commands.

The canonical result plus dispatch evidence is sufficient for v1.

Internal diagnostics may record stages such as:
- requested;
- validated;
- dispatched;
- response observed;

without turning each stage into a durable first-class domain entity.

## 16. Stateful Commands

A stateful Command is an action expected to influence a canonical state.

Examples:
- SetPosition(40);
- SetBrightness(70);
- SetFanMode(Auto).

Preferred rule:

If the intent can be represented as one existing writable canonical TAG, use TAG write.

Use Rich Command only when the action requires additional semantics/arguments or atomic multi-field execution.

Result flow:

CommandInvocation
-> driver command
-> CommandResult
-> independent authoritative report/readback
-> TAG update.

The CommandResult must not mutate TAG state by assumption.

## 17. Stateless Commands

Examples:
- Identify;
- Pulse;
- InvokeScene;
- TriggerAction.

They may have no meaningful resulting TAG.

The CommandResult is therefore the authoritative execution response, but still only at the action/protocol level.

No fake process state should be created merely to expose completion.

## 18. DriverCommandBinding

Recommended envelope is deliberately analogous to CommunicationTagBinding.

Minimum common binding fields:

- ContractVersion
- SchemaId
- SchemaVersion
- DataSourceId
- PortableTarget
- OperationKey
- Settings optional, non-secret

### 18.1 Meaning

ContractVersion:
version of the common Rich Command binding envelope.

SchemaId / SchemaVersion:
driver-owned public binding schema.

DataSourceId:
canonical source/integration authority.

PortableTarget:
stable portable protocol/device target identity owned by the binding.

OperationKey:
bounded driver-owned operation identity.

Settings:
non-secret static binding configuration.

### 18.2 Examples

Canonical:
InvokeScene(sceneId)

Binding may internally resolve to:
- Zigbee scene command;
- Z-Wave scene operation;
- Matter command;
- HAB mapped service.

Canonical:
Pulse(duration)

Binding may resolve to:
- DNP3 pulse profile;
- relay pulse RPC;
- device action.

The HMI/Script sees:
CommandId + canonical parameters.

It does not see:
raw protocol method/topic/service/cluster/CC.

## 19. Binding is not arbitrary RPC

DriverCommandBinding must have a registered schema/validator owned by the driver integration.

Reject:
- unknown SchemaId;
- unsupported SchemaVersion;
- unknown OperationKey;
- malformed PortableTarget;
- unknown settings;
- unbounded settings;
- secret material in Settings;
- arbitrary JSON payload.

A driver cannot register "rawRpc", "rawJson", "rawMqttPublish" or equivalent as a generic bypass.

## 20. Parameter mapping to the driver

The common contract should not attempt to encode every protocol transformation.

Recommended rule:

canonical parameter keys
-> validated CommandInvocation
-> driver-owned binding schema/adapter
-> protocol arguments

If a driver needs protocol-specific mapping/transforms, they remain inside its registered binding schema/adapter.

Do not add a common arbitrary transform language.

Do not allow script/code validators.

## 21. Driver SDK/runtime seam

Current ICommunicationDriver has only:
- ReadAsync;
- WriteAsync.

Do not force all existing drivers to implement rich commands.

Recommended additive direction:

optional Rich Command execution capability, conceptually:

ICommunicationCommandExecutor

or equivalent DriverHost adapter.

The exact interface name/location is a Main implementation decision.

Required semantics:
- implemented only by integrations that support rich operations;
- receives already resolved canonical invocation + validated driver binding;
- does not receive HttpContext/browser objects;
- returns canonical CommandResult/equivalent;
- concrete protocol SDK types never escape the driver adapter.

This is a shared Driver SDK/Runtime contract delta and must be coordinated.

## 22. Runtime execution pipeline

Recommended pipeline:

1. receive CommandId + invocation parameters;
2. resolve active CommandDefinition;
3. verify Active project/revision authority;
4. authorize server-side;
5. validate exact parameter schema;
6. resolve the active execution binding;
7. generate/accept bounded InvocationId;
8. execute through:
   - existing TAG write path for legacy WriteTagValue; or
   - optional driver command executor for Rich Command;
9. obtain CommandResult;
10. record canonical Audit;
11. return truthful result to caller;
12. wait for independent TAG report/readback for process state.

No frontend or Script calls the concrete driver.

## 23. Authorization

Reuse the existing:
SecurityCapability.CommandExecute

for ordinary process Rich Commands.

Existing Command-scoped authorization already has useful dimensions:
- Command identity/key;
- Area;
- Equipment;
- TAG where relevant.

Rules:
- authorization is server-side;
- parameter validity never grants authority;
- changing parameter values must not allow changing the bound target;
- caller cannot select another DataSource/Equipment/driver operation through parameters;
- disabled/inactive Commands fail closed;
- Active revision must remain consistent across authorization/execution.

No new SecurityCapability is required merely because parameters exist.

Privileged operations remain outside this contract.

## 24. Discover versus view versus invoke

Do not create a large new permission matrix prematurely.

Recommended v1:
- Engineering authoring/configuration continues under EngineeringModify and existing Engineering authority;
- runtime visibility/discovery follows existing protected Runtime/Engineering projection rules;
- invocation requires CommandExecute.

If a later product requirement needs "visible but not discoverable" commands, Main can extend authorization deliberately.

## 25. Audit

Reuse the existing Audit system and:
AuditActions.CommandExecute.

Do not create a second Command audit log.

Audit should include bounded/sanitized context such as:
- InvocationId;
- CommandId;
- CommandKey;
- result outcome;
- dispatch state;
- parameter names and safe bounded values where policy permits;
- active ProjectKey/revision;
- caller/source surface.

Ordinary Rich Command parameters must not carry secrets.

### 25.1 Mapping to existing AuditOutcome

Existing AuditOutcome is:
- Succeeded;
- Denied;
- Failed.

Recommended mapping:
- authorization/admission denied -> Denied;
- Accepted or Completed -> Succeeded, with precise CommandResult outcome in Details;
- Rejected/Failed/TimedOut/Unknown after authorized execution -> Failed, with precise CommandResult outcome and dispatch evidence in Details.

This keeps Audit truthful without expanding the Audit domain only for Command protocol nuance.

Audit success still means the Command execution boundary succeeded according to the recorded outcome; it never means process state reached the requested state.

## 26. Operational Event relationship

Do not automatically emit OperationalEventOccurred for every Command request/result.

Audit already records protected Command execution.

If a process transition caused by a Command is operationally meaningful:
- authoritative TAG/state logic may generate an Operational Event; or
- explicit configured process logic may emit one.

Operational Event remains separate from Command Result.

## 27. HMI rendering

HMI should render from canonical CommandDefinition + ParameterDefinitions, never from protocol identity.

Recommended rendering:

Parameterless stateless Command:
- button.

Boolean:
- checkbox/toggle in a dialog or control;
- use a persistent toggle only when it is tied to authoritative state and therefore truthfully represents current state.

Integer/Number/Percentage/Duration:
- numeric input/slider where appropriate and bounded by schema.

Enum:
- select/radio control.

Multiple parameters:
- dialog/form.

Potentially consequential ordinary process command:
- confirmation UI may be an Engineering presentation hint, but confirmation does not replace authorization.

The backend revalidates every parameter regardless of HMI constraints.

## 28. Existing visual action Parameters

VisualNavigationActionEngineeringDto already contains JSON-native Parameters, but current C16 behavior deliberately rejects Parameters on ExecuteCommand.

Do not simply remove that guard and pass arbitrary JSON.

Future safe evolution may use the field only when:
- action contract version is deliberately advanced;
- keys are validated against the referenced CommandParameterDefinition set;
- values are converted through canonical parameter types;
- unknown values fail Preview;
- runtime revalidates again;
- no driver/protocol fields are accepted.

Static action Parameters can represent a preset invocation.

Dynamic runtime user input should be collected by the Command UI and supplied as canonical invocation parameters, not stored as mutable process state.

## 29. HMI process truth

Current Runtime visual code already distinguishes TAG write acceptance from readback confirmation for direct SetTagValue actions.

Rich Command must preserve the same principle.

Recommended UI feedback:
- pending;
- rejected/failed;
- accepted;
- completed;
- unknown;
- state confirmed separately if the visual also observes an authoritative TAG.

Do not label Accepted as "state changed" unless a TAG report confirms it.

## 30. Server Scripts

Current Server Scripts can read/write declared TAGs but cannot invoke canonical Commands.

Recommended future conceptual API:

invoke_command(command_id, parameters)

Exact API name is not fixed by this research.

Required rules:
- Command must be a declared Script dependency;
- active revision only;
- parameters validated against CommandDefinition;
- execution uses canonical Runtime authority;
- no driver method exposure;
- bounded timeout/execution;
- no automatic retry on Unknown;
- Audit records the Server Script identity/project/revision as the execution source.

Server Scripts must not gain blanket Command execution merely because they are server-side.

A service/script authorization policy must be explicit.

## 31. Client Visual Scripts

The architecture already allows Client Visual scripts conceptually to request authorized backend operations.

When Command invocation is exposed:

Client Visual Script
-> canonical backend Command API
-> logged-in principal
-> SecurityCapability.CommandExecute
-> CommandDefinition
-> Runtime execution

Rules:
- same authorization as HMI;
- no stronger principal;
- no driver access;
- parameters use the same canonical schema;
- no browser-side trust for validation.

Do not create a separate Script-only Command path.

## 32. Gateway

Decision for v1:

TAG Gateway remains state-routing only.

Do not add:
TAG value -> Rich Command invocation

to Gateway automatically.

Reasons:
- Gateway is currently a TAG-to-TAG state transfer engine;
- Command execution has authorization/audit/retry/ambiguity semantics not owned by Gateway;
- adding actions would create a second automation engine.

For event/condition -> Command automation, use:
- Server Scripts;
- future canonical Automation contract built on the same Command API.

## 33. Equipment/Capability integration

Current EquipmentCapabilityEngineeringDto can bind roles to CommandId.

This is a strong reuse point.

Example:

Equipment capability:
Cover

Roles:
- positionState -> TagId
- move -> CommandId

or where simple writable state is enough:
- position -> TagId only.

Example:

Scene capability:
- invoke -> CommandId

Example:

Button capability:
- click -> transient event definition
- no fake persistent TAG required.

The Capability model therefore remains a semantic projection and does not become a second command runtime.

## 34. Examples

### 34.1 InvokeScene(sceneId)

Classification:
Rich stateless Command.

Definition:
- parameter sceneId: Integer or bounded Enum where scenes are known.

Binding:
- DataSource + target device/group + driver OperationKey.

Result:
- Accepted/Completed/Rejected/etc.

State:
none required.

Audit:
yes, ordinary command execution.

### 34.2 Pulse(duration)

Classification:
Rich stateless Command unless duration is fixed Engineering configuration behind an ordinary TAG write.

Parameter:
- duration: Duration with bounded min/max.

Binding:
may resolve to DNP3 pulse, Shelly-like relay operation or another driver implementation.

State:
do not create fake pulse-active TAG unless device really reports one.

### 34.3 OpenFor(seconds)

Classification:
Rich action only if protocol/device guarantees one bounded operation.

Parameter:
- duration.

Caution:
Do not emulate long-duration workflow inside a low-level driver if correct semantics actually require higher-level automation:
Open
-> wait
-> Close

That sequence belongs to automation/script unless the device natively owns the OpenFor operation.

### 34.4 Move(position, transitionTime)

Classification:
Rich stateful Command because atomic multi-parameter semantics matter.

Parameters:
- position: Percentage;
- transitionTime: Duration.

Result:
protocol-level execution.

State:
authoritative position TAG updates only from report/readback.

### 34.5 SetColor(red, green, blue)

Classification:
Rich stateful atomic multi-parameter Command when a common composite color state contract does not yet exist.

Parameters:
- red, green, blue: Integer/Number with explicit bounds.

Future:
a proven common Color parameter type may replace this representation later.

### 34.6 SetFanMode(Auto)

If one writable canonical fan-mode TAG exists:
prefer TAG write / WriteTagValue.

If protocol semantics require additional parameters/transaction:
Rich Command may be justified.

### 34.7 StartCalibration(mode)

Potentially ordinary process Rich Command only if calibration is a safe normal operational action.

If calibration changes protected device commissioning/configuration:
move it to a dedicated privileged/admin contract.

### 34.8 AcknowledgeDeviceFault(code)

Do not confuse device-specific acknowledgement with EliteSCADA Alarm acknowledgement.

If it is a normal external device action:
it may be Rich Command.

EliteSCADA Alarm acknowledgement continues through the canonical Alarm contract.

## 35. Command Definition generation from discovery

Future Driver discovery may suggest Commands.

Recommended flow:

driver discovery candidate
-> semantic Capability
-> suggested canonical CommandDefinition
-> suggested DriverCommandBinding
-> Preview
-> Apply
-> normal Save/Publish/Activate.

Discovery must not mutate Runtime directly.

The user should see:
- canonical command name;
- parameters;
- capability/equipment context.

Advanced diagnostics may show binding details, but normal HMI/Script surfaces do not.

## 36. Static binding settings versus invocation parameters

Keep this boundary explicit.

Engineering/static binding settings:
- protocol command mode;
- addressing;
- fixed priority;
- operation variant;
- profile settings;
- target endpoint;
- non-secret execution configuration.

Invocation parameters:
- values the caller is deliberately allowed to choose each time.

Example BACnet:
priority should remain static Engineering configuration unless a future explicit product contract deliberately authorizes caller-selectable priority.

Example DNP3:
Select-Before-Operate may remain static binding policy even when pulse duration is an allowed invocation parameter.

Do not expose every protocol knob as an invocation parameter.

## 37. Secrets

No secrets in:
- CommandDefinition;
- parameter defaults;
- invocation parameters;
- DriverCommandBinding.Settings;
- Audit parameter details.

Protected material remains host-owned through the existing secret/protected-material authority.

## 38. Concurrency and backpressure

Rich Command execution must be bounded per Data Source/target where required.

The existing IEC-104 implementation already demonstrates:
- bounded command concurrency;
- per-point in-flight exclusion;
- no blind queueing when command concurrency is exhausted.

Common rules:
- do not create an unbounded command queue;
- driver executor may reject admission when saturated;
- return truthful Rejected result when not dispatched;
- do not reorder commands to one target if order has process meaning;
- slow protocol execution must not block unrelated Data Sources globally.

Exact queue/concurrency policy may remain driver-specific behind common result semantics.

## 39. Cancellation

Cancellation before dispatch:
can return a definitive failure/cancelled local outcome.

Cancellation after possible dispatch:
must not claim that the external effect was cancelled.

If external outcome is no longer knowable:
CommandResult = Unknown / PossiblyDispatched.

Do not convert client disconnect into physical cancellation truth.

## 40. Timeouts

Timeouts require dispatch context.

Before dispatch:
TimedOut/NotDispatched is truthful.

After confirmed/possible dispatch without definitive response:
Unknown is more truthful than TimedOut alone.

This is directly supported by the existing IEC-104 ambiguity handling.

## 41. Result persistence

Default recommendation:

Do not create a dedicated durable Command Result store in v1.

Use:
- immediate CommandResult response;
- common diagnostics/counters;
- existing security Audit for durable protected action trace.

If future product requirements need a browsable command-execution history distinct from security Audit, Main should evaluate it deliberately rather than silently overloading Operational Events.

## 42. Diagnostics

Rich Command should integrate with the shared diagnostics direction rather than create a parallel diagnostics subsystem.

Candidate counters:
- commandsRequested;
- commandsRejected;
- commandsDispatched;
- commandsAccepted;
- commandsCompleted;
- commandsFailed;
- commandsTimedOut;
- commandsUnknown.

Protocol-specific counters remain behind driver diagnostics where appropriate.

Exact common diagnostics convergence is deferred to CHECKPOINT 3 / #500 relationship review.

## 43. Compatibility

Required direction:

ADDITIVE EVOLUTION

Existing projects:
- continue using WriteTagValue Commands;
- do not need new parameter definitions;
- do not need DriverCommandBinding;
- preserve current HMI no-body ExecuteCommand behavior;
- preserve current Capability CommandId references;
- preserve current authorization/Audit semantics.

A future endpoint may accept an optional validated invocation body while legacy no-body requests remain valid for parameterless/fixed Commands.

## 44. Engineering schema impact

A product implementation will likely require new optional Engineering fields/contracts for:
- parameter definitions;
- rich command binding;
- possibly command execution mode.

This is a schema/product delta.

This research does NOT:
- choose a schema version;
- reserve a schema version;
- modify EngineeringPackage;
- define migration code.

Main must select the schema version only at implementation release after rebasing against the then-current authority.

## 45. Copy/paste / fragment / reusable library impact

Rich Commands referenced by:
- Equipment Capabilities;
- Dynamos;
- Screens/Popups;
- Scripts;
- reusable libraries;

must preserve stable Command identity and dependency closure.

A fragment containing a visual/Capability reference to a Command cannot silently copy only the frontend reference while dropping the Command definition/binding.

Exact copy/remap rules belong to implementation convergence, but stable identity and dependency analysis are mandatory.

## 46. Security abuse cases

Implementation must explicitly test:
- unknown parameter;
- missing required parameter;
- type confusion;
- NaN/infinity;
- out-of-range duration/percentage;
- oversized string;
- oversized request;
- caller tries to change target;
- caller tries to supply DataSourceId;
- caller tries to supply OperationKey;
- unauthorized command;
- stale Active revision;
- duplicate network retry;
- command spam;
- target concurrency exhaustion;
- timeout before dispatch;
- timeout/connection loss after possible dispatch;
- malformed driver result.

## 47. Minimum L0 gates for Rich Command

Full L0-L4 convergence belongs to CHECKPOINT 3.

Before implementation acceptance, L0 must at least prove:
- parameter schema serialization;
- default validation;
- enum/bounds validation;
- invocation validation;
- stable Command identity;
- DriverCommandBinding validation/versioning;
- unknown field rejection;
- no arbitrary JSON bypass;
- result outcome mapping;
- dispatch-state semantics;
- legacy WriteTagValue compatibility.

## 48. Minimum L1 gates

Fake runtime/driver must prove:
- parameterless invocation;
- required/optional parameters;
- invalid parameter rejection before driver call;
- unauthorized rejection before driver call;
- successful Accepted;
- successful Completed;
- definitive driver failure;
- saturation rejection;
- timeout before dispatch;
- possible-dispatch ambiguity -> Unknown;
- stateful Command does not synthesize TAG state;
- subsequent authoritative TAG report updates state;
- stateless Command completes with no TAG creation;
- Audit records result truth;
- legacy fixed Command still executes current TAG write path.

## 49. Research contract deltas required

NONE are implemented in this lane.

### RESEARCH_CONTRACT_DELTA_REQUIRED — RICH-COMMAND-PARAMETER-SCHEMA-01

Add bounded canonical CommandParameterDefinition and validated invocation values.

### RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-COMMAND-BINDING-01

Add a versioned, validated, non-secret driver command binding envelope analogous to CommunicationTagBinding.

### RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-COMMAND-EXECUTOR-01

Add an optional common Runtime/Driver capability for rich operations without forcing all ICommunicationDriver implementations to expose protocol methods publicly.

### RESEARCH_CONTRACT_DELTA_REQUIRED — COMMAND-INVOCATION-RESULT-01

Add canonical invocation/result semantics with dispatch evidence and truthful Accepted/Completed/Unknown distinction.

### RESEARCH_CONTRACT_DELTA_REQUIRED — SCRIPT-COMMAND-INVOKE-01

Add canonical Command invocation for Server Script and, separately, Client Visual Script through their proper authorization identities. No driver access.

### RESEARCH_CONTRACT_DELTA_REQUIRED — HMI-RICH-COMMAND-01

Extend current ExecuteCommand UI/API from identity-only to strictly schema-validated canonical parameters while preserving legacy no-body Commands.

## 50. Main decisions still required

1. Exact common type names and namespace placement for ParameterDefinition / Invocation / Result.
2. Exact location of optional driver executor capability: shared Driver SDK interface versus DriverHost runtime adapter registry.
3. Exact DriverCommandBinding envelope fields, especially PortableTarget representation.
4. Whether static preset parameters on VisualNavigationAction should become supported or whether all Rich Command parameters are supplied through a dedicated Command control/dialog.
5. Trusted Server Script service-principal/authorization policy for Command invocation.
6. Final product limits for parameter count/body/string sizes and command concurrency defaults.

## 51. CHECKPOINT 2 decision

RICH-COMMAND-BINDING-01 = GO_WITH_GATES

Rationale:
- the current Command boundary is already canonical, authorized and audited;
- current Command is limited to fixed WriteTagValue and cannot truthfully express parameterized stateless actions;
- existing HMI/Capability identities can be reused;
- existing protocol implementations already prove richer command result/ambiguity semantics;
- an additive binding/executor model can keep protocol internals behind drivers;
- existing TAG writes remain the correct path for ordinary state setting.

Mandatory gates before product implementation:
1. keep WriteTagValue backward compatible;
2. Main accepts bounded typed parameter schema;
3. Main accepts versioned DriverCommandBinding with no raw RPC/JSON escape hatch;
4. Main accepts CommandResult with Unknown/ambiguity semantics and process-state separation;
5. server-side CommandExecute authorization + existing Audit remain authoritative;
6. no automatic retry after possible dispatch unless operation explicitly proves idempotence;
7. Gateway remains TAG state-routing in v1;
8. scripts/HMI call canonical Command only, never a driver method.

## 52. Checkpoint declarations

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO COMMAND RUNTIME IMPLEMENTED

NO SCHEMA CHANGED

NO DRIVER SDK CHANGED

NO GATEWAY CHANGED

NO SCRIPT RUNTIME CHANGED

NO FRONTEND CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

HA = HIGH AVAILABILITY

HAB = HOME ASSISTANT BRIDGE

NO MERGE PERFORMED
