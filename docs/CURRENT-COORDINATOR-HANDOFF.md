# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-08-31 BRT**  
Operational status: **DRIVER CONVERGENCE 7/7 CLOSED / PREVIEW 200-TAG CAP VALIDATED / PR #175 DRAFT / PRE-MERGE MAINLINE VALIDATION**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> Live GitHub refs and exact-head Actions evidence override SHAs copied into prose. Architecture semantics remain governed by ADRs and `DRIVER-CONVERGENCE-COORDINATION-V1.md`.

## 1. Resume protocol

A replacement Coordinator should read, in this order:

1. live PR **#175** and branch `coordination/driver-convergence-v3`;
2. this file;
3. live issue **#174**;
4. issue **#180** for the post-main integrated L3 gate;
5. Actions for the exact current code head;
6. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` for evidence policy;
7. `docs/PREVIEW-CAPACITY-POLICY.md` for the Preview product-capacity contract.

Do not reconstruct current state from old worker PR descriptions, assignment documents or historical handoffs.

Status vocabulary:

- **MERGED** — present on `main`;
- **IMPLEMENTED IN PR** — implemented on coordinator/worker line, not yet in `main`;
- **SPECIFIED / NOT IMPLEMENTED** — requirement/architecture exists but production code does not satisfy it.

## 2. Current integration line

- Repository: `brunolrogerio-collab/EliteSCADA`
- Coordinator branch: `coordination/driver-convergence-v3`
- Draft PR: **#175 — Driver convergence v3 — shared host contracts**
- Current exact code-validated head: **`6d340e8ca3baaabf138c19be2fb947297854e1f6`**
- Exact validation: **EliteSCADA CI #982 — SUCCESS**
- PR state: **DRAFT / OPEN / DO NOT MERGE**
- Last audited `main` base before the current code gate: `d0a4e13816992b0a0eb0eb68c36e78c560cc1d88`

CI #982 evidence:

- Release backend build: **SUCCESS**, 0 warnings / 0 errors;
- `Scada.Core.Tests`: **246 passed**;
- `Scada.Drivers.Tests`: **347 passed**;
- `Scada.Historian.TimescaleDb.Tests`: **23 passed**;
- `Scada.Security.Tests`: **27 passed**;
- `Scada.Persistence.PostgreSql.Tests`: **107 passed**;
- total backend tests: **750 passed / 0 failed**;
- runtime smoke: **SUCCESS**;
- Web build: **SUCCESS**;
- Chromium E2E: **SUCCESS**.

Documentation-only `[skip ci]` commits after `6d340e8...` do not create a new code-validation claim.

## 3. Closed shared gates

- Engineering schema v15 / canonical `CommunicationBinding`: **CLOSED**
- MQTT coordinator convergence: **CLOSED**
- IEC-104 coordinator convergence: **CLOSED**
- CIP / EtherNet/IP coordinator convergence: **CLOSED**
- OPC UA coordinator convergence: **CLOSED**
- DNP3 coordinator convergence: **CLOSED**
- Siemens S7 ISO-on-TCP coordinator convergence: **CLOSED**
- BACnet/IP coordinator convergence: **CLOSED**
- Independent product-path L2: **7/7 PASS / ACCEPTED**
- Preview 200-TAG project-capacity enforcement: **IMPLEMENTED IN PR / VALIDATED**

There is no eighth Driver ingress in this convergence scope.

## 4. Driver checkpoint summary

| Driver | Coordinator | Product-path L2 |
| --- | --- | --- |
| MQTT | **CLOSED** | **PASS / ACCEPTED** |
| IEC-104 | **CLOSED** | **PASS / ACCEPTED 13/13** |
| CIP / EtherNet/IP | **CLOSED** | **PASS / ACCEPTED** |
| OPC UA | **CLOSED** | **PASS / ACCEPTED** |
| DNP3 | **CLOSED** | **PASS / ACCEPTED** |
| Siemens S7 ISO-on-TCP | **CLOSED** | **PASS / ACCEPTED** |
| BACnet/IP | **CLOSED** | **PASS / ACCEPTED** |

Worker PRs remain source/evidence history, not merge trains. Re-read live worker refs only when a historical protocol implementation/evidence question requires it.

## 5. Preview product capacity

The externally distributed Preview edition is limited to:

**200 TAGs per project**

This is a project-wide total across all communication Drivers and internal memory sources. It is not 200 TAGs per Driver.

Authoritative policy:

`docs/PREVIEW-CAPACITY-POLICY.md`

Implementation boundaries:

- central constant/contract in `src/Scada.Core/Product/ProductCapacityPolicy.cs`;
- canonical `InMemoryTagRegistry` rejects creation of the 201st TAG before mutation;
- existing TAGs remain editable while the project is at capacity;
- Engineering Preview calculates the projected resulting project count;
- Apply is blocked atomically when the projected result exceeds the limit;
- runtime candidate construction uses the same capped canonical registry, so a manipulated package cannot activate an oversized candidate through the normal runtime path;
- no environment-variable or command-line unlimited bypass is part of the Preview contract.

Boundary regressions are in `tests/Scada.Core.Tests/PreviewProductCapacityTests.cs` and are green in CI #982.

This is capacity control/misuse deterrence, not cryptographic anti-tamper DRM. Signing, licensing and stronger distribution controls remain separate future gates.

## 6. Shared architecture that must remain intact

- common Driver module registry keyed by stable DriverType;
- common runtime planner/factory component registry;
- shared protocol-neutral readiness contract;
- host-owned scoped short-lived protected-material resolver/lease seam;
- Engineering v15 `CommunicationBinding` as canonical rich communication TAG envelope;
- canonical TAG registry/cache/event flow;
- no protocol SDK/session objects across shared planning boundaries;
- no Driver-to-Driver runtime calls;
- no plaintext secret/private-key material in Engineering/packages/logs/diagnostics;
- no duplicated product-capacity constants in Drivers/UI/importers.

## 7. Evidence policy and next stage

EliteSCADA currently uses:

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — Driver against an independent software peer over the real wire protocol;
- **L3** — post-main integrated seven-Driver laboratory with one EliteSCADA build/runtime operating all seven Drivers concurrently;
- **L4** — physical hardware/site validation using the Preview build, performed and accepted by Development Lead **Bruno Luiz Rogerio**.

Required transition:

```text
PR #175 final pre-merge gate
    -> merge Driver convergence + Preview capacity policy to main
    -> exact post-main CI green
    -> issue #180 integrated seven-Driver L3 laboratory
    -> L3 PASS
    -> Wave 11
