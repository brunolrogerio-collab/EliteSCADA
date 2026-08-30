# IEC 60870-5-104 Coordinator Integration Proposal — DEV Driver 06

Date: 2026-08-30
Branch: `driver6/iec-60870-5-104`
Status: **PROPOSAL ONLY — NO SHARED CONTRACT CHANGES AUTHORIZED HERE**

This document converts the remaining Driver 06 integration blockers into explicit Coordinator decisions. It does not modify `main`, common DTOs, the DriverHost composition root, project-package schema, or public command contracts.

The goal is to make the smallest safe first integration possible while preserving a clean migration path to richer driver bindings later.

---

## 1. Current mainline facts reviewed

At the reviewed mainline baseline, the common Engineering/runtime shape is still:

- `TagEngineeringDto.Source`: associates a TAG with a Data Source key;
- `TagEngineeringDto.Address`: portable driver address text;
- `TagEngineeringDto.Metadata`: protocol-specific namespaced configuration where required;
- `TagEngineeringDto.AddressSelector`: common integer bit-selection contract;
- `DataSourceEngineeringDto.Settings`: public/versioned driver Data Source settings;
- `DriverConfigurationSchemaDescriptor.TagBindingFields`: descriptor metadata for protocol-owned TAG-binding fields;
- `ICommunicationDriver.WriteAsync`: `ValueTask` with no common rich command result;
- `EngineeringDriverCompiler`: directly compiles built-in simulation/Modbus plans and does not expose a generic per-driver runtime-planner registry.

The current Modbus integration already demonstrates a precedent in which canonical `Source`/`Address` are combined with namespaced protocol metadata during compilation.

This means Driver 06 does **not necessarily require a project-package schema migration merely to integrate a first IEC-104 runtime slice**. A richer generic binding DTO remains desirable, but it is not the only technically sound path.

---

## 2. Decision A — first-release IEC-104 TAG binding persistence

### Option A1 — incremental use of the current canonical TAG contract

**Recommended for first integration.**

Persist IEC-104 TAG configuration using existing public/versioned fields:

- `TagEngineeringDto.Source` = IEC-104 Data Source key;
- `TagEngineeringDto.Address` = canonical portable point address, e.g. `ca=1;ioa=77`;
- `TagEngineeringDto.Metadata` = IEC-104 semantic/command configuration using strictly namespaced keys;
- `TagEngineeringDto.AddressSelector` = canonical bit selector only where the decoded TAG type is an integer and bit access is requested.

Recommended namespaced metadata keys:

| Key | Purpose | Example |
|---|---|---|
| `iec104.monitor.type` | Expected monitored IEC Type ID or stable first-release symbolic name | `M_SP_NA_1` |
| `iec104.monitor.family` | Stable semantic family used to detect incompatible Type-ID changes | `singlePoint` |
| `iec104.command.type` | Optional writable command Type ID | `C_SC_NA_1` |
| `iec104.command.mode` | Optional command mode | `direct` / `sbo` |
| `iec104.command.qualifier` | Optional command qualifier when exposed by the approved contract | protocol-defined invariant text |

For read-only monitored points, command metadata is absent.

For writable points, the monitored semantic family and command profile are both explicit. Runtime must never infer a command type merely from `TagDataType`.

No IEC-104 byte/word-order metadata is defined because standard IEC-104 information-object encodings already define byte order and this protocol slice does not expose artificial swap options.

#### Advantages

- no breaking shared DTO change;
- no immediate package-schema migration;
- follows the same broad Source/Address/metadata pattern already used by current mainline Modbus compilation;
- portable `ca=...;ioa=...` address already exists and is tested in Driver 06;
- protocol binding remains inspectable/exportable;
- namespaced metadata can later migrate mechanically into a richer generic driver-binding DTO.

#### Risks

- metadata is stringly typed at persistence level;
- semantic validation remains compiler/provider-owned rather than structurally typed by `TagEngineeringDto`;
- UI authoring must rely on `DriverConfigurationSchemaDescriptor.TagBindingFields` and validation rather than a strongly typed project DTO.

