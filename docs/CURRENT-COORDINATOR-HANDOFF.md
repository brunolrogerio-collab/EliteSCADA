# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-08-31 BRT**  
Operational status: **DRIVER CONVERGENCE 7/7 CLOSED / L2 7/7 PASS / PRE-MERGE MAINLINE VALIDATION / L3 NEXT AFTER MAIN / DEMO-LICENSING SPECIFIED NOT IMPLEMENTED**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> Live GitHub refs and exact-head Actions evidence override SHAs copied into prose. Stable product intent is governed by `PROJECT GOAL.md`. Current implementation truth is repository/live CI evidence. Do not reconstruct state from old chat messages or stale worker PR prose.

## 1. Mandatory resume protocol

A replacement Coordinator must read, in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/COORDINATOR-TRANSFER-2026-08-31.md`;
5. live PR **#175** and branch `coordination/driver-convergence-v3`;
6. live issue **#174**;
7. issue **#180** for the post-main integrated L3 gate;
8. issue **#183** plus `docs/LICENSING-AND-DEMO-MODE.md` for the future Demo/licensing track;
9. Actions for the exact current code head;
10. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` for laboratory evidence policy.

Status vocabulary:

- **MERGED** — present on `main`;
- **IMPLEMENTED IN PR** — implemented on coordinator/feature line, not yet in `main`;
- **SPECIFIED / NOT IMPLEMENTED** — locked product requirement exists, but production code does not yet satisfy it.

## 2. Current live integration line

- Repository: `brunolrogerio-collab/EliteSCADA`
- Coordinator branch: `coordination/driver-convergence-v3`
- Draft PR: **#175 — Driver convergence v3 — shared host contracts**
- PR state at last audit: **DRAFT / OPEN / MERGEABLE / DO NOT MERGE until controlled integration**
- `main` at last audit: **`d0a4e13816992b0a0eb0eb68c36e78c560cc1d88`**
- Latest exact **code-validated** coordinator head: **`6d340e8ca3baaabf138c19be2fb947297854e1f6`**
- Exact validation: **EliteSCADA CI #982 — SUCCESS**

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

Documentation-only `[skip ci]` commits after `6d340e8...` do not create a newer code-validation claim.

## 3. Closed Driver/shared gates

- Engineering schema v15 / canonical `CommunicationBinding`: **CLOSED**
- MQTT coordinator convergence: **CLOSED**
- IEC-104 coordinator convergence: **CLOSED**
- CIP / EtherNet/IP coordinator convergence: **CLOSED**
- OPC UA coordinator convergence: **CLOSED**
- DNP3 coordinator convergence: **CLOSED**
- Siemens S7 ISO-on-TCP coordinator convergence: **CLOSED**
- BACnet/IP coordinator convergence: **CLOSED**
- Independent product-path L2: **7/7 PASS / ACCEPTED**

There is no eighth Driver ingress in this convergence scope.

## 4. Driver checkpoint

| Driver | Coordinator convergence | Product-path L2 |
| --- | --- | --- |
| MQTT | **CLOSED** | **PASS / ACCEPTED** |
| IEC-104 | **CLOSED** | **PASS / ACCEPTED 13/13** |
| CIP / EtherNet/IP | **CLOSED** | **PASS / ACCEPTED** |
| OPC UA | **CLOSED** | **PASS / ACCEPTED** |
| DNP3 | **CLOSED** | **PASS / ACCEPTED** |
| Siemens S7 ISO-on-TCP | **CLOSED** | **PASS / ACCEPTED** |
| BACnet/IP | **CLOSED** | **PASS / ACCEPTED** |

Worker PRs/branches are historical source/evidence, not merge trains.

## 5. Current Preview capacity code versus final Demo goal

### Current implemented/validated behavior

Functional head `6d340e8...` / CI #982 introduced a **transitional static 200-TAG project cap**:

- `ProductCapacityPolicy.MaxTagsPerProject = 200`;
- canonical registry rejects creation of the 201st TAG;
- Engineering Preview/Apply rejects imports that would exceed 200;
- updates to existing TAGs at the limit remain allowed;
- oversized manipulated runtime candidates also fail through the capped registry.

This behavior is **IMPLEMENTED IN PR / VALIDATED**.

### New final product requirement

The product requirement was subsequently refined. Final Preview distribution must use a **Demo + hardware-bound licensing model**.

That final behavior is **SPECIFIED / NOT IMPLEMENTED**.

Locked contract: `docs/LICENSING-AND-DEMO-MODE.md`  
Tracking issue: **#183**

Required Demo behavior:

