# LAST CHANGE — EliteSCADA

Date: 2026-09-01 (BRT)

## Read first

Stable product intent: [`PROJECT GOAL.md`](PROJECT%20GOAL.md)  
Current handoff: [`docs/CURRENT-COORDINATOR-HANDOFF.md`](docs/CURRENT-COORDINATOR-HANDOFF.md)  
Roadmap: [`docs/ROADMAP.md`](docs/ROADMAP.md)  
Driver/lab evidence: [`docs/DRIVER-AND-INTEROP-LAB-STATUS.md`](docs/DRIVER-AND-INTEROP-LAB-STATUS.md)  
Active pre-Wave gate: [issue #191](https://github.com/brunolrogerio-collab/EliteSCADA/issues/191)

Live GitHub refs and exact-SHA Actions evidence override stale SHAs copied into prose.

## Mandatory continuity rule

The repository, not chat history, is the persistent coordination memory. A fresh coordinator must be able to resume safely from repository state alone.

For every material coordination cycle, decision, blocker, fix, validation run or next action:

1. read `PROJECT GOAL.md` and `LAST CHANGE.md` before changing code;
2. inspect live `main`, active branches/issues and exact-SHA Actions when implementation truth matters;
3. persist exact branch/SHA/run/issue evidence;
4. never leave a critical decision, blocker, diagnosis or next action only in chat;
5. keep stable product/architecture rules in `PROJECT GOAL.md` and mutable operational truth here;
6. when the Development Lead says `siga`, continue until completion or a real blocker;
7. before reporting completion, synchronize repository documentation/issues affected by the work.

## Current checkpoint

The Driver convergence + L3 stage is **COMPLETE**.

- Driver convergence issue #174: **CLOSED / COMPLETED**.
- Integrated L3 issue #180: **CLOSED / COMPLETED**.
- Demo/licensing issue #183: **CLOSED / COMPLETED / INTEGRATED**.
- Pre-Wave 11 gate issue #191: **OPEN / ACTIVE**.
- Wave 11: **NOT STARTED**.
- Active stage: **License Generator executable delivery + industrial Slider visual control**.

Although L3 technically released the next development wave, the Development Lead explicitly inserted two tasks before Wave 11. **Do not create/start Wave 11 work until issue #191 is completed and accepted.**

## Pre-Wave 11 gate #191

### Task A — actual License Generator executable

The licensing implementation and Windows x64 publish path are already accepted under #183. The remaining operational requirement is delivery of the real executable to the Development Lead.

Current verified artifact:

- code provenance: `9b963c40f013f115b9787049cdb90949a30cbcbc`;
- Preview Licensing CI #81 / run `33510855126`: **SUCCESS**;
- Actions artifact `EliteSCADA-LicenseGenerator-win-x64`, artifact id `9801589579`;
- extracted payload: one self-contained `EliteSCADA.LicenseGenerator.exe`, Windows x64 PE32+;
- executable SHA-256: `11d856889b14c61524214e640bc0e63e42d52687eadc7703926a5eb6ebe83a75`;
- production private signing material remains external to GitHub, CI and normal product artifacts.

State: **EXECUTABLE VERIFIED / DELIVERY IN CURRENT COORDINATION CYCLE**.

### Task B — industrial Slider visual control

Research of the established Elipse E3 HMI behavior identified Linear Slider / Rotation Slider semantics backed by translation/rotation animation and properties including interaction enablement, range and current value. This validates the requested dual use: passive indication or operator adjustment.

EliteSCADA repository audit found **no built-in Slider**. Current built-in visual types are rectangle, text, numeric display, button, analog fill and image. `builtin.display.analog-fill` is passive indication only and is not a substitute for an interactive Slider.

Required implementation is tracked in #191 and must remain inside the canonical visual/Engineering/runtime architecture. It must support passive display and an interactive authorized value-adjustment mode without frontend-to-Driver bypass.

State: **SPECIFIED / IMPLEMENTATION ACTIVE**.

## Driver/L3 acceptance evidence

First complete technical L3 proof:

- SHA `958bc9aa2bbaf788d9a15c19d986ba728a7562fd`;
- L3 Seven-Driver Lab #23 / run `33478345659`: **SUCCESS**.

Final exact-head stabilization proof before the final integration:

- SHA `02b7408e68b81355de6a56dc0267c9e28c0c74bf`;
- EliteSCADA CI #1023 / run `33510090342`: **SUCCESS**;
- Preview Licensing CI #80 / run `33510090333`: **SUCCESS**;
- L3 Seven-Driver Lab #30 / run `33510090344`: **SUCCESS**.

Final code integration into `main`:

- PR #190: **MERGED**;
- code SHA `9b963c40f013f115b9787049cdb90949a30cbcbc`;
- EliteSCADA CI #1024 / run `33510855124`: **SUCCESS**;
- Preview Licensing CI #81 / run `33510855126`: **SUCCESS**.

Documentation-only coordination commits after that code SHA use `[skip ci]`; they update handoff/roadmap/evidence and do not represent a newer code-validation claim.

## What L3 proved

One EliteSCADA runtime operated all seven communication Drivers concurrently:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

The accepted sequence proved seven-peer startup, common activation/readiness, concurrent acquisition 7/7 through canonical TAG/cache flow, supported writes/commands, heterogeneous TAG Gateway behavior, fault isolation/recovery, clean shutdown and no weakened assertion to manufacture acceptance.

Resolved L3 defects must not be reopened without fresh evidence. Historical fixes include BACnet explicit loopback binding, IEC-104 common diagnostics, cross-slice CIP state reset, BACnet REAL analog writes, commandable BACnet priority fixture and reconnect-safe MQTT recovery stimulus.

## Demo/licensing state

Status: **IMPLEMENTED / ACCEPTED / INTEGRATED**.

Accepted behavior includes:

- Engineering may exceed 200 TAGs;
- Demo Run allowed at <=200 TAGs;
- Demo runtime limited to 300 continuous minutes per explicit Run session;
- later explicit Run starts a fresh Demo session;
- valid machine-bound signed licenses grant 500 / 1000 / 1500 / 3000 / 5000 / Unlimited TAG tiers;
- licensed/evaluation tiers remove the Demo runtime timer under the accepted contract;
- invalid/tampered/expired/unknown-key/wrong-machine installed license blocks Run;
- versioned machine request code;
- protected licensing API and management UI;
- offline Windows x64 `EliteSCADA.LicenseGenerator.exe` publish path;
- private signing material remains external to GitHub, CI and normal product binaries.

Authority: issue #183, `docs/LICENSING-AND-DEMO-MODE.md`, `docs/licensing/ACCEPTANCE-EVIDENCE-2026-08-31.md`.

## Current stage / exact next action

**Issue #191 is the active gate.**

Execute in order:

1. deliver the verified Windows x64 License Generator executable and retain provenance/checksum;
2. create an implementation branch from current `main` for Slider work;
3. integrate the Slider into the canonical built-in visual schema, Engineering surface and Runtime renderer;
4. implement passive indication plus interactive authorized adjustment through the canonical write boundary;
5. add focused unit/mounted/E2E coverage;
6. require normal EliteSCADA CI green on the exact implementation SHA;
7. integrate to `main` only when green, then obtain post-main evidence appropriate to the change;
8. synchronize #191, this file, roadmap and handoff;
9. close #191 only after both tasks are accepted;
10. only then may Wave 11 start.

## Later Wave 11 context

When eventually released, Wave 11 remains the owner-testable HMI Runtime vertical slice. Current `web/scada-web/src/main.tsx` still includes a hardcoded Runtime Demo while reusable alarms, TAG inspector, trends, history, client memory and visual-runtime infrastructure already exist. Wave 11 should make the Runtime derive from canonical Engineering rather than preserve hardcoded Demo truth.

This is future context only, not authorization to begin Wave 11.

## Resume instruction

A coordinator should:

1. read `PROJECT GOAL.md`;
2. read this file;
3. read issue #191;
4. read `docs/CURRENT-COORDINATOR-HANDOFF.md` and `docs/ROADMAP.md`;
5. confirm live `main` and latest Actions;
6. continue issue #191 until accepted before any Wave 11 work.

Repository/CI state wins over stale chat memory.