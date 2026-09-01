# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-09-01 BRT**  
Operational status: **DRIVER CONVERGENCE CLOSED / L2 7/7 PASS / L3 PASS+INTEGRATED / PRE-WAVE-11 #191 COMPLETE / WAVE 11 READY**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> Live GitHub refs and exact-head Actions evidence override SHAs copied into prose. Stable product intent is governed by `PROJECT GOAL.md`. Mutable exact state belongs in `LAST CHANGE.md`.

## 1. Mandatory resume protocol

A replacement Coordinator must read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/ROADMAP.md`;
5. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`;
6. live `main`, active coordination branch/issue and latest Actions;
7. issues #174, #180, #183 and #191 as historical acceptance authority.

Repository state, not old chat messages, is the continuity source.

## 2. Current mainline

Latest validated **code** integration checkpoint:

- PR #193 — pre-Wave-11 owner-usability gate: **MERGED**;
- validated main code SHA: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`;
- implementation head before merge: `aeb9b3b5641adee344c4ead166b97cc0adba3dbf`;
- Preview Licensing CI #92 / run `33527294658`: **SUCCESS**;
- EliteSCADA CI #1035 / run `33527294657`: **SUCCESS after unchanged rerun of a transient IEC-104 timing test**;
- final normal CI jobs: backend build/tests/runtime smoke **SUCCESS**, Web build **SUCCESS**, Chromium E2E **SUCCESS**.

Pre-merge evidence on `aeb9b3b...`:

- EliteSCADA CI #1033 / run `33525910566`: **SUCCESS**;
- Preview Licensing CI #90 / run `33525910582`: **SUCCESS**;
- L3 Seven-Driver Lab #39 / run `33525910552`: **SUCCESS**.

The initial post-main #1035 attempt failed one existing IEC-104 T2 timing assertion. The failed backend job was rerun unchanged and passed build, all tests and runtime smoke; dependent Chromium E2E then passed. No code or assertion was changed to obtain the post-main green result.

Documentation-only synchronization commits after `64ba134f...` use `[skip ci]` and do not supersede that code-validation checkpoint.

## 3. Pre-Wave-11 gate #191

Issue #191 is **COMPLETE / ACCEPTED / INTEGRATED** after its final repository documentation synchronization.

Accepted scope:

1. Windows License Generator opens a graphical WinForms interface on normal double-click while retaining controlled CLI automation;
2. canonical industrial `core.slider` supports passive display and protected/audited interactive TAG writes with fail-closed eligibility;
3. **Save Application As / Open Application** uses the portable `.escadapkg` boundary with inspect/Preview/Apply semantics;
4. built-in canonical Dynamo library contains eight starter definitions: two motors, two pumps, two valves and two tanks;
5. Wave 13 retains the mandatory Authenticode + trusted timestamp release requirement.

Post-main License Generator evidence from Preview Licensing CI #92:

- artifact name: `EliteSCADA-LicenseGenerator-win-x64`;
- artifact id: `9808306320`;
- source code SHA: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`;
- `EliteSCADA.LicenseGenerator.exe`: 116,257,103 bytes;
- executable type: PE32+ Windows GUI, x86-64;
- executable SHA-256: `841dea832d67f44e07aa10b2de96ccfffd5d518beeadafb48ed34e16d0317523`.

Private license-signing material remains external. The normal product contains/loads only public verification material/key identifiers.

PR #192 was closed without merge only because the connector could not transition its draft state; PR #193 reused the exact same validated implementation head and was merged normally. This is coordination history, not a code divergence.

## 4. Driver convergence and interoperability

- Engineering schema v15 / canonical `CommunicationBinding`: **CLOSED**.
- MQTT: **CLOSED / L2 PASS**.
- IEC-104: **CLOSED / L2 PASS**.
- CIP / EtherNet/IP: **CLOSED / L2 PASS**.
- OPC UA: **CLOSED / L2 PASS**.
- DNP3: **CLOSED / L2 PASS**.
- Siemens S7 ISO-on-TCP: **CLOSED / L2 PASS**.
- BACnet/IP: **CLOSED / L2 PASS**.
- Independent product-path L2: **7/7 PASS / ACCEPTED**.
- Integrated L3: **PASS / ACCEPTED / INTEGRATED**.

Historical authority: issues #174 and #180 plus `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`.

## 5. Demo/licensing

Issue #183: **COMPLETED / ACCEPTED / INTEGRATED**. Historical issue #184 has been closed as superseded/resolved so its obsolete red checkpoint cannot be mistaken for a current blocker.

Accepted behavior includes:

- Demo with <=200 TAG Run gate while Engineering may exceed 200 TAGs;
- 300-minute continuous Demo runtime session;
- machine-bound signed entitlement tiers 500 / 1000 / 1500 / 3000 / 5000 / Unlimited;
- invalid installed licenses block Run;
- versioned machine request code;
- protected licensing API and management UI;
- graphical offline Windows x64 License Generator;
- private signing material remains outside GitHub/CI/product binaries.

Authority: `docs/LICENSING-AND-DEMO-MODE.md`, `docs/licensing/ACCEPTANCE-EVIDENCE-2026-08-31.md`, `docs/licensing/OFFLINE-LICENSE-OPERATIONS.md`, issue #183.

## 6. Current stage boundary — Wave 11

The completed sequence is:

`Driver convergence -> main integration -> exact post-main CI -> integrated L3 -> pre-Wave-11 #191 -> PR #193 -> exact post-main validation -> PASS`

**Wave 11 is now authorized.** At the instant of this handoff synchronization it is **READY / NOT STARTED**. `LAST CHANGE.md` must be updated again as soon as a Wave 11 issue/branch is created.

Wave 11 objective from the roadmap:

> Complete an owner-testable HMI Runtime vertical slice derived from the active canonical Engineering revision, replacing the current hand-authored Demo process surface as Runtime application truth.

Existing reusable foundation already includes:

- canonical Screen/Popup/Dynamo Engineering models;
- canonical visual renderer;
- Runtime visual navigation actions for screens/popups;
- Dynamo runtime composition and instance equipment-path substitution;
- Client Visual Python runtime/event bridge;
- realtime TAG transport and protected Runtime TAG write API;
- alarms, Runtime TAG Inspector, basic trends and Historical Data Browser.

The current default Runtime in `web/scada-web/src/main.tsx` still manually renders `Demo.Tank01`, `Demo.P01` and related process metrics. Wave 11 must remove that hardcoded process surface as application truth and mount the **active persisted Engineering revision** instead.

The backend persistence service already exposes `LoadActiveAsync(projectKey)` internally, but the current frontend Engineering snapshot endpoint reflects Working state. Wave 11 therefore needs a protected, deterministic Runtime projection of the active revision rather than reusing Working export and accidentally leaking unactivated edits into Runtime.

### Wave 11 quality boundary

- active revision, not Working/browser state, drives the Runtime visual application;
- normal save/publish/activate lifecycle remains authoritative;
- no frontend-to-Driver bypass;
- reuse `RuntimeVisualDefinitionRenderer` / canonical visual composition rather than inventing a second renderer;
- screens, popups, Dynamos and their stable identities/navigation remain canonical;
- protected Slider/process writes remain backend-authorized/audited;
- Working changes do not appear in Runtime until activated;
- exact backend, Web build and Chromium E2E evidence is required before integration.

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