### Option A2 — introduce a generic versioned driver-binding DTO before IEC-104 integration

Example direction only:

```text
DriverTagBindingEngineeringDto
  DriverType
  SchemaId
  SchemaVersion
  Settings<string,string>
```

`TagEngineeringDto` would gain an optional driver-binding property while `Source`, `Address` and legacy metadata remain readable for migration.

This is architecturally cleaner long term, but it creates a shared DTO/package migration and affects more than Driver 06. It should therefore be a Coordinator/Wave decision rather than a Driver 06 prerequisite unless the Coordinator explicitly chooses it now.

### Driver 06 recommendation

Approve **A1 for first IEC-104 integration** and track A2 as a later common Driver SDK/package evolution.

---

## 3. Proposed first-release binding validation rules under Option A1

### 3.1 Point identity

`Address` must parse as exactly one canonical `{CommonAddress, IOA}` pair:

```text
ca=<0..65535>;ioa=<0..16777215>
```

The parser may accept tolerant case/order/whitespace on import, but export/persistence should normalize to canonical `ca=...;ioa=...` form.

### 3.2 Expected monitored semantic profile

A bound monitored TAG must declare `iec104.monitor.type` and/or a Coordinator-approved stable semantic family.

First-release monitored Type IDs:

- `M_SP_NA_1` / `M_SP_TB_1` -> Boolean / `singlePoint`;
- `M_DP_NA_1` / `M_DP_TB_1` -> Enum / `doublePoint`;
- `M_BO_NA_1` / `M_BO_TB_1` -> Int32 / `bitstring32`;
- `M_ME_NA_1` / `M_ME_TD_1` -> Float / `normalized`;
- `M_ME_NB_1` / `M_ME_TE_1` -> Int16 / `scaled16`;
- `M_ME_NC_1` / `M_ME_TF_1` -> Float / `shortFloat`.

A received Type ID that is incompatible with the persisted expected family must not be silently reinterpreted. The affected point becomes `BadConfiguration` while unrelated points remain operational.

Timed and untimed forms of the same semantic family may be treated as compatible only if the Coordinator approves that policy. Driver 06 currently decodes both forms to the same canonical value type and exposes CP56 time when present.

### 3.3 Writable command profile

Writable IEC-104 points must explicitly define an approved command type and mode. First-release command Type IDs:

- `C_SC_NA_1`;
- `C_DC_NA_1`;
- `C_SE_NA_1`;
- `C_SE_NB_1`;
- `C_SE_NC_1`.

Modes:

- `direct`;
- `sbo`.

Time-tagged commands remain deferred.

No command type is inferred solely from Boolean/Enum/Float/Int16 TAG type.

### 3.4 Common Address membership

The TAG's persisted CA must be among the Common Addresses configured for its owning Data Source. A mismatch is `BadConfiguration` and an Engineering validation error.

---

## 4. Decision B — common rich command outcome without breaking simple drivers

### Current limitation

The current common runtime surface exposes:

```text
ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken)
```

That is sufficient for simple request/response drivers but cannot faithfully represent telecontrol outcomes such as:

- protocol acceptance followed by later completion;
- explicit negative confirmation;
- timeout before any physical transmission;
- ambiguous physical outcome after an execute may have reached the station;
- cancellation before versus after execute transmission.

Changing `ICommunicationDriver.WriteAsync` directly would be a broad breaking change.

### Recommended additive contract

Add an **optional** common interface rather than changing `ICommunicationDriver` immediately. Conceptual shape:

```text
ICommunicationCommandExecutor
  ExecuteWriteAsync(tagId, value, cancellationToken) -> CommunicationCommandResult
```

Recommended final outcome enum:

- `Completed`;
- `Rejected`;
- `TimedOut`;
- `Ambiguous`;
- `Cancelled`.

Recommended result metadata:

- final outcome;
- `WasTransmitted`;
- `WasAccepted`;
- started/completed timestamps;
- sanitized protocol detail/code where useful;
- no raw payloads or credentials.

