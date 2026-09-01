# LAST CHANGE — EliteSCADA

Date: 2026-09-01 (BRT)

## Read first

Stable product intent: [`PROJECT GOAL.md`](PROJECT%20GOAL.md)  
Current handoff: [`docs/CURRENT-COORDINATOR-HANDOFF.md`](docs/CURRENT-COORDINATOR-HANDOFF.md)  
Roadmap: [`docs/ROADMAP.md`](docs/ROADMAP.md)  
Driver/lab evidence: [`docs/DRIVER-AND-INTEROP-LAB-STATUS.md`](docs/DRIVER-AND-INTEROP-LAB-STATUS.md)

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
- Wave 11: **NOT STARTED**.
- Active stage: **Development Lead pre-Wave-11 task — scope pending instruction**.

Although L3 now technically releases the next development wave, the Development Lead explicitly stated on 2026-09-01 that another task must be completed before Wave 11. **Do not create/start Wave 11 work until that task is supplied, recorded, executed and accepted.**

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

The accepted sequence proved:

- seven-peer startup/endpoint verification;
- common activation/readiness;
- concurrent acquisition 7/7 through canonical TAG/cache flow;
- supported writes/commands;
- heterogeneous TAG Gateway through canonical event/cache boundaries;
- serial peer fault isolation/recovery;
- Gateway source/destination fault/recovery;
- no Driver-to-Driver coupling;
- persistent evidence artifacts;
- clean shutdown;
- no weakened assertion to manufacture acceptance.

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

**STOP before Wave 11.**

The next action is to receive the Development Lead's pre-Wave-11 task, then:

1. record its scope and acceptance criteria in the repository;
2. decide whether it needs an issue/branch/PR;
3. execute and validate it with exact evidence appropriate to its risk;
4. update this file, roadmap and handoff;
5. only after that task is accepted may Wave 11 be started.

Do not infer the missing task scope from old chat or invent a Wave 11 substitute.

## Later Wave 11 context

When eventually released, Wave 11 remains the owner-testable HMI Runtime vertical slice. Current `web/scada-web/src/main.tsx` still includes a hardcoded Runtime Demo while reusable alarms, TAG inspector, trends, history, client memory and visual-runtime infrastructure already exist. Wave 11 should make the Runtime derive from canonical Engineering rather than preserve hardcoded Demo truth.

This is future context only, not authorization to begin Wave 11.

## Resume instruction

A new coordinator should:

1. read `PROJECT GOAL.md`;
2. read this file;
3. read `docs/CURRENT-COORDINATOR-HANDOFF.md` and `docs/ROADMAP.md`;
4. confirm live `main` and latest Actions;
5. confirm #174, #180 and #183 are closed;
6. locate the Development Lead's new pre-Wave-11 task/issue if it has since been supplied;
7. continue that task before any Wave 11 work.

Repository/CI state wins over stale chat memory.