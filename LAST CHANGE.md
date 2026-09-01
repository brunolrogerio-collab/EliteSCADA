# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **PRE-WAVE-11 #191 COMPLETE / ACCEPTED / INTEGRATED — WAVE 11 READY / NOT STARTED**

> This file is the mutable coordinator resume point. Stable product intent remains in `PROJECT GOAL.md`. Always verify live refs/Actions before acting because documentation-only `[skip ci]` commits may advance `main` beyond the validated code SHA.

## 1. Accepted code checkpoint

Pre-Wave-11 owner-usability implementation:

- issue #191: **COMPLETE / ACCEPTED / INTEGRATED**;
- implementation branch: `coordination/pre-wave11-licensegen-slider`;
- exact implementation head: `aeb9b3b5641adee344c4ead166b97cc0adba3dbf`;
- replacement integration PR #193: **MERGED**;
- validated main code merge: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`.

PR #192 was closed unmerged only because the connector failed to transition the draft PR to ready-for-review. PR #193 used the exact same validated head; there was no implementation divergence.

## 2. Validation evidence

### Pre-merge exact implementation head `aeb9b3b...`

- EliteSCADA CI #1033 / run `33525910566`: **SUCCESS**;
- Preview Licensing CI #90 / run `33525910582`: **SUCCESS**;
- L3 Seven-Driver Lab #39 / run `33525910552`: **SUCCESS**.

### Post-main exact code SHA `64ba134f...`

- Preview Licensing CI #92 / run `33527294658`: **SUCCESS**;
- EliteSCADA CI #1035 / run `33527294657`: **SUCCESS**.

EliteSCADA CI #1035 initially failed one pre-existing IEC-104 T2 timing assertion (`Adapter_T2FlushesPendingReceiveAcknowledgementWithoutFaultingSession`, expected 0 / actual 1). The failed backend job was rerun **unchanged**. On the rerun:

- backend build: **SUCCESS**;
- all backend tests: **SUCCESS**;
- Runtime smoke: **SUCCESS**;
- Web build: **SUCCESS**;
- Chromium E2E: **SUCCESS**.

No production code or test assertion was changed to manufacture the post-main green result. The initial failure is classified as transient timing noise because it did not reproduce on the unchanged exact code SHA.

## 3. Accepted pre-Wave-11 scope

### A — Graphical Windows License Generator

**MERGED / ACCEPTED**

- normal double-click opens a WinForms graphical interface;
- machine request, TAG tier, external private PEM, Key ID, optional expiry and output path are supported;
- controlled CLI remains available;
- private signing material is loaded only from an explicit external path and is not embedded in GitHub/CI/product artifacts.

Post-main artifact from Preview Licensing CI #92:

- artifact name: `EliteSCADA-LicenseGenerator-win-x64`;
- artifact id: `9808306320`;
- code SHA: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`;
- `EliteSCADA.LicenseGenerator.exe`: 116,257,103 bytes;
- format: PE32+ Windows GUI, x86-64;
- executable SHA-256: `841dea832d67f44e07aa10b2de96ccfffd5d518beeadafb48ed34e16d0317523`;
- GitHub artifact ZIP digest: `sha256:888ed224f686918f50e27dd5998e105a5e26900edd87254a1f163d7be9416943`.

Earlier implementation-head artifact #90 remains historical evidence:

- artifact id `9807739588`;
- executable SHA-256 `b0aa2f86433fdc141689eafb53f6acb3a25a12a513afde8061f805f6dacc94f5`.

### B — Canonical industrial Slider

**MERGED / ACCEPTED**

`core.slider` is integrated in the shared backend/frontend visual model. Passive display never writes. Interactive adjustment writes only through the protected/audited Runtime TAG boundary and fails closed for invalid, unavailable, read-only, non-writable or unauthorized targets.

### C — Explicit application file Save/Open

**MERGED / ACCEPTED**

Developer-selected **Save Application As** and **Open Application** use the portable `.escadapkg` boundary while preserving inspect/Preview/Apply safety and the distinction from server Working/Revisions/Published/Active lifecycle state.

### D — Minimum built-in Dynamo library

**MERGED / ACCEPTED**

Eight canonical ready-to-insert definitions exist:

- `process.motor.standard`;
- `process.motor.vfd`;
- `dynamo.pump.standard`;
- `process.pump.submersible`;
- `process.valve.onoff`;
- `process.valve.control`;
- `process.tank.vertical`;
- `process.tank.horizontal`.

Instance `{equipmentPath}` substitution remains canonical and does not mutate shared definitions.

## 4. Coordination cleanup

- historical issue #184 was closed as completed/superseded after later accepted licensing #183, main integration and L3 evidence made its old `BLOCKS PR #175` status obsolete;
- `PROJECT GOAL.md`, `docs/ROADMAP.md` and `docs/CURRENT-COORDINATOR-HANDOFF.md` were synchronized after the accepted gate using documentation-only `[skip ci]` commits;
- issue #191 can now be closed as completed with its final evidence comment.

## 5. Next authorized stage — Wave 11

**READY / NOT STARTED at this checkpoint.**

Roadmap objective: **Complete HMI Runtime demo vertical slice**.

The current default Runtime in `web/scada-web/src/main.tsx` still hand-renders the Demo process (`Demo.Tank01`, `Demo.P01`, discharge metrics and modal). That hand-authored surface must stop being Runtime application truth.

Existing foundation to reuse:

- canonical Screen/Popup/Dynamo Engineering;
- `RuntimeVisualDefinitionRenderer` and the canonical visual renderer;
- screen/popup runtime navigation model;
- Dynamo runtime composition;
- Client Visual Python/event runtime;
- realtime TAG transport;
- protected Runtime TAG writes;
- alarms, TAG Inspector, Trends and Historical Data Browser;
- persisted lifecycle with an **Active Revision**.

Critical architectural requirement for Wave 11:

> Runtime visual application state must derive from the **active persisted canonical Engineering revision**, not the editable Working export. Working edits must not appear in Runtime until normal save/publish/activate lifecycle semantics make them active.

The persistence service already exposes `LoadActiveAsync(projectKey)` internally, but the browser currently has no dedicated active-package Runtime projection. The first Wave 11 slice should add a protected deterministic active-revision projection, then mount the active Screen/Popup/Dynamo application through the existing canonical Runtime visual stack.

## 6. Exact next action

1. close issue #191 as completed after its final acceptance comment;
2. create a dedicated Wave 11 issue;
3. create a Wave 11 coordination branch from live `main` after the documentation-only gate commits;
4. update this file/handoff/roadmap to the resulting live issue, branch and head;
5. implement the active-Engineering HMI Runtime vertical slice without bypassing lifecycle/security/canonical visual boundaries;
6. require exact-head backend build/tests/runtime smoke, Web build and Chromium E2E before integration.

Wave 12 remains hardening. Wave 13 remains the mandatory Authenticode + trusted timestamp Windows release-signing stage. Physical L4 remains later Preview/device-specific validation.