`Accepted` is better represented as `WasAccepted=true` on a final result rather than as a terminal state when the protocol still expects ACT_TERM. If the product needs streaming/intermediate command state later, that should be a separate event/status surface.

### Backward compatibility

- drivers that do not implement the optional interface continue using `WriteAsync`;
- host command execution checks for the richer interface when available;
- simple successful drivers can eventually map to `Completed` without protocol-specific complexity;
- IEC-104 maps its existing private outcomes into the common result;
- legacy `WriteAsync` on IEC-104 can delegate to the rich executor and throw a bounded typed exception for non-success if required by existing callers.

### Driver 06 recommendation

Approve the optional additive command-executor interface. Do not force all communication drivers to change merely because IEC-104 has richer control semantics.

---

## 5. Decision C — station timezone

### Recommended first-release rule

Keep `stationTimeZone` as an explicit IEC-104 Data Source setting.

Do **not** inherit an implicit machine-local timezone.

For first integration, Driver 06 recommends one of:

1. require an explicit IANA/OS-supported timezone ID; or
2. allow omission only if the Coordinator explicitly defines deterministic `UTC` as the product default.

Explicit per-Data-Source configuration is preferred because utility stations may use different timezone/DST policies even inside one EliteSCADA project.

The persisted/exported setting must be deterministic. Runtime environment local timezone must never change decoded CP56 semantics silently.

A later common site/project timezone may be offered as an authoring default, but the resolved Data Source value should remain explicit in canonical export if reproducibility is required.

---

## 6. Decision D — Engineering provider/runtime planner registration

### Current limitation

The current `EngineeringDriverCompiler` directly recognizes built-in simulation and Modbus and builds Modbus-specific runtime plans. Adding IEC-104 as another hard-coded branch would work but scales poorly as the parallel driver workstreams arrive.

### Recommended shared direction

Introduce a host-owned registry of per-driver Engineering runtime planners/compilers, conceptually:

```text
IEngineeringDriverRuntimePlanner
  DriverType
  Compile(package, dataSource, associatedTags) -> runtime plan + issues
```

The host remains authoritative for composition. Protocol projects provide planners/adapters but do not self-register globally.

Benefits:

- IEC-104 integration does not add another protocol switch into one compiler;
- S7, OPC UA, MQTT, BACnet and CIP can use the same composition pattern;
- common validation/issue aggregation remains host-owned;
- driver-private protocol types do not leak into public Engineering contracts;
- mainline can decide when each parked driver becomes registered.

### Minimal fallback if registry work is not yet scheduled

Coordinator may explicitly authorize one temporary IEC-104 branch in `EngineeringDriverCompiler`, provided it is treated as integration debt and not copied independently by every parallel Driver DEV.

Driver 06 should not make either change unilaterally while parked.

---

## 7. Decision E — runtime TAG publication and communication failure

Once public binding is approved, the IEC-104 public runtime provider should:

- own only TAGs whose `Source` references its Data Source;
- resolve each point by CA+IOA plus expected semantic profile;
- publish supported monitored values through the normal common TAG/cache/event boundary;
- use CP56Time2a as `SourceTimestamp` only when valid;
- use arrival/publication time as `TagValue.Timestamp`;
- map IEC quality through the already implemented mapping;
- publish type/profile mismatches as `BadConfiguration` per affected point;
- map session communication loss to `BadCommunication` for its bound points through the common provider path;
- leave unrelated Data Sources untouched;
- never auto-create canonical TAGs from unknown ASDUs;
- route writes only through the owning provider and its explicit command profile.

Gateway behavior remains TAG-to-TAG and must not call the IEC-104 protocol layer directly.

---

## 8. Decision F — Engineering browse/import/reconcile to canonical apply

Driver 06 already exposes transient:

- connection test;
- bounded Observe+GI browse;
- monitored point-list CSV import;
- reconcile.

These outputs must remain candidates/evidence.

