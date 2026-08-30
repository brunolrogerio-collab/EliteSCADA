# IEC 60870-5-104 Interoperability Lab Playbook — DEV Driver 06

Date: 2026-08-30
Branch: `driver6/iec-60870-5-104`
Prepared after implementation handoff: `dadcf2986b6d9516a95ec867120330b03e7675d9`
Status: **LAB EXECUTION PENDING**

This playbook turns the Driver 06 acceptance matrix into a repeatable interoperability procedure. It is intentionally separate from the implementation handoff: the handoff states what the branch contains, while this document states how to prove the implementation against independent IEC 60870-5-104 peers and, later, a representative RTU/IED.

No result in this document may be marked PASS merely because an automated test exists in the repository. A lab PASS requires execution evidence from the peer named by the test phase.

---

## 1. Purpose

The lab must answer four different questions without conflating them:

1. **Does the .NET implementation compile and pass its deterministic tests?**
2. **Does the EliteSCADA IEC-104 client interoperate with a mature external C implementation?**
3. **Does it also interoperate with an independent implementation family rather than only one stack?**
4. **Does the behavior remain correct against at least one representative real RTU/IED or utility gateway?**

The branch is not production-accepted until all required gates are satisfied or an explicit Coordinator exception is documented.

---

## 2. Required implementation baseline

Before any interoperability run, record:

- EliteSCADA repository commit SHA;
- operating system and architecture;
- `.NET SDK` version;
- test project version/dependencies resolved by restore;
- IEC-104 Data Source settings;
- station timezone;
- peer implementation name and exact version/tag/commit;
- whether the peer is running natively, in a VM, or in a container;
- capture tool/version when packet capture is used.

A result without an EliteSCADA SHA and peer version is not reusable evidence.

---

## 3. Reference peers

### 3.1 Peer A — MZ Automation lib60870-C

Primary C-family reference peer:

- project: `mz-automation/lib60870`;
- lab target version at preparation date: **2.4.1**;
- protocol role for this lab: CS104 server/outstation;
- first-release lab transport: plain TCP;
- TLS is a later IEC 62351-3 integration track and is not part of the current Driver 06 acceptance gate.

The upstream project provides CS104 client/server support and example programs. Its documented build flow is `make` for the library and `make` in individual example directories; CMake is also supported.

Use the library only as an **external lab peer** for this workstream. Do not add it as a production dependency to `Scada.Drivers` merely to simplify testing.

Licensing note: the upstream project documents GPLv3/commercial dual licensing. Laboratory use and any redistribution of upstream binaries/source must remain compliant with the applicable license. Production embedding is a separate legal/Coordinator decision.

### 3.2 Peer B — OpenMUC j60870

Independent Java-family peer:

- project: OpenMUC j60870;
- lab target version at preparation date: **1.8.0**;
- release date: 2026-07-17;
- protocol role for this lab: server/outstation;
- default IEC-104 port: 2404;
- configurable COT/CA/IOA field lengths and acknowledgement timers.

The OpenMUC distribution includes a sample server under:

`cli-app/src/main/java/org/openmuc/j60870/app/`

Use the official distribution/user guide for the exact build/run procedure for the tested release.

j60870 is also GPLv3 with proprietary licensing available. As with Peer A, it is an independent lab oracle, not a production runtime dependency.

### 3.3 Optional Peer C — third implementation family

A third peer may be used to broaden evidence, especially when resolving behavior where Peer A and Peer B differ. A current candidate is a Rust IEC-104 implementation with RTU-style server examples, but it is **optional** and does not replace the mandatory C-family + Java-family + real-device gates.

### 3.4 Real hardware gate

Before production acceptance, execute the applicable subset of this playbook against at least one representative:

- RTU;
- IED;
- telecontrol gateway; or
- vendor IEC-104 simulator whose implementation is independent from both mandatory software peers.

Record manufacturer, model, firmware/software version and its interoperability/profile settings.

---

## 4. Lab isolation and safety

IEC-104 control commands can represent physical operations. The first command phases must therefore run only against software peers or isolated test equipment.

For real hardware:

- use a dedicated lab VLAN/network segment;
- ensure commands cannot propagate to production process equipment;
- disable or physically isolate field outputs where applicable;
- use designated test IOAs only;
- confirm local permissives/interlocks before command testing;
- never use discovery by sending commands to unknown addresses;
- do not route the lab through a production control-center network.

