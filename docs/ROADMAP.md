# EliteSCADA Roadmap

**Status date:** 2026-09-01 (BRT)  
**Active direction:** **POST-L3 / PRE-WAVE-11 TASK**  
**Wave 11:** **TECHNICALLY RELEASED BY L3, BUT INTENTIONALLY NOT STARTED**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable coordination state: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Driver/lab evidence: `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`.  
Demo/licensing contract: `docs/LICENSING-AND-DEMO-MODE.md`.

## Current validated foundation

- Waves 03 through 10: **COMPLETE / MERGED**.
- Common seven-peer interoperability infrastructure: **COMPLETE / MERGED**.
- Independent product-path Driver L2: **7/7 PASS / ACCEPTED**.
- Shared Driver runtime/Engineering convergence: **7/7 COMPLETE / MERGED**.
- Demo/hardware-bound licensing: **IMPLEMENTED / ACCEPTED / MERGED**; issue #183 closed.
- Integrated seven-Driver L3: **PASS / ACCEPTED / INTEGRATED**.
- L3 final stabilization SHA before merge: `02b7408e68b81355de6a56dc0267c9e28c0c74bf`.
- Exact pre-merge evidence: EliteSCADA CI #1023, Preview Licensing CI #80 and L3 Seven-Driver Lab #30: **SUCCESS**.
- Main integration through PR #190: `9b963c40f013f115b9787049cdb90949a30cbcbc`.
- Post-main evidence: EliteSCADA CI #1024 and Preview Licensing CI #81: **SUCCESS**.

## Ordered path to v0.1

```text
Wave 03      Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04      Project portability + basic Trends + Administration                        COMPLETE
Wave 05      Canonical Script Engineering                                                COMPLETE
Wave 06      Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07      Visual Runtime Object Model + typed visual Engineering                      COMPLETE
Wave 08      Graphical Editor + Image + Engineering Development Monitor                  COMPLETE
08-FOLLOW-A  TAG Bit Access + Driver Bit-Level Boolean Binding                           COMPLETE
08-FOLLOW-B  Typed Visual Expressions + Boolean Conditions + Analog Fill                 COMPLETE
Wave 09      Screens + Popups + Dynamos + navigation + Historical Data + Reporting       COMPLETE
Wave 10      Python visual events + animation + preview                                  COMPLETE
Driver Lab   Seven-peer reproducible interoperability infrastructure                     COMPLETE / MERGED
Driver L2    Independent product-path protocol evidence                                  7/7 PASS
Drivers      Shared runtime/Engineering convergence                                      COMPLETE / MERGED
DemoLicense  Demo + hardware-bound licensing + offline License Generator                 COMPLETE / ACCEPTED / MERGED
Driver L3    Seven Drivers concurrently + Gateway + fault/recovery                       PASS / ACCEPTED / INTEGRATED
Pre-Wave 11  Development Lead task                                                       REQUIRED / SCOPE PENDING
Wave 11      Complete HMI Runtime demo vertical slice                                    HELD UNTIL PRE-WAVE-11 TASK COMPLETES
Wave 12      Hardening                                                                   WAITING
Wave 13      Windows x64 product package                                                 WAITING
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation by Development Lead                       AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Current transition

The previous sequence is complete:

`Driver convergence -> main -> post-main CI -> L3 #180 -> PASS`

The L3 gate no longer blocks Wave 11. However, the Development Lead explicitly established on 2026-09-01 that **another task must be completed before Wave 11 begins**. The task scope has not yet been supplied, so the coordinator must not create or start Wave 11 work until that scope is recorded in the repository and completed/accepted.

## Demo and licensed behavior

Issue #183 is complete and closed. The accepted implementation includes:

- no installed license => Demo;
- Engineering may exceed 200 TAGs;
- Demo Run is limited to <=200 TAGs;
- Demo runtime has a 300-minute continuous-session limit;
- valid machine-bound signed licenses grant 500 / 1000 / 1500 / 3000 / 5000 / Unlimited TAG tiers and remove the Demo timer;
- invalid/tampered/expired/unknown-key/wrong-machine installed licenses block Run;
- versioned machine request code;
- protected licensing API and management UI;
- offline Windows x64 `EliteSCADA.LicenseGenerator.exe` publish path;
- private signing material remains external to GitHub, CI and normal product binaries.

Authority: `docs/LICENSING-AND-DEMO-MODE.md` and `docs/licensing/ACCEPTANCE-EVIDENCE-2026-08-31.md`.

## Driver evidence policy

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — independent software peer over the real wire protocol;
- **L3** — one integrated EliteSCADA runtime with all seven Drivers concurrently, including Gateway and fault/recovery evidence;
- **L4** — physical hardware/site validation using a Preview build.

L3 is complete. L4 remains later and device-specific.

## Quality locks

- canonical Engineering/backend authority;
- schema-v15 `CommunicationBinding` remains the rich communication TAG authority;
- licensing is host-owned and Drivers do not inspect license/hardware state directly;
- private signing keys never enter GitHub, CI or normal product builds;
- no plaintext protected material;
- no Driver-to-Driver coupling;
- no canonical TAG/cache/event bypass;
- no test weakening to manufacture green evidence;
- L2 does not imply L3;
- L3 does not imply physical L4;
- exact CI evidence is required at material stage transitions.