Coordinator integration should map them into the normal candidate -> validate -> preview -> merge -> apply workflow. Driver 06 must not create or mutate canonical TAGs directly.

Recommended candidate-to-binding projection under Option A1:

- `PortableAddress` -> `TagEngineeringDto.Address`;
- selected Data Source -> `TagEngineeringDto.Source`;
- declared/observed type -> `iec104.monitor.type` / family metadata;
- command profile only when explicitly authored/selected;
- suggested data type remains a suggestion until canonical validation accepts it.

---

## 9. Decision G — export/import

The existing protocol-local point-list exporter is useful for Engineering exchange but is not a replacement for canonical project export.

Canonical project export should naturally preserve IEC-104 configuration through:

- Data Source `Settings`;
- TAG `Source`;
- TAG `Address`;
- namespaced binding metadata under Option A1;
- normal project package/revision machinery.

No passwords, private keys, resolved secrets, raw packet payloads or certificate private material may enter exports.

If a future generic `DriverTagBindingEngineeringDto` is introduced, migration from the namespaced metadata must be deterministic and versioned.

---

## 10. Decision H — TLS / IEC 62351-3

Keep TLS outside the first public IEC-104 runtime integration unless the shared certificate/trust resolver is already approved.

When added:

- certificate selection/trust policy must be host-owned/common;
- secrets/private keys must not be stored in driver metadata;
- Driver 06 should receive only resolved protected handles/context;
- TLS status belongs in common diagnostics without exposing sensitive certificate material.

Plain TCP first-release support must not be marketed as IEC 62351-secured communication.

---

## 11. Explicit Coordinator decision checklist

The Coordinator can unblock Driver 06 integration by recording decisions for these items:

| ID | Decision | Driver 06 recommendation |
|---|---|---|
| C-IEC104-01 | First persisted TAG binding shape | Approve current `Source` + canonical `Address` + namespaced metadata for first integration |
| C-IEC104-02 | Timed/untimed monitored type compatibility within one semantic family | Approve if source timestamp is optional evidence, otherwise require exact Type ID |
| C-IEC104-03 | Common rich command result | Add optional command-executor interface; keep `ICommunicationDriver.WriteAsync` backward compatible |
| C-IEC104-04 | Terminal command result model | `Completed/Rejected/TimedOut/Ambiguous/Cancelled` + `WasTransmitted/WasAccepted` |
| C-IEC104-05 | Station timezone | Explicit per Data Source; never implicit machine local timezone |
| C-IEC104-06 | Runtime planner/provider registration | Prefer generic host planner registry; temporary compiler branch only by explicit approval |
| C-IEC104-07 | Candidate -> Preview/Apply integration | Use existing canonical workflow, no driver-side mutation |
| C-IEC104-08 | Public project export | Preserve Source/Address/namespaced metadata through canonical package until richer binding DTO exists |
| C-IEC104-09 | TLS/62351 | Defer until shared cert/trust resolver exists |
| C-IEC104-10 | Production external library | Keep EliteSCADA-owned implementation unless commercial lib60870 licensing/redistribution is explicitly approved |

---

## 12. Work authorized after Coordinator approval

Once the relevant decisions are approved, Driver 06 integration work can proceed in this order:

1. add/confirm IEC-104 `TagBindingFields` descriptors matching the approved persisted keys;
2. implement an IEC-104 Engineering runtime planner against canonical package data;
3. implement the public `ICommunicationDriver` wrapper over the existing managed client/session/decoder/command coordinator;
4. map communication state and diagnostics into common snapshots;
5. map bound TAGs to CA+IOA+semantic profile and publish updates;
6. map write requests through explicit command profiles;
7. add rich common command-result adapter if approved;
8. register Engineering/runtime services in the host composition root;
9. add canonical Preview/Apply/import/export integration tests;
10. compile/run the full IEC-104 suite on .NET 10;
11. execute the external lab playbook against Peer A, Peer B and representative hardware.

Until those decisions exist, the Driver 06 branch should remain parked and should not invent protocol-private substitutes for shared product contracts.