```

**Wave 11 MUST NOT start before issue #180 passes.**

The L3 project must remain within the same 200-TAG project capacity. L3 validates concurrency and isolation, not large-project capacity.

## 8. L3 minimum acceptance

Issue #180 owns the detailed matrix. At minimum, one exact `main` build/project must run these seven Data Sources simultaneously:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

The run must prove concurrent acquisition, supported writes/commands, shared readiness, canonical cache identity isolation, one-peer fault isolation, recovery/reconnect and clean shutdown without cross-Driver interference.

Seven isolated L2 results do not satisfy L3.

## 9. Future L4 physical validation

Physical Driver validation is deferred until the Preview build exists and does not block Wave 11.

Physical acceptance authority: **Bruno Luiz Rogerio, Development Lead**.

L4 evidence must be recorded per exact Preview build and real device manufacturer/model/firmware. A PASS for one representative device must not be generalized to every device using that protocol.

## 10. Immediate coordinator action

Before PR #175 can leave Draft / DO NOT MERGE:

1. re-read live `main` and live PR #175 after the Preview-capacity documentation commits;
2. confirm the PR remains mergeable and that the base has not moved unexpectedly;
3. treat `6d340e8...` / CI #982 as the latest code-validation checkpoint unless a later code commit is introduced;
4. audit the final delta for accidental host-contract duplication, plaintext protected material, canonical TAG/cache bypass or capacity-policy bypass;
5. perform the controlled merge only after those checks remain clean;
6. require exact post-merge `main` CI green;
7. only then start issue #180 L3.

## 11. Non-negotiable rules

- No worker self-merges.
- Red CI does not enter `main`.
- Do not weaken a test to manufacture green evidence.
- No Driver-to-Driver calls or canonical TAG/cache/event bypass.
- No plaintext protected material.
- Shared readiness is not every TAG `Good`.
- `CommunicationBinding` remains canonical in schema v15.
- Preview capacity is 200 TAGs per project until explicitly revised through the central product policy.
- L2 does not imply L3; L3 does not imply physical L4.
- Licensing/formal conformance remain separate evidence claims.

## 12. Merge / stage boundary

PR #175 remains **DRAFT / OPEN / DO NOT MERGE**.

Driver convergence and the Preview TAG-capacity safeguard are code-complete and green on CI #982. The remaining boundary is controlled mainline integration, exact post-main CI, then issue #180 L3. Only an L3 PASS releases Wave 11.