The objective is protocol interoperability, not an accidental breaker test with paperwork afterward.

---

## 5. Canonical EliteSCADA session profile

Unless a test explicitly varies a parameter, use:

- TCP port: `2404`;
- COT length: `2` bytes;
- Originator Address: `0`;
- Common Address length: `2` bytes;
- IOA length: `3` bytes;
- T0: `30 s`;
- T1: `15 s`;
- T2: `10 s`;
- T3: `20 s`;
- K: `12`;
- W: `8`;
- station timezone: explicitly configured and recorded;
- Common Addresses: `1,2` when the peer supports both in one endpoint.

For accelerated timer tests, override only the timer being tested and preserve the required ordering, especially `T2 < T1`.

---

## 6. Canonical point table

Both software peers should expose the same logical table as closely as their APIs permit.

### 6.1 Common Address 1 — monitored points

| IOA | Type | Meaning | Initial value |
|---:|---|---|---|
| 100 | `M_SP_NA_1` | untimed single point | false |
| 101 | `M_SP_TB_1` | timed single point | true |
| 110 | `M_DP_NA_1` | untimed double point | OFF |
| 111 | `M_DP_TB_1` | timed double point | ON |
| 120 | `M_BO_NA_1` | untimed bitstring | `0x01234567` |
| 121 | `M_BO_TB_1` | timed bitstring | `0x76543210` |
| 130 | `M_ME_NA_1` | normalized measured value | `0.25` nominal |
| 131 | `M_ME_TD_1` | timed normalized measured value | `-0.5` nominal |
| 140 | `M_ME_NB_1` | scaled measured value | `1234` |
| 141 | `M_ME_TE_1` | timed scaled measured value | `-1234` |
| 150 | `M_ME_NC_1` | short float measured value | `12.5` |
| 151 | `M_ME_TF_1` | timed short float measured value | `-7.25` |

For normalized values, compare the exact IEC encoded/raw value and the EliteSCADA decoded float policy rather than assuming a peer GUI rounds identically.

### 6.2 Common Address 1 — command/setpoint points

| IOA | Command type | Mode coverage | Test purpose |
|---:|---|---|---|
| 200 | `C_SC_NA_1` | Direct + SBO | single command |
| 201 | `C_DC_NA_1` | Direct + SBO | double command |
| 210 | `C_SE_NA_1` | Direct + SBO where peer supports | normalized setpoint |
| 211 | `C_SE_NB_1` | Direct + SBO where peer supports | scaled setpoint |
| 212 | `C_SE_NC_1` | Direct + SBO where peer supports | short-float setpoint |

The peer must log select/execute separately for SBO so the evidence can prove that execute was not sent when selection failed.

### 6.3 Common Address 2 — identity separation

Expose at least:

| CA | IOA | Type | Initial value |
|---:|---:|---|---|
| 2 | 100 | `M_SP_NA_1` | true |
| 2 | 150 | `M_ME_NC_1` | `99.5` |

Reusing IOAs from CA 1 is intentional. The test proves that identity is `CA + IOA`, not IOA alone.

---

## 7. Evidence package per run

Create one evidence directory/archive per peer/run containing:

1. `RUN.md` with date, EliteSCADA SHA, peer/version, OS/runtime and configuration;
2. EliteSCADA console/service log with secrets removed;
3. peer server log;
4. EliteSCADA IEC-104 diagnostic snapshot before, during and after the run;
5. packet capture (`pcapng`) for handshake/sequence/command fault cases when allowed;
6. exported point-list/browse evidence when Engineering is tested;
7. command transaction table with timestamps and outcomes;
8. PASS/FAIL table using the case IDs from `IEC-60870-5-104-ACCEPTANCE-AND-INTEROPERABILITY.md`;
9. exact deviations/workarounds, if any.

Never store passwords, private keys, certificate private material or production endpoint credentials in the evidence package.

---

## 8. Phase 0 — compile and deterministic suite

### Preconditions

- install the repository-targeted .NET 10 SDK;
- restore dependencies from approved feeds;
- do not add a protocol package merely to make the Driver 06 tests compile.

### Required execution

At minimum:

```text
dotnet --info
dotnet restore
dotnet build --no-restore
dotnet test tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj --no-build
```

If repository build orchestration requires different commands, record them in `RUN.md` and preserve the same build/test evidence.

### Gate

