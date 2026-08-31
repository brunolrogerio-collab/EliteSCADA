# DRIVER AND INTEROPERABILITY LAB STATUS — EliteSCADA

Date: 2026-08-30  
Status: **DRIVER CONVERGENCE + INTEROPERABILITY LAB ACTIVE / WAVE 11 DEFERRED**

This file is a coordination snapshot, not a substitute for live GitHub state. Re-read the exact branch, PR and workflow before every merge or evidence claim.

Wave 10 is closed on `main`. Additional industrial Drivers and the common interoperability laboratory are now the project priority before Wave 11.

## Live Driver snapshot

| Driver | Exact observed worker head | Handoff / product CI | Independent-peer evidence | Coordinator classification |
| --- | --- | --- | --- | --- |
| Driver 4 — BACnet/IP | `de3357750f79266e43588e7bb26d66093f8cf3d5` | Draft PR #109; CI #860 GREEN | Independent BACnet peer not yet implemented | Software mature; **waiting on lab peer + shared Coordinator convergence**. |
| Driver 5 — Allen-Bradley Logix CIP | `18ff6dc989a65c1f8b006f83c08d8394a5510914` | Draft PR #111; CI #785 GREEN | Validation PR #165 head `c47a1cd...`; Driver 5 CIP L2 Smoke #6 GREEN | **Ready for shared Coordinator convergence**; later hardware/ODVA evidence remains. |
| Driver 6 — IEC 60870-5-104 | `d597ef5ed1885b63dcd0b3568287bc1e34330bee` | Draft PR #146; CI #798 GREEN | PR #168, head `948f5588...`; L2 #7 GREEN, 13/13 | **Highest-priority convergence candidate**: product + substantive L2 green. |
| Driver 7 — DNP3 | `ac0dd6944f53d19447f3353addd404c02da7249c` | Draft PR #108; CI #697 GREEN | PR #167 reaches Online and receives all points but L2 #5 RED on canonical Analog Input type | **Return to Driver 7 for product type-boundary correction**, then rerun L2; licensing remains release gate. |
| Driver 8 — Siemens S7 ISO-on-TCP | `0c37b922b44f591ebd143470abf3ebaa6b4bffae` | Draft PR #135; CI #789 GREEN | Independent S7 peer not yet implemented | Software mature; **waiting on lab peer + shared Coordinator convergence**. |
| Driver 9 — OPC UA | `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6` | Draft PR #169; CI #869 GREEN | Common open62541 peer/reference-client tool exists; Driver 9 product-path L2 not yet run | **Unblocked by lab**; next action is Driver 9 L2 against common peer, then shared convergence. |
| Driver 10 — MQTT Industrial | `232383ec4b51b38775f674bf375cf7f7f595b875` | Draft PR #128; CI #858 GREEN | Mosquitto + HiveMQ, TLS/auth, negative security, broker restart and live freshness lines are GREEN | **Highest-priority convergence candidate**: very mature product + broad live evidence. |

## Evidence levels

Keep these claims distinct:

- **L0** — unit/codec/contract tests;
- **L1** — same-stack/in-process/loopback protocol evidence;
- **L2** — independent software peer over the real wire protocol;
- **L3** — representative vendor simulator/device;
- **L4** — representative hardware/site acceptance.

A green normal CI means the branch is compatible with the tested merge context. It does not automatically mean L2, licensing clearance, shared DriverHost integration or production certification.

## Common interoperability lab

Active integration branch:

`integration/driver-interop-lab-finalization`

The branch is based on post-Wave-10 `main` and centralizes peer infrastructure that had become fragmented across validation branches.

### Implemented common peers

- MQTT: Eclipse Mosquitto + Node-RED control plane;
- Allen-Bradley CIP: pinned ControlLogix and CompactLogix simulator profiles;
- OPC UA: pinned open62541 1.5.4 server plus independent node-opcua client smoke;
- IEC-104: pinned MZ Automation lib60870-C outstation;
- DNP3: pinned dnp3py independent outstation.

Linux/Git Bash:

```bash
cd interop-lab
bash scripts/lab.sh all-start
bash scripts/lab.sh status
```

PowerShell:

```powershell
cd interop-lab
./scripts/lab.ps1 all-start
./scripts/lab.ps1 status
```

Protocol-specific start/status/stop commands are available for CIP, OPC UA, IEC-104 and DNP3. OPC UA additionally exposes `opcua-smoke`.

### Missing common peers

1. Siemens S7 ISO-on-TCP independent server/PLC simulator;
2. BACnet/IP independent device/reference peer with RP/RPM/WP/COV and later BBMD/FDR behavior.

These are the next laboratory implementation targets after the current common-stack PR is green.

## Current independent-product evidence

### Driver 5 — CIP

Validation-only PR #165 uses the existing independent CIP simulator over real EtherNet/IP TCP/44818. Exact head `c47a1cd2093c0f3fc30ea9376eba806ae4455168` passed **Driver 5 CIP L2 Interop Smoke #6**.

The scenario exercises RegisterSession/SendRRData, DINT/REAL reads, write/readback and complete Driver polling/write behavior against the independent peer.

