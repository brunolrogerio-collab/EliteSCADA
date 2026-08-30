# DRIVER AND INTEROPERABILITY LAB STATUS — EliteSCADA

Date: 2026-08-30
Status: **WAVE 10 ACTIVE / DRIVERS PARKED-REVIEWABLE EXCEPT OPC UA / INTEROP LAB PARTIAL**

This file is a coordination snapshot, not a replacement for live GitHub state. Before any merge, assignment, CI claim or production-readiness claim, re-read the exact branch, PR and workflow run.

Wave 10 remains the product priority. Driver work and interoperability-lab work continue in parallel and must not be merged into Wave 10 merely to obtain reassurance CI.

## Driver status snapshot

| Driver | Exact observed worker head | Handoff | Exact-head CI | Current coordination classification |
| --- | --- | --- | --- | --- |
| Driver 4 — BACnet/IP | `2ced848124350a5d83ec563a4fb22312ac224fe1` | Draft PR #109 | CI #787 GREEN | Protocol software mature/reviewable; parked for shared convergence and external interoperability evidence. |
| Driver 5 — Allen-Bradley Logix EtherNet/IP/CIP | `18ff6dc989a65c1f8b006f83c08d8394a5510914` | Draft PR #111 | CI #785 GREEN | Protocol software mature/reviewable; parked for shared convergence, production dependency/conformance decision and hardware evidence. |
| Driver 6 — IEC 60870-5-104 | `d597ef5ed1885b63dcd0b3568287bc1e34330bee` | Draft PR #146 | CI #798 GREEN | Formal handoff now exists and exact head is green; ready for Coordinator convergence review, still lacking independent simulator/hardware acceptance. |
| Driver 7 — DNP3 | `ac0dd6944f53d19447f3353addd404c02da7249c` | Draft PR #108 | CI #697 GREEN | Protocol software mature/reviewable; same-stack wire evidence exists, but independent interoperability and commercial Step Function licensing remain release gates. |
| Driver 8 — Siemens S7 ISO-on-TCP | `0c37b922b44f591ebd143470abf3ebaa6b4bffae` | Draft PR #135 | CI #789 GREEN | One of the strongest parked worker milestones; still needs shared convergence and representative Siemens simulator/hardware evidence. |
| Driver 9 — OPC UA | `8ba5870d7dbe119a2999d8a73394289e2349f401` | No worker handoff PR | No Actions runs on canonical worker branch | Least formalized Driver workstream. Canonical branch exists and fails unknown data types closed, but secure/runtime/Engineering milestone still needs a reviewable handoff and exact-head CI. |
| Driver 10 — MQTT Industrial | `fd2f3cbba3e8fc701e376cfcbd1685b28e3d98ef` | Draft PR #128 | CI #791 GREEN | Protocol software mature/reviewable; current exact head is green. Independent broker/product-path evidence and shared convergence remain required. |

## What “green” does and does not mean

A green normal EliteSCADA CI on a Driver branch means that exact worker head is build/test/smoke/E2E compatible with the PR merge context exercised by that run. It does **not** mean:

- the Driver has been accepted into the common DriverHost registry/runtime;
- representative vendor hardware has been validated;
- an independent protocol implementation has interoperated with the EliteSCADA Driver;
- commercial redistribution/licensing gates are cleared;
- the Driver is production certified.

These are separate acceptance dimensions and must stay separate in Coordinator reporting.

## Shared Coordinator-owned convergence still required

The parked Driver branches must not each implement their own copy of the following shared concerns:

1. fail-closed DriverHost registry/planner/factory composition;
2. canonical rich Communication TAG binding in the post-Wave-09 Engineering schema revision;
3. `Address == CommunicationBinding.PortableAddress` compatibility/migration rules;
4. common Data Source readiness activation;
5. protected credential/certificate/private-key resolution;
6. installable module/catalog/loading policy;
7. common command/operation surface where simple `WriteAsync` is insufficient;
8. source timestamp/current-value/historical-event ordering policy;
9. central Engineering ConnectionTest/Browse/Import/Reconcile registration and protected API/UI exposure;
10. exact integration-head CI before any mainline Driver transition.

## Driver-specific remaining evidence

### Driver 4 — BACnet/IP

Worker protocol scope is reviewable and exact-head CI is green. Still required:

- independent BACnet/IP simulator/reference-stack scenarios;
- multi-vendor device evidence when practical;
- RPM fallback, COV lease/renew/recovery, priority/relinquish and BBMD/FDR behavior against real peers;
- shared registry/readiness/binding/security/module integration.

### Driver 5 — Allen-Bradley Logix EtherNet/IP/CIP

Worker protocol scope is reviewable and exact-head CI is green. Still required:

- CompactLogix/ControlLogix or credible independent simulator evidence;
- real route/session/MSP ceilings and reconnect behavior;
- production CIP implementation/dependency and ODVA/conformance/licensing decision;
- shared registry/readiness/binding/Engineering integration.

### Driver 6 — IEC 60870-5-104

Driver 6 now has a formal Draft handoff PR and exact-head green CI. This supersedes the earlier state where the worker had not formally closed its milestone.

Still required:

- independent IEC-104 server/outstation interoperability;
- representative RTU/IED evidence later;
- shared rich binding, registry/factory/readiness and event-ingress policy;
- explicit first-release security/TLS decision.

### Driver 7 — DNP3

Strong same-stack protocol-wire evidence exists through the Step Function adapter, but same-stack Master/Outstation tests are not independent interoperability.

Still required:

- independent DNP3 outstation peer/hardware evidence;
- commercial Step Function licensing evidence before commercial production distribution;
- shared rich command, event-ingress, registry/readiness/module integration.

### Driver 8 — Siemens S7 ISO-on-TCP

