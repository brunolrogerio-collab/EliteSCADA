# EliteSCADA Roadmap

**Status date:** 2026-08-31 (BRT)  
**Active direction:** **FINAL DRIVER MAINLINE INTEGRATION -> POST-MAIN L3 LAB**  
**Wave 11:** **DEFERRED UNTIL POST-MAIN L3 PASS**

Authoritative product intent: `PROJECT GOAL.md`.  
Operational coordinator handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Driver/lab evidence policy: `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`.  
Shared convergence issue: `#174`.  
Draft integration PR: `#175`.

## Current validated foundation

- Wave 10: COMPLETE / MERGED / POST-MAIN GREEN.
- Common seven-peer interoperability infrastructure: COMPLETE / MERGED.
- Independent product-path Driver L2: **7/7 PASS / ACCEPTED**.
- Shared Driver convergence on PR #175: **7/7 CLOSED FOR COORDINATOR CONVERGENCE**.
- Remaining immediate boundary: final merge into `main`, post-merge CI, then integrated L3 laboratory.

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
Driver Lab   Seven-peer reproducible interoperability infrastructure                     COMPLETE / MERGED
Driver L2    Independent product-path protocol evidence                                  7/7 PASS
Drivers      Shared runtime/Engineering convergence                                      7/7 CLOSED IN PR
Mainline     Merge Driver convergence + exact post-main CI                               NEXT GATE
Driver L3    All seven Drivers concurrently in one main build/runtime                    REQUIRED BEFORE WAVE 11
Wave 11      Complete HMI Runtime demo vertical slice                                    DEFERRED UNTIL L3 PASS
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation by Development Lead                       AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Driver evidence policy

EliteSCADA currently uses these operational evidence levels:

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — Driver against an independent software peer over the real wire protocol;
- **L3** — one `main` EliteSCADA build running **all seven Drivers concurrently** against the independent laboratory peers;
- **L4** — real physical hardware/site validation using a Preview build.

See `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` for the detailed acceptance matrix.

## Immediate stage gate: main -> L3 -> Wave 11

The next transition is strictly ordered:

```text
PR #175 final pre-merge green
    -> merge Driver convergence to main
    -> exact post-main CI green
    -> integrated seven-Driver L3 laboratory
    -> L3 PASS
    -> Wave 11 may start
```

### L3 requirement

The L3 laboratory must run a single EliteSCADA instance/project with all seven communication Data Sources active simultaneously:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

It must prove concurrent acquisition, supported writes/commands, shared readiness, cache identity isolation, one-peer fault isolation, recovery and clean shutdown without cross-Driver interference.

A collection of seven independent L2 results does not satisfy this gate.

## Wave 11

Wave 11 remains the complete owner-testable HMI Runtime demo vertical slice.

**Do not start Wave 11 until the post-main integrated seven-Driver L3 laboratory is PASS.**

Once L3 passes, Driver convergence/laboratory stops being the active stage and the project proceeds to Wave 11.

## L4 physical validation

Physical Driver evaluation is deferred until the EliteSCADA Preview build exists.

The Development Lead and physical Driver acceptance authority is:

**Bruno Luiz Rogerio**

L4 is recorded per representative real device/model/firmware and does not block the start of Wave 11. It is a later Preview validation gate.

## Quality locks

- canonical Engineering/backend authority;
- schema-v15 `CommunicationBinding` remains the rich communication TAG authority;
- no plaintext protected material;
- no Driver-to-Driver coupling;
- no canonical TAG/cache/event bypass;
- no test weakening to hide product/protocol defects;
- L2 does not imply L3;
- L3 does not imply physical hardware validation;
- physical L4 evidence is device-specific;
- licensing and formal conformance/certification remain separate claims;
- exact CI evidence is required at every stage transition.
