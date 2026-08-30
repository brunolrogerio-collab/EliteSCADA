# IEC 60870-5-104 Convergence Handoff Addendum — DEV Driver 06

Date: 2026-08-30  
Branch: `driver6/iec-60870-5-104`  
Coordinator authority: `docs/DRIVER-CONVERGENCE-COORDINATION-V1.md`, locked by commit `e0272be4ad35ba333b6b33ced2387b48d2133bf7`  
Status: **DRAFT HANDOFF ADDENDUM / PARKED FROM MAINLINE**

This addendum is part of the Driver 06 handoff set. Where it conflicts with the earlier Driver 06 integration proposal, the Coordinator convergence lock controls.

In particular, the earlier suggestion to use namespaced `TagEngineeringDto.Metadata` as a first public IEC-104 binding is no longer the preferred convergence direction. Driver 06 will preserve portable CA+IOA and semantic/command evidence so the future Coordinator-owned rich Driver binding can persist it without a protocol-local central DTO.

---

## 1. Coordinator convergence alignment

Driver 06 now follows these locked directions:

- point address identity remains **Common Address + IOA**;
- monitored Type ID / semantic family / command profile remain binding concerns, not address identity;
- no byte/word swap options are invented for standard IEC-104 information-object encodings;
- readiness is distinct from TAG quality;
- readiness for the current first-release profile requires TCP connectivity, completed STARTDT and successful completion of the configured startup General Interrogation for every configured Common Address;
- COT, quality and CP56Time2a evidence remain available to the future common current-value versus historical-event ingress policy;
- no Driver 06-local late-event monotonic/current-cache filter is introduced;
- command success is not inferred from bytes transmitted;
- rich operational command convergence remains Coordinator-owned;
- DriverHost registry, common readiness seam and rich binding DTO remain Coordinator-owned shared infrastructure;
- Driver 06 may expose narrow protocol-owned readiness/planner/factory evidence but must not create a competing central registry.

---

## 2. Protocol-local readiness evidence implemented

Driver 06 now exposes `Iec104ReadinessSnapshot` with protocol-local states aligned to the Coordinator vocabulary:

- `NotStarted`;
- `Starting`;
- `Ready`;
- `Faulted`;
- `Stopped`.

For the current first-release configuration, GI is mandatory because the managed/session constructors require at least one configured Common Address and issue startup GI for every configured address.

`Ready` requires all of the following simultaneously:

1. an active managed session;
2. transport still connected;
3. STARTDT completed and the session is running;
4. every configured startup GI transaction is `Completed`;
5. no configured startup GI is `Rejected`.

A rejected GI makes readiness `Faulted` even if the TCP socket remains connected and data transfer is technically running.

During reconnect backoff, readiness is `Faulted`. A fresh reconnect attempt returns to `Starting`; it becomes `Ready` only after the new session completes its own STARTDT and all startup GIs.

Cancellation of the managed run produces `Stopped`.

### Command admission hardening

`Iec104ManagedClient.ExecuteCommandAsync` now rejects a command unless the active session is `Ready`.

This closes the previous gap where session state could already be `Running` immediately after STARTDT while initial GI was still being dispatched/collected. A command rejected by this readiness gate is not transmitted and is not queued for replay.

The lower protocol transaction/coordinator remains responsible for Direct/SBO confirmation, termination and ambiguous-outcome semantics after a command is admitted.

### Readiness regression evidence written

`Iec104ManagedClientTests` now covers:

- `NotStarted` before a managed run;
- `Starting` after STARTDT while GI is incomplete;
- command rejection before readiness;
- `Ready` after positive GI ACT_CON + ACT_TERM;
- GI rejection -> readiness `Faulted` with command blocked;
- multiple Common Addresses -> readiness remains `Starting` until every configured GI completes;
- link failure/reconnect delay -> `Faulted`;
- fresh reconnect attempt -> back to `Starting`;
- cancellation -> `Stopped`;
- command execution still succeeds after readiness is established.

These tests are written but remain **not executed** in the Driver 06 environment until .NET 10/CI evidence exists.

---

## 3. Driver Module / production-readiness manifest

The physical installable-module packaging contract is still deferred by ADR-007. The identity below is therefore a **proposed stable package/module ID**, not an assertion that the module loader already exists.

