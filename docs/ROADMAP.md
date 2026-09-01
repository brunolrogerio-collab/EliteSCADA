# EliteSCADA Roadmap

**Status date:** 2026-09-01 (BRT)  
**Active direction:** **WAVE 11 — ACTIVE ENGINEERING HMI RUNTIME**  
**Wave 11:** **ACTIVE — issue #194 / `coordination/wave11-hmi-runtime`**

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
- Pre-Wave 11 owner-usability gate #191: **COMPLETE / ACCEPTED / INTEGRATED**.
- Pre-Wave 11 implementation head: `aeb9b3b5641adee344c4ead166b97cc0adba3dbf`.
- Pre-merge evidence on that head: EliteSCADA CI #1033 / `33525910566`, Preview Licensing CI #90 / `33525910582`, L3 Seven-Driver Lab #39 / `33525910552`: **SUCCESS**.
- Main integration through PR #193: code merge `64ba134f88df61233c492f6c5e2b1ea8f244bf19`.
- Post-main Preview Licensing CI #92 / `33527294658`: **SUCCESS**.
- Post-main EliteSCADA CI #1035 / `33527294657`: **SUCCESS after unchanged rerun of one transient IEC-104 timing failure**; backend build/tests/smoke, Web build and Chromium E2E all passed.
- Graphical Windows License Generator artifact from post-main Preview #92: artifact `9808306320`; self-contained Windows x64 GUI executable validated. Exact executable checksum/provenance is retained in `LAST CHANGE.md` and issue #191.

Documentation-only coordination commits after the validated code merge use `[skip ci]`; they do not supersede the code-validation claim above.

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
Pre-Wave 11  GUI License Generator + Slider + application file + minimum Dynamo library  COMPLETE / ACCEPTED / MERGED
Wave 11      Complete HMI Runtime demo vertical slice                                    ACTIVE — #194
Wave 12      Hardening                                                                   WAITING / BLOCKED BY #194
Wave 13      Signed Windows x64 package + Authenticode release verification              WAITING / REQUIRED
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation by Development Lead                       AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Current transition

Completed sequence:

`Driver convergence -> main -> post-main CI -> L3 #180 -> pre-Wave-11 owner-usability gate #191 -> PR #193 -> exact post-main validation -> PASS`

Current sequence:

`Wave 11 #194 -> active canonical Engineering HMI Runtime vertical slice -> exact-head CI -> controlled PR/main integration -> post-main validation`

Wave 11 replaces the current hand-authored Runtime Demo as application truth with an owner-testable HMI Runtime vertical slice derived from the **active canonical Engineering revision**. Existing visual runtime, Screen/Popup/Dynamo composition, Client Visual scripting, TAG realtime, alarm, trend/history and protected write foundations must be reused rather than bypassed or duplicated.

Working Engineering edits must not silently leak into Runtime before normal save/publish/activate lifecycle semantics make them active.

### Wave 11 implementation boundary

1. protected backend projection of the active persisted canonical Engineering package;
2. deterministic Runtime project/revision consistency checks;
3. canonical Screen/Popup/Dynamo catalog mounted through the existing Runtime visual renderer/navigation stack;
4. preserved protected Slider/TAG writes and Client Visual runtime behavior;
5. simulation fallback remains explicitly separate when no Engineering runtime is active;
6. automated proof that Working edits do not affect Runtime until activation;
7. exact backend, Web and Chromium evidence before integration.

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
- graphical offline Windows x64 `EliteSCADA.LicenseGenerator.exe` publish path;
- private signing material remains external to GitHub, CI and normal product binaries.

Authority: `docs/LICENSING-AND-DEMO-MODE.md`, `docs/licensing/ACCEPTANCE-EVIDENCE-2026-08-31.md`, `docs/licensing/OFFLINE-LICENSE-OPERATIONS.md` and issue #183.

## Driver evidence policy

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — independent software peer over the real wire protocol;
- **L3** — one integrated EliteSCADA runtime with all seven Drivers concurrently, including Gateway and fault/recovery evidence;
- **L4** — physical hardware/site validation using a Preview build.

L3 is complete. L4 remains later and device-specific.

## Quality locks

- canonical Engineering/backend authority;
- Runtime presentation must derive from the active canonical Engineering revision, not from unsaved Working/browser-only state;
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
