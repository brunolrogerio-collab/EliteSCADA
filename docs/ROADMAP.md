# EliteSCADA Roadmap

**Status date:** 2026-09-01 (BRT)  
**Active direction:** **WAVE 11 — ACTIVE ENGINEERING HMI RUNTIME**  
**Wave 11:** **ACTIVE / RESUMED — issue #194 / draft PR #195**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable coordination state: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
CI policy: `docs/CI-VALIDATION-POLICY.md`.

## Current validated foundation

- Waves 03 through 10: **COMPLETE / MERGED**.
- Seven communication Drivers: shared convergence **COMPLETE / MERGED**, independent L2 **7/7 PASS**, integrated L3 **PASS / ACCEPTED**.
- Demo/hardware-bound licensing: **IMPLEMENTED / ACCEPTED / MERGED**.
- Pre-Wave-11 owner-usability gate #191: **COMPLETE / ACCEPTED / MERGED** through PR #193.
- Repository/CI hygiene: **COMPLETE / ACCEPTED / INTEGRATED** through PRs #196 and #197.
- Latest validated post-hygiene product-code `main`: `e117849827f1409ad6dd383dbdc2ed936ce62567`.
- Post-main #197: EliteSCADA CI #1058 **SUCCESS**, Preview Licensing #116 **SUCCESS**, L3 Seven-Driver Lab #63 **SUCCESS**.

Documentation-only `[skip ci]` commits may advance `main` beyond validated product-code SHAs without superseding their validation evidence.

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
CI Hygiene   Specialized CI routing + stale PR/issue sanitation + focused ownership      COMPLETE / MERGED
Wave 11      Active persisted Engineering HMI Runtime vertical slice                     ACTIVE / #194 / draft #195
Wave 12      Hardening                                                                   WAITING / BLOCKED BY #194
Wave 13      Signed Windows x64 package + Authenticode release verification              WAITING / REQUIRED
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation by Development Lead                       AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## CI validation strategy

`EliteSCADA CI` is the universal Coordinator acceptance gate for PRs to `main`.

Specialized validation is affected-subsystem based:

- `Preview Licensing CI`: licensing, License Generator, product capacity and known licensing-sensitive shared paths; manual/release execution remains available.
- `L3 Seven-Driver Lab`: Drivers, DriverHost, communication, Gateway, TAG/event, Driver tests and interoperability lab; manual/cross-cutting/release execution remains available.
- structural impact can require a manual specialized run even when a path filter does not select it automatically.

Wave-11-only HMI changes have already demonstrated the intended routing: **EliteSCADA CI + the dedicated Wave 11 workflow**, without automatic Preview Licensing or L3 execution.

Repository fact: `main` currently has no configured branch protection / required status checks. The universal CI requirement remains an operational merge rule rather than GitHub enforcement.

## Repository hygiene state

Stale/superseded Driver/licensing PRs identified by the hygiene audit were closed with integration-lineage comments. Completed Driver coordination issues #120-#123 are closed. Issue #178 remains only as **deferred L4 Siemens hardware/vendor-simulator evidence**, not a Wave 11 blocker.

Historical branch refs remain until a safe delete-ref capability is available; old refs must not be repointed merely to reduce their count.

## Wave 11 target

Wave 11 replaces the hand-authored Runtime Demo as application truth with an owner-testable HMI Runtime derived from the **active persisted canonical Engineering revision**.

Required authority:

`Working -> saved Revision -> Published -> Active -> HMI Runtime projection`

Working edits must not silently leak into Runtime before activation.

Current Wave 11 implementation already provides:

1. protected backend projection of persisted Active Engineering;
2. deterministic Runtime project/revision consistency checks and fail-closed behavior;
3. canonical Screen/Popup/Dynamo Runtime mount;
4. Active visual assets with integrity validation and explicit Runtime asset authority;
5. protected Slider/TAG write boundary and existing Client Visual runtime path;
6. explicit Simulation fallback when no Engineering runtime is active;
7. automated Active A -> Working isolation -> Active B browser proof;
8. operator Runtime `View` without Working Engineering read authority;
9. dedicated proof that a real imported PNG in the Active revision is served from the Runtime Active asset endpoint.

Current pre-reconciliation Wave 11 head and exact CI state live in `LAST CHANGE.md`.

### Owner-test Demo package

After the demonstration application is finalized for owner testing with the Preview software, the project must provide an actual portable application package, preferably `.escadapkg`, usable through the normal application Save/Open workflow. Source code or CI results alone are not the owner-test deliverable.

## Quality locks

- canonical Engineering/backend authority;
- Runtime derives from Active persisted Engineering, never mutable Working/browser-only state;
- schema-v15 `CommunicationBinding` remains the rich communication TAG authority;
- licensing remains host-owned; Drivers do not inspect license/hardware state directly;
- private signing keys never enter GitHub, CI or distributed product builds;
- no Driver-to-Driver coupling or canonical TAG/cache/event bypass;
- no test weakening to manufacture green evidence;
- L2 does not imply L3; L3 does not imply physical L4;
- exact CI evidence is required at material stage transitions.