Worker milestone is reviewable and exact-head green with no external S7 runtime dependency.

Still required:

- independent simulator and representative S7-300/400/1200/1500 evidence where practical;
- protection/PUT-GET, TSAP, PDU, layout, write rejection and reconnect evidence;
- shared transform/binding/readiness/registry/module integration.

### Driver 9 — OPC UA

Canonical branch currently ends at `8ba5870d...` with explicit fail-closed unknown-data-type handling. There is no worker handoff PR and no Actions run on `driver9/opc-ua`.

This workstream should not be described as review-ready yet. It still needs a coherent worker milestone covering at least:

- stable NodeId identity;
- secure endpoint/session policy;
- subscriptions/monitored-item or explicit polling readiness;
- separate SourceTimestamp and ServerTimestamp preservation;
- injected credential/certificate/private-key resolution seams;
- discovery/browse/reconcile/runtime-provider scope;
- Draft handoff PR and exact-head CI.

The separate OPC UA interoperability-lab branch described below is test infrastructure. It does not substitute for closing the Driver 9 worker milestone.

### Driver 10 — MQTT Industrial

Current exact head `fd2f3cbb...` has green CI #791. This supersedes older CI references recorded in the PR body.

Still required:

- live product-path broker evidence, not just opt-in tests that return when no broker is configured;
- Mosquitto plus an independent second broker where practical;
- MQTT 5/3.1.1, QoS, retained, TLS/auth, persistent sessions, broker restart, backpressure and freshness evidence;
- shared registry/readiness/secret-resolution/binding/module integration.

## Interoperability Lab — purpose and evidence levels

The repository contains `interop-lab/` as destructive/reproducible **test infrastructure**, intentionally separate from the product runtime. Node-RED acts as control-plane tooling while independent peers are intended to exercise real wire protocols.

Do not collapse these evidence levels into one claim:

- **L0**: unit/codec/contract tests only;
- **L1**: same-stack or in-process/loopback protocol evidence;
- **L2**: independent software peer over real wire protocol;
- **L3**: representative vendor simulator/device evidence;
- **L4**: representative hardware/site acceptance.

A higher level does not erase the need for lower-level deterministic tests, and L2 is not hardware certification.

## Current lab state on main

The current mainline lab has:

- MQTT: Eclipse Mosquitto + Node-RED base lab, runnable;
- Allen-Bradley CIP: simulator overlay present/runnable, but the standard lab smoke currently validates its Compose model rather than proving the complete EliteSCADA Driver 5 product path;
- OPC UA: base mainline previously had palette/port preparation only;
- IEC-104, DNP3, Siemens S7 and BACnet: reserved independent-peer slots, not yet accepted runnable scenarios.

## OPC UA independent-software lab PR #148

Branch: `coordination/driver-interop-opcua-v1`

Observed exact head:

`ffa810c2a4e6524fdb4d05c7c094a899e80af67b`

The branch adds:

- open62541 1.5.7 as an independent OPC UA server built from SHA-256-pinned upstream single-file release artifacts;
- separate `compose.opcua.yaml` overlay;
- stable writable Double/Int32/Boolean/String nodes;
- `node-opcua` reference-client smoke from the Node-RED image;
- intended anonymous browse/read/write/readback/monitored-item scenario.

### Current exact evidence

Normal EliteSCADA CI #807 on `ffa810c2...`: **GREEN**.

Interop Lab Smoke #8 on the same head: **RED**.

The failing step is:

`Build and start independent OPC UA peer`

The workflow successfully completed before that failure:

- scenario catalog JSON validation;
- Node-RED flow JSON validation;
- base Compose validation;
- CIP overlay Compose validation;
- OPC UA overlay Compose validation;
- base lab build/start;
- MQTT round-trip smoke.

Because the open62541 peer failed to build/start, the `OPC UA open62541 interoperability smoke` step was skipped. Therefore **there is not yet accepted L2 OPC UA interoperability evidence from PR #148**, despite the normal product CI being green.

Do not merge PR #148 until the dedicated Interop Lab gate is green on the exact head and the normal CI remains green for that same integration decision.

## MQTT lab evidence boundary

Interop Lab Smoke #8 proves that the base lab can build/start and that its MQTT round-trip smoke succeeds against the lab broker/control plane.

That is useful infrastructure evidence, but it must not be misreported as proof that the current `driver10/mqtt` product implementation completed a live end-to-end EliteSCADA Driver 10 broker scenario. Driver 10 still needs an explicitly wired product-path live broker acceptance scenario.

## Recommended laboratory expansion order

While Wave 10 remains priority, the lab can progress independently without touching Wave product contracts. Recommended order after the OPC UA build/start defect is fixed:

1. make OPC UA L2 smoke green and deterministic;
2. wire an explicit EliteSCADA Driver 10 -> Mosquitto product-path scenario, then an independent second broker;
3. promote the CIP overlay from Compose validation to explicit Driver 5 read/write/reconnect scenarios;
4. add independent IEC-104 peer for Driver 6;
5. add independent DNP3 outstation for Driver 7, intentionally not Step Function;
6. add independent S7 peer/simulator for Driver 8;
7. add independent BACnet/IP peer for Driver 4.

Driver 9's product milestone should progress in parallel with, but remain logically separate from, the OPC UA lab infrastructure.

## Coordinator rules

- Wave 10 has priority over Driver/lab integration work.
- Never merge a Driver branch directly to `main` because its worker CI is green.
- Never treat lab infrastructure success as product-driver acceptance unless the scenario explicitly exercises that Driver's product path.
- Never treat independent software interoperability as hardware/vendor certification.
- Re-read exact heads/checks before every status claim or mutation.
- Preserve CI policy NORMAL; run expensive matrices only for real integration/evidence checkpoints.