- zero compile errors;
- zero warnings where the project treats warnings as errors;
- all IEC-104 unit/in-process/loopback tests pass;
- any unrelated failing test is triaged before claiming the branch build green.

Do not proceed to hardware acceptance with a failing deterministic suite.

---

## 9. Phase 1 — TCP and U-format lifecycle

Run against Peer A, then repeat against Peer B.

### Cases

1. connect TCP;
2. verify socket connected alone does not count as running data transfer;
3. send `STARTDT act`, receive `STARTDT con`;
4. exchange normal traffic;
5. issue `STOPDT act`, receive `STOPDT con`;
6. verify process data is not accepted as normal running traffic while stopped;
7. restart with `STARTDT`;
8. idle beyond shortened T3 and verify `TESTFR act/con`;
9. suppress TESTFR confirmation in a controllable peer/fault proxy and verify failure under T1.

### Evidence

- pcap for STARTDT/STOPDT/TESTFR;
- diagnostics counters;
- session state transitions;
- no duplicate STARTDT/STOPDT state transition.

---

## 10. Phase 2 — GI and addressing

### Cases

1. start fresh session;
2. issue GI for CA 1 with `C_IC_NA_1`, QOI 20;
3. verify positive activation confirmation;
4. verify all configured monitored points can arrive during GI;
5. verify activation termination;
6. repeat for CA 2;
7. prove `CA=1, IOA=100` and `CA=2, IOA=100` remain distinct;
8. repeat the sequence after a forced reconnect;
9. verify no automatic TAG creation occurs for an unknown/unconfigured ASDU.

### Gate

GI completion is tracked independently per Common Address and reconnect bootstrap re-runs GI without replaying operational commands.

---

## 11. Phase 3 — monitored ASDU matrix

For every Type ID in the canonical table:

1. publish during GI;
2. publish spontaneously after GI;
3. exercise at least one value transition;
4. where timed, send a valid CP56Time2a;
5. verify Common Address, IOA, COT, value, quality and source timestamp;
6. repeat representative messages with `SQ=0` and `SQ=1` where the peer supports constructing both forms.

Required first-release monitored types:

- `M_SP_NA_1` (1)
- `M_DP_NA_1` (3)
- `M_BO_NA_1` (7)
- `M_ME_NA_1` (9)
- `M_ME_NB_1` (11)
- `M_ME_NC_1` (13)
- `M_SP_TB_1` (30)
- `M_DP_TB_1` (31)
- `M_BO_TB_1` (33)
- `M_ME_TD_1` (34)
- `M_ME_TE_1` (35)
- `M_ME_TF_1` (36)

Unknown Type IDs must not be reinterpreted as a supported point type.

---

## 12. Phase 4 — quality and timestamp semantics

### IEC process quality cases

For representative SIQ/DIQ/QDS points, independently exercise:

- healthy;
- IV;
- NT;
- SB;
- BL;
- OV where applicable.

Verify the current mapping contract:

- communication failure -> `BadCommunication` through the owning runtime path;
- IV -> `BadDevice`;
- NT -> `Stale`;
- SB/BL/OV -> `Uncertain`;
- healthy -> `Good`;
- binding mismatch, once public binding exists -> `BadConfiguration`;
- administratively disabled, once public binding exists -> `Disabled`.

### CP56Time2a cases

Exercise:

- ordinary valid time;
- configured non-UTC station timezone;
- DST/summer-time indication where meaningful;
- CP56 IV bit;
- reserved/invalid layout already covered by deterministic tests;
- ambiguous local wall time around DST transition if the peer can generate it.

Verify:

- EliteSCADA publication `Timestamp` remains local observation/publication time;
- valid CP56 becomes `SourceTimestamp`;
- invalid CP56 does not fabricate source time;
- CP56 SU alone does not lower process quality.

### Non-finite short float

Where the peer can emit raw IEEE-754 non-finite values:

- send NaN;
- send +Infinity;
- send -Infinity.

Verify EliteSCADA preserves the float representation and marks semantic quality `Uncertain`, unless a stronger IEC quality condition such as IV maps to a worse canonical quality.

---

## 13. Phase 5 — command transactions

Run first against software peers only.

### 13.1 Direct operate

For each supported command family:

- send activation;
- verify peer receives exactly one execute;
- return positive ACT_CON;
- where applicable return ACT_TERM;
- verify EliteSCADA outcome and `WasAccepted` semantics.

Then exercise:

