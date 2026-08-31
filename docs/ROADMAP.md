# EliteSCADA Roadmap

**Status date:** 2026-08-30 (BRT)  
**Active direction:** **DRIVER CONVERGENCE**  
**Wave 11:** **DEFERRED UNTIL DRIVER CONVERGENCE**

Authoritative product intent: `PROJECT GOAL.md`.  
Coordinator handoff: `docs/COORDINATOR-HANDOFF.md`.  
Live ownership: `docs/CHAT-WORK-ASSIGNMENTS.md`.  
Driver/lab status: `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`.  
Shared convergence issue: `#174`.  
Current checkpoint: `LAST CHANGE.md`.

## Validated foundation

Wave 10 is COMPLETE / MERGED / POST-MAIN GREEN.

- final Wave 10 product merge: `15daff2cc076f46f9433812babbd5cbb4b8d9554`;
- final Wave 10 integration CI #873: GREEN;
- post-main CI #874: GREEN.

The common seven-peer Driver interoperability laboratory is COMPLETE / MERGED:

- PR #173 merge: `a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`;
- functional lab head: `3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`;
- Interop Lab Smoke #42: GREEN;
- normal CI #886: GREEN after rerunning unrelated Modbus timing failures on unchanged functional SHA.

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

## Driver convergence — ACTIVE

Shared authority:

- issue #174;
- branch `coordination/driver-convergence-v3`;
- Draft PR #175;
- audited PR head `06c7d408c76926bf5d37dfec4be20ea6044f52b1`;
- exact normal CI #895 GREEN.

Current shared foundation includes registry/planner/factory contracts, readiness, protected-material resolution and a **partial** rich Communication TAG binding scaffold.

### Immediate blocking slice: Engineering schema v15

The next Coordinator step is not Driver import yet. Complete the Communication TAG binding lifecycle first:

1. advance canonical Engineering to schema v15 with <=v14 compatibility;
2. wire rich-binding validation into Preview;
3. preserve binding through Apply/materialization/export;
4. enforce compatibility `Address == CommunicationBinding.PortableAddress`;
5. preserve `TagValueSelector` and ADR-007 transform-before-selection semantics;
6. implement TAG CSV fidelity where applicable;
7. prove JSON/CSV/Preview/Apply/re-export, `.escadapkg`, revisions and PostgreSQL persistence;
8. exact-head normal CI.

At audited #175 head, `EngineeringExchangeService.CurrentSchemaVersion` remains 14 and TAG Apply drops `CommunicationBinding`, so the v15 scaffold must **not** be reported as complete merely because CI #895 is green.

## Evidence-driven Driver order

After the v15 gate:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

- MQTT: ready for shared convergence; broad independent broker/security/restart/freshness evidence green.
- IEC-104: ready; independent lib60870 L2 13/13 green.
- CIP: ready; independent CIP L2 green.
- OPC UA: worker product-path open62541 L2 still active.
- DNP3: worker must fix real configured Int32 -> canonical Double mismatch; PR #167 remains active.
- Siemens S7: worker product-path python-snap7 L2 still active.
- BACnet/IP: worker product-path BACpypes discovery/RP/RPM/WP/COV/recovery L2 still active.

Protocol branches remain isolated source/evidence lines. Re-port/adapt narrowly against current `main`; never merge historical Driver branch baggage wholesale.

## Interoperability laboratory — COMPLETE

Common test-only peers on main:

- MQTT — Eclipse Mosquitto + Node-RED;
- Allen-Bradley CIP — ControlLogix + CompactLogix simulator profiles;
- OPC UA — open62541 + node-opcua reference client;
- IEC-104 — lib60870-C outstation;
- DNP3 — dnp3py outstation;
- Siemens S7 — python-snap7 server;
- BACnet/IP — BACpypes peer.

Peer/tool readiness is not automatic Driver product acceptance. L0/L1/L2/L3/L4, normal CI, licensing and conformance remain separate claims.

## Wave 11 — DEFERRED

Wave 11 still owns the complete owner-testable HMI Runtime demo vertical slice. Its scope is unchanged. Do not start Wave 11 implementation while Driver convergence is the active priority unless explicitly reprioritized.

## Quality locks

- canonical Engineering/backend authority;
- no plaintext protected material;
- no Driver-to-Driver coupling;
- no test weakening to hide real product/protocol defects;
- `Address == CommunicationBinding.PortableAddress` during v15 migration;
- `TagValueSelector` remains generic bit identity;
- ADR-007 transform precedes bit selection;
- exact final integration/main CI before stage transitions;
- keep `LAST CHANGE.md`, assignments, Driver status and handoff synchronized.