# EliteSCADA Roadmap

**Status date:** 2026-09-01 (BRT)  
**Active direction:** **PRE-WAVE-11 REPOSITORY/CI HYGIENE GATE**  
**Wave 11:** **ACTIVE BUT TEMPORARILY PAUSED — issue #194 / draft PR #195**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable coordination state: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
CI routing/hygiene policy: `docs/CI-VALIDATION-POLICY.md`.  
Driver/lab evidence: `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`.  
Demo/licensing contract: `docs/LICENSING-AND-DEMO-MODE.md`.

## Current validated foundation

- Waves 03 through 10: **COMPLETE / MERGED**.
- Common seven-peer interoperability infrastructure: **COMPLETE / MERGED**.
- Independent product-path Driver L2: **7/7 PASS / ACCEPTED**.
- Shared Driver runtime/Engineering convergence: **7/7 COMPLETE / MERGED** through PR #187.
- Demo/hardware-bound licensing: **IMPLEMENTED / ACCEPTED / MERGED**.
- Integrated seven-Driver L3: **PASS / ACCEPTED / INTEGRATED**, issue #180 closed.
- Pre-Wave 11 owner-usability gate #191: **COMPLETE / ACCEPTED / INTEGRATED** through PR #193.
- Validated pre-Wave-11 main code merge: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`.
- Post-main Preview Licensing CI #92 / `33527294658`: **SUCCESS**.
- Post-main EliteSCADA CI #1035 / `33527294657`: **SUCCESS after unchanged rerun of one transient IEC-104 timing failure**.
- Graphical Windows License Generator artifact from post-main Preview #92 remains accepted; exact checksum/provenance is retained in `LAST CHANGE.md` and issue #191.

Documentation-only coordination commits after validated code checkpoints may use `[skip ci]`; they do not supersede the validated code SHA.

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
CI Hygiene   Specialized CI routing + stale PR/issue sanitation                          ACTIVE — PR #196
Wave 11      Complete HMI Runtime demo vertical slice                                    ACTIVE / PAUSED BY CI HYGIENE
Wave 12      Hardening                                                                   WAITING / BLOCKED BY #194
Wave 13      Signed Windows x64 package + Authenticode release verification              WAITING / REQUIRED
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation by Development Lead                       AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Current transition

Completed product sequence:

`Driver convergence -> PR #187 -> integrated L3 #180 -> pre-Wave-11 gate #191 -> PR #193 -> post-main validation -> PASS`

Temporary coordination sequence now in force:

`pause Wave 11 code -> close/reclassify stale PR/issues -> narrow specialized CI triggers -> PR #196 -> exact-head CI -> merge -> reconcile Wave 11 branch -> resume #194/#195`

This hygiene gate does not add a new product wave and does not change Wave 11 acceptance criteria. It exists so repository state and CI routing match the actual development stage before more product code is stacked on top.

## CI validation strategy

`EliteSCADA CI` remains the universal Coordinator acceptance gate for PRs to `main`.

Specialized validation is affected-subsystem based:

- `Preview Licensing CI` runs automatically for licensing, License Generator, product-capacity and known licensing-sensitive shared paths; it remains available manually for cross-cutting and release validation.
- `L3 Seven-Driver Lab` runs automatically for Driver/DriverHost/communication/Gateway/TAG-event/Driver-test/interop-lab changes; it remains available manually for cross-cutting host/composition and release validation.
- path filters do not overrule engineering judgment: a structural change capable of affecting a specialized subsystem requires a manual specialized run even if GitHub did not select it automatically.
- changing routing does not weaken the workflows themselves.

Full policy: `docs/CI-VALIDATION-POLICY.md`.

Current repository fact: `main` has no configured branch protection / required status checks. Therefore the universal EliteSCADA CI requirement is currently an operational merge rule, not a GitHub-enforced block.

## Repository hygiene state

Historical worker/validation PRs no longer need to remain open merely to preserve evidence. The stale Driver/licensing PR inventory identified before this gate was closed with integration-lineage comments; closed commits/runs/artifacts remain audit evidence.

Completed Driver coordination issues #120-#123 are closed. Issue #178 remains open only as **deferred L4 Siemens hardware/vendor-simulator evidence** and is not a Wave 11 blocker.

The branch namespace still contains many historical refs. Branch deletion is a later mechanical cleanup because the current connected GitHub action set does not provide delete-ref; historical refs must not be rewritten merely for cosmetics.

## Wave 11 target after hygiene gate

Wave 11 replaces the hand-authored Runtime Demo as application truth with an owner-testable HMI Runtime derived from the **active persisted canonical Engineering revision**.

Required authority:

`Working -> saved Revision -> Published -> Active -> HMI Runtime projection`

Working Engineering edits must not silently leak into Runtime before activation.

The active Wave 11 branch/PR already contains implementation work and is not to be recreated:

- issue #194;
- branch `coordination/wave11-hmi-runtime`;
- draft PR #195;
- pre-hygiene branch head recorded in `LAST CHANGE.md`.

After PR #196 integrates, reconcile #195 with live `main` and continue its existing slices rather than restarting the wave.

### Wave 11 implementation boundary

1. protected backend projection of the active persisted canonical Engineering package;
2. deterministic Runtime project/revision consistency checks;
3. canonical Screen/Popup/Dynamo catalog mounted through the existing Runtime visual renderer/navigation stack;
4. visual assets resolved from the active persisted revision rather than mutable Working state;
5. preserved protected Slider/TAG writes and Client Visual runtime behavior;
6. simulation fallback explicitly separate when no Engineering runtime is active;
7. automated proof that Working edits do not affect Runtime until activation;
8. exact backend, Web and Chromium evidence before integration.

## Driver evidence policy

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — independent software peer over the real wire protocol;
- **L3** — one integrated EliteSCADA runtime with all seven Drivers concurrently, including Gateway and fault/recovery evidence;
- **L4** — physical hardware/site validation using a Preview build.

L3 is complete. L4 remains later and device-specific.

## Quality locks

- canonical Engineering/backend authority;
- Runtime presentation derives from the active canonical Engineering revision, never unsaved Working/browser-only state;
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