- no installed license => **Demo**;
- Engineering may contain more than 200 TAGs;
- Demo **Run** allowed only for projects with <= **200 TAGs**;
- >200 TAGs must block Run without deleting/truncating Engineering data;
- Demo runtime may execute for at most **300 continuous minutes per explicit Run session**;
- at expiry, industrial runtime stops gracefully and the application remains available;
- user is informed that the 300-minute evaluation period expired;
- user may explicitly start Runtime again for a fresh 300-minute Demo session.

Required Licensed behavior:

- hardware-bound signed license;
- initial TAG tiers: **500 / 1000 / 1500 / 3000 / 5000 / Unlimited**;
- valid licensed/evaluation tier removes the 300-minute Demo runtime limit;
- project above licensed tier blocks Run;
- EliteSCADA generates a copyable machine request code from a canonical hashed hardware fingerprint;
- controlled offline License Generator returns a signed license code/file;
- normal product contains public verification material only;
- **private signing key must never be committed to GitHub or distributed with EliteSCADA**;
- installed license with invalid signature/schema or wrong hardware blocks Run and reports invalid license;
- absence of a license enters Demo mode.

Important: **no Demo timer, machine request code, signed-license verifier, License Generator or licensing UI has been implemented yet.**

The current mutation-time 200-TAG cap must later be refactored into the final entitlement-aware Run/activation gate. Do not report the final Demo behavior as implemented merely because a 200-TAG constant currently exists.

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
- licensing/entitlement is host-owned; Drivers must never read license files/hardware fingerprints directly.

## 7. Evidence policy

EliteSCADA uses:

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — Driver against an independent software peer over the real wire protocol;
- **L3** — post-main integrated seven-Driver laboratory with one EliteSCADA build/runtime operating all seven Drivers concurrently;
- **L4** — physical hardware/site validation using the Preview build, performed and accepted by Development Lead **Bruno Luiz Rogerio**.

Licensing, protocol conformance/certification and vendor breadth are separate evidence claims.

## 8. Immediate stage order

The current required transition remains:

```text
PR #175 controlled final integration
    -> merge Driver convergence/current Preview-capacity code to main
    -> exact post-main CI green
    -> issue #180 integrated seven-Driver L3 laboratory
    -> L3 PASS
    -> Wave 11
```

**Wave 11 MUST NOT start before issue #180 passes.**

The Demo/licensing track (#183) is a separate Preview/distribution requirement. It does not replace the immediate post-main L3 gate.

## 9. L3 minimum acceptance

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

The L3 project can remain <=200 TAGs, so the transitional capacity code does not block this integration gate.

## 10. L4 physical validation

Physical Driver validation is deferred until a Preview build exists and does not block Wave 11.

Physical acceptance authority: **Bruno Luiz Rogerio, Development Lead**.

L4 evidence is recorded per exact Preview build and actual manufacturer/model/firmware. A PASS on one device must not be generalized to all devices using the protocol.

A controlled evaluation license may later be issued through the same hardware-bound licensing mechanism when L4 or external Preview evaluation requires capacity above 200 TAGs or uninterrupted runtime above 300 minutes.

## 11. Immediate actions for the replacement Coordinator

1. Read the mandatory resume files listed in section 1.
2. Re-read live `main` and PR #175 before making any merge decision.
3. Treat `6d340e8...` / CI #982 as the latest exact code-validation checkpoint unless a newer code commit exists.
4. Confirm PR #175 remains mergeable and audit final delta for accidental duplicate host contracts, protected-material leakage or canonical TAG/cache bypass.
5. Do **not** begin implementing #183 inside PR #175 merely because the spec is now documented; keep licensing as a separate product track unless explicitly retargeted.
6. Perform the controlled merge only after the final integration audit remains clean.
7. Require exact post-merge `main` CI green.
8. Then execute issue #180 L3.
9. Only an L3 PASS releases Wave 11.

## 12. Non-negotiable rules

- No worker self-merges.
- Red CI does not enter `main`.
- Do not weaken tests to manufacture green evidence.
- No Driver-to-Driver calls or canonical TAG/cache/event bypass.
- No plaintext protected material.
- Shared readiness is not every TAG `Good`.
- `CommunicationBinding` remains canonical in schema v15.
- L2 does not imply L3; L3 does not imply physical L4.
- Final Demo/licensing behavior must distinguish **no license (Demo)** from **installed invalid license (Run blocked)**.
- Private license-signing keys never enter this repository, CI or distributed product binaries.

## 13. Current stage boundary

PR #175 remains **DRAFT / OPEN / DO NOT MERGE** until a controlled mainline integration decision is taken.

Driver convergence is code-complete and green. The immediate remaining boundary is mainline integration + exact post-main CI + issue #180 L3. Demo/licensing is now a locked future product requirement tracked separately in #183 and must not be mistaken for already implemented code.