| Field | Driver 06 manifest |
|---|---|
| Proposed module/package ID | `EliteSCADA.Driver.Iec60870_5_104` |
| Driver type provided | `iec60870.5.104` |
| Product role | IEC 60870-5-104 client/master |
| Driver contract version | `1` |
| Public configuration schema | `elite.iec60870.5.104` v1 |
| Current Data Source schema status | Implemented in descriptor |
| Future rich TAG-binding schema | Coordinator-owned / not yet assigned a shared schema version |
| Portable point identity | `ca=<0..65535>;ioa=<0..16777215>` |
| Runtime capabilities | Read, Write, Subscribe, Diagnostics, SourceTimestamp |
| Acquisition modes | EventDriven, Hybrid |
| Engineering capabilities | ConnectionTest, Browse, FileImport, Reconcile |
| Runtime transport | TCP, default port 2404 |
| TLS / IEC 62351-3 | Deferred; not implemented in first slice |
| Production external runtime dependency | None currently committed; current CS104 implementation is EliteSCADA-owned source in `Scada.Drivers` |
| External lab/reference dependencies | lib60870-C / lib60870.NET and OpenMUC j60870 may be used out-of-process as interoperability peers |
| Lab peer licensing | Public lib60870 and j60870 distributions are GPLv3; commercial licensing options exist upstream. They are not embedded production dependencies in this branch. |
| Production dependency/license decision | Open. Current EliteSCADA-owned implementation may remain the production path if validation is sufficient; any future external runtime stack requires explicit legal/redistribution review. |
| Production distribution status | **BLOCKED ON EVIDENCE / SHARED INTEGRATION** |
| Secrets/private keys | None required for first plain-TCP slice; future TLS must use host-owned credential/certificate/trust resolution |
| Hardware/vendor validation | Required before production acceptance |

### 3.1 Production blockers

Production distribution/integration remains blocked until all applicable items below are resolved:

1. exact-head .NET 10 build and IEC-104 test execution succeeds;
2. external Peer A interoperability evidence succeeds;
3. independent Peer B interoperability evidence succeeds;
4. representative RTU/IED/utility-gateway evidence succeeds;
5. burst/soak/resource testing is acceptable;
6. Coordinator-owned rich binding is available and Driver 06 maps into it;
7. Coordinator-owned runtime registry/planner/factory composition is available;
8. common readiness adapter/source is integrated from the protocol-local evidence;
9. canonical TAG quality/publication and communication-loss behavior are validated through the owning provider;
10. canonical Preview/Apply/import/export/project-package behavior is validated;
11. command authorization/audit/common result integration is resolved where required;
12. the final production implementation/library licensing decision is recorded.

### 3.2 Hardware/simulator validation still required

At minimum:

- mature external CS104 station/reference peer;
- independent implementation family;
- representative real RTU/IED/gateway;
- STARTDT/STOPDT/TESTFR;
- startup GI for one and multiple Common Addresses where supported;
- all supported monitored Type IDs available on the peer;
- spontaneous indications;
- CP56Time2a and quality flags;
- Direct and SBO controls on safe targets;
- negative confirmations;
- network interruption around command ACT_CON/ACT_TERM;
- proof of no automatic command replay;
- sustained spontaneous burst;
- long idle/TESTFR behavior;
- reconnect/soak/resource behavior.

---

## 4. Shared seams Driver 06 will consume later

The Coordinator convergence lock reserves these shared responsibilities. Driver 06 must adapt to them rather than implement substitutes:

- `IDriverRuntimePlan`-equivalent common plan abstraction;
- `ICommunicationDriverRuntimePlanner`-equivalent registry participant;
- `ICommunicationDriverRuntimeFactory`-equivalent runtime factory participant;
- one registry/module registration per stable Driver type;
- common readiness source/snapshot adapter;
- rich versioned Driver TAG binding;
- common credential/certificate/trust resolver;
- common current-value versus historical-event ingress policy;
- future rich command-operation capability where invocation-time semantics exceed an ordinary typed TAG write.

Driver 06 does not add protocol names to the shared `CommandKind` enum and does not patch the central DriverHost compiler/switch from the parked branch.

---

## 5. Exact-head CI requirement

The Coordinator convergence milestone requires a Draft handoff PR and justified **exact-head CI**.

Before calling the branch reviewable for integration:

1. record the final Driver 06 branch SHA in the Draft PR;
2. run CI against that exact SHA, not an older successful commit;
3. retain pass/fail evidence and any artifacts/logs;
4. if the branch advances after CI, the new HEAD requires new exact-head evidence;
5. do not label unexecuted local tests as passed.

The current Driver 06 execution environment still lacks a local .NET compiler/runtime, so GitHub CI is the preferred first executable evidence once the Draft PR is opened and the repository workflow schedules the branch head.

---

## 6. Scope remains bounded

No scope expansion is authorized by this convergence update.

Still deferred:

- `M_ST_*`;
- `M_IT_*` / BCR semantics;
- time-tagged operational commands;
- IEC 62351-3 TLS;
- IEC 60870-5-7 authentication;
- file transfer;
- redundancy-group behavior;
- vendor-specific ASDU expansion;
- private Driver 06 module loader/registry;
- private Driver 06 current-cache late-event policy.

The next evidence milestone is execution and review, not more protocol surface.
