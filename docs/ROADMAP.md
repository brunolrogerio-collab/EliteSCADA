# EliteSCADA Roadmap

**Status date:** 2026-08-30  
**Active direction:** **DRIVER CONVERGENCE**  
**Wave 11:** **DEFERRED UNTIL DRIVER CONVERGENCE**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Live ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Driver/lab status: `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`.  
Shared convergence issue: `#174`.  
Current coordinator checkpoint: `LAST CHANGE.md`.  
CI policy: `docs/CI-USAGE-POLICY.md`.

## Current validated product foundation

Wave 10 is COMPLETE / MERGED / POST-MAIN GREEN.

Final Wave 10 product merge:

`15daff2cc076f46f9433812babbd5cbb4b8d9554`

Evidence:

- Wave 10 integration head `adb0153dff36e172d0553463cc961a11bd7c7e1e` — CI #873 SUCCESS;
- exact post-main Wave 10 product head — CI #874 SUCCESS.

The common seven-peer Driver interoperability laboratory is also now merged on `main` through PR #173, merge `a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`.

## Ordered path to v0.1

```text
Wave 03      Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04      Project portability + basic Trends + Administration                        COMPLETE
Wave 05      Canonical Script Engineering                                                COMPLETE
Wave 06      Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07      Visual Runtime Object Model + typed visual Engineering                      COMPLETE
Wave 08      Graphical Editor + Image + Engineering Development Monitor                  COMPLETE
08-FOLLOW-A  TAG Bit Access + Driver Bit-Level Boolean Binding                           COMPLETE
08-FOLLOW-B  Typed Visual Expressions + Boolean Conditions + Analog Fill                 COMPLETE
Wave 09      Screens + Popups + Dynamos + navigation + Historical Data + Reporting       COMPLETE
Wave 10      Python visual events + animation + preview                                  COMPLETE
Driver Lab   Seven-peer reproducible interoperability tool stack                         COMPLETE / MERGED
Drivers      Shared convergence + product L2 gates + accepted integration                 ACTIVE / PRIORITY
Wave 11      Complete HMI Runtime demo vertical slice                                    DEFERRED
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Driver convergence — ACTIVE / PRIORITY

The additional industrial Drivers are an explicit pre-Wave-11 completion objective.

Execution strategy:

1. use the merged common reproducible interoperability laboratory;
2. converge Drivers with product + independent-peer evidence first;
3. return real product/protocol defects to the owning worker with exact evidence;
4. implement shared host contracts once under Coordinator issue #174;
5. preserve protocol branches as isolated evidence sources rather than merging historical branch baggage wholesale;
6. require exact integrated CI before every mainline Driver transition.

Current evidence-driven convergence order:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

OPC UA, DNP3, S7 and BACnet enter shared integration after their currently assigned L2/fix gates close.

## Interoperability laboratory — COMPLETE / MERGED / SEVEN-PEER STACK GREEN

Merged PR:

`#173 — Driver interoperability lab — common multi-protocol peer stack`

Merge commit:

`a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`

Exact validated functional head:

`3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`

Validation:

- **Interop Lab Smoke #42: SUCCESS**;
- **EliteSCADA CI #886: SUCCESS** after rerunning failed jobs on the unchanged functional SHA;
- no product/Modbus code was modified in response to the first-attempt timing flakes;
- later pre-merge commits were documentation-only `[skip ci]`.

Common test peers now include:

- MQTT — Eclipse Mosquitto + Node-RED control plane;
- Allen-Bradley CIP — pinned ControlLogix + CompactLogix simulators;
- OPC UA — open62541 1.5.4 + independent node-opcua reference client;
- IEC 60870-5-104 — pinned lib60870-C deterministic outstation;
- DNP3 — pinned dnp3py independent outstation;
- Siemens S7 ISO-on-TCP — python-snap7 3.1.2 deterministic server/DB peer;
- BACnet/IP — BACpypes 0.19.0 independent device/reference peer.

Smoke #42 proves the whole tool stack can build/start together, S7 TCP readiness, BACnet health, MQTT round-trip and OPC UA browse/read/write/subscription reference behavior.

This remains **tool/peer readiness**, not automatic Driver product acceptance. Each Driver still requires its own product-path L2 where assigned.

## Shared Coordinator scope — issue #174

Coordinator owns once for all Drivers:

- fail-closed Driver registry/planner/factory and central activation;
- canonical rich Communication TAG binding + compatibility migration;
- common Data Source readiness;
- protected credential/certificate/private-key resolution;
- module/catalog/loading policy;
- common rich operation surface where simple `WriteAsync` is insufficient;
- SourceTimestamp/ServerTimestamp/current/historical late-event policy;
- central Engineering ConnectionTest/Browse/Import/Reconcile API/UI;
- integration and mainline CI.

Workers must not create protocol-private alternatives for these shared contracts.

## Wave 11 — DEFERRED

Wave 11 still owns the complete owner-testable HMI Runtime demo vertical slice. Its scope is unchanged. No Wave 11 implementation begins while Driver convergence is the active priority unless explicitly reprioritized.

## Evidence discipline

- L0: unit/codec/contracts;
- L1: same-stack/in-process/loopback;
- L2: independent software peer over real wire;
- L3: representative vendor simulator/device;
- L4: representative hardware/site.

Normal CI, L2/L3/L4, licensing and conformance are separate gates. Never improve a status by weakening a test.

## Development quality

- preserve canonical Engineering/backend authority;
- no Driver-to-Driver coupling;
- no plaintext protected material;
- fix root causes rather than widening timeouts/assertions merely for green badges;
- third-party lab peers stay test-only unless an explicit production dependency decision says otherwise;
- use Actions for meaningful evidence, not ritual;
- keep `LAST CHANGE.md`, Roadmap and assignment state synchronized because continuation depends on them.