# DRIVER AND INTEROPERABILITY LAB STATUS — EliteSCADA

Last evidence policy update: **2026-08-31 BRT**  
Scope: **DRIVER / INTEROPERABILITY EVIDENCE**

> Current coordinator implementation state and merge gates live in `CURRENT-COORDINATOR-HANDOFF.md`.

## Evidence levels used by EliteSCADA

From this checkpoint forward, the project uses the following operational evidence levels:

- **L0** — unit, codec and contract tests;
- **L1** — same-stack / in-process / loopback integration;
- **L2** — EliteSCADA Driver against an independent software peer over the real wire protocol;
- **L3** — **post-main integrated seven-Driver laboratory**: one EliteSCADA build/runtime with all seven converged Drivers active concurrently against their independent laboratory peers;
- **L4** — **physical hardware / site evaluation using a Preview build**, executed and accepted by the Development Lead, **Bruno Luiz Rogerio**.

L3 and L4 are deliberately different evidence gates. L3 proves the integrated multi-Driver software system. L4 proves representative real hardware/site behavior. Licensing, formal protocol certification/conformance and vendor breadth remain separate claims.

## L2 — independent product-path laboratory

Status: **7/7 PASS / ACCEPTED**.

| Driver | Independent peer/evidence | L2 |
| --- | --- | --- |
| MQTT | Eclipse Mosquitto / HiveMQ | **PASS** |
| IEC-104 | lib60870-C | **PASS 13/13** |
| CIP / EtherNet/IP | independent Logix/CIP peer | **PASS** |
| OPC UA | open62541 | **PASS** |
| DNP3 | dnp3py | **PASS** |
| Siemens S7 ISO-on-TCP | python-snap7 | **PASS** |
| BACnet/IP | BACpypes | **PASS** |

The common peer infrastructure is also **7/7 healthy**. Peer health alone is not product acceptance; each Driver has separately accepted L2 evidence.

## L3 — post-main integrated seven-Driver laboratory

Status: **PLANNED / BLOCKED UNTIL DRIVER CONVERGENCE IS MERGED TO `main` AND POST-MERGE CI IS GREEN**.

### Purpose

L3 is not seven isolated Driver tests. It must prove that the converged host can operate all seven Drivers **at the same time** without cross-Driver interference, shared-host contract regressions or resource/lifecycle collisions.

### Required topology

Use one exact EliteSCADA build from `main` with one Engineering project containing seven active communication Data Sources:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

Each Data Source must connect to its independent laboratory peer using the real protocol path already accepted at L2.

### Minimum L3 acceptance matrix

The integrated run must prove, concurrently:

1. all seven Data Sources compile from canonical schema-v15 `CommunicationBinding`;
2. all seven runtime factories activate through the shared host registry/composition root;
3. all seven reach their protocol readiness state without requiring every TAG to be `Good`;
4. deterministic acquisition from every protocol reaches the canonical TAG cache;
5. at least one supported write/command path per Driver succeeds where the first-release Driver supports writes;
6. timestamps/quality/typed values remain protocol-correct and do not leak SDK/session objects into shared boundaries;
7. loss of one peer degrades/faults only its own Data Source and does **not** interrupt the other six;
8. the failed peer can return and its Driver can recover/reconnect without restarting the EliteSCADA host when the Driver contract supports recovery;
9. concurrent traffic does not create TAG/cache identity collisions, protected-material scope leakage or Driver-to-Driver coupling;
10. runtime shutdown cleanly stops all seven Drivers;
11. backend/runtime smoke and the dedicated seven-Driver L3 workflow are green on the exact `main` SHA;
12. no assertion is weakened to manufacture a green laboratory result.

### Stage transition rule

**Wave 11 MUST NOT start until:**

`Driver convergence merged to main -> post-main CI green -> L3 seven-Driver integrated laboratory PASS`

When that chain is green, the Driver convergence/laboratory stage is closed and the project proceeds to the next Wave.

## L4 — physical hardware / site validation

Status: **DEFERRED UNTIL A PREVIEW BUILD EXISTS**.

L4 is intentionally **not** a prerequisite for starting Wave 11.

Physical Driver evaluation will be performed after the Preview build is assembled. The Development Lead and acceptance authority for this gate is:

**Bruno Luiz Rogerio**

L4 evidence should be recorded per actual device, not as a blanket protocol claim. Each record should capture at least:

- exact EliteSCADA Preview build / commit;
- Driver and DriverType;
- manufacturer;
- model;
- firmware/software revision;
- network/topology and relevant communication settings;
- tested reads, writes/commands and reconnect scenarios;
- observed quality/timestamps/diagnostics;
- result (`PASS`, `FAIL`, `PARTIAL`, `NOT TESTED`);
- evaluator notes and final acceptance.

Example classification:

`Siemens S7 -> CPU 1214C / firmware X.Y -> L4 PASS`

A PASS on one physical model must not be generalized to every device implementing that protocol.

## Claim discipline

- Normal CI green is not L2 or L3.
- Seven independent L2 PASS results are not L3.
- L3 requires all seven Drivers operating concurrently in one EliteSCADA runtime/build.
- L3 is not physical hardware evidence.
- L4 requires real hardware/site evaluation using the Preview build.
- L4 acceptance is device-specific.
- Licensing, conformance/certification and vendor breadth remain separate from L0-L4 interoperability evidence.