- negative ACT_CON;
- missing ACT_CON until timeout;
- positive ACT_CON followed by missing ACT_TERM;
- wrong Type ID response;
- wrong CA;
- wrong IOA;
- wrong OA;
- TEST-marked confirmation.

### 13.2 Select-Before-Operate

For single, double and supported setpoint commands:

1. send select;
2. verify positive selection confirmation;
3. send execute only after valid selection confirmation;
4. verify execute confirmation/termination;
5. repeat with selection rejection;
6. repeat with selection timeout;
7. repeat with disconnect after select and before execute.

Gate: no physical execute is transmitted after a failed, timed-out or cancelled selection.

### 13.3 Ambiguous transmission and reconnect

Use a network fault proxy or peer-side forced close to produce:

- disconnect after execute is visible to the peer but before EliteSCADA receives ACT_CON;
- disconnect after positive ACT_CON but before ACT_TERM.

Verify:

- result is `Ambiguous`, not `Completed`;
- `ExecuteWasTransmitted=true` when applicable;
- accepted state is preserved if ACT_CON was observed;
- reconnect starts a fresh session and GI;
- **the prior operational command is never replayed automatically**.

This is a safety gate, not merely a functional test.

---

## 14. Phase 6 — APCI sequence/window supervision

Use peer controls, a fault proxy, or a dedicated scripted station to cover:

- normal I-frame acknowledgement;
- S-frame acknowledgement;
- W threshold acknowledgement;
- T2 delayed S-frame flush;
- K window saturation/backpressure;
- T1 acknowledgement timeout;
- impossible peer N(R);
- out-of-order N(S);
- sequence values around 32767 -> 0;
- simultaneous data in both directions around wrap.

The pure codec/state tests already cover modulo arithmetic; this phase proves live adapter behavior.

Gate: sequence errors fail closed and must not publish corrupted/out-of-order process data.

---

## 15. Phase 7 — malformed transport/protocol input

Automated loopback tests cover several cases, but at least one external-peer/fault-proxy run should verify live behavior for:

- bad APDU start byte;
- impossible APDU length;
- truncated APDU/EOF;
- malformed ASDU object count/payload length;
- unsupported Type ID;
- sequence error.

Expected behavior:

- no process crash;
- bounded diagnostic text;
- no raw payload dump containing secrets/unbounded data;
- protocol errors distinguished from transport/session failures where meaningful;
- session closed when continuation would be unsafe.

---

## 16. Phase 8 — receive pressure and spontaneous burst

### Burst profile

Run at least:

- 1,000 ASDUs/s for 60 s;
- 5,000 ASDUs/s for 60 s if the peer and host can sustain it;
- a burst above downstream consumption rate to exercise bounded queues;
- mixed Type IDs and multiple IOAs;
- mixed CA 1/CA 2 if peer supports a single connection for both.

Record CPU, memory, queue/session diagnostics and peer send counts.

The adapter currently fails closed if its bounded receive queue cannot accept a process ASDU rather than silently dropping it. Lab evidence must confirm no unbounded memory growth and no invisible data loss.

---

## 17. Phase 9 — reconnect/soak

Run each mandatory software peer for a minimum soak period agreed by Coordinator; recommended initial gate is **8 hours** and the production-candidate gate should include at least one **24-hour** run.

During soak:

- normal spontaneous traffic continues;
- idle periods exercise T3/TESTFR;
- periodically interrupt TCP;
- verify reconnect backoff progression;
- verify reconnect backoff resets after stable success;
- verify GI occurs on each new session;
- verify command counters and diagnostics remain bounded/sane;
- verify no command replay;
- check handle/socket/task leakage.

Any monotonically growing memory/task/socket behavior must be investigated before hardware acceptance.

---

## 18. Phase 10 — Engineering interoperability

With Peer A and Peer B point tables aligned:

1. run Engineering connection test;
2. run bounded Observe+GI browse;
3. verify all observed candidates are marked partial/transient;
4. verify duplicate evidence groups by `CA + IOA`;
5. verify type conflict becomes an explicit issue;
6. verify no candidate is persisted automatically;
7. export protocol-local point list;
8. import it back;
9. verify deterministic CA/IOA/type fidelity;
10. reconcile a present point;
11. reconcile a missing point after complete, non-truncated GI;
12. force incomplete GI and verify absence becomes Ambiguous rather than Missing.