### Driver 6 — IEC-104

Validation-only PR #168 exact head `948f5588d30dcdf1909db80f2efc3258585b0f13` passed **Driver 6 IEC-104 L2 Interop Smoke #7**, 13/13.

Evidence includes:

- TCP + STARTDT;
- GI with process values;
- spontaneous values;
- readiness after startup GI;
- peer stop/start and reconnect;
- no command replay;
- all five first-release command Type IDs in Direct and SBO modes.

Driver 6 therefore no longer waits on basic independent software interoperability. Its main blocker is shared Coordinator convergence plus later L3/L4/security decisions.

### Driver 7 — DNP3

The independent dnp3py peer itself builds, starts and reaches Healthy. Driver 7 validation PR #167 also proves that the Step Function master reaches `Online` and receives all three configured static points with Good quality.

However the exact L2 run is **RED** for a real canonical type discrepancy:

- configured TAG: Analog Input 0, `TagDataType.Int32`;
- protocol variation: G30V1;
- adapter/raw measurement: `4242` as `System.Int32`;
- canonical cache after `Dnp3Driver`: `4242` as `System.Double`.

Binary and Counter values are correct, communications are healthy and failed-operation count is zero. The failure is therefore not a peer-health problem. Driver 7 must preserve the configured/canonical numeric type rather than changing the test to accept Double.

### Driver 9 — OPC UA

The stale earlier status is superseded. Driver 9 now has Draft PR #169 and exact-head CI #869 green at `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6`.

The product milestone includes secure endpoint/session selection, approved server certificate pinning, anonymous/username/certificate user modes, runtime-only secret/certificate resolver seams, reads/writes, subscriptions, SourceTimestamp/ServerTimestamp, reconnect, browse/import/reconcile and fail-closed datatype handling.

The common lab now provides the independent open62541 peer needed for the next validation branch. Driver 9 should next prove its actual product path against this peer, including reconnect/resubscribe and secure cases where the peer/tooling can support them.

### Driver 10 — MQTT

Driver 10 is much further than the older snapshot implied.

Current worker head `232383ec4b51b38775f674bf375cf7f7f595b875` has normal CI #858 green. Separate validation lines have already passed:

- Mosquitto + HiveMQ product-path live broker matrix;
- MQTT 5.0 and 3.1.1;
- QoS 0/1/2 and retained behavior;
- trusted TLS + mandatory authentication;
- invalid credentials and revoked-certificate fail-closed behavior;
- persistent sessions across real broker process restart;
- live freshness `Good -> Stale -> Good` while connection/readiness remain healthy.

Freshness validation exact head `25a23c028fb096d77d51ff527a5d74ac54be7736` passed MQTT Live Freshness Smoke #1, Broker Restart #2, Security Negative #3, Secure #11, Interop Lab #30 and normal EliteSCADA CI #852.

Driver 10 is therefore waiting primarily on shared Coordinator convergence, not basic broker tooling.

## Shared Coordinator-owned convergence

Do not let individual Driver branches clone these contracts privately:

1. fail-closed DriverHost registry/planner/factory composition;
2. canonical rich Communication TAG binding and compatibility migration;
3. common Data Source readiness activation;
4. protected credential/certificate/private-key resolution;
5. installable module/catalog/loading policy;
6. common rich command/operation surface where simple `WriteAsync` is insufficient;
7. current-value versus historical late-event/source-timestamp ingress policy;
8. central Engineering ConnectionTest/Browse/Import/Reconcile registration and protected API/UI exposure;
9. exact integrated CI before any Driver mainline transition.

## Coordination order after common lab validation

1. **Driver 10 MQTT** — converge shared runtime/secret/readiness/binding surfaces; broad live evidence already exists.
2. **Driver 6 IEC-104** — converge shared runtime/readiness/binding/event ingress; substantive L2 already green.
3. **Driver 5 CIP** — converge shared runtime/readiness/binding/Engineering; L2 green; retain later hardware/conformance gates.
4. **Driver 9 OPC UA** — run product-path L2 against common open62541 peer, then converge secure/shared surfaces.
5. **Driver 7 DNP3** — worker fixes canonical Analog Input type mismatch, reruns L2, then Coordinator convergence; Step Function commercial license remains release gate.
6. **Driver 8 S7** — build independent lab peer and obtain L2 before/alongside convergence.
7. **Driver 4 BACnet/IP** — build independent lab peer and obtain COV/RPM/WP/relinquish evidence before/alongside convergence.

The order may change when a real dependency is discovered, but Wave 11 remains deferred until this Driver phase is closed or explicitly reprioritized.

## Coordinator rules

- never merge a Driver directly because worker CI is green;
- never downgrade a product mismatch into a permissive test expectation merely to make L2 green;
- never claim lab-peer health as Driver product acceptance;
- never claim L2 as vendor/hardware certification;
- keep third-party peer/runtime licensing boundaries explicit;
- preserve canonical Engineering and shared host authority;
- run expensive CI for real integration/evidence checkpoints, not reassurance.
