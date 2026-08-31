# EliteSCADA Roadmap

**Status date:** 2026-08-30  
**Active direction:** **DRIVER CONVERGENCE + INTEROPERABILITY LAB**  
**Wave 11:** **DEFERRED UNTIL DRIVER CONVERGENCE**

Authoritative detailed plan: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.  
Parallel execution model: `docs/DEVELOPMENT-WAVES.md`.  
Driver/lab live state: `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`.  
Current coordinator checkpoint: `LAST CHANGE.md`.  
CI policy: `docs/CI-USAGE-POLICY.md`.

Engineering Import/Export remains cross-cutting: canonical Engineering entities participate in versioned JSON, validation/Preview/Apply, Working/revision lifecycle, PostgreSQL persistence where applicable and `.escadapkg` backup/restore.

## Current validated foundation

Wave 10 product merge on `main`:

`15daff2cc076f46f9433812babbd5cbb4b8d9554`

Validation evidence:

- exact Wave 10 integration head `adb0153dff36e172d0553463cc961a11bd7c7e1e` — CI #873 SUCCESS;
- exact post-main Wave 10 product head `15daff2cc076f46f9433812babbd5cbb4b8d9554` — CI #874 SUCCESS.

## Completed waves and follow-ups

- **Wave 03 — COMPLETE / MERGED.** Main merge `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; CI #418 green.
- **Wave 04 — COMPLETE / MERGED.** Main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`; CI #446 green.
- **Wave 05 — COMPLETE / MERGED.** Main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; CI #466 green.
- **Wave 06 — COMPLETE / MERGED.** Main merge `cc79713434c1d7b5988158b843b137eaf488d923`; CI #487 green.
- **Wave 07 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `8de706882ba20afedd666532ac41ae11115d06b3`; post-merge CI #510 green.
- **Wave 08 — COMPLETE / MERGED / POST-MERGE GREEN.** Main merge `bfd17d035d905e9bcae263f68244cfb2b6453aa2`; final integration CI #531 and post-merge CI #533 green.
- **08-FOLLOW-A — COMPLETE / MERGED / POST-MERGE GREEN.** PR #105; post-merge CI #543 green.
- **08-FOLLOW-B — COMPLETE / MERGED / POST-MERGE GREEN.** Final product head `dededaca980fdb72b5d4955685ab1161aca441fd`; CI #657 and #658 green.
- **Wave 09 — COMPLETE / MERGED.** Screens, Popups, Dynamos, canonical navigation, Historical Data Browser and Reporting/Report Designer.
- **Wave 10 — COMPLETE / MERGED / POST-MAIN GREEN.** Final PR #172; integration CI #873 green; product merge `15daff2cc076f46f9433812babbd5cbb4b8d9554`; post-main CI #874 green.

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
Drivers       Driver interoperability lab + shared convergence + final driver evidence    ACTIVE / PRIORITY
Wave 11      Complete HMI Runtime demo vertical slice                                    DEFERRED
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Driver convergence — ACTIVE / PRIORITY

The current priority is to finish the additional industrial protocol Drivers before starting Wave 11.

Execution order is evidence-driven rather than numerical:

1. consolidate the common `interop-lab/` so Driver workers share reproducible independent peers instead of private one-off harnesses;
2. immediately converge Drivers that already have strong product + independent-peer evidence;
3. route Drivers with a real protocol/type defect back to the owning worker with exact failing evidence;
4. add missing independent peers for Siemens S7 and BACnet/IP;
5. converge shared Coordinator-owned contracts once rather than copying them into every protocol branch;
6. run exact integration CI and only then promote accepted Drivers into `main`.

Shared Coordinator-owned convergence includes:

- fail-closed DriverHost registry/planner/factory composition;
- canonical rich Communication TAG binding and compatibility migration;
- common Data Source readiness activation;
- protected credential/certificate/private-key resolution;
- installable module/catalog/loading policy;
- common rich command/operation surface where `WriteAsync` is insufficient;
- source timestamp/current-value/historical-event ordering policy;
- central Engineering ConnectionTest/Browse/Import/Reconcile registration and API/UI exposure.

## Interoperability laboratory — ACTIVE

Integration branch:

`integration/driver-interop-lab-finalization`

The common lab is being promoted from a base MQTT/CIP scaffold into a reusable multi-protocol peer stack. Current implemented peers on the integration branch are:

- Eclipse Mosquitto + Node-RED control plane;
- ControlLogix + CompactLogix independent CIP simulators;
- open62541 OPC UA server + independent node-opcua reference client;
- lib60870-C IEC-104 outstation;
- dnp3py DNP3 outstation.

Siemens S7 and BACnet/IP independent peers remain the next missing laboratory tools.

The lab distinguishes peer/tool readiness from Driver product-path acceptance. A healthy simulator does not make a Driver green; a Driver L2 test must actually exercise the product path and canonical TAG semantics.

## Wave 11 — DEFERRED

Wave 11 still owns the complete owner-testable HMI Runtime demo vertical slice. Its scope is unchanged, but execution is intentionally deferred until the current Driver convergence phase is completed or explicitly reprioritized again.

No Wave 11 branch/worker work should consume Coordinator attention while Driver convergence is the active priority.

## Protocol boundary

Required v0.1 protocol remains Modbus TCP. Simulation, Client Memory, Server Memory and Gateway remain part of product validation. The additional protocol Drivers are now an explicit pre-Wave-11 completion objective rather than background parked work.

## Development quality

- never merge a Driver because its isolated worker CI is green;
- require independent peer evidence where practical and keep L2/L3/L4 claims separate;
- fix root causes instead of weakening tests/security/concurrency;
- preserve canonical Engineering/backend authority;
- keep third-party protocol libraries behind Driver-owned adapters and document licensing/distribution gates;
- use Actions to buy evidence, not ceremony;
- require exact integration CI before every Driver mainline transition;
- keep coordination checkpoints synchronized because `siga` depends on them.