Public canonical Preview/Apply/package fidelity remains blocked until the Coordinator resolves the rich persisted binding integration.

---

## 19. Phase 11 — real RTU/IED acceptance

Repeat the applicable safe subset against the selected device/gateway.

Mandatory minimum:

- TCP lifecycle;
- STARTDT/STOPDT/TESTFR;
- GI;
- at least one supported single point;
- at least one supported double point if device exposes one;
- at least one supported measured value;
- CP56Time2a if device emits it;
- real quality flag transition if device/simulator supports forcing one;
- spontaneous event;
- reconnect after network interruption;
- Direct or SBO command only on a designated safe test point;
- command rejection path if the device can be configured to reject safely.

Record the device's IEC-104 interoperability/profile configuration alongside evidence.

A vendor GUI showing “Connected” is not sufficient acceptance evidence.

---

## 20. Packet capture checks

For representative captures, verify manually or with a decoder:

- APDU start `0x68`;
- APCI format classification;
- N(S)/N(R) progression;
- correct ASDU Type ID;
- VSQ count/SQ;
- COT including TEST/P-N bits and OA;
- CA;
- 3-byte IOA;
- command select/execute qualifier where applicable;
- CP56 bytes;
- STARTDT/STOPDT/TESTFR sequences.

Capture only on isolated test networks. Sanitize endpoint metadata before attaching evidence outside the engineering team.

---

## 21. PASS / FAIL policy

A case is:

- **PASS** only when the required peer was executed and evidence matches the expected behavior;
- **FAIL** when behavior contradicts the contract;
- **BLOCKED** when the required peer/hardware/capability is unavailable;
- **N/A** only when the selected peer genuinely cannot expose that feature and another required peer/device covers it;
- **NOT RUN** when no execution occurred.

`PASS (by code review)` is not a valid interoperability status.

---

## 22. Exit criteria for Driver 06 lab validation

The Driver 06 implementation may be presented to the Coordinator as lab-validated when all of the following are true:

1. .NET 10 build is green;
2. deterministic IEC-104 test suite is green;
3. Peer A mandatory cases pass;
4. Peer B mandatory cases pass;
5. differences between Peer A/B are documented and resolved against the IEC contract rather than vendor preference;
6. sequence/window/timer live tests pass;
7. all supported monitored Type IDs have external-peer evidence;
8. Direct and SBO transaction safety cases pass where peer capabilities permit;
9. command ambiguity/no-replay safety gate passes;
10. burst/backpressure test has no silent loss/unbounded growth;
11. soak test has no resource leak or protocol degradation;
12. one independent real RTU/IED/gateway acceptance run is recorded;
13. no plaintext protected credential/private key appears in logs/evidence;
14. remaining Coordinator-gated integration items are still explicit rather than hidden behind protocol-local workarounds.

---

## 23. Coordinator-gated work that this playbook does not authorize

Successful lab validation does not by itself authorize Driver 06 to modify shared contracts. The following still require coordinated integration:

- canonical persisted TAG binding containing CA + IOA + semantic family/type profile + command profile;
- public `ICommunicationDriver` registration/composition;
- canonical runtime command outcome/API integration;
- project/site timezone inheritance policy;
- canonical Engineering Preview/Apply/package persistence;
- host-owned TLS certificate/trust resolution;
- canonical export mechanism if a common exporter contract is introduced;
- support for deferred `M_ST_*`, `M_IT_*`, time-tagged commands or IEC 62351-3.

---

## 24. External references captured for this playbook

Snapshot date: 2026-08-30.

- MZ Automation lib60870-C repository/readme: `https://github.com/mz-automation/lib60870`
- MZ Automation lib60870-C releases: `https://github.com/mz-automation/lib60870/releases`
- lib60870 documentation: `https://support.mz-automation.de/doc/lib60870/latest/`
- OpenMUC j60870 overview: `https://www.openmuc.org/iec-60870-5-104/`
- OpenMUC j60870 download/release list: `https://www.openmuc.org/j60870/download/`
- OpenMUC j60870 user guide: `https://www.openmuc.org/j60870/user-guide/`
- OpenMUC j60870 1.8.0 Javadoc: `https://www.openmuc.org/javadoc-j60870/`

External peer versions should be rechecked when the lab is actually executed. The protocol behavior and EliteSCADA acceptance requirements remain governed by the Driver 06 research, errata, acceptance matrix and implementation handoff in this repository.
