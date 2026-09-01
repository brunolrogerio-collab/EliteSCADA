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
- Active branch: `coordination/pre-wave11-licensegen-slider` from `main`/`628f54a9c91be5113f4a1b0dfcf1672041eb7f2c`.
- Active stage: **GUI License Generator + industrial Slider + application file workflow + minimum Dynamo library**.

Although L3 technically released the next development wave, the Development Lead explicitly inserted an owner-usability gate before Wave 11. **Do not create/start Wave 11 work until issue #191 is completed and accepted.**

## Pre-Wave 11 gate #191

### Task A — actual License Generator executable

The licensing implementation and Windows x64 publish path were accepted under #183, but the previous executable was CLI-only: double-clicking without arguments printed usage in a transient console and exited. That does not satisfy the Development Lead's operational requirement.

Current verified artifact:

- code provenance: `9b963c40f013f115b9787049cdb90949a30cbcbc`;
- Preview Licensing CI #81 / run `33510855126`: **SUCCESS**;
- Actions artifact `EliteSCADA-LicenseGenerator-win-x64`, artifact id `9801589579`;
- extracted payload: one self-contained `EliteSCADA.LicenseGenerator.exe`, Windows x64 PE32+;
- executable SHA-256: `11d856889b14c61524214e640bc0e63e42d52687eadc7703926a5eb6ebe83a75`;
- production private signing material remains external to GitHub, CI and normal product artifacts.

Current implementation changes the Windows entry point to a WinForms application. Starting without arguments opens a Portuguese graphical form; command-line issuance remains available for controlled automation. Windows CI publishes a self-contained single-file `win-x64` executable and invokes a non-interactive `--smoke-test` path before artifact upload.

State: **GUI IMPLEMENTED LOCALLY / WINDOWS BUILD AND ARTIFACT EVIDENCE PENDING CI**.

### Task B — industrial Slider visual control

Research of the established Elipse E3 HMI behavior identified Linear Slider / Rotation Slider semantics backed by translation/rotation animation and properties including interaction enablement, range and current value. This validates the requested dual use: passive indication or operator adjustment.

EliteSCADA repository audit found no built-in Slider before this gate. `Analog Fill` was passive indication only and was not a substitute for an interactive Slider.

Required implementation is tracked in #191 and must remain inside the canonical visual/Engineering/runtime architecture. It must support passive display and an interactive authorized value-adjustment mode without frontend-to-Driver bypass.

Current branch adds `core.slider` to the shared backend/frontend Visual Property Registry and built-in schema, palette, editor defaults, live-value projection and canonical renderer. Passive mode displays the bound value. Interactive mode requires a writable stable TAG binding, good quality and runtime write callback; commands use the protected audited `/api/tags/{id}/write` boundary and fail closed for read-only/unavailable/unauthorized state.

State: **IMPLEMENTED LOCALLY / FOCUSED TESTS ADDED / CI PENDING**.

### Task C — developer-selected application file

EliteSCADA already had a versioned `.escadapkg` ZIP container containing manifest, canonical Engineering JSON and assets, but Engineering presented it mainly as backup/download. The branch makes the product language explicit: **Save Application As** chooses a developer-owned `.escadapkg` path when the browser supports the File System Access API, with browser download fallback; **Open Application** retains inspect/Preview/Apply safety.

The portable application file is intentionally distinct from the server-side Working/Revision lifecycle. The design follows the useful E3 Domain/Project idea—an explicit developer-owned application boundary—without copying a multi-file layout for the first release. See `docs/APPLICATION-PROJECT-STORAGE.md`.

State: **IMPLEMENTED LOCALLY / WEB BUILD PASS / CI PENDING**.

### Task D — minimum built-in Dynamo library

The branch seeds eight canonical, original and insertable definitions through the Engineering asset registry:

- pumps: centrifugal standard and submersible;
- motors: standard and VFD;
- valves: on/off and control;
- tanks: vertical and horizontal.

The graphical editor exposes a Dynamo library palette and creates instances with stable `dynamoKey`, placement, default bounds and optional `equipmentPath`. Runtime composition substitutes `{equipmentPath}` in child TAG bindings without mutating the shared definition.

State: **IMPLEMENTED LOCALLY / FOCUSED TESTS ADDED / CI PENDING**.

### Windows 11 publisher trust

Unsigned or unknown-publisher warnings are not considered fixed in this Preview gate. Stable product goals and Wave 13 now require an Authenticode-signed Windows x64 package with a trusted timestamp and release verification. Signing credentials must remain outside repository and normal build artifacts; SmartScreen reputation is a separate deployment/reputation concern and cannot be truthfully claimed from a locally produced unsigned Preview executable.

State: **REQUIREMENT LOCKED IN PRODUCT GOAL AND ROADMAP / IMPLEMENTATION DEFERRED TO WAVE 13**.

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

1. finish source-level validation and focused coverage for all four implementation tasks;
2. commit/push the active branch and update #191 to the full accepted scope;
3. require normal EliteSCADA CI and Preview Licensing CI green on the exact implementation SHA;
4. retrieve and verify the new graphical Windows x64 artifact, retaining provenance/checksum;
5. integrate to `main` only when green, then obtain appropriate post-main evidence;
6. synchronize #191, this file, roadmap and handoff with exact SHA/run evidence;
7. close #191 only after the gate is accepted;
8. only then may Wave 11 start.

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
