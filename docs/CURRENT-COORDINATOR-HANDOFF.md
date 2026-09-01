# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-09-01 BRT**  
Operational status: **DRIVER CONVERGENCE CLOSED / L2 7/7 PASS / L3 PASS+INTEGRATED / PRE-WAVE-11 GATE #191 IMPLEMENTATION ACTIVE**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> Live GitHub refs and exact-head Actions evidence override SHAs copied into prose. Stable product intent is governed by `PROJECT GOAL.md`. Mutable exact state belongs in `LAST CHANGE.md`.

## 1. Mandatory resume protocol

A replacement Coordinator must read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`;
5. live `main` and latest Actions;
6. issues #174, #180 and #183 as historical acceptance authority;
7. active pre-Wave-11 gate issue #191.

Repository state, not old chat messages, is the continuity source.

## 2. Current mainline

Latest code integration checkpoint:

- PR #190 — `Stabilize post-L3 Gateway convergence evidence`: **MERGED**;
- main code SHA: `9b963c40f013f115b9787049cdb90949a30cbcbc`;
- EliteSCADA CI #1024, run `33510855124`: **SUCCESS**;
- Preview Licensing CI #81, run `33510855126`: **SUCCESS**.

The PR #190 change only stabilizes observation of the already-required final Gateway diagnostics state after the real Modbus destination value is observed. It does not weaken production semantics or L3 acceptance.

## 3. Driver convergence and interoperability

- Engineering schema v15 / canonical `CommunicationBinding`: **CLOSED**.
- MQTT: **CLOSED / L2 PASS**.
- IEC-104: **CLOSED / L2 PASS 13/13**.
- CIP / EtherNet/IP: **CLOSED / L2 PASS**.
- OPC UA: **CLOSED / L2 PASS**.
- DNP3: **CLOSED / L2 PASS**.
- Siemens S7 ISO-on-TCP: **CLOSED / L2 PASS**.
- BACnet/IP: **CLOSED / L2 PASS**.
- Independent product-path L2: **7/7 PASS / ACCEPTED**.
- Integrated L3: **PASS / ACCEPTED / INTEGRATED**.

### L3 evidence chain

First complete technical proof:

- SHA `958bc9aa2bbaf788d9a15c19d986ba728a7562fd`;
- L3 #23, run `33478345659`: **SUCCESS**.

Final exact-head stabilization proof:

- SHA `02b7408e68b81355de6a56dc0267c9e28c0c74bf`;
- EliteSCADA CI #1023: **SUCCESS**;
- Preview Licensing CI #80: **SUCCESS**;
- L3 Seven-Driver Lab #30: **SUCCESS**.

Integrated main proof:

- PR #190 -> main `9b963c40f013f115b9787049cdb90949a30cbcbc`;
- EliteSCADA CI #1024: **SUCCESS**;
- Preview Licensing CI #81: **SUCCESS**.

Issue #180 and convergence issue #174 satisfy their completion boundaries and are to be treated as closed historical gates after their final state is recorded.

## 4. Demo/licensing

Issue #183: **COMPLETED / ACCEPTED / INTEGRATED**.

Implemented behavior includes:

- Demo with <=200 TAG Run gate;
- Engineering may exceed 200 TAGs;
- 300-minute continuous Demo runtime session;
- machine-bound signed entitlement tiers 500 / 1000 / 1500 / 3000 / 5000 / Unlimited;
- invalid installed licenses block Run;
- versioned machine request code;
- protected licensing API and management UI;
- offline Windows x64 License Generator publish path;
- private signing material remains outside GitHub/CI/product binaries.

Authority: `docs/LICENSING-AND-DEMO-MODE.md`, `docs/licensing/ACCEPTANCE-EVIDENCE-2026-08-31.md`, issue #183.

## 5. Current stage boundary

The previous mandatory gate is complete:

`Driver convergence -> main integration -> exact post-main CI -> integrated seven-Driver L3 -> PASS`

Therefore L3 no longer blocks Wave 11.

On **2026-09-01**, the Development Lead established issue #191 as a required owner-usability gate before Wave 11. Its current scope is:

1. replace the transient CLI-only License Generator startup with a directly usable Windows GUI while preserving controlled CLI automation;
2. add a canonical industrial Slider with passive indication and authorized adjustment modes;
3. make application persistence explicit as developer-selected **Save Application As / Open Application** using one portable `.escadapkg` file;
4. ship a minimum insertable Dynamo library with two motors, two pumps, two valves and two tanks;
5. place Windows 11 publisher trust in the release roadmap: Authenticode signing and trusted timestamp are mandatory for the Wave 13 Windows package, while unsigned Preview limitations must be disclosed.

Implementation is active on `coordination/pre-wave11-licensegen-slider`. Exact SHA and CI truth belong in `LAST CHANGE.md` and live GitHub Actions.

**Do not start, branch, issue, or implement Wave 11 until #191 is implemented, validated, integrated and accepted.**

## 6. Existing Runtime context for the later Wave 11

The current frontend still contains a hardcoded Runtime demo surface in `web/scada-web/src/main.tsx`, while reusable runtime capabilities already exist for alarms, TAG inspection, trends, history, client memory and the visual runtime object model.

Wave 11 remains intended to convert those capabilities into an owner-testable HMI Runtime vertical slice derived from canonical Engineering rather than relying on a hardcoded Demo screen. This is future context only; it must not be started before #191 closes.

## 7. Non-negotiable rules

- Repository/CI state overrides stale chat and stale prose for implementation truth.
- Stable product rules belong in `PROJECT GOAL.md`; mutable exact state belongs in `LAST CHANGE.md`.
- No red CI into `main`.
- Do not weaken tests to manufacture green evidence.
- No Driver-to-Driver calls or canonical TAG/cache/event bypass.
- No plaintext protected material.
- `CommunicationBinding` remains canonical in schema v15.
- Licensing remains host-owned; Drivers never inspect license files/hardware IDs directly.
- Private license-signing material never enters GitHub, CI or distributed product binaries.
- L2 does not imply L3; L3 does not imply physical L4.
- Every material coordination transition must be persisted in repository documentation/issues before the coordinator reports completion.
