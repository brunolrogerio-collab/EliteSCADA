# L3 Seven-Driver Integrated Laboratory

Status: **ACTIVE**

Issue: #180

Branch: `coordination/driver-l3-seven-protocol-lab`

Validated base: `main` merge `f6210a1539741847aab8949a7e453c8cf141162d`

## Objective

Prove one EliteSCADA build and one active Engineering runtime operating all seven converged communication Drivers concurrently against independent real-wire software peers:

1. MQTT
2. IEC 60870-5-104
3. Allen-Bradley CIP / EtherNet/IP
4. OPC UA
5. DNP3
6. Siemens S7 ISO-on-TCP
7. BACnet/IP

L3 is a system-integration gate. Existing L2 evidence remains valid but is not sufficient by itself.

## Mandatory architecture invariant

All cross-protocol behavior must travel through EliteSCADA's canonical runtime contracts:

`Driver -> canonical TAG cache/event path -> TAG Gateway -> runtime WriteAsync -> different Driver`

Direct Driver-to-Driver calls are forbidden.

## Slice A — seven peers + heterogeneous TAG Gateway

Status: **IMPLEMENTED / CI VALIDATION IN PROGRESS**

Keep all seven independent peers alive concurrently while proving a product-path heterogeneous Gateway route:

`S7 DB1.DBW0 (Source TAG) -> TAG Gateway -> CIP MyInt (Destination TAG)`

Acceptance:

- S7 Source acquires initial `1234` with Good quality;
- initial Gateway synchronization reaches the CIP peer;
- the test writes `2222` through the Source TAG's owning S7 Driver;
- the canonical S7 TAG event triggers Gateway transfer;
- CIP Driver writes the Destination TAG;
- an independent CIP protocol client reads `2222` back from the peer;
- Source and Destination belong to different Driver types;
- Gateway remains Running and write failures remain zero.

Test: `L3HeterogeneousGatewayPeerTests.S7SourceWrite_TransfersThroughTagGateway_ToDifferentCipDriverAndRealPeer`

Workflow: `.github/workflows/l3-seven-driver-lab.yml`

This slice proves the user's explicit heterogeneous TAG Gateway requirement against real peers. It does **not** by itself close L3 because the active EliteSCADA runtime still contains only the Source/Destination pair.

## Slice B — one runtime, seven Data Sources

Build one schema-v15 EngineeringPackage containing at least one deterministic TAG per protocol and activate it through `CommunicationDriverRuntimeComposition.BuildForCurrentSchema()`.

Acceptance:

- exactly seven converged communication Driver types are present in runtime diagnostics;
- all seven reach shared protocol readiness in one activation transaction;
- each protocol publishes at least one deterministic Good TAG into the canonical cache;
- TAG IDs and paths remain isolated across protocols;
- no protocol-private host contract is used by the coordinator.

The project must remain within Demo's 200-TAG Run capacity so licensing does not obscure Driver evidence.

## Slice C — writes / commands while seven are live

With all seven Sources online, execute the supported first-release write/command path for every Driver that advertises write capability.

Acceptance must verify the result at each independent peer where deterministic readback is available. Successful socket transmission alone is not acceptance.

## Slice D — isolated peer failures and recovery

Interrupt one peer at a time while the other six remain live.

For each protocol:

- only the affected Data Source may degrade/fault;
- unrelated TAG qualities must remain healthy;
- Gateway routes unrelated to the failed Source/Destination remain operational;
- recovery must occur without restarting the EliteSCADA host where the Driver supports automatic recovery;
- stale commands must not replay after reconnection.

## Slice E — Gateway quality and destination-failure policy

Run the heterogeneous Gateway through this sequence:

`healthy transfer -> Source unacceptable quality -> write suppression -> Source recovery -> Destination peer failure -> route/write failure evidence -> Destination recovery -> later Source change transfers successfully`

Acceptance:

- bad Source quality is never forwarded as a normal write;
- Destination failure does not damage Source Driver state;
- the other five Drivers remain live;
- recovery requires no host restart;
- transfer/write-failure counters reflect the sequence accurately.

## Slice F — exact-head closure

L3 is PASS only when, on one exact branch SHA:

- dedicated `L3 Seven-Driver Lab` workflow is green;
- normal EliteSCADA build/test/smoke remains green;
- all Slice B-E assertions are automated and green;
- evidence is recorded in issue #180 and coordinator handoff documents.

Only after that gate may Wave 11 begin.

## L4 boundary

Physical PLC/device/vendor hardware validation remains L4 and is intentionally outside this software-laboratory gate.
